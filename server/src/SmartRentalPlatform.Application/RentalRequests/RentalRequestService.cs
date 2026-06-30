using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Contracts.RentalRequests.Requests;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Notifications;
using System.Text.Json;

namespace SmartRentalPlatform.Application.RentalRequests;

public class RentalRequestService : IRentalRequestService
{
    private const int TenantRequestMinimumStartOffsetDays = 3;
    private const int LandlordApprovalMinimumStartOffsetDays = 2;

    private static readonly RoomDepositStatus[] ActiveDepositStatuses =
    [
        RoomDepositStatus.PendingPayment,
        RoomDepositStatus.Paid
    ];

    private static readonly RentalContractStatus[] TerminalContractStatuses =
    [
        RentalContractStatus.Cancelled,
        RentalContractStatus.Expired,
        RentalContractStatus.Rejected
    ];

    private readonly IAppDbContext context;
    private readonly INotificationService notificationService;

    public RentalRequestService(
        IAppDbContext context,
        INotificationService notificationService)
    {
        this.context = context;
        this.notificationService = notificationService;
    }

    public async Task<RentalRequestResponse> CreateAsync(
        Guid tenantUserId,
        Guid roomId,
        CreateRentalRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        await EnsureTenantBillingEligibilityAsync(tenantUserId, cancellationToken);

        var room = await context.Rooms
            .Include(x => x.RoomingHouse)
            .Include(x => x.PriceTiers)
            .FirstOrDefaultAsync(
                x => x.Id == roomId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (room is null)
        {
            throw new NotFoundException(
                ErrorCodes.RoomNotFound,
                "Không tìm thấy phòng.",
                new { roomId });
        }

        EnsureRoomCanReceiveRentalRequest(room, tenantUserId);

        var duplicateExists = await context.RentalRequests.AnyAsync(
            x => x.RoomId == roomId &&
                 x.TenantUserId == tenantUserId &&
                 (x.Status == RentalRequestStatus.Pending ||
                  (x.Status == RentalRequestStatus.Accepted &&
                   (x.RentalContract == null ||
                    !TerminalContractStatuses.Contains(x.RentalContract.Status)))),
            cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException(
                ErrorCodes.RentalRequestDuplicate,
                "Bạn đã có yêu cầu thuê đang xử lý cho phòng này.",
                new { roomId, tenantUserId });
        }

        var rentalPolicy = await context.RentalPolicies.FirstOrDefaultAsync(
            x => x.RoomingHouseId == room.RoomingHouseId && x.IsActive,
            cancellationToken);

        if (rentalPolicy is null)
        {
            throw new ConflictException(
                ErrorCodes.RentalPolicyRequired,
                "Khu trọ chưa cấu hình chính sách thuê.",
                new { room.RoomingHouseId });
        }

        ValidateRentalDuration(request, rentalPolicy.MinRentalMonths, rentalPolicy.MaxRentalMonths);

        var monthlyRent = ResolveMonthlyRent(room, request.ExpectedOccupantCount);
        var depositAmount = monthlyRent * rentalPolicy.DepositMonths;
        var now = DateTimeOffset.UtcNow;

        var rentalRequest = new RentalRequest
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            TenantUserId = tenantUserId,
            DesiredStartDate = request.DesiredStartDate,
            ExpectedEndDate = request.ExpectedEndDate,
            ExpectedOccupantCount = request.ExpectedOccupantCount,
            MonthlyRentSnapshot = monthlyRent,
            DepositAmountSnapshot = depositAmount,
            TenantNote = NormalizeOptionalText(request.TenantNote),
            Status = RentalRequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.RentalRequests.Add(rentalRequest);
        await context.SaveChangesAsync(cancellationToken);

        var tenantUser = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantUserId, cancellationToken);
        var tenantDisplayName = tenantUser?.DisplayName ?? "Người thuê";

        await notificationService.CreateAsync(
            room.RoomingHouse.LandlordUserId,
            NotificationType.NewRentalRequest,
            "Yêu cầu thuê phòng mới",
            $"{tenantDisplayName} muốn thuê phòng {room.RoomNumber} tại {room.RoomingHouse.Name} từ {request.DesiredStartDate:dd/MM/yyyy}.",
            rentalRequest.Id.ToString(),
            "RentalRequest",
            cancellationToken);

        return await GetByIdAsync(tenantUserId, rentalRequest.Id, cancellationToken)
            ?? throw new InternalServerException(
                ErrorCodes.InternalServerError,
                "Đã tạo yêu cầu thuê nhưng không thể tải lại dữ liệu.",
                new { rentalRequest.Id });
    }

    public async Task<List<RentalRequestResponse>> GetMyRequestsAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        var rentalRequests = await BaseQuery()
            .Where(x => x.TenantUserId == tenantUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return rentalRequests.Select(MapToResponse).ToList();
    }

    public async Task<List<RentalRequestResponse>> GetIncomingRequestsAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var rentalRequests = await BaseQuery()
            .Where(x => x.Room.RoomingHouse.LandlordUserId == landlordUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return rentalRequests.Select(MapToResponse).ToList();
    }

    public async Task<RentalRequestResponse?> GetByIdAsync(
        Guid userId,
        Guid rentalRequestId,
        CancellationToken cancellationToken = default)
    {
        var rentalRequest = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == rentalRequestId, cancellationToken);

        if (rentalRequest is null)
        {
            return null;
        }

        EnsureCanView(userId, rentalRequest);
        return MapToResponse(rentalRequest);
    }

    public async Task<RentalRequestResponse?> ApproveAsync(
        Guid landlordUserId,
        Guid rentalRequestId,
        ApproveRentalRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.PaymentDeadlineAt.HasValue ||
            request.PaymentDeadlineAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestPaymentDeadlineInvalid,
                "Hạn thanh toán cọc phải lớn hơn thời điểm hiện tại.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var rentalRequest = await BaseQuery()
                .FirstOrDefaultAsync(x => x.Id == rentalRequestId, cancellationToken);

            if (rentalRequest is null)
            {
                return null;
            }

            EnsureLandlordOwnsRequest(landlordUserId, rentalRequest);
            EnsureStatus(rentalRequest, RentalRequestStatus.Pending);
            EnsureDesiredStartDateCanBeApproved(rentalRequest.DesiredStartDate);

            var room = await context.Rooms
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM rooms
                    WHERE id = {rentalRequest.RoomId}
                    FOR UPDATE
                    """)
                .FirstOrDefaultAsync(cancellationToken);

            if (room is null || room.DeletedAt is not null)
            {
                throw new NotFoundException(
                    ErrorCodes.RoomNotFound,
                    "Không tìm thấy phòng.",
                    new { rentalRequest.RoomId });
            }

            if (room.Status != RoomStatus.Available)
            {
                throw new ConflictException(
                    ErrorCodes.RoomNotAvailable,
                    "Phòng hiện không còn trống để duyệt yêu cầu thuê.",
                    new { rentalRequest.RoomId, currentStatus = room.Status.ToString() });
            }

            var activeDepositExists = await context.RoomDeposits.AnyAsync(
                x => x.RoomId == room.Id &&
                     ActiveDepositStatuses.Contains(x.Status),
                cancellationToken);

            if (activeDepositExists)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositAlreadyExists,
                    "Phòng đã có khoản cọc đang xử lý hoặc đã thanh toán.",
                    new { RoomId = room.Id });
            }

            var now = DateTimeOffset.UtcNow;
            rentalRequest.Status = RentalRequestStatus.Accepted;
            rentalRequest.ApprovedByLandlordId = landlordUserId;
            rentalRequest.RespondedAt = now;
            rentalRequest.RejectedReason = null;
            rentalRequest.UpdatedAt = now;

            room.Status = RoomStatus.Reserved;
            room.UpdatedAt = now;

            var deposit = new RoomDeposit
            {
                Id = Guid.NewGuid(),
                RentalRequestId = rentalRequest.Id,
                RoomId = room.Id,
                TenantUserId = rentalRequest.TenantUserId,
                LandlordUserId = landlordUserId,
                DepositAmount = rentalRequest.DepositAmountSnapshot,
                Currency = "VND",
                Status = RoomDepositStatus.PendingPayment,
                PaymentDeadlineAt = request.PaymentDeadlineAt.Value,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.RoomDeposits.Add(deposit);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await notificationService.CreateAsync(
                rentalRequest.TenantUserId,
                NotificationType.RentalRequestApproved,
                "Yêu cầu thuê đã được chấp nhận",
                $"Chủ trọ đã chấp nhận yêu cầu thuê phòng {rentalRequest.Room.RoomNumber} tại {rentalRequest.Room.RoomingHouse.Name}. Vui lòng thanh toán cọc {rentalRequest.DepositAmountSnapshot:N0} VND trước {request.PaymentDeadlineAt:HH:mm dd/MM/yyyy} để hoàn tất.",
                rentalRequest.Id.ToString(),
                "RentalRequest",
                cancellationToken);

            return await GetByIdAsync(landlordUserId, rentalRequest.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RentalRequestResponse?> RejectAsync(
        Guid landlordUserId,
        Guid rentalRequestId,
        RejectRentalRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var rejectedReason = request.RejectedReason.Trim();
        if (string.IsNullOrWhiteSpace(rejectedReason))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Lý do từ chối không được để trống.");
        }

        var rentalRequest = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == rentalRequestId, cancellationToken);

        if (rentalRequest is null)
        {
            return null;
        }

        EnsureLandlordOwnsRequest(landlordUserId, rentalRequest);
        EnsureStatus(rentalRequest, RentalRequestStatus.Pending);

        var now = DateTimeOffset.UtcNow;
        rentalRequest.Status = RentalRequestStatus.Rejected;
        rentalRequest.RespondedAt = now;
        rentalRequest.RejectedReason = rejectedReason;
        rentalRequest.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(
            rentalRequest.TenantUserId,
            NotificationType.RentalRequestRejected,
            "Yêu cầu thuê đã bị từ chối",
            $"Chủ trọ đã từ chối yêu cầu thuê phòng {rentalRequest.Room.RoomNumber} tại {rentalRequest.Room.RoomingHouse.Name}. Lý do: {rejectedReason}",
            rentalRequest.Id.ToString(),
            "RentalRequest",
            cancellationToken);

        return await GetByIdAsync(landlordUserId, rentalRequest.Id, cancellationToken);
    }

    public async Task<RentalRequestResponse?> CancelAsync(
        Guid tenantUserId,
        Guid rentalRequestId,
        CancellationToken cancellationToken = default)
    {
        var rentalRequest = await BaseQuery()
            .FirstOrDefaultAsync(x => x.Id == rentalRequestId, cancellationToken);

        if (rentalRequest is null)
        {
            return null;
        }

        if (rentalRequest.TenantUserId != tenantUserId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalRequestForbidden,
                "Bạn không có quyền hủy yêu cầu thuê này.",
                new { rentalRequestId });
        }

        EnsureStatus(rentalRequest, RentalRequestStatus.Pending);

        rentalRequest.Status = RentalRequestStatus.Cancelled;
        rentalRequest.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(tenantUserId, rentalRequest.Id, cancellationToken);
    }

    private IQueryable<RentalRequest> BaseQuery()
    {
        return context.RentalRequests
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.TenantUser)
            .Include(x => x.RoomDeposit)
            .Include(x => x.RentalContract);
    }

    private static RentalRequestResponse MapToResponse(RentalRequest rentalRequest)
    {
        return new RentalRequestResponse
        {
            Id = rentalRequest.Id,
            RoomId = rentalRequest.RoomId,
            RoomNumber = rentalRequest.Room.RoomNumber,
            RoomingHouseId = rentalRequest.Room.RoomingHouseId,
            RoomingHouseName = rentalRequest.Room.RoomingHouse.Name,
            TenantUserId = rentalRequest.TenantUserId,
            TenantName = rentalRequest.TenantUser.DisplayName,
            ApprovedByLandlordId = rentalRequest.ApprovedByLandlordId,
            DesiredStartDate = rentalRequest.DesiredStartDate,
            ExpectedEndDate = rentalRequest.ExpectedEndDate,
            ExpectedOccupantCount = rentalRequest.ExpectedOccupantCount,
            MonthlyRentSnapshot = rentalRequest.MonthlyRentSnapshot,
            DepositAmountSnapshot = rentalRequest.DepositAmountSnapshot,
            TenantNote = rentalRequest.TenantNote,
            Status = rentalRequest.Status.ToString(),
            RespondedAt = rentalRequest.RespondedAt,
            RejectedReason = rentalRequest.RejectedReason,
            Deposit = rentalRequest.RoomDeposit is null ? null : MapDepositToResponse(rentalRequest.RoomDeposit),
            Contract = rentalRequest.RentalContract is null ? null : MapContractToBriefResponse(rentalRequest.RentalContract),
            CreatedAt = rentalRequest.CreatedAt,
            UpdatedAt = rentalRequest.UpdatedAt
        };
    }

    private static ContractBriefResponse MapContractToBriefResponse(Domain.Entities.RentalContracts.RentalContract contract)
    {
        return new ContractBriefResponse
        {
            Id = contract.Id,
            Status = contract.Status.ToString(),
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            SignatureDeadlineAt = contract.SignatureDeadlineAt,
            ActivatedAt = contract.ActivatedAt,
            StatusReason = contract.StatusReason
        };
    }

    private static RoomDepositResponse MapDepositToResponse(RoomDeposit deposit)
    {
        return new RoomDepositResponse
        {
            Id = deposit.Id,
            RentalRequestId = deposit.RentalRequestId,
            RoomId = deposit.RoomId,
            TenantUserId = deposit.TenantUserId,
            LandlordUserId = deposit.LandlordUserId,
            DepositAmount = deposit.DepositAmount,
            Currency = deposit.Currency,
            Status = deposit.Status.ToString(),
            PaymentDeadlineAt = deposit.PaymentDeadlineAt,
            PaidAt = deposit.PaidAt,
            RefundedAt = deposit.RefundedAt,
            ForfeitedAt = deposit.ForfeitedAt,
            RefundAmount = deposit.RefundAmount,
            ForfeitedAmount = deposit.ForfeitedAmount,
            Note = deposit.Note,
            PaymentTransferGroupId = deposit.PaymentTransferGroupId,
            RefundTransferGroupId = deposit.RefundTransferGroupId,
            CreatedAt = deposit.CreatedAt,
            UpdatedAt = deposit.UpdatedAt
        };
    }

    private static void ValidateCreateRequest(CreateRentalRequestRequest request)
    {
        if (request.ExpectedOccupantCount <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Số người ở dự kiến phải lớn hơn 0.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minimumStartDate = today.AddDays(TenantRequestMinimumStartOffsetDays);
        if (request.DesiredStartDate < minimumStartDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Ngày bắt đầu thuê phải cách hôm nay ít nhất 3 ngày để hai bên có thời gian hoàn tất hợp đồng.");
        }

        if (request.ExpectedEndDate <= request.DesiredStartDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Ngày kết thúc dự kiến phải lớn hơn ngày bắt đầu.");
        }
    }

    private static void ValidateRentalDuration(
        CreateRentalRequestRequest request,
        int minRentalMonths,
        int maxRentalMonths)
    {
        var minimumEndDate = request.DesiredStartDate.AddMonths(minRentalMonths);
        var maximumEndDate = request.DesiredStartDate.AddMonths(maxRentalMonths);

        if (request.ExpectedEndDate < minimumEndDate ||
            request.ExpectedEndDate > maximumEndDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Thời gian thuê không nằm trong chính sách của khu trọ.",
                new
                {
                    request.DesiredStartDate,
                    request.ExpectedEndDate,
                    minRentalMonths,
                    maxRentalMonths
                });
        }
    }

    private async Task EnsureTenantBillingEligibilityAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken)
    {
        bool hasOutstandingInvoice = await context.Invoices.AsNoTracking().AnyAsync(
            x => x.TenantUserId == tenantUserId &&
                 (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.Overdue),
            cancellationToken);
        if (hasOutstandingInvoice)
        {
            throw new ConflictException(
                ErrorCodes.TenantOutstandingInvoice,
                "Bạn cần thanh toán tất cả hóa đơn đã phát hành hoặc quá hạn trước khi gửi yêu cầu thuê mới.");
        }

        var pendingFinalInvoiceContracts = await context.RentalContracts.AsNoTracking()
            .Where(contract =>
                contract.Status == RentalContractStatus.Cancelled &&
                contract.TerminationType == ContractTerminationType.TenantUnilateral &&
                contract.TerminationDate.HasValue &&
                contract.TerminationDate.Value >= contract.StartDate &&
                !context.Invoices.Any(invoice =>
                    invoice.ContractId == contract.Id &&
                    invoice.BillingPeriodEnd == contract.TerminationDate.Value &&
                    invoice.Status != InvoiceStatus.Cancelled))
            .Select(contract => new
            {
                contract.Id,
                contract.MainTenantUserId,
                TerminationDate = contract.TerminationDate!.Value
            })
            .ToListAsync(cancellationToken);

        var hasPendingFinalInvoice = false;
        foreach (var contract in pendingFinalInvoiceContracts)
        {
            var effectiveMainTenantUserId = await ResolveEffectiveMainTenantUserIdAsync(
                contract.Id,
                contract.MainTenantUserId,
                contract.TerminationDate,
                cancellationToken);

            if (effectiveMainTenantUserId == tenantUserId)
            {
                hasPendingFinalInvoice = true;
                break;
            }
        }

        if (hasPendingFinalInvoice)
        {
            throw new ConflictException(
                ErrorCodes.TenantFinalInvoicePending,
                "Hợp đồng vừa chấm dứt vẫn đang chờ chủ trọ tạo hóa đơn kỳ cuối.");
        }
    }

    private async Task<Guid> ResolveEffectiveMainTenantUserIdAsync(
        Guid contractId,
        Guid currentContractTenantUserId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var tenantChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null)
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.SortOrder,
                x.OldValue,
                x.NewValue,
                x.FieldName
            })
            .ToListAsync(cancellationToken);

        var mainTenantChanges = tenantChanges
            .Where(x => NormalizeAppendixFieldName(x.FieldName) == "maintenantuserid")
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.SortOrder)
            .ToList();

        var effectiveTenantUserId = currentContractTenantUserId;
        var latestAppliedChange = mainTenantChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.SortOrder)
            .FirstOrDefault();

        if (latestAppliedChange is not null &&
            TryExtractGuid(latestAppliedChange.NewValue, out var appliedTenantUserId))
        {
            effectiveTenantUserId = appliedTenantUserId;
        }
        else if (mainTenantChanges.Count > 0 &&
                 effectiveOn < mainTenantChanges[0].EffectiveDate &&
                 TryExtractGuid(mainTenantChanges[0].OldValue, out var oldTenantUserId))
        {
            effectiveTenantUserId = oldTenantUserId;
        }

        return effectiveTenantUserId;
    }

    private static string NormalizeAppendixFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool TryExtractGuid(string? value, out Guid result)
    {
        result = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out result))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return Guid.TryParse(root.GetString(), out result);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var propertyName in new[] { "id", "userId", "tenantUserId", "mainTenantUserId", "value" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.GetString(), out result))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static void EnsureDesiredStartDateCanBeApproved(DateOnly desiredStartDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minimumStartDate = today.AddDays(LandlordApprovalMinimumStartOffsetDays);
        if (desiredStartDate < minimumStartDate)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestInvalidDuration,
                "Ngày bắt đầu thuê phải còn cách hôm nay ít nhất 2 ngày để hai bên có thời gian hoàn tất hợp đồng.");
        }
    }

    private static void EnsureRoomCanReceiveRentalRequest(
        Domain.Entities.Properties.Room room,
        Guid tenantUserId)
    {
        if (room.RoomingHouse.DeletedAt is not null)
        {
            throw new NotFoundException(
                ErrorCodes.HouseNotFound,
                "Không tìm thấy khu trọ.",
                new { room.RoomingHouseId });
        }

        if (room.RoomingHouse.LandlordUserId == tenantUserId)
        {
            throw new ForbiddenException(
                ErrorCodes.RentalRequestForbidden,
                "Chủ trọ không thể gửi yêu cầu thuê phòng của chính mình.",
                new { room.Id });
        }

        if (room.RoomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            throw new ConflictException(
                ErrorCodes.HouseNotApproved,
                "Chỉ có thể gửi yêu cầu thuê phòng thuộc khu trọ đã được duyệt.",
                new { room.RoomingHouseId, currentStatus = room.RoomingHouse.ApprovalStatus.ToString() });
        }

        if (room.RoomingHouse.VisibilityStatus != RoomingHouseVisibilityStatus.Visible)
        {
            throw new ConflictException(
                ErrorCodes.HouseNotPublic,
                "Khu trọ hiện không công khai.",
                new { room.RoomingHouseId, currentStatus = room.RoomingHouse.VisibilityStatus.ToString() });
        }

        if (room.Status != RoomStatus.Available)
        {
            throw new ConflictException(
                ErrorCodes.RoomNotAvailable,
                "Phòng hiện không còn trống.",
                new { room.Id, currentStatus = room.Status.ToString() });
        }
    }

    private static decimal ResolveMonthlyRent(
        Domain.Entities.Properties.Room room,
        int expectedOccupantCount)
    {
        if (expectedOccupantCount > room.MaxOccupants)
        {
            throw new BadRequestException(
                ErrorCodes.RentalRequestOccupantLimitExceeded,
                "Số người ở dự kiến vượt quá sức chứa của phòng.",
                new { expectedOccupantCount, room.MaxOccupants });
        }

        var occupantCount = room.IsTieredPricing ? expectedOccupantCount : 1;
        var tier = room.PriceTiers.FirstOrDefault(x => x.IsActive && x.OccupantCount == occupantCount);

        if (tier is null)
        {
            throw new ConflictException(
                ErrorCodes.PriceTierInvalid,
                "Phòng chưa có bảng giá phù hợp với số người ở dự kiến.",
                new { room.Id, occupantCount });
        }

        return tier.MonthlyRent;
    }

    private static void EnsureCanView(Guid userId, RentalRequest rentalRequest)
    {
        if (rentalRequest.TenantUserId == userId ||
            rentalRequest.Room.RoomingHouse.LandlordUserId == userId)
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalRequestForbidden,
            "Bạn không có quyền xem yêu cầu thuê này.",
            new { rentalRequest.Id });
    }

    private static void EnsureLandlordOwnsRequest(Guid landlordUserId, RentalRequest rentalRequest)
    {
        if (rentalRequest.Room.RoomingHouse.LandlordUserId == landlordUserId)
        {
            return;
        }

        throw new ForbiddenException(
            ErrorCodes.RentalRequestForbidden,
            "Bạn không có quyền xử lý yêu cầu thuê này.",
            new { rentalRequest.Id });
    }

    private static void EnsureStatus(RentalRequest rentalRequest, RentalRequestStatus expectedStatus)
    {
        if (rentalRequest.Status == expectedStatus)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.RentalRequestInvalidStatus,
            "Trạng thái yêu cầu thuê không hợp lệ cho thao tác này.",
            new
            {
                rentalRequest.Id,
                currentStatus = rentalRequest.Status.ToString(),
                expectedStatus = expectedStatus.ToString()
            });
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
