using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class FavoriteRoomingHouseService : IFavoriteRoomingHouseService
{
    private readonly IAppDbContext _dbContext;

    public FavoriteRoomingHouseService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ToggleFavoriteAsync(Guid roomingHouseId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra xem đã yêu thích chưa
        var existingFavorite = await _dbContext.FavoriteRoomingHouses
            .FirstOrDefaultAsync(f => f.UserId == currentUserId && f.RoomingHouseId == roomingHouseId, cancellationToken);

        if (existingFavorite != null)
        {
            _dbContext.FavoriteRoomingHouses.Remove(existingFavorite);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false; // Trạng thái mới: Đã gỡ yêu thích
        }
        else
        {
            // 2. Nếu chưa yêu thích, kiểm tra khu trọ tồn tại và đang public trước khi thêm
            var roomingHouseExists = await _dbContext.RoomingHouses
                .AnyAsync(r => r.Id == roomingHouseId 
                            && r.DeletedAt == null
                            && r.ApprovalStatus == RoomingHouseApprovalStatus.Approved
                            && r.VisibilityStatus == RoomingHouseVisibilityStatus.Visible, 
                          cancellationToken);

            if (!roomingHouseExists) 
                throw new NotFoundException(ErrorCodes.HouseNotFound, "Khu trọ không tồn tại hoặc đã bị ẩn.");

            _dbContext.FavoriteRoomingHouses.Add(new FavoriteRoomingHouse
            {
                UserId = currentUserId,
                RoomingHouseId = roomingHouseId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true; // Trạng thái mới: Đã thêm yêu thích
        }
    }

    public async Task<PagedResult<RoomingHouseListingResponse>> GetMyFavoritesAsync(Guid currentUserId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FavoriteRoomingHouses
            .Where(f => f.UserId == currentUserId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.RoomingHouse)
            .Where(x => x.DeletedAt == null && x.ApprovalStatus == RoomingHouseApprovalStatus.Approved && x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible);

        var totalItems = await query.CountAsync(cancellationToken);
        
        var rawItems = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                AddressDisplay = x.Ward != null && x.Province != null
                    ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                    : x.AddressDisplay,
                CoverImageMediaAssetId = x.Images
                    .Where(i => i.MediaAssetId.HasValue)
                    .OrderByDescending(i => i.IsCover)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.MediaAssetId)
                    .FirstOrDefault(),
                AvailableRooms = x.Rooms.Count(r => r.Status == RoomStatus.Available && r.DeletedAt == null),
                MinMonthlyRent = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                    .SelectMany(r => r.PriceTiers)
                    .Where(p => p.IsActive)
                    .Select(p => (decimal?)p.MonthlyRent)
                    .Min(),
                MaxMonthlyRent = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                    .SelectMany(r => r.PriceTiers)
                    .Where(p => p.IsActive)
                    .Select(p => (decimal?)p.MonthlyRent)
                    .Max(),
                MinAreaM2 = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                    .Select(r => (decimal?)r.AreaM2)
                    .Min(),
                MaxAreaM2 = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                    .Select(r => (decimal?)r.AreaM2)
                    .Max(),
                Amenities = x.RoomingHouseAmenities
                    .Select(a => new AmenityResponse
                    {
                        Id = a.Amenity.Id,
                        Name = a.Amenity.Name,
                        IconCode = a.Amenity.IconCode
                    })
                    .ToList(),
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(x => new RoomingHouseListingResponse
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                    ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                    : null,
                AvailableRooms = x.AvailableRooms,
                MinMonthlyRent = x.MinMonthlyRent,
                MaxMonthlyRent = x.MaxMonthlyRent,
                MinAreaM2 = x.MinAreaM2,
                MaxAreaM2 = x.MaxAreaM2,
                Amenities = x.Amenities,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        return new PagedResult<RoomingHouseListingResponse>
        {
            Items = items,
            Page = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<List<Guid>> GetMyFavoriteIdsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FavoriteRoomingHouses
            .Where(f => f.UserId == currentUserId 
                     && f.RoomingHouse.DeletedAt == null
                     && f.RoomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Approved
                     && f.RoomingHouse.VisibilityStatus == RoomingHouseVisibilityStatus.Visible)
            .Select(f => f.RoomingHouseId)
            .ToListAsync(cancellationToken);
    }
}
