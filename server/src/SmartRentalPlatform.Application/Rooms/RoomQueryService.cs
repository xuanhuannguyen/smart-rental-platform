using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomQueryService : IRoomQueryService
{
    private readonly IAppDbContext context;
    private readonly RoomAccessService roomAccessService;

    public RoomQueryService(IAppDbContext context, RoomAccessService roomAccessService)
    {
        this.context = context;
        this.roomAccessService = roomAccessService;
    }

    public async Task<List<RoomResponse>> GetByRoomingHouseAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        await roomAccessService.EnsureOwnedApprovedRoomingHouseAsync(
            landlordUserId,
            roomingHouseId,
            cancellationToken);

        var rooms = await BuildRoomQuery()
            .Where(x => x.RoomingHouseId == roomingHouseId && x.DeletedAt == null)
            .OrderBy(x => x.Floor)
            .ThenBy(x => x.RoomNumber)
            .ToListAsync(cancellationToken);

        return rooms.Select(RoomReadModelMapper.ToResponse).ToList();
    }

    public async Task<RoomResponse?> GetByIdAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = await BuildRoomQuery()
            .FirstOrDefaultAsync(
                x => x.Id == roomId &&
                     x.DeletedAt == null &&
                     x.RoomingHouse.LandlordUserId == landlordUserId,
                cancellationToken);

        return room is null ? null : RoomReadModelMapper.ToResponse(room);
    }

    private IQueryable<Room> BuildRoomQuery()
    {
        return context.Rooms
            .AsNoTracking()
            .Include(x => x.RoomingHouse)
            .Include(x => x.PriceTiers)
            .Include(x => x.Images)
            .Include(x => x.RoomAmenities)
                .ThenInclude(x => x.Amenity);
    }
}
