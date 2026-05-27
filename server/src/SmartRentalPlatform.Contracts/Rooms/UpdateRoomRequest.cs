namespace SmartRentalPlatform.Contracts.Rooms
{
    public class UpdateRoomRequest
    {
        public string RoomNumber { get; set; } = string.Empty;

        public int Floor { get; set; } = 1;

        public decimal? AreaM2 { get; set; }

        public int MaxOccupants { get; set; } = 1;

        public bool IsTieredPricing { get; set; } = false;

        public string? Description { get; set; }
    }
}
