using SmartRentalPlatform.Domain.Entities.Users;
using System;

namespace SmartRentalPlatform.Domain.Entities.Properties;

public class FavoriteRoomingHouse
{
    public Guid UserId { get; set; }
    public Guid RoomingHouseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation Properties
    public User User { get; set; } = null!;
    public RoomingHouse RoomingHouse { get; set; } = null!;
}
