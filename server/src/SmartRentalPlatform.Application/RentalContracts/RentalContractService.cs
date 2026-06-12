using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public class RentalContractService : IRentalContractService
{
    private static readonly TimeSpan TenantSignatureTtl = TimeSpan.FromHours(48);

    private readonly IAppDbContext context;
    private readonly IHashService hashService;
    private readonly IContractPdfRenderer contractPdfRenderer;
    private readonly IContractSignatureOtpService contractSignatureOtpService;
    private readonly IContractFileService contractFileService;
    private readonly ISensitiveDataProtector sensitiveDataProtector;

    public RentalContractService(
        IAppDbContext context,
        IHashService hashService,
        IContractPdfRenderer contractPdfRenderer,
        IContractSignatureOtpService contractSignatureOtpService,
        IContractFileService contractFileService,
        ISensitiveDataProtector sensitiveDataProtector)
    {
        this.context = context;
        this.hashService = hashService;
        this.contractPdfRenderer = contractPdfRenderer;
        this.contractSignatureOtpService = contractSignatureOtpService;
        this.contractFileService = contractFileService;
        this.sensitiveDataProtector = sensitiveDataProtector;
    }

    public async Task<ContractDetailResponse?> GetByIdAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureCanView(userId, contract);
        return MapToDetailResponse(contract);
    }

    public async Task<ContractPreviewPdfResult?> GetPreviewPdfAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureContractCanPreview(contract);

        var viewerAccess = ResolvePreviewViewerAccess(userId, contract);
        if (viewerAccess is null)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Bạn không có quyền xem bản xem trước hợp đồng này.",
                new { contract.Id });
        }

        var renderOptions = await BuildPreviewRenderOptionsAsync(
            contract,
            viewerAccess,
            cancellationToken);

        var pdfBytes = contractPdfRenderer.RenderSignedRentalContract(contract, renderOptions);
        var fileName = $"contract-preview-{contract.ContractNumber}.pdf";

        return new ContractPreviewPdfResult(
            pdfBytes,
            "application/pdf",
            fileName);
    }

    public async Task<ContractDetailResponse?> SubmitOccupantsAsync(
        Guid tenantUserId,
        Guid contractId,
        SubmitContractOccupantsRequest request,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureMainTenant(tenantUserId, contract);
        EnsureCanSubmitOccupants(contract);
        ValidateOccupantsRequest(contract.MainTenantUser.Email, request, GetSnapshotMaxOccupants(contract));
        var verifiedAccounts = await ValidateOccupantAccountsAsync(request, cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;

            context.ContractOccupantDocuments.RemoveRange(contract.Occupants.SelectMany(x => x.Documents));
            context.ContractOccupants.RemoveRange(contract.Occupants);

            var createdByClientReference = new Dictionary<string, ContractOccupant>(StringComparer.OrdinalIgnoreCase);
            var pendingGuardianLinks = new List<(ContractOccupant Occupant, string GuardianClientReferenceId)>();

            foreach (var occupantRequest in request.Occupants)
            {
                var emailKey = occupantRequest.Email?.Trim().ToLowerInvariant();
                var verifiedAccount = !string.IsNullOrEmpty(emailKey) && verifiedAccounts.TryGetValue(emailKey, out var account)
                    ? account
                    : null;

                var occupant = new ContractOccupant
                {
                    Id = Guid.NewGuid(),
                    RentalContractId = contract.Id,
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

                if (!string.IsNullOrWhiteSpace(occupantRequest.ClientReferenceId))
                {
                    createdByClientReference[occupantRequest.ClientReferenceId.Trim()] = occupant;
                }

                if (!string.IsNullOrWhiteSpace(occupantRequest.GuardianClientReferenceId))
                {
                    pendingGuardianLinks.Add((occupant, occupantRequest.GuardianClientReferenceId.Trim()));
                }

                context.ContractOccupants.Add(occupant);
            }

            foreach (var (occupant, guardianClientReferenceId) in pendingGuardianLinks)
            {
                if (!createdByClientReference.TryGetValue(guardianClientReferenceId, out var guardian))
                {
                    throw new BadRequestException(
                        ErrorCodes.RentalContractInvalidOccupant,
                        "Không tìm thấy người bảo hộ trong danh sách người ở.",
                        new { guardianClientReferenceId });
                }

                occupant.GuardianOccupantId = guardian.Id;
            }

            contract.Status = RentalContractStatus.PendingLandlordSignature;
            contract.StatusReason = null;
            contract.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(tenantUserId, contract.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContractDetailResponse?> UpdateTermsAsync(
        Guid landlordUserId,
        Guid contractId,
        UpdateContractTermsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTermsRequest(request);

        var contract = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureLandlord(landlordUserId, contract);
        EnsureStatus(contract, RentalContractStatus.TenantRevisionRequested);

        var rentalPolicy = await context.RentalPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.RoomingHouseId == contract.Room.RoomingHouseId && x.IsActive,
                cancellationToken);

        if (rentalPolicy is null)
        {
            throw new ConflictException(
                ErrorCodes.RentalPolicyRequired,
                "Khu trọ chưa cấu hình chính sách thuê.",
                new { contract.Room.RoomingHouseId });
        }

        ValidateTermsDuration(request, rentalPolicy.MinRentalMonths, rentalPolicy.MaxRentalMonths);

        var now = DateTimeOffset.UtcNow;
        var updatedRows = await context.RentalContracts
            .Where(x => x.Id == contractId &&
                        x.DeletedAt == null &&
                        x.Status == RentalContractStatus.TenantRevisionRequested)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.StartDate, request.StartDate)
                .SetProperty(x => x.EndDate, request.EndDate)
                .SetProperty(x => x.PaymentDay, request.PaymentDay)
                .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        if (updatedRows == 0)
        {
            throw new ConflictException(
                ErrorCodes.RentalContractInvalidStatus,
                "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.",
                new { contractId });
        }

        return await GetByIdAsync(landlordUserId, contract.Id, cancellationToken);
    }

    public async Task<ContractDetailResponse?> LandlordSignAsync(
        Guid landlordUserId,
        Guid contractId,
        SignContractRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureLandlord(landlordUserId, contract);
        EnsureCanLandlordSign(contract);
        EnsureNotSigned(contract, ContractSignerRole.Landlord);

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            await contractSignatureOtpService.VerifyAndConsumeOtpAsync(
                landlordUserId,
                contractId,
                ContractSignerRole.Landlord,
                request.Otp,
                cancellationToken);

            context.ContractSignatures.Add(CreateSignature(contract.Id, landlordUserId, ContractSignerRole.Landlord, request, ipAddress, userAgent, now));

            var updatedRows = await context.RentalContracts
                .Where(x => x.Id == contractId &&
                            x.DeletedAt == null &&
                            (x.Status == RentalContractStatus.PendingLandlordSignature ||
                             x.Status == RentalContractStatus.TenantRevisionRequested))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, RentalContractStatus.PendingTenantSignature)
                    .SetProperty(x => x.StatusReason, (string?)null)
                    .SetProperty(x => x.SignatureDeadlineAt, now.Add(TenantSignatureTtl))
                    .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);

            if (updatedRows == 0)
            {
                throw new ConflictException(
                    ErrorCodes.RentalContractInvalidStatus,
                    "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.",
                    new { contractId });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(landlordUserId, contract.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContractDetailResponse?> TenantSignAsync(
        Guid tenantUserId,
        Guid contractId,
        SignContractRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        EnsureMainTenant(tenantUserId, contract);
        EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);
        EnsureNotSigned(contract, ContractSignerRole.Tenant);

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            await contractSignatureOtpService.VerifyAndConsumeOtpAsync(
                tenantUserId,
                contractId,
                ContractSignerRole.Tenant,
                request.Otp,
                cancellationToken);

            context.ContractSignatures.Add(CreateSignature(contract.Id, tenantUserId, ContractSignerRole.Tenant, request, ipAddress, userAgent, now));

            var updatedRows = await context.RentalContracts
                .Where(x => x.Id == contractId &&
                            x.DeletedAt == null &&
                            x.Status == RentalContractStatus.PendingTenantSignature)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, RentalContractStatus.Active)
                    .SetProperty(x => x.StatusReason, (string?)null)
                    .SetProperty(x => x.SignatureDeadlineAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.ActivatedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);

            if (updatedRows == 0)
            {
                throw new ConflictException(
                    ErrorCodes.RentalContractInvalidStatus,
                    "Trạng thái hợp đồng đã thay đổi, vui lòng tải lại dữ liệu.",
                    new { contractId });
            }

            await context.Rooms
                .Where(x => x.Id == contract.RoomId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, RoomStatus.Occupied)
                    .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await contractFileService.GenerateSignedContractFileAsync(
                tenantUserId,
                contract.Id,
                cancellationToken);

            return await GetByIdAsync(tenantUserId, contract.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ContractDetailResponse?> RejectAsync(
        Guid userId,
        Guid contractId,
        RejectContractRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeRequiredReason(request.Reason);

        var contract = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            EnsureLandlordCanReject(contract);

            contract.Status = RentalContractStatus.Rejected;
            contract.StatusReason = reason;
            contract.SignatureDeadlineAt = null;
            contract.UpdatedAt = now;

            contract.RoomDeposit.Status = RoomDepositStatus.Refunded;
            contract.RoomDeposit.RefundedAt = now;
            contract.RoomDeposit.RefundAmount = contract.RoomDeposit.DepositAmount;
            contract.RoomDeposit.UpdatedAt = now;

            contract.RentalRequest.Status = RentalRequestStatus.Rejected;
            contract.RentalRequest.RejectedReason = reason;
            contract.RentalRequest.UpdatedAt = now;

            if (contract.Room.Status == RoomStatus.Reserved)
            {
                contract.Room.Status = RoomStatus.Available;
                contract.Room.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            return await GetByIdAsync(userId, contract.Id, cancellationToken);
        }

        EnsureMainTenant(userId, contract);
        EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);

        contract.Status = RentalContractStatus.Rejected;
        contract.StatusReason = reason;
        contract.SignatureDeadlineAt = null;
        contract.UpdatedAt = now;

        contract.RoomDeposit.Status = RoomDepositStatus.Forfeited;
        contract.RoomDeposit.ForfeitedAt = now;
        contract.RoomDeposit.ForfeitedAmount = contract.RoomDeposit.DepositAmount;
        contract.RoomDeposit.UpdatedAt = now;

        contract.RentalRequest.Status = RentalRequestStatus.Cancelled;
        contract.RentalRequest.RejectedReason = reason;
        contract.RentalRequest.UpdatedAt = now;

        if (contract.Room.Status == RoomStatus.Reserved)
        {
            contract.Room.Status = RoomStatus.Available;
            contract.Room.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(userId, contract.Id, cancellationToken);
    }

    public async Task<ContractDetailResponse?> RequestRevisionAsync(
        Guid userId,
        Guid contractId,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeRequiredReason(request.Reason);

        var contract = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (contract.Room.RoomingHouse.LandlordUserId == userId)
        {
            if (request.RevisionType != ContractRevisionType.Occupants)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Chủ trọ chỉ có thể yêu cầu người thuê sửa thông tin người ở.");
            }

            EnsureStatus(contract, RentalContractStatus.PendingLandlordSignature);
            contract.Status = RentalContractStatus.LandlordRevisionRequested;
            contract.StatusReason = reason;
            contract.SignatureDeadlineAt = null;
            contract.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            return await GetByIdAsync(userId, contract.Id, cancellationToken);
        }

        EnsureMainTenant(userId, contract);
        EnsureStatus(contract, RentalContractStatus.PendingTenantSignature);

        var existingSignatures = contract.Signatures.ToList();
        context.ContractSignatures.RemoveRange(existingSignatures);

        contract.Status = request.RevisionType switch
        {
            ContractRevisionType.Occupants => RentalContractStatus.WaitingTenantOccupants,
            ContractRevisionType.ContractTerms => RentalContractStatus.TenantRevisionRequested,
            _ => throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Loại yêu cầu sửa hợp đồng không hợp lệ.")
        };
        contract.StatusReason = reason;
        contract.SignatureDeadlineAt = null;
        contract.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(userId, contract.Id, cancellationToken);
    }

    public async Task<ContractDetailResponse?> TerminateAsync(
        Guid userId,
        Guid contractId,
        TerminateContractRequest request,
        CancellationToken cancellationToken = default)
    {
        var contract = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, cancellationToken);

        if (contract is null)
        {
            return null;
        }

        var isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
        var isTenant = contract.MainTenantUserId == userId;

        if (!isLandlord && !isTenant)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalContractForbidden,
                "Bạn không có quyền thao tác với hợp đồng này.",
                new { contractId });
        }

        EnsureStatus(contract, RentalContractStatus.Active);

        var now = DateTimeOffset.UtcNow;
        var deposit = contract.RoomDeposit;
        
        var reasonText = string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : request.Reason.Trim();

        switch (request.TerminationType)
        {
            case ContractTerminationType.LandlordUnilateral:
                if (!isLandlord) throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Chỉ chủ trọ mới có quyền dùng thao tác này.");
                contract.Status = RentalContractStatus.Cancelled;
                deposit.Status = RoomDepositStatus.Refunded;
                // Luật 1: Đền bù = Refund x2 (Hoàn cọc gốc + đền bù 1 khoản tương đương)
                deposit.RefundAmount = deposit.DepositAmount * 2;
                deposit.ForfeitedAmount = 0;
                deposit.RefundedAt = now;
                contract.StatusReason = $"Chủ trọ đơn phương chấm dứt. Hoàn cọc gốc: {deposit.DepositAmount:N0} VNĐ, Đền bù: {deposit.DepositAmount:N0} VNĐ. Tổng hoàn: {deposit.RefundAmount:N0} VNĐ. Lý do: {reasonText}";
                break;

            case ContractTerminationType.TenantUnilateral:
                if (!isTenant && !isLandlord) throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Không có quyền thao tác.");
                contract.Status = RentalContractStatus.Cancelled;
                deposit.Status = RoomDepositStatus.Forfeited;
                // Luật 2: Người thuê bỏ hợp đồng -> Mất cọc
                deposit.RefundAmount = 0;
                deposit.ForfeitedAmount = deposit.DepositAmount;
                deposit.ForfeitedAt = now;
                contract.StatusReason = $"Người thuê đơn phương chấm dứt. Tịch thu cọc: {deposit.DepositAmount:N0} VNĐ. Lý do: {reasonText}";
                break;

            case ContractTerminationType.MutualAgreement:
            case ContractTerminationType.NormalExpiration:
                if (!isLandlord) throw new ForbiddenException(ErrorCodes.RentalContractForbidden, "Chỉ chủ trọ mới có quyền xác nhận thanh lý.");
                contract.Status = request.TerminationType == ContractTerminationType.NormalExpiration 
                    ? RentalContractStatus.Expired 
                    : RentalContractStatus.Cancelled;
                
                deposit.Status = RoomDepositStatus.Refunded;
                
                var damageFee = request.DamageFee >= 0 ? request.DamageFee : 0;
                if (damageFee > deposit.DepositAmount) damageFee = deposit.DepositAmount;

                deposit.ForfeitedAmount = damageFee;
                deposit.RefundAmount = deposit.DepositAmount - damageFee;
                deposit.RefundedAt = now;
                
                var typeStr = request.TerminationType == ContractTerminationType.NormalExpiration ? "Đáo hạn hợp đồng" : "Hai bên thỏa thuận chấm dứt";
                contract.StatusReason = $"{typeStr}. Trừ hư hỏng/dọn dẹp: {damageFee:N0} VNĐ. Hoàn cọc thực tế: {deposit.RefundAmount:N0} VNĐ. Lý do bổ sung: {reasonText}";
                break;
                
            default:
                throw new BadRequestException(ErrorCodes.ValidationError, "Loại thanh lý không hợp lệ.");
        }

        contract.UpdatedAt = now;
        deposit.UpdatedAt = now;

        if (contract.Room.Status == RoomStatus.Occupied)
        {
            contract.Room.Status = RoomStatus.Available;
            contract.Room.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(userId, contract.Id, cancellationToken);
    }

    public async Task<int> ExpireOverdueTenantSignaturesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var overdueContracts = await BaseQuery()
                .Where(x => x.Status == RentalContractStatus.PendingTenantSignature &&
                            x.SignatureDeadlineAt.HasValue &&
                            x.SignatureDeadlineAt.Value <= now &&
                            x.DeletedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var contract in overdueContracts)
            {
                contract.Status = RentalContractStatus.Expired;
                contract.StatusReason = "Người thuê không ký hợp đồng trong thời hạn quy định.";
                contract.SignatureDeadlineAt = null;
                contract.UpdatedAt = now;

                contract.RoomDeposit.Status = RoomDepositStatus.Forfeited;
                contract.RoomDeposit.ForfeitedAt = now;
                contract.RoomDeposit.ForfeitedAmount = contract.RoomDeposit.DepositAmount;
                contract.RoomDeposit.UpdatedAt = now;

                contract.RentalRequest.Status = RentalRequestStatus.Expired;
                contract.RentalRequest.RejectedReason = "Người thuê không ký hợp đồng trong thời hạn quy định.";
                contract.RentalRequest.UpdatedAt = now;

                if (contract.Room.Status == RoomStatus.Reserved)
                {
                    contract.Room.Status = RoomStatus.Available;
                    contract.Room.UpdatedAt = now;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return overdueContracts.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private IQueryable<RentalContract> BaseQuery()
    {
        return context.RentalContracts
            .Include(x => x.RentalRequest)
            .Include(x => x.RoomDeposit)
            .Include(x => x.MainTenantUser)
                .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.Landlord)
                        .ThenInclude(x => x.UserProfile)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.Documents)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.User)
                    .ThenInclude(x => x!.UserProfile)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Signatures);
    }

    private async Task<ContractRenderOptions> BuildPreviewRenderOptionsAsync(
        RentalContract contract,
        ContractPreviewViewerAccess viewerAccess,
        CancellationToken cancellationToken)
    {
        var userDocumentNumbersByUserId = viewerAccess.ShowFullDocumentNumbers
            ? await GetDecryptedUserDocumentNumbersAsync(contract, cancellationToken)
            : new Dictionary<Guid, string?>();

        var occupantDocumentNumbersByDocumentId = viewerAccess.ShowFullDocumentNumbers
            ? GetDecryptedOccupantDocumentNumbers(contract)
            : new Dictionary<Guid, string?>();

        return new ContractRenderOptions
        {
            ViewerMode = viewerAccess.ViewerMode,
            ShowFullDocumentNumbers = viewerAccess.ShowFullDocumentNumbers,
            VisibleOccupantIds = viewerAccess.VisibleOccupantIds,
            UserDocumentNumbersByUserId = userDocumentNumbersByUserId,
            OccupantDocumentNumbersByDocumentId = occupantDocumentNumbersByDocumentId
        };
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> GetDecryptedUserDocumentNumbersAsync(
        RentalContract contract,
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

    private static ContractPreviewViewerAccess? ResolvePreviewViewerAccess(
        Guid userId,
        RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId ||
            GetMainTenantUserIds(contract).Contains(userId))
        {
            return new ContractPreviewViewerAccess(
                "Full",
                ShowFullDocumentNumbers: true,
                VisibleOccupantIds: null);
        }

        var visibleOccupantIds = contract.Occupants
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToList();

        if (visibleOccupantIds.Count > 0)
        {
            return new ContractPreviewViewerAccess(
                "MaskedLimited",
                ShowFullDocumentNumbers: false,
                VisibleOccupantIds: visibleOccupantIds);
        }

        return null;
    }

    private sealed record ContractPreviewViewerAccess(
        string ViewerMode,
        bool ShowFullDocumentNumbers,
        IReadOnlyCollection<Guid>? VisibleOccupantIds);

    private static ContractSignature CreateSignature(
        Guid contractId,
        Guid signerUserId,
        ContractSignerRole signerRole,
        SignContractRequest request,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        return new ContractSignature
        {
            Id = Guid.NewGuid(),
            RentalContractId = contractId,
            SignerUserId = signerUserId,
            SignerRole = signerRole,
            SignatureMethod = ContractSignatureMethod.EmailOtp,
            SignatureText = NormalizeOptionalText(request.SignatureText),
            IpAddress = NormalizeOptionalText(ipAddress),
            UserAgent = NormalizeOptionalText(userAgent),
            SignedAt = now,
            CreatedAt = now
        };
    }

    private static ContractDetailResponse MapToDetailResponse(RentalContract contract)
    {
        ResolveCurrentContractTerms(contract);

        return new ContractDetailResponse
        {
            Id = contract.Id,
            RentalRequestId = contract.RentalRequestId,
            RoomDepositId = contract.RoomDepositId,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room.RoomNumber,
            RoomingHouseId = contract.Room.RoomingHouseId,
            RoomingHouseName = contract.Room.RoomingHouse.Name,
            MainTenantUserId = contract.MainTenantUserId,
            MainTenantName = contract.MainTenantUser.DisplayName,
            ContractNumber = contract.ContractNumber,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            PaymentDay = contract.PaymentDay,
            Status = contract.Status.ToString(),
            RoomSnapshot = contract.RoomSnapshot,
            SignatureDeadlineAt = contract.SignatureDeadlineAt,
            ActivatedAt = contract.ActivatedAt,
            StatusReason = contract.StatusReason,
            Occupants = contract.Occupants
                .OrderBy(x => x.CreatedAt)
                .Select(MapOccupantToResponse)
                .ToList(),
            Signatures = contract.Signatures
                .OrderBy(x => x.SignedAt)
                .Select(MapSignatureToResponse)
                .ToList(),
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }

    private static void ResolveCurrentContractTerms(RentalContract contract)
    {
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes)
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update)
                {
                    continue;
                }

                var field = NormalizeFieldName(change.FieldName);
                if (string.IsNullOrWhiteSpace(change.NewValue))
                {
                    continue;
                }

                switch (field)
                {
                    case "monthlyrent":
                        if (decimal.TryParse(change.NewValue, out var rent))
                        {
                            contract.MonthlyRent = rent;
                        }
                        break;
                    case "depositamount":
                        if (decimal.TryParse(change.NewValue, out var deposit))
                        {
                            contract.DepositAmount = deposit;
                        }
                        break;
                    case "paymentday":
                        if (int.TryParse(change.NewValue, out var day))
                        {
                            contract.PaymentDay = day;
                        }
                        break;
                    case "startdate":
                        if (DateOnly.TryParse(change.NewValue, out var start))
                        {
                            contract.StartDate = start;
                        }
                        break;
                    case "enddate":
                        if (DateOnly.TryParse(change.NewValue, out var end))
                        {
                            contract.EndDate = end;
                        }
                        break;
                    case "maintenantuserid":
                        var userId = ExtractUserId(change.NewValue);
                        if (userId.HasValue)
                        {
                            contract.MainTenantUserId = userId.Value;
                            var occupantUser = contract.Occupants.FirstOrDefault(x => x.UserId == userId.Value)?.User;
                            if (occupantUser is not null)
                            {
                                contract.MainTenantUser = occupantUser;
                            }
                        }
                        break;
                }
            }
        }
    }

    private static ContractOccupantResponse MapOccupantToResponse(ContractOccupant occupant)
    {
        return new ContractOccupantResponse
        {
            Id = occupant.Id,
            UserId = occupant.UserId,
            Email = occupant.User?.Email,
            GuardianOccupantId = occupant.GuardianOccupantId,
            FullName = occupant.FullName,
            PhoneNumber = occupant.PhoneNumber,
            DateOfBirth = occupant.DateOfBirth,
            RelationshipToMainTenant = occupant.RelationshipToMainTenant,
            MoveInDate = occupant.MoveInDate,
            MoveOutDate = occupant.MoveOutDate,
            Status = occupant.Status.ToString(),
            Document = occupant.Documents
                .OrderBy(x => x.CreatedAt)
                .Select(MapDocumentToResponse)
                .FirstOrDefault()
        };
    }

    private static ContractOccupantDocumentResponse MapDocumentToResponse(ContractOccupantDocument document)
    {
        return new ContractOccupantDocumentResponse
        {
            Id = document.Id,
            ContractOccupantId = document.RentalContractOccupantId,
            DocumentType = document.DocumentType,
            DocumentNumberMasked = document.DocumentNumberMasked,
            FrontImageObjectKey = document.FrontImageObjectKey,
            BackImageObjectKey = document.BackImageObjectKey,
            ExtraImageObjectKey = document.ExtraImageObjectKey,
            UploadedAt = document.UploadedAt
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

    private static void ValidateOccupantsRequest(
        string tenantEmail,
        SubmitContractOccupantsRequest request,
        int maxOccupants)
    {
        if (request.Occupants.Count == 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractOccupantsRequired,
                "Danh sách người ở không được để trống.");
        }

        if (request.Occupants.Count > maxOccupants)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestOccupantLimitExceeded,
                "Số người ở vượt quá sức chứa tối đa đã chốt trong hợp đồng.",
                new { request.Occupants.Count, maxOccupants });
        }

        if (!request.Occupants.Any(x => x.Email?.Trim().Equals(tenantEmail.Trim(), StringComparison.OrdinalIgnoreCase) == true))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Danh sách người ở phải bao gồm người thuê chính.",
                new { tenantEmail });
        }

        foreach (var occupant in request.Occupants)
        {
            if (string.IsNullOrWhiteSpace(occupant.RelationshipToMainTenant))
            {
                throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Người ở phải có quan hệ với người thuê chính.");
            }

            if (occupant.MoveInDate == default)
            {
                throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Người ở phải có ngày chuyển vào.");
            }

            if (occupant.MoveOutDate.HasValue && occupant.MoveOutDate.Value <= occupant.MoveInDate)
            {
                throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Ngày rời đi phải lớn hơn ngày chuyển vào.");
            }

            if (!string.IsNullOrWhiteSpace(occupant.Email))
            {
                if (occupant.Document is not null)
                {
                    throw new BadRequestException(
                        ErrorCodes.RentalContractInvalidOccupant,
                        "Người ở đã có tài khoản và KYC không được gửi giấy tờ trong hợp đồng.",
                        new { occupant.Email });
                }

                continue;
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
    }

    private static void ValidateTermsRequest(UpdateContractTermsRequest request)
    {
        if (request.StartDate == default)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày bắt đầu hợp đồng không được để trống.");
        }

        if (request.EndDate == default)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày kết thúc hợp đồng không được để trống.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Ngày bắt đầu hợp đồng không được nằm trong quá khứ.");
        }

        if (request.EndDate <= request.StartDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Ngày kết thúc hợp đồng phải lớn hơn ngày bắt đầu.");
        }

        if (request.PaymentDay is < 1 or > 28)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày thanh toán phải nằm trong khoảng từ 1 đến 28.");
        }
    }

    private static void ValidateTermsDuration(
        UpdateContractTermsRequest request,
        int minRentalMonths,
        int maxRentalMonths)
    {
        var minimumEndDate = request.StartDate.AddMonths(minRentalMonths);
        var maximumEndDate = request.StartDate.AddMonths(maxRentalMonths);

        if (request.EndDate < minimumEndDate ||
            request.EndDate > maximumEndDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Thời hạn hợp đồng không nằm trong chính sách thuê của khu trọ.",
                new
                {
                    request.StartDate,
                    request.EndDate,
                    minRentalMonths,
                    maxRentalMonths
                });
        }
    }

    private async Task<Dictionary<string, VerifiedOccupantAccount>> ValidateOccupantAccountsAsync(
        SubmitContractOccupantsRequest request,
        CancellationToken cancellationToken)
    {
        var emails = request.Occupants
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .Select(x => x.Email!.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (emails.Count == 0)
        {
            return new Dictionary<string, VerifiedOccupantAccount>();
        }

        var users = await context.Users
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .Where(x => emails.Contains(x.Email.ToLower()) && x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var existingEmails = users.Select(x => x.Email.ToLowerInvariant()).ToList();
        var missingEmails = emails.Except(existingEmails).ToList();
        if (missingEmails.Count > 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Người ở có tài khoản không tồn tại.",
                new { emails = missingEmails });
        }

        var userIdsForKyc = users.Select(x => x.Id).ToList();
        var approvedKycs = await context.KycVerifications
            .AsNoTracking()
            .Where(x => userIdsForKyc.Contains(x.UserId) && x.Status == KycVerificationStatus.Approved)
            .ToListAsync(cancellationToken);

        var latestApprovedKycByUserId = approvedKycs
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(k => k.ReviewedAt ?? k.UpdatedAt).First());

        var notApprovedKycUserIds = userIdsForKyc.Except(latestApprovedKycByUserId.Keys).ToList();
        if (notApprovedKycUserIds.Count > 0)
        {
            var notApprovedEmails = users.Where(u => notApprovedKycUserIds.Contains(u.Id)).Select(u => u.Email).ToList();
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Người ở có tài khoản phải hoàn tất KYC trước khi được thêm vào hợp đồng.",
                new { emails = notApprovedEmails });
        }

        var result = new Dictionary<string, VerifiedOccupantAccount>();
        foreach (var user in users)
        {
            var approvedKyc = latestApprovedKycByUserId[user.Id];
            var fullName = NormalizeOptionalText(approvedKyc.OcrFullName)
                ?? NormalizeOptionalText(user.UserProfile?.FullName)
                ?? NormalizeOptionalText(user.DisplayName);
            var dateOfBirth = approvedKyc.OcrDateOfBirth ?? user.UserProfile?.DateOfBirth;

            if (string.IsNullOrWhiteSpace(fullName) || !dateOfBirth.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.KycRequired,
                    "Thông tin KYC đã duyệt của người ở chưa đủ họ tên hoặc ngày sinh.",
                    new { email = user.Email });
            }

            result[user.Email.ToLowerInvariant()] = new VerifiedOccupantAccount(
                user.Id,
                fullName,
                NormalizeOptionalText(user.PhoneNumber),
                dateOfBirth.Value);
        }

        return result;
    }

    private sealed record VerifiedOccupantAccount(
        Guid UserId,
        string FullName,
        string? PhoneNumber,
        DateOnly DateOfBirth);

    private static void EnsureCanSubmitOccupants(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.WaitingTenantOccupants or RentalContractStatus.LandlordRevisionRequested)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Trạng thái hợp đồng không cho phép cập nhật thông tin người ở.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    private static void EnsureCanLandlordSign(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.PendingLandlordSignature or RentalContractStatus.TenantRevisionRequested)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Trạng thái hợp đồng không cho phép chủ trọ ký.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    private static void EnsureContractCanPreview(RentalContract contract)
    {
        if (contract.Status is not RentalContractStatus.Active)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Hợp đồng đã có hiệu lực, vui lòng xem file hợp đồng đã ký.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    private static void EnsureLandlordCanReject(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.PendingLandlordSignature or RentalContractStatus.TenantRevisionRequested)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Trạng thái hợp đồng không cho phép chủ trọ từ chối.",
            new { contract.Id, currentStatus = contract.Status.ToString() });
    }

    private static void EnsureCanView(Guid userId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId ||
            GetMainTenantUserIds(contract).Contains(userId))
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền xem hợp đồng này.",
            new { contract.Id });
    }

    private static void EnsureMainTenant(Guid userId, RentalContract contract)
    {
        if (contract.MainTenantUserId == userId)
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác trên hợp đồng này.",
            new { contract.Id });
    }

    private static void EnsureLandlord(Guid landlordUserId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == landlordUserId)
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalContractForbidden,
            "Bạn không có quyền thao tác trên hợp đồng này.",
            new { contract.Id });
    }

    private static void EnsureStatus(RentalContract contract, RentalContractStatus expectedStatus)
    {
        if (contract.Status == expectedStatus)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractInvalidStatus,
            "Trạng thái hợp đồng không hợp lệ cho thao tác này.",
            new
            {
                contract.Id,
                currentStatus = contract.Status.ToString(),
                expectedStatus = expectedStatus.ToString()
            });
    }

    private static void EnsureNotSigned(RentalContract contract, ContractSignerRole signerRole)
    {
        if (!contract.Signatures.Any(x => x.SignerRole == signerRole))
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalContractAlreadySigned,
            "Bên này đã ký hợp đồng.",
            new { contract.Id, signerRole = signerRole.ToString() });
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
            : sensitiveDataProtector.Encrypt(documentNumber);
    }

    private static string NormalizeRequiredReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Lý do không được để trống.");
        }

        return reason.Trim();
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

    private static IEnumerable<ContractAppendix> GetActiveAppendicesInOrder(RentalContract contract)
    {
        return contract.Appendices
            .Where(x => x.Status == ContractAppendixStatus.Active)
            .OrderBy(x => x.ActivatedAt ?? x.UpdatedAt)
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
}


