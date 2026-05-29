using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class Room
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseId { get; set; }
        public string RoomNumber {  get; set; } = string.Empty;
        public int Floor { get; set; } = 1;
        public decimal? AreaM2 { get; set; }
        public int MaxOccupants { get; set; } = 1;
        public bool IsTieredPricing { get; set; } = false;
        public RoomStatus Status { get; set; } = RoomStatus.Available;
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        
        public RoomingHouse RoomingHouse { get; set; } = null!;
        public ICollection<RoomPriceTier> PriceTiers { get; set; } = new List<RoomPriceTier>();
        public ICollection<RoomAmenity> RoomAmenities { get; set; } = new List<RoomAmenity>();
        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    }
}
