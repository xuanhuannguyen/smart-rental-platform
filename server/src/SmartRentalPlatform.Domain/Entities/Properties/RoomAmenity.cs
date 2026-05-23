namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomAmenity
    {
        public Guid RoomId { get; set; }
        public int AmenityId { get; set; }


        public Room Room { get; set; } = null!;
        public Amenity Amenity { get; set; } = null!;
    }
}