using System;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class LeasePolicy
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseId { get; set; }
        public bool AllowShortTermRenewal { get; set; }
        public int RenewalNoticeDays { get; set; }
        public decimal DepositMonths { get; set; }
        public decimal Discount6MonthsPercent { get; set; }
        public decimal Discount9MonthsPercent { get; set; }
        public decimal Discount12MonthsPercent { get; set; }
        public decimal Discount24MonthsPercent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public RoomingHouse RoomingHouse { get; set; } = null!;
    }
}
