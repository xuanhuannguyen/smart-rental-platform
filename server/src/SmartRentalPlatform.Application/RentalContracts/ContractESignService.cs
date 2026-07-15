using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models.ESign;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractESignService : IContractESignService
{
    private static readonly SigningEnvelopeStatus[] OtpCapableEnvelopeStatuses =
    [
        SigningEnvelopeStatus.SentToProvider,
        SigningEnvelopeStatus.WaitingForSigners,
        SigningEnvelopeStatus.PartiallySigned
    ];

    private readonly IAppDbContext _context;
    private readonly IESignProviderClient _eSignProviderClient;
    private readonly IESignWebhookVerifier _eSignWebhookVerifier;
    private readonly IContractFileService _contractFileService;
    private readonly IContractAppendixService _contractAppendixService;
    private readonly ISensitiveDataProtector _sensitiveDataProtector;
    private readonly ILogger<ContractESignService> _logger;

    public ContractESignService(
        IAppDbContext context,
        IESignProviderClient eSignProviderClient,
        IESignWebhookVerifier eSignWebhookVerifier,
        IContractFileService contractFileService,
        IContractAppendixService contractAppendixService,
        ISensitiveDataProtector sensitiveDataProtector,
        ILogger<ContractESignService> logger)
    {
        _context = context;
        _eSignProviderClient = eSignProviderClient;
        _eSignWebhookVerifier = eSignWebhookVerifier;
        _contractFileService = contractFileService;
        _contractAppendixService = contractAppendixService;
        _sensitiveDataProtector = sensitiveDataProtector;
        _logger = logger;
    }

    public async Task<StartESignEnvelopeResponse?> StartContractEnvelopeAsync(Guid userId, Guid contractId, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var contract = await BaseContractQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract == null)
            return null;

        // Ensure user has access
        if (contract.Room!.RoomingHouse!.LandlordUserId != userId && contract.MainTenantUserId != userId)
            throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Không có quyền khởi tạo ký số cho hợp đồng này.");

        if (contract.Status != RentalContractStatus.PendingLandlordSignature && contract.Status != RentalContractStatus.PendingTenantSignature)
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Hợp đồng không ở trạng thái chờ ký.");

        // Check for existing active envelope
        var activeEnvelope = await _context.ContractSigningEnvelopes
            .Include(x => x.Signatures)
            .Where(x => x.RentalContractId == contractId &&
                        x.RentalContractAppendixId == null &&
                        OtpCapableEnvelopeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeEnvelope != null)
        {
            return MapToStartResponse(activeEnvelope);
        }

        await MarkAbandonedDraftEnvelopesFailedAsync(contractId, null, cancellationToken);

        if (contract.Status != RentalContractStatus.PendingLandlordSignature ||
            contract.Room.RoomingHouse.LandlordUserId != userId)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Chỉ Landlord được khởi tạo phiên ký; Tenant phải chờ Landlord hoàn tất.");
        }

        EnsureVnptSignerContacts(
            ("Landlord", contract.Room.RoomingHouse.Landlord?.Email, contract.Room.RoomingHouse.Landlord?.PhoneNumber),
            ("Tenant", contract.MainTenantUser?.Email, contract.MainTenantUser?.PhoneNumber));

        var participants = new List<ESignSignerInput>
        {
            new ESignSignerInput
            {
                UserId = contract.Room.RoomingHouse.LandlordUserId,
                SignerRole = ContractSignerRole.Landlord.ToString(),
                FullName = contract.Room.RoomingHouse.Landlord?.UserProfile?.FullName ?? "Landlord",
                Email = contract.Room.RoomingHouse.Landlord?.Email ?? string.Empty,
                PhoneNumber = contract.Room.RoomingHouse.Landlord?.PhoneNumber ?? string.Empty,
                SigningOrder = 1
            },
            new ESignSignerInput
            {
                UserId = contract.MainTenantUserId,
                SignerRole = ContractSignerRole.Tenant.ToString(),
                FullName = contract.MainTenantUser?.UserProfile?.FullName ?? "Tenant",
                Email = contract.MainTenantUser?.Email ?? string.Empty,
                PhoneNumber = contract.MainTenantUser?.PhoneNumber ?? string.Empty,
                SigningOrder = 2
            }
        };

        var title = $"Hợp đồng thuê phòng {contract.Room.RoomNumber} - {contract.Room.RoomingHouse.Name}";
        var envelope = new ContractSigningEnvelope
        {
            Id = Guid.NewGuid(),
            RentalContractId = contractId,
            Provider = ESignProvider.Vnpt,
            Status = SigningEnvelopeStatus.Draft,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContractSigningEnvelopes.Add(envelope);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var pdfResult = await _contractFileService.CreateUnsignedContractPdfForESignAsync(envelope.Id, cancellationToken);
            if (pdfResult == null)
            {
                throw new ApplicationException("Failed to generate unsigned PDF for ESign.");
            }
            var (unsignedPdf, signatureZones) = pdfResult.Value;

            var fileStreamInfo = await _contractFileService.OpenFileAsync(userId, contractId, unsignedPdf.Id, cancellationToken);
            if (fileStreamInfo == null)
            {
                throw new ApplicationException("Failed to open unsigned PDF for ESign.");
            }

            await using var stream = fileStreamInfo.Value.Content;
            envelope.Status = SigningEnvelopeStatus.SentToProvider;
            await _context.SaveChangesAsync(cancellationToken);

            var request = new CreateEnvelopeInput
            {
                Title = title,
                FileName = fileStreamInfo.Value.FileName,
                FileStream = stream,
                ReferenceId = contractId.ToString(),
                Signers = participants,
                SignatureZones = signatureZones
            };

            var createResult = await _eSignProviderClient.CreateEnvelopeAsync(request, cancellationToken);
            if (!createResult.IsSuccess)
            {
                throw new ApplicationException($"Failed to create envelope: {createResult.ErrorMessage}");
            }

            envelope.ProviderEnvelopeId = createResult.ProviderEnvelopeId;
            envelope.Status = SigningEnvelopeStatus.WaitingForSigners;
            envelope.SentAt = DateTimeOffset.UtcNow;
            ValidateProviderSignerResults(createResult.Signers, participants);

            foreach (var participant in createResult.Signers)
            {
                var matchingReq = participants.First(p => p.UserId == participant.UserId);
                if (string.IsNullOrWhiteSpace(participant.ProviderAccessCode) || string.IsNullOrWhiteSpace(participant.ProviderEvidenceJson))
                {
                    throw new ApplicationException($"VNPT did not return MA_TRUYCAP/evidence for {matchingReq.SignerRole}.");
                }

                var sig = new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    RentalContractId = contractId,
                    ContractSigningEnvelopeId = envelope.Id,
                    SignerUserId = participant.UserId,
                    SignerRole = Enum.Parse<ContractSignerRole>(matchingReq.SignerRole),
                    SignatureMethod = ContractSignatureMethod.Unknown,
                    Status = ContractSignatureStatus.Pending,
                    SigningOrder = matchingReq.SigningOrder,
                    ProviderParticipantId = participant.ProviderParticipantId,
                    Provider = ESignProvider.Vnpt,
                    ProviderEnvelopeId = createResult.ProviderEnvelopeId,
                    SigningUrl = participant.SigningUrl,
                    ProviderEvidenceJson = BuildStoredEvidence(participant.ProviderEvidenceJson, participant.ProviderAccessCode),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                envelope.Signatures.Add(sig);
                _context.ContractSignatures.Add(sig);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return MapToStartResponse(envelope);
        }
        catch (Exception exception)
        {
            await MarkEnvelopeFailedAsync(envelope, exception);
            throw;
        }
    }

    public async Task<RequestESignOtpResponse> RequestSignatureOtpAsync(
        Guid userId,
        Guid contractId,
        Guid? appendixId,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default)
    {
        if (method is not ESignOtpMethod.EmailOtp)
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Tạm thời chỉ hỗ trợ Email OTP do tài khoản VNPT đã hết sản lượng SMS.");
        }

        var envelope = await LoadActiveEnvelopeAsync(contractId, appendixId, cancellationToken);
        var signature = GetNextSignatureForUser(envelope, userId);
        var evidence = ReadEvidence(signature);

        if (evidence.OtpRequestedAt.HasValue && DateTimeOffset.UtcNow - evidence.OtpRequestedAt.Value < TimeSpan.FromSeconds(60))
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Vui lòng chờ trước khi yêu cầu gửi lại OTP.");
        }

        var signer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy người ký.");
        var contact = method == ESignOtpMethod.EmailOtp ? signer.Email : signer.PhoneNumber;
        if (string.IsNullOrWhiteSpace(contact))
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus,
                method == ESignOtpMethod.EmailOtp ? "Người ký chưa có email." : "Người ký chưa có số điện thoại.");
        }

        var accessCode = _sensitiveDataProtector.Decrypt(evidence.AccessCodeEncrypted);
        var result = await _eSignProviderClient.SendSignOtpAsync(
            envelope.ProviderEnvelopeId!, evidence.HdctId.ToString(), contact, accessCode, method, cancellationToken);
        if (!result.IsSuccess || result.OtpId == null || result.HdctPhienKyId == null)
        {
            throw new ExternalServiceException(
                ErrorCodes.ESignProviderError,
                "VNPT chưa thể gửi OTP. Vui lòng thử lại sau.",
                new
                {
                    provider = "VNPT",
                    providerCode = result.ProviderCode,
                    providerStatusCode = result.ProviderStatusCode
                });
        }

        var now = DateTimeOffset.UtcNow;
        evidence.OtpId = result.OtpId;
        evidence.HdctPhienKyId = result.HdctPhienKyId;
        evidence.OtpMethod = (int)method;
        evidence.OtpRequestedAt = now;
        evidence.OtpExpiresAt = result.ValiditySeconds.HasValue ? now.AddSeconds(result.ValiditySeconds.Value) : null;
        signature.ProviderEvidenceJson = JsonSerializer.Serialize(evidence);
        signature.SignatureMethod = method == ESignOtpMethod.EmailOtp
            ? ContractSignatureMethod.VnptEmailOtp
            : ContractSignatureMethod.VnptSmsOtp;
        signature.Status = ContractSignatureStatus.Notified;
        signature.NotifiedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new RequestESignOtpResponse
        {
            Method = (int)method,
            MethodName = method.ToString(),
            MaskedDestination = MaskContact(result.Destination ?? contact, method),
            ExpiresAt = evidence.OtpExpiresAt
        };
    }

    public async Task SubmitSignatureOtpAsync(
        Guid userId,
        Guid contractId,
        Guid? appendixId,
        string otpCode,
        string signatureImageBase64,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(otpCode) || otpCode.Length is < 4 or > 10 || !otpCode.All(char.IsDigit))
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Mã OTP không hợp lệ.");
        }

        var normalizedImage = SignatureImageNormalizer.Normalize(signatureImageBase64);
        var envelope = await LoadActiveEnvelopeAsync(contractId, appendixId, cancellationToken);
        var signature = GetNextSignatureForUser(envelope, userId);
        var evidence = ReadEvidence(signature);
        if (evidence.OtpId == null || evidence.HdctPhienKyId == null || evidence.OtpMethod == null)
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Chưa khởi tạo phiên OTP với VNPT.");
        }
        if (evidence.OtpExpiresAt.HasValue && evidence.OtpExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Mã OTP đã hết hạn.");
        }

        var method = (ESignOtpMethod)evidence.OtpMethod.Value;
        if (method is not ESignOtpMethod.SmsOtp and not ESignOtpMethod.EmailOtp)
        {
            throw new ApplicationException("Unsupported VNPT OTP method in signing evidence.");
        }

        var signer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy người ký.");
        var contact = method == ESignOtpMethod.EmailOtp ? signer.Email : signer.PhoneNumber;
        if (string.IsNullOrWhiteSpace(contact))
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Thiếu thông tin liên hệ của người ký.");
        }

        var accessCode = _sensitiveDataProtector.Decrypt(evidence.AccessCodeEncrypted);
        var submitResult = await _eSignProviderClient.SubmitSignOtpAsync(
            evidence.OtpId.Value, evidence.HdctPhienKyId.Value, otpCode, normalizedImage,
            signature.ProviderEvidenceJson!, contact, accessCode, method, cancellationToken);
        if (!submitResult.IsSuccess)
        {
            throw new ExternalServiceException(
                ErrorCodes.ESignProviderError,
                "VNPT chưa thể hoàn tất ký điện tử. Vui lòng thử lại với OTP mới.",
                new
                {
                    provider = "VNPT",
                    providerCode = submitResult.ProviderCode,
                    providerStatusCode = submitResult.ProviderStatusCode
                });
        }

        var now = DateTimeOffset.UtcNow;
        signature.Status = ContractSignatureStatus.Signed;
        signature.SignedAt = now;
        var allSigned = envelope.Signatures.All(x => x.Status == ContractSignatureStatus.Signed);
        envelope.Status = allSigned ? SigningEnvelopeStatus.Completed : SigningEnvelopeStatus.PartiallySigned;
        envelope.CompletedAt = allSigned ? now : null;

        var contract = await _context.RentalContracts.FirstOrDefaultAsync(x => x.Id == contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");
        if (appendixId.HasValue)
        {
            if (allSigned)
            {
                var appendix = await _context.ContractAppendices.FirstOrDefaultAsync(x => x.Id == appendixId.Value, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.ContractAppendixNotFound, "Không tìm thấy phụ lục.");
                appendix.Status = ContractAppendixStatus.Active;
                appendix.UpdatedAt = now;
            }
        }
        else if (allSigned)
        {
            contract.Status = RentalContractStatus.Active;
            contract.ActivatedAt = now;
        }
        else
        {
            contract.Status = RentalContractStatus.PendingTenantSignature;
        }

        contract.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        if (allSigned && !string.IsNullOrWhiteSpace(envelope.ProviderEnvelopeId))
        {
            try
            {
                using var signedPdf = await _eSignProviderClient.DownloadSignedPdfAsync(envelope.ProviderEnvelopeId, cancellationToken);
                await _contractFileService.StoreProviderSignedPdfAsync(envelope.Id, signedPdf, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VNPT signing completed but signed PDF download failed for envelope {EnvelopeId}", envelope.Id);
            }

            try
            {
                await _contractFileService.EnsureMaskedReferenceFileAsync(
                    contractId,
                    appendixId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create masked reference PDF for envelope {EnvelopeId}", envelope.Id);
            }
        }
    }

    public async Task<StartESignEnvelopeResponse?> StartAppendixEnvelopeAsync(Guid userId, Guid contractId, Guid appendixId, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var appendix = await _context.ContractAppendices
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.Landlord)
                            .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.MainTenantUser)
                    .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Changes)
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix == null)
            return null;

        var contract = appendix.RentalContract;

        // Ensure user has access
        if (contract.Room!.RoomingHouse!.LandlordUserId != userId && contract.MainTenantUserId != userId)
            throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Không có quyền khởi tạo ký số cho phụ lục này.");

        if (appendix.Status != ContractAppendixStatus.PendingSignature)
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Phụ lục không ở trạng thái chờ ký.");

        // Check for existing active envelope
        var activeEnvelope = await _context.ContractSigningEnvelopes
            .Include(x => x.Signatures)
            .Where(x => x.RentalContractId == contractId &&
                        x.RentalContractAppendixId == appendixId &&
                        OtpCapableEnvelopeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeEnvelope != null)
        {
            // Return existing envelope info
            return MapToStartResponse(activeEnvelope);
        }

        await MarkAbandonedDraftEnvelopesFailedAsync(contractId, appendixId, cancellationToken);

        if (appendix.CreatedByUserId != userId)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Chỉ người tạo phụ lục mới được quyền khởi tạo phiên ký.");
        }

        var currentMainTenantUserId = GetCurrentMainTenantUserId(contract);
        var currentMainTenantUser = contract.MainTenantUserId == currentMainTenantUserId
            ? contract.MainTenantUser
            : await _context.Users.Include(x => x.UserProfile).FirstOrDefaultAsync(x => x.Id == currentMainTenantUserId, cancellationToken);

        if (currentMainTenantUser == null)
            throw new ApplicationException("Không tìm thấy thông tin người thuê chính hiện tại.");

        EnsureVnptSignerContacts(
            ("Landlord", contract.Room.RoomingHouse.Landlord?.Email, contract.Room.RoomingHouse.Landlord?.PhoneNumber),
            ("Tenant", currentMainTenantUser.Email, currentMainTenantUser.PhoneNumber));

        var isLandlordCreator = appendix.CreatedByUserId == contract.Room.RoomingHouse.LandlordUserId;
        var participants = new List<ESignSignerInput>
        {
            new ESignSignerInput
            {
                UserId = contract.Room.RoomingHouse.LandlordUserId,
                SignerRole = ContractSignerRole.Landlord.ToString(),
                FullName = contract.Room.RoomingHouse.Landlord?.UserProfile?.FullName ?? "Landlord",
                Email = contract.Room.RoomingHouse.Landlord?.Email ?? string.Empty,
                PhoneNumber = contract.Room.RoomingHouse.Landlord?.PhoneNumber ?? string.Empty,
                SigningOrder = isLandlordCreator ? 1 : 2
            },
            new ESignSignerInput
            {
                UserId = currentMainTenantUserId,
                SignerRole = ContractSignerRole.Tenant.ToString(),
                FullName = currentMainTenantUser.UserProfile?.FullName ?? "Tenant",
                Email = currentMainTenantUser.Email ?? string.Empty,
                PhoneNumber = currentMainTenantUser.PhoneNumber ?? string.Empty,
                SigningOrder = isLandlordCreator ? 2 : 1
            }
        };

        var title = $"Phụ lục {appendix.AppendixNumber} - Hợp đồng {contract.Room.RoomNumber} - {contract.Room.RoomingHouse.Name}";
        var envelope = new ContractSigningEnvelope
        {
            Id = Guid.NewGuid(),
            RentalContractId = contractId,
            RentalContractAppendixId = appendixId,
            Provider = ESignProvider.Vnpt,
            Status = SigningEnvelopeStatus.Draft,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContractSigningEnvelopes.Add(envelope);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var appendixPdfResult = await _contractFileService.CreateUnsignedAppendixPdfForESignAsync(envelope.Id, cancellationToken);
            if (appendixPdfResult == null)
            {
                throw new ApplicationException("Failed to generate unsigned PDF for ESign.");
            }
            var (unsignedPdf, signatureZones) = appendixPdfResult.Value;

            var fileStreamInfo = await _contractFileService.OpenFileAsync(userId, contractId, unsignedPdf.Id, cancellationToken);
            if (fileStreamInfo == null)
            {
                throw new ApplicationException("Failed to open unsigned PDF for ESign.");
            }

            await using var stream = fileStreamInfo.Value.Content;
            envelope.Status = SigningEnvelopeStatus.SentToProvider;
            await _context.SaveChangesAsync(cancellationToken);

            var request = new CreateEnvelopeInput
            {
                Title = title,
                FileName = fileStreamInfo.Value.FileName,
                FileStream = stream,
                ReferenceId = appendixId.ToString(),
                Signers = participants,
                SignatureZones = signatureZones
            };

            var createResult = await _eSignProviderClient.CreateEnvelopeAsync(request, cancellationToken);
            if (!createResult.IsSuccess)
            {
                throw new ApplicationException($"Failed to create envelope: {createResult.ErrorMessage}");
            }

            envelope.ProviderEnvelopeId = createResult.ProviderEnvelopeId;
            envelope.Status = SigningEnvelopeStatus.WaitingForSigners;
            envelope.SentAt = DateTimeOffset.UtcNow;
            ValidateProviderSignerResults(createResult.Signers, participants);

            foreach (var participant in createResult.Signers)
            {
                var matchingReq = participants.First(p => p.UserId == participant.UserId);
                if (string.IsNullOrWhiteSpace(participant.ProviderAccessCode) || string.IsNullOrWhiteSpace(participant.ProviderEvidenceJson))
                {
                    throw new ApplicationException($"VNPT did not return MA_TRUYCAP/evidence for {matchingReq.SignerRole}.");
                }

                var sig = new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    RentalContractAppendixId = appendixId,
                    ContractSigningEnvelopeId = envelope.Id,
                    SignerUserId = participant.UserId,
                    SignerRole = Enum.Parse<ContractSignerRole>(matchingReq.SignerRole),
                    SignatureMethod = ContractSignatureMethod.Unknown,
                    Status = ContractSignatureStatus.Pending,
                    SigningOrder = matchingReq.SigningOrder,
                    ProviderParticipantId = participant.ProviderParticipantId,
                    Provider = ESignProvider.Vnpt,
                    ProviderEnvelopeId = createResult.ProviderEnvelopeId,
                    SigningUrl = participant.SigningUrl,
                    ProviderEvidenceJson = BuildStoredEvidence(participant.ProviderEvidenceJson, participant.ProviderAccessCode),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                envelope.Signatures.Add(sig);
                _context.ContractSignatures.Add(sig);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return MapToStartResponse(envelope);
        }
        catch (Exception exception)
        {
            await MarkEnvelopeFailedAsync(envelope, exception);
            throw;
        }
    }

    public async Task<ESignEnvelopeResponse?> GetEnvelopeAsync(Guid userId, Guid envelopeId, CancellationToken cancellationToken = default)
    {
        var envelope = await _context.ContractSigningEnvelopes
            .Include(x => x.RentalContract)
                .ThenInclude(c => c!.Room)
                    .ThenInclude(r => r!.RoomingHouse)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == envelopeId, cancellationToken);

        if (envelope == null)
            return null;

        var contract = envelope.RentalContract;
        if (contract != null && contract.Room!.RoomingHouse!.LandlordUserId != userId && contract.MainTenantUserId != userId)
            throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Không có quyền truy cập envelope này.");

        return new ESignEnvelopeResponse
        {
            EnvelopeId = envelope.Id,
            ContractId = envelope.RentalContractId ?? Guid.Empty,
            AppendixId = envelope.RentalContractAppendixId,
            Provider = envelope.Provider.ToString(),
            Status = envelope.Status.ToString(),
            SignedFileId = null, // Can fetch from ContractFiles if needed
            CompletedAt = envelope.CompletedAt,
            Participants = envelope.Signatures.Select(s => new ESignParticipantResponse
            {
                UserId = s.SignerUserId,
                SignerRole = s.SignerRole.ToString(),
                SigningOrder = GetRequiredSigningOrder(s),
                Status = s.SignedAt.HasValue ? "Signed" : "Waiting",
                SigningUrl = s.SigningUrl
            }).ToList()
        };
    }

    public async Task ProcessProviderWebhookAsync(ESignProvider provider, string rawPayload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        if (!_eSignWebhookVerifier.VerifySignature(signatureHeader, rawPayload))
        {
            throw new UnauthorizedAccessException("Invalid webhook signature.");
        }

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload))).ToLowerInvariant();
        if (await _context.ESignWebhookLogs.AnyAsync(
                x => x.Provider == provider && x.IdempotencyKey == payloadHash,
                cancellationToken))
        {
            return;
        }

        var log = new ESignWebhookLog
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            RawPayload = rawPayload,
            RawPayloadHash = payloadHash,
            IdempotencyKey = payloadHash,
            SignatureStatus = WebhookSignatureStatus.Valid,
            ReceivedAt = DateTimeOffset.UtcNow,
            ProcessingStatus = ESignWebhookProcessingStatus.Pending
        };
        _context.ESignWebhookLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<VnptWebhookPayload>(rawPayload);
            if (payload == null)
            {
                throw new ApplicationException("Invalid webhook payload format.");
            }

            string? providerEnvelopeId = payload.DocumentId
                ?? payload.MaGiaoDich
                ?? payload.HopDongId?.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(providerEnvelopeId))
            {
                log.ProcessingStatus = ESignWebhookProcessingStatus.Failed;
                log.ErrorMessage = "Cannot extract Envelope ID from webhook payload.";
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "VNPT webhook did not contain a supported envelope id field. Status {ProviderStatus}; RawPayloadHash {PayloadHash}.",
                    payload.TrangThai,
                    payloadHash);
                return;
            }

            var envelope = await _context.ContractSigningEnvelopes
                .Include(e => e.Signatures)
                .Include(e => e.RentalContract)
                .Include(e => e.ContractAppendix)
                .FirstOrDefaultAsync(e => e.ProviderEnvelopeId == providerEnvelopeId, cancellationToken);

            if (envelope == null)
            {
                log.ProviderEnvelopeId = providerEnvelopeId;
                log.ProcessingStatus = ESignWebhookProcessingStatus.Failed;
                log.ErrorMessage = $"Envelope {providerEnvelopeId} not found.";
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "VNPT webhook envelope {ProviderEnvelopeId} was not found. Status {ProviderStatus}; RawPayloadHash {PayloadHash}.",
                    providerEnvelopeId,
                    payload.TrangThai,
                    payloadHash);
                return;
            }

            log.SigningEnvelopeId = envelope.Id;
            log.ProviderEnvelopeId = providerEnvelopeId;

            if (payload.TrangThai == 1 || payload.TrangThai == 4) // Waiting / signer configured by VNPT callback
            {
                envelope.Status = SigningEnvelopeStatus.WaitingForSigners;
            }
            else if (payload.TrangThai == 5) // PartiallySigned
            {
                envelope.Status = SigningEnvelopeStatus.PartiallySigned;
            }
            else if (payload.TrangThai == 10) // Completed
            {
                envelope.Status = SigningEnvelopeStatus.Completed;
                envelope.CompletedAt = DateTimeOffset.UtcNow;
                
                foreach (var sig in envelope.Signatures)
                {
                    if (sig.Status != ContractSignatureStatus.Signed)
                    {
                        sig.Status = ContractSignatureStatus.Signed;
                        sig.SignedAt = DateTimeOffset.UtcNow;
                    }
                }

                if (envelope.RentalContract != null)
                {
                    envelope.RentalContract.Status = RentalContractStatus.Active;
                    envelope.RentalContract.UpdatedAt = DateTimeOffset.UtcNow;
                }
                
                if (envelope.ContractAppendix != null)
                {
                    envelope.ContractAppendix.Status = ContractAppendixStatus.Active;
                    envelope.ContractAppendix.UpdatedAt = DateTimeOffset.UtcNow;
                }

                // Download and store the signed PDF
                using var signedPdfStream = await _eSignProviderClient.DownloadSignedPdfAsync(providerEnvelopeId, cancellationToken);
                await _contractFileService.StoreProviderSignedPdfAsync(envelope.Id, signedPdfStream, cancellationToken);
                await _contractFileService.EnsureMaskedReferenceFileAsync(
                    envelope.RentalContractId!.Value,
                    envelope.RentalContractAppendixId,
                    cancellationToken);
            }
            else if (payload.TrangThai == 6) // Failed/Rejected
            {
                envelope.Status = SigningEnvelopeStatus.Failed;
            }

            log.ProcessingStatus = ESignWebhookProcessingStatus.Processed;
            log.ProcessedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            if (payload.TrangThai == 10) // Completed
            {
                await _contractAppendixService.ApplyDueAppendicesAsync();
            }
        }
        catch (Exception ex)
        {
            log.ProcessingStatus = ESignWebhookProcessingStatus.Failed;
            log.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private class VnptWebhookPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("documentId")]
        public string? DocumentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("maGiaoDich")]
        public string? MaGiaoDich { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hopDongId")]
        public long? HopDongId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("trangThai")]
        public int? TrangThai { get; set; }
    }

    private async Task<ContractSigningEnvelope> LoadActiveEnvelopeAsync(
        Guid contractId,
        Guid? appendixId,
        CancellationToken cancellationToken)
    {
        return await _context.ContractSigningEnvelopes
            .Include(x => x.Signatures)
            .Where(x => x.RentalContractId == contractId && x.RentalContractAppendixId == appendixId &&
                        OtpCapableEnvelopeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Không có phiên ký VNPT đang hoạt động.");
    }

    private static ContractSignature GetNextSignatureForUser(ContractSigningEnvelope envelope, Guid userId)
    {
        var ownSignature = envelope.Signatures.FirstOrDefault(x => x.SignerUserId == userId)
            ?? throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Bạn không phải người ký của phiên này.");
        if (ownSignature.Status == ContractSignatureStatus.Signed)
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Bạn đã ký tài liệu này.");
        }

        var nextSignature = envelope.Signatures
            .Where(x => x.Status != ContractSignatureStatus.Signed)
            .OrderBy(GetRequiredSigningOrder)
            .FirstOrDefault()
            ?? throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Phiên ký đã hoàn tất.");
        if (nextSignature.SignerUserId != userId)
        {
            throw new ConflictException(ErrorCodes.RentalContractInvalidStatus, "Đang chờ người ký trước hoàn tất.");
        }

        return ownSignature;
    }

    private static int GetRequiredSigningOrder(ContractSignature signature)
    {
        if (signature.SigningOrder > 0)
        {
            return signature.SigningOrder;
        }

        return signature.SignerRole switch
        {
            ContractSignerRole.Landlord => 1,
            ContractSignerRole.Tenant => 2,
            _ => int.MaxValue
        };
    }

    private static void EnsureVnptSignerContacts(
        params (string Role, string? Email, string? PhoneNumber)[] signers)
    {
        var missingFields = new List<string>();
        foreach (var signer in signers)
        {
            if (string.IsNullOrWhiteSpace(signer.Email))
            {
                missingFields.Add($"{signer.Role} thiếu email");
            }
        }

        if (missingFields.Count > 0)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                $"Không thể tạo phiên ký VNPT: {string.Join("; ", missingFields)}. " +
                "Cả hai người ký phải cập nhật email để sử dụng Email OTP.");
        }
    }

    private string BuildStoredEvidence(string providerEvidenceJson, string providerAccessCode)
    {
        using var document = JsonDocument.Parse(providerEvidenceJson);
        var root = document.RootElement;
        var evidence = new StoredProviderEvidence
        {
            HdctId = root.GetProperty("HdctId").GetInt32(),
            PositionX = root.GetProperty("PositionX").GetInt32(),
            PositionY = root.GetProperty("PositionY").GetInt32(),
            PositionW = root.GetProperty("PositionW").GetInt32(),
            PositionH = root.GetProperty("PositionH").GetInt32(),
            PositionPage = root.GetProperty("PositionPage").GetInt32(),
            AccessCodeEncrypted = _sensitiveDataProtector.Encrypt(providerAccessCode)
        };
        return JsonSerializer.Serialize(evidence);
    }

    private static StoredProviderEvidence ReadEvidence(ContractSignature signature)
    {
        if (string.IsNullOrWhiteSpace(signature.ProviderEvidenceJson))
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Phiên ký VNPT cũ thiếu dữ liệu xác thực. Landlord cần khởi tạo lại phiên ký.");
        }

        try
        {
            return JsonSerializer.Deserialize<StoredProviderEvidence>(signature.ProviderEvidenceJson)
                ?? throw new JsonException("VNPT signing evidence is empty.");
        }
        catch (JsonException)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Dữ liệu xác thực của phiên ký VNPT không hợp lệ. Landlord cần khởi tạo lại phiên ký.");
        }
    }

    private static string MaskContact(string contact, ESignOtpMethod method)
    {
        if (method == ESignOtpMethod.EmailOtp)
        {
            var parts = contact.Split('@', 2);
            if (parts.Length != 2) return "***";
            var visible = parts[0].Length > 2 ? parts[0][..2] : parts[0][..1];
            return $"{visible}***@{parts[1]}";
        }

        var digits = contact.Trim();
        return digits.Length > 4 ? $"***{digits[^4..]}" : "***";
    }

    private sealed class StoredProviderEvidence
    {
        public int HdctId { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int PositionW { get; set; }
        public int PositionH { get; set; }
        public int PositionPage { get; set; }
        public string AccessCodeEncrypted { get; set; } = string.Empty;
        public long? OtpId { get; set; }
        public long? HdctPhienKyId { get; set; }
        public int? OtpMethod { get; set; }
        public DateTimeOffset? OtpRequestedAt { get; set; }
        public DateTimeOffset? OtpExpiresAt { get; set; }
    }

    private static void ValidateProviderSignerResults(
        IReadOnlyList<ESignSignerResult> providerSigners,
        IReadOnlyList<ESignSignerInput> requestedSigners)
    {
        if (providerSigners.Count != requestedSigners.Count)
        {
            throw new ApplicationException("VNPT returned an incomplete signer list.");
        }

        foreach (var requestedSigner in requestedSigners)
        {
            var providerSigner = providerSigners.FirstOrDefault(x => x.UserId == requestedSigner.UserId);
            if (providerSigner is null ||
                string.IsNullOrWhiteSpace(providerSigner.ProviderAccessCode) ||
                string.IsNullOrWhiteSpace(providerSigner.ProviderEvidenceJson))
            {
                throw new ApplicationException($"VNPT did not return complete signing evidence for {requestedSigner.SignerRole}.");
            }
        }
    }

    private async Task MarkEnvelopeFailedAsync(ContractSigningEnvelope envelope, Exception exception)
    {
        try
        {
            envelope.Status = SigningEnvelopeStatus.Failed;
            envelope.ProviderStatusReason = Truncate(exception.Message, 2000);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception saveException)
        {
            _logger.LogError(
                saveException,
                "Could not persist failed status for signing envelope {EnvelopeId}",
                envelope.Id);
        }
    }

    private async Task MarkAbandonedDraftEnvelopesFailedAsync(
        Guid contractId,
        Guid? appendixId,
        CancellationToken cancellationToken)
    {
        var draftEnvelopes = await _context.ContractSigningEnvelopes
            .Where(x => x.RentalContractId == contractId &&
                        x.RentalContractAppendixId == appendixId &&
                        x.Status == SigningEnvelopeStatus.Draft)
            .ToListAsync(cancellationToken);

        if (draftEnvelopes.Count == 0)
        {
            return;
        }

        foreach (var draftEnvelope in draftEnvelopes)
        {
            draftEnvelope.Status = SigningEnvelopeStatus.Failed;
            draftEnvelope.ProviderStatusReason = "Draft envelope was abandoned before VNPT provider creation.";
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool CanRequestOtp(ContractSigningEnvelope envelope)
    {
        return !string.IsNullOrWhiteSpace(envelope.ProviderEnvelopeId) &&
               OtpCapableEnvelopeStatuses.Contains(envelope.Status) &&
               envelope.Signatures.Any(x =>
                   x.Status != ContractSignatureStatus.Signed &&
                   !string.IsNullOrWhiteSpace(x.ProviderEvidenceJson));
    }

    private StartESignEnvelopeResponse MapToStartResponse(ContractSigningEnvelope envelope)
    {
        return new StartESignEnvelopeResponse
        {
            EnvelopeId = envelope.Id,
            ContractId = envelope.RentalContractId ?? Guid.Empty,
            AppendixId = envelope.RentalContractAppendixId,
            Provider = envelope.Provider.ToString(),
            Status = envelope.Status.ToString(),
            RequiresOtp = CanRequestOtp(envelope),
            Participants = envelope.Signatures.Select(s => new ESignParticipantResponse
            {
                UserId = s.SignerUserId,
                SignerRole = s.SignerRole.ToString(),
                SigningOrder = GetRequiredSigningOrder(s),
                Status = s.SignedAt.HasValue ? "Signed" : "Waiting",
                SigningUrl = s.SigningUrl
            }).ToList()
        };
    }

    private IQueryable<RentalContract> BaseContractQuery()
    {
        return _context.RentalContracts
            .Include(x => x.MainTenantUser)
                .ThenInclude(x => x!.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x!.RoomingHouse)
                    .ThenInclude(x => x!.Landlord)
                        .ThenInclude(x => x!.UserProfile);
    }

    private static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetAppliedAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                var newMainTenantUserId = ExtractUserId(change.NewValue);
                if (newMainTenantUserId.HasValue)
                {
                    currentMainTenantUserId = newMainTenantUserId.Value;
                }
            }
        }

        return currentMainTenantUserId;
    }

    private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.FieldName) == "maintenantuserid";
    }

    private static IEnumerable<ContractAppendix> GetAppliedAppendicesInOrder(RentalContract contract)
    {
        return contract.Appendices
            .Where(x =>
                x.AppliedAt.HasValue &&
                x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.AppliedAt ?? x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt);
    }

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out var directGuid))
        {
            return directGuid;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                Guid.TryParse(root.GetString(), out var jsonStringGuid))
            {
                return jsonStringGuid;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("userId", out var userIdElement) &&
                userIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(userIdElement.GetString(), out var objectGuid))
            {
                return objectGuid;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }
}
