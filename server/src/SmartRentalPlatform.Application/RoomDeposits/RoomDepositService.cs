using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RoomDeposits;

public class RoomDepositService : IRoomDepositService
{
    private readonly IAppDbContext context;

    public RoomDepositService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<RoomDepositResponse?> MarkPaidAsync(
        Guid tenantUserId,
        Guid roomDepositId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var deposit = await context.RoomDeposits
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM room_deposits
                    WHERE id = {roomDepositId}
                    FOR UPDATE
                    """)
                .Include(x => x.RentalRequest)
                .Include(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.RentalPolicy)
                .FirstOrDefaultAsync(cancellationToken);

            if (deposit is null)
            {
                return null;
            }

            if (deposit.TenantUserId != tenantUserId)
            {
                throw new ForbiddenException(
                    ErrorCodes.RoomDepositForbidden,
                    "Bạn không có quyền thanh toán khoản cọc này.",
                    new { roomDepositId });
            }

            if (deposit.Status != RoomDepositStatus.PendingPayment)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositInvalidStatus,
                    "Khoản cọc không ở trạng thái chờ thanh toán.",
                    new { roomDepositId, currentStatus = deposit.Status.ToString() });
            }

            var now = DateTimeOffset.UtcNow;
            if (deposit.PaymentDeadlineAt.HasValue && deposit.PaymentDeadlineAt.Value <= now)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositExpired,
                    "Khoản cọc đã quá hạn thanh toán.",
                    new { roomDepositId, deposit.PaymentDeadlineAt });
            }

            var contractExists = await context.RentalContracts.AnyAsync(
                x => x.RoomDepositId == deposit.Id || x.RentalRequestId == deposit.RentalRequestId,
                cancellationToken);

            if (contractExists)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDepositInvalidStatus,
                    "Khoản cọc này đã được dùng để tạo hợp đồng.",
                    new { roomDepositId });
            }

            deposit.Status = RoomDepositStatus.Paid;
            deposit.PaidAt = now;
            deposit.UpdatedAt = now;

            var rentalPolicy = deposit.Room.RoomingHouse.RentalPolicy;
            if (rentalPolicy is null || !rentalPolicy.IsActive)
            {
                throw new ConflictException(
                    ErrorCodes.RentalPolicyRequired,
                    "Khu tro chua co chinh sach thue dang hoat dong.",
                    new { deposit.Room.RoomingHouseId });
            }

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

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var overdueDeposits = await context.RoomDeposits
                .Include(x => x.RentalRequest)
                .Include(x => x.Room)
                .Where(x => x.Status == RoomDepositStatus.PendingPayment &&
                            x.PaymentDeadlineAt.HasValue &&
                            x.PaymentDeadlineAt.Value <= now)
                .ToListAsync(cancellationToken);

            foreach (var deposit in overdueDeposits)
            {
                deposit.Status = RoomDepositStatus.Expired;
                deposit.UpdatedAt = now;

                deposit.RentalRequest.Status = RentalRequestStatus.Expired;
                deposit.RentalRequest.UpdatedAt = now;

                if (deposit.Room.Status == RoomStatus.Reserved)
                {
                    deposit.Room.Status = RoomStatus.Available;
                    deposit.Room.UpdatedAt = now;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return overdueDeposits.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
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
