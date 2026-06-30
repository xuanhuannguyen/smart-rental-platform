using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomPriceTierService : IRoomPriceTierService
{
    private readonly IAppDbContext context;
    private readonly RoomAccessService roomAccessService;
    private readonly IRoomQueryService roomQueryService;

    public RoomPriceTierService(
        IAppDbContext context,
        RoomAccessService roomAccessService,
        IRoomQueryService roomQueryService)
    {
        this.context = context;
        this.roomAccessService = roomAccessService;
        this.roomQueryService = roomQueryService;
    }

    public async Task<RoomResponse?> UpdatePriceTiersAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomPriceTiersRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var room = await roomAccessService.GetOwnedRoomForUpdateAsync(
                landlordUserId,
                roomId,
                cancellationToken);

            if (room is null)
            {
                return null;
            }

            roomAccessService.EnsureRoomingHouseApproved(room.RoomingHouse);
            EnsureRoomCanEditPriceTiers(room);
            RoomValidationRules.ValidatePriceTiers(
                request.PriceTiers,
                room.MaxOccupants,
                room.IsTieredPricing);

            var currentPriceTiers = await context.RoomPriceTiers
                .Where(x => x.RoomId == roomId)
                .ToListAsync(cancellationToken);

            context.RoomPriceTiers.RemoveRange(currentPriceTiers);

            var now = DateTimeOffset.UtcNow;
            foreach (var tier in request.PriceTiers)
            {
                context.RoomPriceTiers.Add(new RoomPriceTier
                {
                    Id = Guid.NewGuid(),
                    RoomId = roomId,
                    OccupantCount = tier.OccupantCount,
                    MonthlyRent = tier.MonthlyRent,
                    IsActive = tier.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            room.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    private static void EnsureRoomCanEditPriceTiers(Room room)
    {
        if (room.Status is RoomStatus.Reserved or RoomStatus.Occupied)
        {
            throw new ConflictException(
                ErrorCodes.RoomInvalidStatus,
                "Không thể chỉnh sửa bảng giá phòng khi phòng đang được giữ chỗ hoặc đang có hợp đồng active.",
                new { currentStatus = room.Status.ToString() });
        }
    }
}
