using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomPriceTiers.Responses;
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

    public async Task<List<RoomResponse>> GetPublicAvailableRoomsAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var rooms = await ProjectPublicRoomResponse(
                context.Rooms
                    .Where(x => x.RoomingHouseId == roomingHouseId &&
                                x.DeletedAt == null &&
                                x.Status == RoomStatus.Available &&
                                x.RoomingHouse.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                                x.RoomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Approved))
            .OrderBy(x => x.Floor)
            .ThenBy(x => x.RoomNumber)
            .ToListAsync(cancellationToken);

        HydratePublicImageUrls(rooms);
        return rooms;
    }

    public async Task<RoomResponse?> GetPublicRoomByIdAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = await ProjectPublicRoomResponse(
                context.Rooms
                    .Where(x => x.Id == roomId &&
                                x.DeletedAt == null &&
                                x.Status == RoomStatus.Available &&
                                x.RoomingHouse.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                                x.RoomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Approved))
            .FirstOrDefaultAsync(cancellationToken);

        if (room is not null)
        {
            HydratePublicImageUrls(room);
        }

        return room;
    }

    private IQueryable<Room> BuildRoomQuery()
    {
        return context.Rooms
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.RoomingHouse)
            .Include(x => x.PriceTiers)
            .Include(x => x.Images)
            .Include(x => x.RoomAmenities)
                .ThenInclude(x => x.Amenity);
    }

    private static IQueryable<RoomResponse> ProjectPublicRoomResponse(IQueryable<Room> rooms)
    {
        return rooms
            .AsNoTracking()
            .Select(room => new RoomResponse
            {
                Id = room.Id,
                RoomingHouseId = room.RoomingHouseId,
                RoomNumber = room.RoomNumber,
                Floor = room.Floor,
                AreaM2 = room.AreaM2,
                MaxOccupants = room.MaxOccupants,
                IsTieredPricing = room.IsTieredPricing,
                Status = room.Status.ToString(),
                Description = room.Description,
                CreatedAt = room.CreatedAt,
                UpdatedAt = room.UpdatedAt,
                PriceTiers = room.PriceTiers
                    .OrderBy(x => x.OccupantCount)
                    .Select(x => new RoomPriceTierResponse
                    {
                        Id = x.Id,
                        OccupantCount = x.OccupantCount,
                        MonthlyRent = x.MonthlyRent,
                        IsActive = x.IsActive,
                    })
                    .ToList(),
                Images = room.Images
                    .Where(x => x.MediaAssetId.HasValue)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new PropertyImageResponse
                    {
                        Id = x.Id,
                        MediaAssetId = x.MediaAssetId,
                        Caption = x.Caption,
                        IsCover = x.IsCover,
                        SortOrder = x.SortOrder,
                        CreatedAt = x.CreatedAt,
                    })
                    .ToList(),
                Amenities = room.RoomAmenities
                    .Select(x => new AmenityResponse
                    {
                        Id = x.Amenity.Id,
                        Name = x.Amenity.Name,
                        Scope = x.Amenity.Scope.ToString(),
                        IconCode = x.Amenity.IconCode,
                    })
                    .ToList(),
            });
    }

    private static void HydratePublicImageUrls(IEnumerable<RoomResponse> rooms)
    {
        foreach (var room in rooms)
        {
            HydratePublicImageUrls(room);
        }
    }

    private static void HydratePublicImageUrls(RoomResponse room)
    {
        foreach (var image in room.Images)
        {
            image.ImageUrl = image.MediaAssetId.HasValue
                ? PublicMediaPathBuilder.Build(image.MediaAssetId.Value)
                : string.Empty;
        }
    }
}
