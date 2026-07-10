using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.Rooms;

internal static class RoomReadModelMapper
{
    public static RoomResponse ToResponse(Room room)
    {
        return new RoomResponse
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
                    IsActive = x.IsActive
                })
                .ToList(),
            Images = room.Images
                .OrderBy(x => x.SortOrder)
                .Select(x => new PropertyImageResponse
                {
                    Id = x.Id,
                    ObjectKey = x.ObjectKey,
                    ImageUrl = BuildImageUrl(x.ObjectKey),
                    Caption = x.Caption,
                    IsCover = x.IsCover,
                    SortOrder = x.SortOrder,
                    CreatedAt = x.CreatedAt
                })
                .ToList(),
            Amenities = room.RoomAmenities
                .Select(x => new AmenityResponse
                {
                    Id = x.Amenity.Id,
                    Name = x.Amenity.Name,
                    Scope = x.Amenity.Scope.ToString(),
                    IconCode = x.Amenity.IconCode
                })
                .ToList()
        };
    }

    public static string BuildImageUrl(string objectKey)
    {
        return PublicMediaPathBuilder.Build(objectKey);
    }
}
