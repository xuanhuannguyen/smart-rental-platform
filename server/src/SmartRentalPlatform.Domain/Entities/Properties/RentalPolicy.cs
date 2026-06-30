namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RentalPolicy
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseId { get; set; }
        public int MinRentalMonths { get; set; }
        public int MaxRentalMonths { get; set; }
        public bool AllowShortTermRenewal { get; set; }
        public int RenewalNoticeDays { get; set; }
        public decimal DepositMonths { get; set; }
        public int DefaultPaymentDay { get; set; } = 5;
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public RoomingHouse RoomingHouse { get; set; } = null!;
    }
}
