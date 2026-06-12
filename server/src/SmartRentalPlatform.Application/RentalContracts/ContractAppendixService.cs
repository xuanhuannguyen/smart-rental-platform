using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class ContractAppendixService : IContractAppendixService
{
    private const string PdfContentType = "application/pdf";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext context;
    private readonly IContractSignatureOtpService contractSignatureOtpService;
    private readonly IContractPdfRenderer contractPdfRenderer;
    private readonly IPrivateStorageService privateStorageService;
    private readonly IHashService hashService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public ContractAppendixService(
        IAppDbContext context,
        IContractSignatureOtpService contractSignatureOtpService,
        IContractPdfRenderer contractPdfRenderer,
        IPrivateStorageService privateStorageService,
        IHashService hashService,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.contractSignatureOtpService = contractSignatureOtpService;
        this.contractPdfRenderer = contractPdfRenderer;
        this.privateStorageService = privateStorageService;
        this.hashService = hashService;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public async Task<ContractAppendixResponse?> CreateAsync(
        Guid userId,
        Guid contractId,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var contract = await BaseContractQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureCanAccess(userId, contract);
        EnsureContractActive(contract);
        EnsureNoPendingAppendix(contract);

        var signerRole = GetSignerRole(userId, contract);
        var parsedChanges = request.Changes
            .Select((change, index) => ParseAppendixChange(change, index + 1))
            .ToList();

        await ValidateBusinessRulesAsync(contract, signerRole, parsedChanges, request.EffectiveDate, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var appendix = new ContractAppendix
        {
            Id = Guid.NewGuid(),
            RentalContractId = contract.Id,
            AppendixNumber = GenerateAppendixNumber(contract),
            EffectiveDate = request.EffectiveDate,
            Status = ContractAppendixStatus.PendingSignature,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var index = 0; index < request.Changes.Count; index++)
        {
            var change = request.Changes[index];
            var changeType = ParseEnum<ContractAppendixChangeType>(change.ChangeType, "Loại thay đổi phụ lục không hợp lệ.");
            var targetType = ParseEnum<ContractAppendixTargetType>(change.TargetType, "Đối tượng thay đổi phụ lục không hợp lệ.");

            appendix.Changes.Add(new ContractAppendixChange
            {
                Id = Guid.NewGuid(),
                RentalContractAppendixId = appendix.Id,
                ChangeType = changeType,
                TargetType = targetType,
                TargetId = change.TargetId,
                FieldName = NormalizeOptionalText(change.FieldName),
                OldValue = ResolveOldValue(contract, change, changeType, targetType),
                NewValue = NormalizeNewValue(change, changeType, targetType),
                SortOrder = index + 1,
                CreatedAt = now
            });
        }

        context.ContractAppendices.Add(appendix);
        await context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(userId, contract.Id, appendix.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ContractAppendixResponse>> GetByContractAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return Array.Empty<ContractAppendixResponse>();
        }

        EnsureCanAccess(userId, contract);

        var appendices = await BaseAppendixQuery()
            .AsNoTracking()
            .Where(x => x.RentalContractId == contractId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return appendices
            .Where(x => CanViewAppendix(userId, x))
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<ContractAppendixResponse?> GetByIdAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureAppendixCanPreview(appendix);
        EnsureCanViewAppendix(userId, appendix);
        return MapToResponse(appendix);
    }

    public async Task<ContractAppendixResponse?> UpdateAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var appendix = await BaseAppendixQuery()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureContractActive(appendix.RentalContract);
        EnsureCanUpdateRevision(userId, appendix);

        var signerRole = GetSignerRole(userId, appendix.RentalContract);
        var parsedChanges = request.Changes
            .Select((change, index) => ParseAppendixChange(change, index + 1))
            .ToList();

        await ValidateBusinessRulesAsync(
            appendix.RentalContract,
            signerRole,
            parsedChanges,
            request.EffectiveDate,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            context.ContractAppendixChanges.RemoveRange(appendix.Changes);
            appendix.Changes.Clear();

            if (appendix.Signatures.Count > 0)
            {
                context.ContractSignatures.RemoveRange(appendix.Signatures);
                appendix.Signatures.Clear();
            }

            appendix.EffectiveDate = request.EffectiveDate;
            appendix.Status = ContractAppendixStatus.PendingSignature;
            appendix.StatusReason = null;
            appendix.UpdatedAt = now;

            for (var index = 0; index < request.Changes.Count; index++)
            {
                var change = request.Changes[index];
                var changeType = ParseEnum<ContractAppendixChangeType>(change.ChangeType, "Loại thay đổi phụ lục không hợp lệ.");
                var targetType = ParseEnum<ContractAppendixTargetType>(change.TargetType, "Đối tượng thay đổi phụ lục không hợp lệ.");

                appendix.Changes.Add(new ContractAppendixChange
                {
                    Id = Guid.NewGuid(),
                    RentalContractAppendixId = appendix.Id,
                    ChangeType = changeType,
                    TargetType = targetType,
                    TargetId = change.TargetId,
                    FieldName = NormalizeOptionalText(change.FieldName),
                    OldValue = ResolveOldValue(appendix.RentalContract, change, changeType, targetType),
                    NewValue = NormalizeNewValue(change, changeType, targetType),
                    SortOrder = index + 1,
                    CreatedAt = now
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(userId, contractId, appendixId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContractPreviewPdfResult?> GetPreviewPdfAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureCanViewAppendix(userId, appendix);

        var renderOptions = await BuildAppendixRenderOptionsAsync(appendix, ContractFileVariant.Raw, cancellationToken);
        var pdfBytes = contractPdfRenderer.RenderSignedContractAppendix(appendix, renderOptions);
        var fileName = $"appendix-preview-{appendix.AppendixNumber}.pdf";

        return new ContractPreviewPdfResult(pdfBytes, PdfContentType, fileName);
    }

    public async Task<RequestContractSignatureOtpResponse?> RequestSignOtpAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        var signerRole = GetSignerRole(userId, appendix.RentalContract);
        return await contractSignatureOtpService.RequestAppendixOtpAsync(
            userId,
            contractId,
            appendixId,
            signerRole,
            cancellationToken);
    }

    public async Task<ContractAppendixResponse?> SignAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        SignContractRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureContractActive(appendix.RentalContract);
        EnsureAppendixPendingSignature(appendix);
        var signerRole = GetSignerRole(userId, appendix.RentalContract);
        EnsureAppendixNotSigned(appendix, signerRole);

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            await contractSignatureOtpService.VerifyAndConsumeAppendixOtpAsync(
                userId,
                appendix.Id,
                signerRole,
                request.Otp,
                cancellationToken);

            appendix.Signatures.Add(new ContractSignature
            {
                Id = Guid.NewGuid(),
                RentalContractAppendixId = appendix.Id,
                SignerUserId = userId,
                SignerRole = signerRole,
                SignatureMethod = ContractSignatureMethod.EmailOtp,
                SignatureText = NormalizeOptionalText(request.SignatureText),
                IpAddress = NormalizeOptionalText(ipAddress),
                UserAgent = NormalizeOptionalText(userAgent),
                SignedAt = now,
                CreatedAt = now
            });

            appendix.UpdatedAt = now;

            if (HasBothSignatures(appendix, signerRole))
            {
                appendix.Status = ContractAppendixStatus.Active;
                appendix.ActivatedAt = now;
                appendix.StatusReason = null;
                await ApplyAppendixChangesAsync(appendix, now, cancellationToken);
                await GenerateAppendixFileAsync(appendix, now, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(userId, contractId, appendixId, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContractAppendixResponse?> RejectAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        RejectContractRequest request,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureCanAccess(userId, appendix.RentalContract);
        EnsureAppendixPendingSignature(appendix);
        _ = GetSignerRole(userId, appendix.RentalContract);

        var reason = NormalizeRequiredText(request.Reason, "Lý do từ chối không được để trống.");
        var now = DateTimeOffset.UtcNow;

        appendix.Status = ContractAppendixStatus.Rejected;
        appendix.StatusReason = reason;
        appendix.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(userId, contractId, appendixId, cancellationToken);
    }

    public async Task<ContractAppendixResponse?> RequestRevisionAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var appendix = await BaseAppendixQuery()
            .FirstOrDefaultAsync(x => x.Id == appendixId && x.RentalContractId == contractId, cancellationToken);

        if (appendix is null)
        {
            return null;
        }

        EnsureCanAccess(userId, appendix.RentalContract);
        EnsureAppendixPendingSignature(appendix);

        var signerRole = GetSignerRole(userId, appendix.RentalContract);
        var reason = NormalizeRequiredText(request.Reason, "Lý do yêu cầu sửa phụ lục không được để trống.");
        var now = DateTimeOffset.UtcNow;

        appendix.Status = signerRole == ContractSignerRole.Landlord
            ? ContractAppendixStatus.LandlordRevisionRequested
            : ContractAppendixStatus.TenantRevisionRequested;
        appendix.StatusReason = reason;
        appendix.UpdatedAt = now;

        if (signerRole == ContractSignerRole.Tenant && appendix.Signatures.Count > 0)
        {
            context.ContractSignatures.RemoveRange(appendix.Signatures);
            appendix.Signatures.Clear();
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(userId, contractId, appendixId, cancellationToken);
    }

    private IQueryable<RentalContract> BaseContractQuery()
    {
        return context.RentalContracts
            .Include(x => x.MainTenantUser)
                .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.Landlord)
                        .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.RentalPolicy)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.Documents)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.User)
                    .ThenInclude(x => x!.UserProfile);
    }

    private IQueryable<ContractAppendix> BaseAppendixQuery()
    {
        return context.ContractAppendices
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.MainTenantUser)
                    .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.Landlord)
                            .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.Documents)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.User)
                        .ThenInclude(x => x!.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Changes)
            .Include(x => x.Changes)
            .Include(x => x.Signatures)
            .Include(x => x.Files);
    }

    private async Task GenerateAppendixFileAsync(
        ContractAppendix appendix,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await EnsureAppendixFileVariantAsync(appendix, ContractFileVariant.Raw, now, cancellationToken);
        await EnsureAppendixFileVariantAsync(appendix, ContractFileVariant.Masked, now, cancellationToken);
    }

    private async Task EnsureAppendixFileVariantAsync(
        ContractAppendix appendix,
        ContractFileVariant fileVariant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (appendix.Files.Any(x =>
                x.FileVariant == fileVariant &&
                x.StorageObjectKey.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var renderOptions = await BuildAppendixRenderOptionsAsync(appendix, fileVariant, cancellationToken);
        var pdfBytes = contractPdfRenderer.RenderSignedContractAppendix(appendix, renderOptions);
        var objectKey = $"contracts/{appendix.RentalContractId:N}/appendices/{appendix.Id:N}/signed-appendix-{fileVariant.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmmss}.pdf";

        await using var stream = new MemoryStream(pdfBytes);
        var storageObjectKey = await privateStorageService.UploadAsync(
            stream,
            PdfContentType,
            objectKey,
            cancellationToken);

        appendix.Files.Add(new ContractFile
        {
            Id = Guid.NewGuid(),
            RentalContractId = appendix.RentalContractId,
            RentalContractAppendixId = appendix.Id,
            StorageObjectKey = storageObjectKey,
            FileVariant = fileVariant,
            CreatedAt = now
        });
    }

    private async Task ApplyAppendixChangesAsync(
        ContractAppendix appendix,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
        {
            if (change.TargetType != ContractAppendixTargetType.ContractOccupant)
            {
                continue;
            }

            if (change.ChangeType == ContractAppendixChangeType.Add)
            {
                var occupantRequest = ParseOccupantRequest(change.NewValue);
                var occupant = await CreateOccupantFromAppendixChangeAsync(
                    appendix.RentalContractId,
                    occupantRequest,
                    now,
                    cancellationToken);

                change.TargetId = occupant.Id;
                context.ContractOccupants.Add(occupant);
                appendix.RentalContract.Occupants.Add(occupant);
                continue;
            }

            if (change.ChangeType == ContractAppendixChangeType.Remove)
            {
                var occupant = appendix.RentalContract.Occupants.FirstOrDefault(x => x.Id == change.TargetId);
                if (occupant is null || occupant.Status != ContractOccupantStatus.Active)
                {
                    throw new ConflictException(
                        ErrorCodes.RentalContractInvalidOccupant,
                        "Người ở cần xóa không tồn tại hoặc không còn đang ở trong hợp đồng.",
                        new { change.TargetId });
                }

                occupant.Status = ContractOccupantStatus.MoveOut;
                occupant.MoveOutDate ??= TryParseDateOnly(change.NewValue, out var moveOutDate)
                    ? moveOutDate
                    : appendix.EffectiveDate;
                occupant.UpdatedAt = now;
            }
        }
    }

    private async Task<ContractOccupant> CreateOccupantFromAppendixChangeAsync(
        Guid contractId,
        ContractOccupantRequest occupantRequest,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateAppendixOccupantRequest(occupantRequest);

        var emailKey = occupantRequest.Email?.Trim().ToLowerInvariant();
        var verifiedAccount = !string.IsNullOrEmpty(emailKey)
            ? await GetVerifiedOccupantAccountByEmailAsync(emailKey, cancellationToken)
            : null;

        var occupant = new ContractOccupant
        {
            Id = Guid.NewGuid(),
            RentalContractId = contractId,
            UserId = verifiedAccount?.UserId,
            FullName = verifiedAccount?.FullName ?? occupantRequest.FullName!.Trim(),
            PhoneNumber = verifiedAccount?.PhoneNumber ?? NormalizeOptionalText(occupantRequest.PhoneNumber),
            DateOfBirth = verifiedAccount?.DateOfBirth ?? occupantRequest.DateOfBirth!.Value,
            RelationshipToMainTenant = NormalizeOptionalText(occupantRequest.RelationshipToMainTenant),
            MoveInDate = occupantRequest.MoveInDate,
            MoveOutDate = occupantRequest.MoveOutDate,
            Status = ContractOccupantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (occupantRequest.Document is not null)
        {
            var documentRequest = occupantRequest.Document;
            occupant.Documents.Add(new ContractOccupantDocument
            {
                Id = Guid.NewGuid(),
                RentalContractOccupantId = occupant.Id,
                DocumentType = documentRequest.DocumentType.Trim(),
                DocumentNumberMasked = MaskDocumentNumber(documentRequest.DocumentNumber),
                DocumentNumberHash = HashDocumentNumber(documentRequest.DocumentNumber),
                DocumentNumberEncrypted = EncryptDocumentNumber(documentRequest.DocumentNumber),
                FrontImageObjectKey = documentRequest.FrontImageObjectKey.Trim(),
                BackImageObjectKey = NormalizeOptionalText(documentRequest.BackImageObjectKey),
                ExtraImageObjectKey = NormalizeOptionalText(documentRequest.ExtraImageObjectKey),
                UploadedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return occupant;
    }

    private async Task<ContractRenderOptions> BuildAppendixRenderOptionsAsync(
        ContractAppendix appendix,
        ContractFileVariant fileVariant,
        CancellationToken cancellationToken)
    {
        var showFullDocumentNumbers = fileVariant == ContractFileVariant.Raw;
        return new ContractRenderOptions
        {
            ViewerMode = fileVariant.ToString(),
            ShowFullDocumentNumbers = showFullDocumentNumbers,
            VisibleOccupantIds = null,
            UserDocumentNumbersByUserId = showFullDocumentNumbers
                ? await GetDecryptedUserDocumentNumbersAsync(appendix.RentalContract, appendix, cancellationToken)
                : new Dictionary<Guid, string?>(),
            OccupantDocumentNumbersByDocumentId = showFullDocumentNumbers
                ? GetDecryptedOccupantDocumentNumbers(appendix.RentalContract)
                : new Dictionary<Guid, string?>()
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> GetDecryptedUserDocumentNumbersAsync(
        RentalContract contract,
        ContractAppendix appendix,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>
        {
            contract.Room.RoomingHouse.LandlordUserId,
            contract.MainTenantUserId
        };

        foreach (var occupantUserId in contract.Occupants
                     .Where(x => x.UserId.HasValue)
                     .Select(x => x.UserId!.Value))
        {
            userIds.Add(occupantUserId);
        }

        foreach (var addedUserId in GetAddedOccupantUserIds(appendix))
        {
            userIds.Add(addedUserId);
        }

        var approvedKycs = await context.KycVerifications
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
            .ToListAsync(cancellationToken);

        return approvedKycs
            .GroupBy(x => x.UserId)
            .Select(x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First())
            .Select(x => new
            {
                x.UserId,
                DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .ToDictionary(x => x.UserId, x => x.DocumentNumber);
    }

    private IReadOnlyDictionary<Guid, string?> GetDecryptedOccupantDocumentNumbers(RentalContract contract)
    {
        return contract.Occupants
            .SelectMany(x => x.Documents)
            .Select(x => new
            {
                x.Id,
                DocumentNumber = DecryptDocumentNumber(x.DocumentNumberEncrypted)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.DocumentNumber))
            .ToDictionary(x => x.Id, x => x.DocumentNumber);
    }

    private static IEnumerable<Guid> GetAddedOccupantUserIds(ContractAppendix appendix)
    {
        foreach (var change in appendix.Changes)
        {
            if (change.TargetType != ContractAppendixTargetType.ContractOccupant ||
                change.ChangeType != ContractAppendixChangeType.Add)
            {
                continue;
            }

            var userId = ExtractUserId(change.NewValue);
            if (userId.HasValue)
            {
                yield return userId.Value;
            }
        }
    }

    private static void ValidateCreateRequest(CreateContractAppendixRequest request)
    {
        if (request.EffectiveDate == default)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày hiệu lực phụ lục không hợp lệ.");
        }

        if (request.Changes.Count == 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phụ lục phải có ít nhất một nội dung thay đổi.");
        }
    }

    private static void EnsureContractActive(RentalContract contract)
    {
        if (contract.Status == RentalContractStatus.Active)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Chỉ có thể lập phụ lục cho hợp đồng đang có hiệu lực.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    private static void EnsureNoPendingAppendix(RentalContract contract)
    {
        if (!contract.Appendices.Any(x =>
                x.Status is ContractAppendixStatus.PendingSignature
                    or ContractAppendixStatus.LandlordRevisionRequested
                    or ContractAppendixStatus.TenantRevisionRequested))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Hợp đồng đang có phụ lục chờ ký, vui lòng hoàn tất hoặc từ chối phụ lục hiện tại trước khi tạo phụ lục mới.",
            new { contract.Id });
    }

    private static void EnsureAppendixPendingSignature(ContractAppendix appendix)
    {
        if (appendix.Status == ContractAppendixStatus.PendingSignature)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Trạng thái phụ lục không cho phép thao tác này.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }

    private static void EnsureAppendixCanPreview(ContractAppendix appendix)
    {
        if (appendix.Status is not ContractAppendixStatus.Active)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Phụ lục đã có hiệu lực, vui lòng xem file phụ lục đã ký.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }

    private static void EnsureCanUpdateRevision(Guid userId, ContractAppendix appendix)
    {
        if (appendix.Status == ContractAppendixStatus.LandlordRevisionRequested)
        {
            if (GetCurrentMainTenantUserId(appendix.RentalContract) == userId)
            {
                return;
            }
        }

        if (appendix.Status == ContractAppendixStatus.TenantRevisionRequested)
        {
            if (appendix.RentalContract.Room.RoomingHouse.LandlordUserId == userId)
            {
                return;
            }
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixInvalidStatus,
            "Trạng thái phụ lục không cho phép cập nhật nội dung.",
            new { appendix.Id, currentStatus = appendix.Status.ToString() });
    }

    private static void EnsureCanAccess(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId ||
            GetMainTenantUserIds(contract).Contains(userId))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác với hợp đồng này.",
            new { contract.Id });
    }

    private static void EnsureCanViewAppendix(Guid userId, ContractAppendix appendix)
    {
        if (CanViewAppendix(userId, appendix))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền xem phụ lục này.",
            new { appendix.Id });
    }

    private static bool CanViewAppendix(Guid userId, ContractAppendix appendix)
    {
        var contract = appendix.RentalContract;
        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            return true;
        }

        if (GetMainTenantUserIdBeforeAppendix(contract, appendix) == userId)
        {
            return true;
        }

        return IsMainTenantChangedToUser(appendix, userId);
    }

    private static ContractSignerRole GetSignerRole(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            return ContractSignerRole.Landlord;
        }

        if (GetCurrentMainTenantUserId(contract) == userId)
        {
            return ContractSignerRole.Tenant;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác với phụ lục này.",
            new { contract.Id });
    }

    private static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    NormalizeFieldName(change.FieldName) != "maintenantuserid")
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

    private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
    {
        var userIds = new HashSet<Guid> { contract.MainTenantUserId };

        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes)
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    NormalizeFieldName(change.FieldName) != "maintenantuserid")
                {
                    continue;
                }

                var userId = ExtractUserId(change.NewValue);
                if (userId.HasValue)
                {
                    userIds.Add(userId.Value);
                }
            }
        }

        return userIds;
    }

    private static Guid GetMainTenantUserIdBeforeAppendix(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        var currentMainTenantUserId = contract.MainTenantUserId;

        foreach (var appendix in GetActiveAppendicesBefore(contract, targetAppendix))
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

    private static bool IsMainTenantChangedToUser(ContractAppendix appendix, Guid userId)
    {
        return appendix.Changes.Any(change =>
            IsMainTenantUserIdChange(change) &&
            ExtractUserId(change.NewValue) == userId);
    }

    private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.FieldName) == "maintenantuserid";
    }

    private static IEnumerable<ContractAppendix> GetActiveAppendicesBefore(
        RentalContract contract,
        ContractAppendix targetAppendix)
    {
        return GetActiveAppendicesInOrder(contract)
            .Where(x => x.Id != targetAppendix.Id && x.CreatedAt <= targetAppendix.CreatedAt);
    }

    private static IEnumerable<ContractAppendix> GetActiveAppendicesInOrder(RentalContract contract)
    {
        return contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt);
    }

    private static void EnsureAppendixNotSigned(ContractAppendix appendix, ContractSignerRole signerRole)
    {
        if (!appendix.Signatures.Any(x => x.SignerRole == signerRole))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.ContractAppendixAlreadySigned,
            "Bên này đã ký phụ lục.",
            new { appendix.Id, signerRole = signerRole.ToString() });
    }

    private static bool HasBothSignatures(ContractAppendix appendix, ContractSignerRole newSignerRole)
    {
        return appendix.Signatures.Any(x => x.SignerRole == ContractSignerRole.Landlord) ||
               newSignerRole == ContractSignerRole.Landlord
            ? appendix.Signatures.Any(x => x.SignerRole == ContractSignerRole.Tenant) ||
              newSignerRole == ContractSignerRole.Tenant
            : false;
    }

    private static string GenerateAppendixNumber(RentalContract contract)
    {
        var nextNumber = contract.Appendices.Count + 1;
        return $"PL-{nextNumber:000}-{contract.ContractNumber}"[..Math.Min(50, $"PL-{nextNumber:000}-{contract.ContractNumber}".Length)];
    }

    private static string? ResolveOldValue(
        RentalContract contract,
        ContractAppendixChangeRequest request,
        ContractAppendixChangeType changeType,
        ContractAppendixTargetType targetType)
    {
        if (changeType == ContractAppendixChangeType.Add)
        {
            return null;
        }

        object? value = targetType switch
        {
            ContractAppendixTargetType.Contract => ResolveContractOldValue(contract, request.FieldName),
            ContractAppendixTargetType.ContractOccupant => ResolveOccupantOldValue(contract, request.TargetId, request.FieldName),
            _ => null
        };

        return value is null ? null : JsonSerializer.Serialize(value);
    }

    private static object? ResolveContractOldValue(RentalContract contract, string? fieldName)
    {
        return NormalizeFieldName(fieldName) switch
        {
            "startdate" => contract.StartDate,
            "enddate" => contract.EndDate,
            "monthlyrent" => contract.MonthlyRent,
            "depositamount" => contract.DepositAmount,
            "paymentday" => contract.PaymentDay,
            _ => null
        };
    }

    private static object? ResolveOccupantOldValue(RentalContract contract, Guid? occupantId, string? fieldName)
    {
        var occupant = contract.Occupants.FirstOrDefault(x => x.Id == occupantId);
        if (occupant is null)
        {
            return null;
        }

        return NormalizeFieldName(fieldName) switch
        {
            "fullname" => occupant.FullName,
            "phonenumber" => occupant.PhoneNumber,
            "dateofbirth" => occupant.DateOfBirth,
            "relationshiptomaintenant" => occupant.RelationshipToMainTenant,
            "moveindate" => occupant.MoveInDate,
            "moveoutdate" => occupant.MoveOutDate,
            "status" => occupant.Status.ToString(),
            _ => null
        };
    }

    private static string? ToJsonString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Serialize(value.Trim());
    }

    private static string? NormalizeNewValue(
        ContractAppendixChangeRequest change,
        ContractAppendixChangeType changeType,
        ContractAppendixTargetType targetType)
    {
        if (changeType == ContractAppendixChangeType.Add &&
            targetType == ContractAppendixTargetType.ContractOccupant)
        {
            var occupant = ParseOccupantRequest(change.NewValue);
            return JsonSerializer.Serialize(occupant, JsonOptions);
        }

        return ToJsonString(change.NewValue);
    }

    private static ContractOccupantRequest ParseOccupantRequest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Thông tin người ở mới trong phụ lục không được để trống.");
        }

        var json = value.Trim();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                json = document.RootElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // The next deserialize step will return a clearer business error.
        }

        try
        {
            return JsonSerializer.Deserialize<ContractOccupantRequest>(json, JsonOptions)
                ?? throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Thông tin người ở mới trong phụ lục không hợp lệ.");
        }
        catch (JsonException exception)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Thông tin người ở mới trong phụ lục phải là JSON hợp lệ.",
                new { exception.Message });
        }
    }

    private async Task ValidateBusinessRulesAsync(
        RentalContract contract,
        ContractSignerRole requesterRole,
        IReadOnlyCollection<ParsedAppendixChange> changes,
        DateOnly effectiveDate,
        CancellationToken cancellationToken)
    {
        if (effectiveDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày hiệu lực phụ lục không được nằm trong quá khứ.");
        }

        if (requesterRole == ContractSignerRole.Tenant)
        {
            ValidateTenantAppendixRules(contract, changes);
            await ValidateAddedOccupantsAsync(changes, cancellationToken);
            await ValidateMainTenantRulesAsync(contract, changes, cancellationToken);
            ValidateRenewalRules(contract, changes);
            ValidateOccupantLimit(contract, changes);
            return;
        }

        ValidateLandlordAppendixRules(changes);
    }

    private async Task ValidateAddedOccupantsAsync(
        IReadOnlyCollection<ParsedAppendixChange> changes,
        CancellationToken cancellationToken)
    {
        var addedOccupants = changes
            .Where(x => x.TargetType == ContractAppendixTargetType.ContractOccupant &&
                        x.ChangeType == ContractAppendixChangeType.Add)
            .Select(x => ParseOccupantRequest(x.Request.NewValue))
            .ToList();

        foreach (var occupant in addedOccupants)
        {
            ValidateAppendixOccupantRequest(occupant);
            if (!string.IsNullOrWhiteSpace(occupant.Email))
            {
                _ = await GetVerifiedOccupantAccountByEmailAsync(occupant.Email.Trim().ToLowerInvariant(), cancellationToken);
            }
        }
    }

    private static void ValidateAppendixOccupantRequest(ContractOccupantRequest occupant)
    {
        if (occupant.MoveInDate == default)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Ngày chuyển vào của người ở không được để trống.");
        }

        if (!string.IsNullOrWhiteSpace(occupant.Email))
        {
            if (occupant.Document is not null)
            {
                throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Người ở đã có tài khoản và KYC không được gửi giấy tờ trong phụ lục.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(occupant.FullName))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở chưa có tài khoản phải có họ tên.");
        }

        if (string.IsNullOrWhiteSpace(occupant.PhoneNumber))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở chưa có tài khoản phải có số điện thoại.");
        }

        if (!occupant.DateOfBirth.HasValue || occupant.DateOfBirth.Value == default)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở chưa có tài khoản phải có ngày sinh.");
        }

        if (occupant.Document is null)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở chưa có tài khoản phải có giấy tờ.");
        }

        if (string.IsNullOrWhiteSpace(occupant.Document.DocumentType) ||
            string.IsNullOrWhiteSpace(occupant.Document.DocumentNumber) ||
            string.IsNullOrWhiteSpace(occupant.Document.FrontImageObjectKey))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Giấy tờ người ở phải có loại giấy tờ, số giấy tờ và ảnh mặt trước.");
        }
    }

    private async Task<VerifiedOccupantAccount> GetVerifiedOccupantAccountByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower() && x.DeletedAt == null, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở có tài khoản không tồn tại.",
                new { email });
        }

        var approvedKyc = await context.KycVerifications
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvedKyc is null)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Người ở có tài khoản phải hoàn tất KYC trước khi được thêm vào phụ lục.",
                new { email });
        }

        var fullName = NormalizeOptionalText(approvedKyc.OcrFullName)
            ?? NormalizeOptionalText(user.UserProfile?.FullName)
            ?? NormalizeOptionalText(user.DisplayName);
        var dateOfBirth = approvedKyc.OcrDateOfBirth ?? user.UserProfile?.DateOfBirth;

        if (string.IsNullOrWhiteSpace(fullName) || !dateOfBirth.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Thông tin KYC đã duyệt của người ở chưa đủ họ tên hoặc ngày sinh.",
                new { email });
        }

        return new VerifiedOccupantAccount(
            user.Id,
            fullName,
            NormalizeOptionalText(user.PhoneNumber),
            dateOfBirth.Value);
    }

    private static void ValidateTenantAppendixRules(
        RentalContract contract,
        IReadOnlyCollection<ParsedAppendixChange> changes)
    {
        foreach (var change in changes)
        {
            var fieldName = NormalizeFieldName(change.Request.FieldName);
            var isOccupantChange =
                change.TargetType == ContractAppendixTargetType.ContractOccupant &&
                change.ChangeType is ContractAppendixChangeType.Add or ContractAppendixChangeType.Remove;

            var isMainTenantChange =
                change.TargetType == ContractAppendixTargetType.Contract &&
                change.ChangeType == ContractAppendixChangeType.Update &&
                fieldName == "maintenantuserid";

            var isRenewalChange =
                change.TargetType == ContractAppendixTargetType.Contract &&
                change.ChangeType == ContractAppendixChangeType.Update &&
                fieldName == "enddate";

            if (change.TargetType == ContractAppendixTargetType.ContractOccupant &&
                change.ChangeType == ContractAppendixChangeType.Add &&
                change.Request.TargetId.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Phụ lục thêm người ở mới không được gửi targetId.");
            }

            if (change.TargetType == ContractAppendixTargetType.ContractOccupant &&
                change.ChangeType == ContractAppendixChangeType.Remove &&
                !contract.Occupants.Any(occupant =>
                    occupant.Id == change.Request.TargetId &&
                    occupant.Status == ContractOccupantStatus.Active))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Người ở cần xóa không tồn tại hoặc không còn đang ở trong hợp đồng.",
                    new { change.Request.TargetId });
            }

            if (isOccupantChange || isMainTenantChange || isRenewalChange)
            {
                continue;
            }

            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Người thuê chỉ được tạo phụ lục để thêm/bớt người ở, chuyển người chịu trách nhiệm hợp đồng hoặc gia hạn hợp đồng.",
                new
                {
                    change.SortOrder,
                    change.ChangeType,
                    change.TargetType,
                    fieldName
                });
        }

        var currentMainTenantUserId = GetCurrentMainTenantUserId(contract);
        var removedMainTenant = changes.Any(change =>
            change.ChangeType == ContractAppendixChangeType.Remove &&
            change.TargetType == ContractAppendixTargetType.ContractOccupant &&
            contract.Occupants.Any(occupant =>
                occupant.Id == change.Request.TargetId &&
                occupant.UserId == currentMainTenantUserId));

        var changesMainTenant = changes.Any(IsMainTenantUserIdChange);

        if (removedMainTenant && !changesMainTenant)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Nếu người chịu trách nhiệm hợp đồng hiện tại rời đi thì phụ lục bắt buộc phải chuyển người chịu trách nhiệm hợp đồng.");
        }
    }

    private static void ValidateLandlordAppendixRules(IReadOnlyCollection<ParsedAppendixChange> changes)
    {
        foreach (var change in changes)
        {
            var fieldName = NormalizeFieldName(change.Request.FieldName);
            var isAllowed =
                change.TargetType == ContractAppendixTargetType.Contract &&
                change.ChangeType == ContractAppendixChangeType.Update &&
                fieldName is "monthlyrent" or "paymentday";

            if (isAllowed)
            {
                ValidateLandlordChangeValue(change, fieldName);
                continue;
            }

            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Chủ trọ chỉ được tạo phụ lục để thay đổi giá thuê hoặc ngày thanh toán hóa đơn.",
                new
                {
                    change.SortOrder,
                    change.ChangeType,
                    change.TargetType,
                    fieldName
                });
        }
    }

    private static void ValidateLandlordChangeValue(ParsedAppendixChange change, string fieldName)
    {
        if (fieldName == "monthlyrent")
        {
            if (decimal.TryParse(change.Request.NewValue, out var monthlyRent) && monthlyRent > 0)
            {
                return;
            }

            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giá thuê mới phải lớn hơn 0.");
        }

        if (int.TryParse(change.Request.NewValue, out var paymentDay) && paymentDay is >= 1 and <= 28)
        {
            return;
        }

        throw new BadRequestException(
            ErrorCodes.ValidationError,
            "Ngày thanh toán mới phải nằm trong khoảng 1 đến 28.");
    }

    private async Task ValidateMainTenantRulesAsync(
        RentalContract contract,
        IReadOnlyCollection<ParsedAppendixChange> changes,
        CancellationToken cancellationToken)
    {
        var mainTenantChange = changes.FirstOrDefault(IsMainTenantUserIdChange);
        if (mainTenantChange is null)
        {
            return;
        }

        var newMainTenantUserId = ExtractUserId(mainTenantChange.Request.NewValue);
        if (newMainTenantUserId is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Người chịu trách nhiệm hợp đồng mới bắt buộc phải có userId.");
        }

        var userExists = await context.Users.AnyAsync(x => x.Id == newMainTenantUserId.Value && x.DeletedAt == null, cancellationToken);
        if (!userExists)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Người chịu trách nhiệm hợp đồng mới không tồn tại.",
                new { userId = newMainTenantUserId.Value });
        }

        var hasApprovedKyc = await context.KycVerifications.AnyAsync(
            x => x.UserId == newMainTenantUserId.Value &&
                 x.Status == KycVerificationStatus.Approved,
            cancellationToken);

        if (!hasApprovedKyc)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Người chịu trách nhiệm hợp đồng mới phải hoàn tất KYC.",
                new { userId = newMainTenantUserId.Value });
        }

        var remainsOrAlreadyOccupant = contract.Occupants.Any(occupant =>
            occupant.UserId == newMainTenantUserId.Value &&
            occupant.Status == ContractOccupantStatus.Active &&
            !changes.Any(change =>
                change.ChangeType == ContractAppendixChangeType.Remove &&
                change.TargetType == ContractAppendixTargetType.ContractOccupant &&
                change.Request.TargetId == occupant.Id));

        var addedInThisAppendix = changes.Any(change =>
            change.ChangeType == ContractAppendixChangeType.Add &&
            change.TargetType == ContractAppendixTargetType.ContractOccupant &&
            ExtractUserId(change.Request.NewValue) == newMainTenantUserId.Value);

        if (remainsOrAlreadyOccupant || addedInThisAppendix)
        {
            return;
        }

        throw new BadRequestException(
            ErrorCodes.ValidationError,
            "Người chịu trách nhiệm hợp đồng mới phải là người ở hiện tại hoặc được thêm vào trong cùng phụ lục.");
    }

    private static void ValidateRenewalRules(
        RentalContract contract,
        IReadOnlyCollection<ParsedAppendixChange> changes)
    {
        var renewalChange = changes.FirstOrDefault(change =>
            change.TargetType == ContractAppendixTargetType.Contract &&
            change.ChangeType == ContractAppendixChangeType.Update &&
            NormalizeFieldName(change.Request.FieldName) == "enddate");

        if (renewalChange is null)
        {
            return;
        }

        if (!TryParseDateOnly(renewalChange.Request.NewValue, out var newEndDate))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày kết thúc mới không hợp lệ.");
        }

        if (newEndDate <= contract.EndDate)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Gia hạn hợp đồng phải có ngày kết thúc mới lớn hơn ngày kết thúc hiện tại.");
        }

        var rentalPolicy = contract.Room.RoomingHouse.RentalPolicy;
        if (rentalPolicy is null || !rentalPolicy.IsActive)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyRequired,
                "Khu trọ chưa có chính sách thuê đang hoạt động.");
        }

        var extensionMonths = CountStartedMonths(contract.EndDate, newEndDate);
        var valid = rentalPolicy.AllowShortTermRenewal
            ? extensionMonths <= rentalPolicy.MaxRentalMonths
            : extensionMonths >= rentalPolicy.MinRentalMonths && extensionMonths <= rentalPolicy.MaxRentalMonths;

        if (valid)
        {
            return;
        }

        throw new BadRequestException(
            ErrorCodes.RentalRequestInvalidDuration,
            "Thời gian gia hạn không phù hợp với chính sách thuê của khu trọ.",
            new
            {
                extensionMonths,
                rentalPolicy.MinRentalMonths,
                rentalPolicy.MaxRentalMonths,
                rentalPolicy.AllowShortTermRenewal
            });
    }

    private static void ValidateOccupantLimit(
        RentalContract contract,
        IReadOnlyCollection<ParsedAppendixChange> changes)
    {
        var activeCount = GetCurrentOccupantCount(contract);
        var addedCount = changes.Count(x =>
            x.ChangeType == ContractAppendixChangeType.Add &&
            x.TargetType == ContractAppendixTargetType.ContractOccupant);
        var removedCount = changes.Count(x =>
            x.ChangeType == ContractAppendixChangeType.Remove &&
            x.TargetType == ContractAppendixTargetType.ContractOccupant &&
            contract.Occupants.Any(o => o.Id == x.Request.TargetId && o.Status == ContractOccupantStatus.Active));

        var finalCount = activeCount + addedCount - removedCount;
        var maxOccupants = GetSnapshotMaxOccupants(contract);
        if (finalCount <= maxOccupants)
        {
            return;
        }

        throw new BadRequestException(
            ErrorCodes.RentalRequestOccupantLimitExceeded,
            "Số người ở sau phụ lục vượt quá sức chứa tối đa của phòng.",
            new { finalCount, maxOccupants });
    }

    private static int GetCurrentOccupantCount(RentalContract contract)
    {
        return contract.Occupants.Count(x => x.Status == ContractOccupantStatus.Active);
    }

    private static int GetSnapshotMaxOccupants(RentalContract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.RoomSnapshot))
        {
            return contract.Room.MaxOccupants;
        }

        try
        {
            using var document = JsonDocument.Parse(contract.RoomSnapshot);
            if (document.RootElement.TryGetProperty("MaxOccupants", out var maxOccupantsElement) &&
                maxOccupantsElement.TryGetInt32(out var maxOccupants) &&
                maxOccupants > 0)
            {
                return maxOccupants;
            }
        }
        catch (JsonException)
        {
            return contract.Room.MaxOccupants;
        }

        return contract.Room.MaxOccupants;
    }

    private static ParsedAppendixChange ParseAppendixChange(ContractAppendixChangeRequest request, int sortOrder)
    {
        return new ParsedAppendixChange(
            request,
            ParseEnum<ContractAppendixChangeType>(request.ChangeType, "Loại thay đổi phụ lục không hợp lệ."),
            ParseEnum<ContractAppendixTargetType>(request.TargetType, "Đối tượng thay đổi phụ lục không hợp lệ."),
            sortOrder);
    }

    private static bool IsMainTenantUserIdChange(ParsedAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.Request.FieldName) == "maintenantuserid";
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

    private static bool TryParseDateOnly(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateOnly.TryParse(value.Trim().Trim('"'), out date);
    }

    private static int CountStartedMonths(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day > from.Day)
        {
            months++;
        }

        return Math.Max(months, 1);
    }

    private static ContractAppendixResponse MapToResponse(ContractAppendix appendix)
    {
        return new ContractAppendixResponse
        {
            Id = appendix.Id,
            RentalContractId = appendix.RentalContractId,
            AppendixNumber = appendix.AppendixNumber,
            EffectiveDate = appendix.EffectiveDate,
            Status = appendix.Status.ToString(),
            CreatedByUserId = appendix.CreatedByUserId,
            ActivatedAt = appendix.ActivatedAt,
            StatusReason = appendix.StatusReason,
            Changes = appendix.Changes
                .OrderBy(x => x.SortOrder)
                .Select(MapChangeToResponse)
                .ToList(),
            Signatures = appendix.Signatures
                .OrderBy(x => x.SignedAt)
                .Select(MapSignatureToResponse)
                .ToList(),
            Files = appendix.Files
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapFileToResponse)
                .ToList(),
            CreatedAt = appendix.CreatedAt,
            UpdatedAt = appendix.UpdatedAt
        };
    }

    private static ContractAppendixChangeResponse MapChangeToResponse(ContractAppendixChange change)
    {
        return new ContractAppendixChangeResponse
        {
            Id = change.Id,
            ChangeType = change.ChangeType.ToString(),
            TargetType = change.TargetType.ToString(),
            TargetId = change.TargetId,
            FieldName = change.FieldName,
            OldValue = change.OldValue,
            NewValue = change.NewValue,
            SortOrder = change.SortOrder,
            CreatedAt = change.CreatedAt
        };
    }

    private static ContractSignatureResponse MapSignatureToResponse(ContractSignature signature)
    {
        return new ContractSignatureResponse
        {
            Id = signature.Id,
            SignerUserId = signature.SignerUserId,
            SignerRole = signature.SignerRole.ToString(),
            SignatureMethod = signature.SignatureMethod.ToString(),
            SignedAt = signature.SignedAt
        };
    }

    private static ContractFileResponse MapFileToResponse(ContractFile file)
    {
        return new ContractFileResponse
        {
            Id = file.Id,
            RentalContractId = file.RentalContractId,
            RentalContractAppendixId = file.RentalContractAppendixId,
            StorageObjectKey = file.StorageObjectKey,
            FileUrl = file.FileUrl,
            CreatedAt = file.CreatedAt
        };
    }

    private static string NormalizeRequiredText(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new BadRequestException(ErrorCodes.ValidationError, message);
    }

    private string? HashDocumentNumber(string? documentNumber)
    {
        return string.IsNullOrWhiteSpace(documentNumber)
            ? null
            : hashService.HashSha256Hex(documentNumber.Trim());
    }

    private string? EncryptDocumentNumber(string? documentNumber)
    {
        return string.IsNullOrWhiteSpace(documentNumber)
            ? null
            : sensitiveDataProtector.Encrypt(documentNumber.Trim());
    }

    private string? DecryptDocumentNumber(string? encryptedDocumentNumber)
    {
        if (string.IsNullOrWhiteSpace(encryptedDocumentNumber))
        {
            return null;
        }

        try
        {
            return sensitiveDataProtector.Decrypt(encryptedDocumentNumber);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string? MaskDocumentNumber(string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return null;
        }

        var value = documentNumber.Trim();
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return $"{new string('*', value.Length - 4)}{value[^4..]}";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private static TEnum ParseEnum<TEnum>(string value, string message)
        where TEnum : struct
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        throw new BadRequestException(ErrorCodes.ValidationError, message, new { value });
    }

    private sealed record ParsedAppendixChange(
        ContractAppendixChangeRequest Request,
        ContractAppendixChangeType ChangeType,
        ContractAppendixTargetType TargetType,
        int SortOrder);

    private sealed record VerifiedOccupantAccount(
        Guid UserId,
        string FullName,
        string? PhoneNumber,
        DateOnly DateOfBirth);
}
