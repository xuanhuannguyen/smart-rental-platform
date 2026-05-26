namespace SmartRentalPlatform.Contracts.RoomPriceTiers
{
    public class RoomPriceTierRequest
    {
        public int OccupantCount { get; set; }

        public decimal MonthlyRent { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
