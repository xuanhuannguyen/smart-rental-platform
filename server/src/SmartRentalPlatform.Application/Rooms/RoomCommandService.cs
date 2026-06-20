using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomCommandService : IRoomCommandService
{
    private readonly IAppDbContext context;
    private readonly RoomAccessService roomAccessService;
    private readonly IRoomQueryService roomQueryService;

    public RoomCommandService(
        IAppDbContext context,
        RoomAccessService roomAccessService,
        IRoomQueryService roomQueryService)
    {
        this.context = context;
        this.roomAccessService = roomAccessService;
        this.roomQueryService = roomQueryService;
    }

    public async Task<RoomResponse> CreateAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        await roomAccessService.EnsureOwnedApprovedRoomingHouseAsync(
            landlordUserId,
            roomingHouseId,
            cancellationToken);

        RoomValidationRules.ValidateRoomFields(
            request.RoomNumber,
            request.Floor,
            request.AreaM2,
            request.MaxOccupants);

        var roomNumber = request.RoomNumber.Trim();
        await roomAccessService.EnsureRoomNumberAvailableAsync(
            roomingHouseId,
            roomNumber,
            excludedRoomId: null,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = roomingHouseId,
            RoomNumber = roomNumber,
            Floor = request.Floor,
            AreaM2 = request.AreaM2,
            MaxOccupants = request.MaxOccupants,
            IsTieredPricing = request.IsTieredPricing,
            Description = request.Description,
            Status = RoomStatus.Hidden,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Rooms.Add(room);
        await context.SaveChangesAsync(cancellationToken);

        return await roomQueryService.GetByIdAsync(landlordUserId, room.Id, cancellationToken)
            ?? throw new InternalServerException(
                ErrorCodes.InternalServerError,
                "Đã tạo phòng nhưng không thể tải lại thông tin phòng.",
                new { roomId = room.Id });
    }

    public async Task<RoomResponse?> UpdateAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default)
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
        EnsureRoomCanEditBasicInfo(room);
        RoomValidationRules.ValidateRoomFields(
            request.RoomNumber,
            request.Floor,
            request.AreaM2,
            request.MaxOccupants);

        var exceedingTiers = await context.RoomPriceTiers
            .Where(x => x.RoomId == roomId && x.OccupantCount > request.MaxOccupants)
            .ToListAsync(cancellationToken);
        if (exceedingTiers.Count > 0)
        {
            context.RoomPriceTiers.RemoveRange(exceedingTiers);
        }

        if (!request.IsTieredPricing)
        {
            var extraTiers = await context.RoomPriceTiers
                .Where(x => x.RoomId == roomId && x.OccupantCount > 1)
                .ToListAsync(cancellationToken);
            if (extraTiers.Count > 0)
            {
                context.RoomPriceTiers.RemoveRange(extraTiers);
            }
        }

        var roomNumber = request.RoomNumber.Trim();
        await roomAccessService.EnsureRoomNumberAvailableAsync(
            room.RoomingHouseId,
            roomNumber,
            roomId,
            cancellationToken);

        room.RoomNumber = roomNumber;
        room.Floor = request.Floor;
        room.AreaM2 = request.AreaM2;
        room.MaxOccupants = request.MaxOccupants;
        room.IsTieredPricing = request.IsTieredPricing;
        room.Description = request.Description;
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    private static void EnsureRoomCanEditBasicInfo(Room room)
    {
        if (room.Status is RoomStatus.Reserved or RoomStatus.Occupied)
        {
            throw new ConflictException(
                ErrorCodes.RoomInvalidStatus,
                "Không thể chỉnh sửa thông tin phòng khi phòng đang được giữ chỗ hoặc đang có hợp đồng active.",
                new { currentStatus = room.Status.ToString() });
        }
    }
}
