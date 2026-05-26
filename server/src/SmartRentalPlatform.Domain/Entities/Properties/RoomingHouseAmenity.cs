namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomingHouseAmenity
    {
        public Guid RoomingHouseId { get; set; }
        public int AmenityId { get; set; }


        public RoomingHouse RoomingHouse { get; set; } = null!;
        public Amenity Amenity { get; set; } = null!;
    }
}