using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.Rooms
{
    public class RoomResponse
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int Floor { get; set; }
        public decimal? AreaM2 { get; set; }
        public int MaxOccupants { get; set; }
        public bool IsTieredPricing { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<RoomPriceTierResponse> PriceTiers { get; set; } = new();
        public List<PropertyImageResponse> Images { get; set; } = new();
        public List<AmenityResponse> Amenities { get; set; } = new();
    }
}
