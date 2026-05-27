using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class AmenitySeed
{
    public const int WifiId = 1;
    public const int SecurityCameraId = 2;
    public const int ParkingId = 3;
    public const int WashingMachineId = 4;
    public const int AirConditionerId = 5;
    public const int MezzanineId = 6;
    public const int PrivateBathroomId = 7;
    public const int BalconyId = 8;

    public static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static List<Amenity> GetAmenities()
    {
        return
        [
            new Amenity
            {
                Id = WifiId,
                Name = "Wifi",
                Scope = AmenityScope.Both,
                IconCode = "wifi",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = SecurityCameraId,
                Name = "Camera an ninh",
                Scope = AmenityScope.House,
                IconCode = "camera",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = ParkingId,
                Name = "Chỗ để xe",
                Scope = AmenityScope.House,
                IconCode = "parking-circle",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = WashingMachineId,
                Name = "Máy giặt",
                Scope = AmenityScope.Both,
                IconCode = "washing-machine",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = AirConditionerId,
                Name = "Máy lạnh",
                Scope = AmenityScope.Room,
                IconCode = "air-vent",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = MezzanineId,
                Name = "Gác lửng",
                Scope = AmenityScope.Room,
                IconCode = "layers",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = PrivateBathroomId,
                Name = "Nhà vệ sinh riêng",
                Scope = AmenityScope.Room,
                IconCode = "bath",
                IsActive = true,
                CreatedAt = SeededAt
            },
            new Amenity
            {
                Id = BalconyId,
                Name = "Ban công",
                Scope = AmenityScope.Room,
                IconCode = "door-open",
                IsActive = true,
                CreatedAt = SeededAt
            }
        ];
    }
}
