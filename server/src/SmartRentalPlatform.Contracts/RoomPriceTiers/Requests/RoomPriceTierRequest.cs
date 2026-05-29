namespace SmartRentalPlatform.Contracts.RoomPriceTiers.Requests;

    public class RoomPriceTierRequest
    {
        public int OccupantCount { get; set; }

        public decimal MonthlyRent { get; set; }

        public bool IsActive { get; set; } = true;
    }