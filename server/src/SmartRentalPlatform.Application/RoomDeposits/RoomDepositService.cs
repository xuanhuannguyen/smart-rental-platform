using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RoomDeposits;

public class RoomDepositService : IRoomDepositService
{
    private readonly IAppDbContext context;
    private readonly IWalletService walletService;
    private readonly IPaymentRowLockService rowLockService;

    public RoomDepositService(
        IAppDbContext context,
        IWalletService walletService,
        IPaymentRowLockService rowLockService)
    {
        this.context = context;
        this.walletService = walletService;
        this.rowLockService = rowLockService;
    }

    public async Task<RoomDepositResponse?> PayAsync(
        Guid tenantUserId,
        Guid roomDepositId,
        CancellationToken cancellationToken = default)
    {
        var depositSnapshot = await context.RoomDeposits
            .AsNoTracking()
            .Include(x => x.RentalContract)
            .FirstOrDefaultAsync(x => x.Id == roomDepositId, cancellationToken);

        if (depositSnapshot is null)
        {
            return null;
        }

        EnsureTenantCanPay(depositSnapshot, tenantUserId);

        if (depositSnapshot.Status == RoomDepositStatus.Paid)
        {
            EnsureCompletedPaymentIsConsistent(depositSnapshot);
            return MapToResponse(depositSnapshot);
        }

        EnsureDepositCanBePaid(depositSnapshot, DateTimeOffset.UtcNow);

        var tenantWallet = await walletService.GetOrCreateWalletAsync(tenantUserId, cancellationToken);
        var landlordWallet = await walletService.GetOrCreateWalletAsync(depositSnapshot.LandlordUserId, cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var lockedDeposit = await rowLockService.LockRoomDepositAsync(roomDepositId, cancellationToken);
            if (lockedDeposit is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var deposit = await context.RoomDeposits
                .Include(x => x.RentalRequest)
                .Include(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.RentalPolicy)
                .Include(x => x.RentalContract)
                .FirstAsync(x => x.Id == roomDepositId, cancellationToken);

            EnsureTenantCanPay(deposit, tenantUserId);

            if (deposit.Status == RoomDepositStatus.Paid)
            {
                EnsureCompletedPaymentIsConsistent(deposit);
                await transaction.CommitAsync(cancellationToken);
                return MapToResponse(deposit);
            }

            var now = DateTimeOffset.UtcNow;
            EnsureDepositCanBePaid(deposit, now);

            if (deposit.PaymentTransferGroupId.HasValue)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositInvalidStatus,
                    "Khoản cọc đã có giao dịch thanh toán nhưng trạng thái chưa đồng bộ.",
                    new { deposit.Id, deposit.PaymentTransferGroupId });
            }

            var contractExists = deposit.RentalContract is not null ||
                await context.RentalContracts.AnyAsync(
                    x => x.RoomDepositId == deposit.Id || x.RentalRequestId == deposit.RentalRequestId,
                    cancellationToken);

            if (contractExists)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositInvalidStatus,
                    "Khoản cọc này đã được dùng để tạo hợp đồng.",
                    new { roomDepositId });
            }

            var rentalPolicy = deposit.Room.RoomingHouse.RentalPolicy;
            if (rentalPolicy is null || !rentalPolicy.IsActive)
            {
                throw new ConflictException(
                    ErrorCodes.RentalPolicyRequired,
                    "Khu trọ chưa có chính sách thuê đang hoạt động.",
                    new { deposit.Room.RoomingHouseId });
            }

            var transfer = await walletService.TransferToReservedWithinTransactionAsync(
                tenantWallet.Id,
                landlordWallet.Id,
                deposit.DepositAmount,
                WalletTransactionType.DepositPayment,
                WalletTransactionType.DepositReceive,
                new WalletTransactionMetadata
                {
                    RelatedEntityType = "RoomDeposit",
                    RelatedEntityId = deposit.Id,
                    Description = $"Thanh toán tiền cọc cho yêu cầu thuê {deposit.RentalRequestId}."
                },
                cancellationToken);

            deposit.Status = RoomDepositStatus.Paid;
            deposit.PaidAt = now;
            deposit.PaymentTransferGroupId = transfer.TransferGroupId;
            deposit.UpdatedAt = now;

            var contract = new RentalContract
            {
                Id = Guid.NewGuid(),
                RentalRequestId = deposit.RentalRequestId,
                RoomDepositId = deposit.Id,
                RoomId = deposit.RoomId,
                MainTenantUserId = deposit.TenantUserId,
                ContractNumber = GenerateContractNumber(now),
                StartDate = deposit.RentalRequest.DesiredStartDate,
                EndDate = deposit.RentalRequest.ExpectedEndDate,
                MonthlyRent = deposit.RentalRequest.MonthlyRentSnapshot,
                DepositAmount = deposit.DepositAmount,
                PaymentDay = rentalPolicy.DefaultPaymentDay,
                Status = RentalContractStatus.WaitingTenantOccupants,
                RoomSnapshot = BuildRoomSnapshot(deposit.Room),
                CreatedAt = now,
                UpdatedAt = now
            };

            context.RentalContracts.Add(contract);

            var otherPendingRequests = await context.RentalRequests
                .Where(x => x.RoomId == deposit.RoomId &&
                            x.Id != deposit.RentalRequestId &&
                            x.Status == RentalRequestStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var rentalRequest in otherPendingRequests)
            {
                rentalRequest.Status = RentalRequestStatus.Expired;
                rentalRequest.RejectedReason = "Phòng đã được người thuê khác thanh toán cọc.";
                rentalRequest.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return MapToResponse(deposit);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var overdueDepositIds = await context.RoomDeposits
            .AsNoTracking()
            .Where(x => x.Status == RoomDepositStatus.PendingPayment &&
                        x.PaymentDeadlineAt.HasValue &&
                        x.PaymentDeadlineAt.Value <= now)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (overdueDepositIds.Count == 0)
        {
            return 0;
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var expiredCount = 0;

            foreach (var depositId in overdueDepositIds)
            {
                var lockedDeposit = await rowLockService.LockRoomDepositAsync(depositId, cancellationToken);
                if (lockedDeposit is null ||
                    lockedDeposit.Status != RoomDepositStatus.PendingPayment ||
                    !lockedDeposit.PaymentDeadlineAt.HasValue ||
                    lockedDeposit.PaymentDeadlineAt.Value > now)
                {
                    continue;
                }

                var deposit = await context.RoomDeposits
                    .Include(x => x.RentalRequest)
                    .Include(x => x.Room)
                    .FirstAsync(x => x.Id == depositId, cancellationToken);

                deposit.Status = RoomDepositStatus.Expired;
                deposit.UpdatedAt = now;

                deposit.RentalRequest.Status = RentalRequestStatus.Expired;
                deposit.RentalRequest.UpdatedAt = now;

                if (deposit.Room.Status == RoomStatus.Reserved)
                {
                    deposit.Room.Status = RoomStatus.Available;
                    deposit.Room.UpdatedAt = now;
                }

                expiredCount++;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return expiredCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void EnsureTenantCanPay(RoomDeposit deposit, Guid tenantUserId)
    {
        if (deposit.TenantUserId != tenantUserId)
        {
            throw new ForbiddenException(
                ErrorCodes.RoomDepositForbidden,
                "Bạn không có quyền thanh toán khoản cọc này.",
                new { deposit.Id });
        }
    }

    private static void EnsureDepositCanBePaid(RoomDeposit deposit, DateTimeOffset now)
    {
        if (deposit.Status != RoomDepositStatus.PendingPayment)
        {
            throw new ConflictException(
                ErrorCodes.RoomDepositInvalidStatus,
                "Khoản cọc không ở trạng thái chờ thanh toán.",
                new { deposit.Id, currentStatus = deposit.Status.ToString() });
        }

        if (deposit.PaymentDeadlineAt.HasValue && deposit.PaymentDeadlineAt.Value <= now)
        {
            throw new ConflictException(
                ErrorCodes.RoomDepositExpired,
                "Khoản cọc đã quá hạn thanh toán.",
                new { deposit.Id, deposit.PaymentDeadlineAt });
        }
    }

    private static void EnsureCompletedPaymentIsConsistent(RoomDeposit deposit)
    {
        if (!deposit.PaymentTransferGroupId.HasValue || deposit.RentalContract is null)
        {
            throw new ConflictException(
                ErrorCodes.RoomDepositInvalidStatus,
                "Khoản cọc đã được đánh dấu thanh toán nhưng thiếu giao dịch ví hoặc hợp đồng.",
                new
                {
                    deposit.Id,
                    deposit.PaymentTransferGroupId,
                    hasContract = deposit.RentalContract is not null
                });
        }
    }

    private static RoomDepositResponse MapToResponse(RoomDeposit deposit)
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

    private static string GenerateContractNumber(DateTimeOffset now)
    {
        return $"HD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
    }

    private static string BuildRoomSnapshot(Room room)
    {
        var snapshot = new
        {
            room.Id,
            room.RoomingHouseId,
            RoomingHouseName = room.RoomingHouse.Name,
            room.RoomNumber,
            room.Floor,
            room.AreaM2,
            room.MaxOccupants,
            room.Description
        };

        return JsonSerializer.Serialize(snapshot);
    }
}
