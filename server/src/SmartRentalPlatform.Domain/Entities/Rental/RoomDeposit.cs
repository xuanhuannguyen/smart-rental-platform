using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Rental;

namespace SmartRentalPlatform.Domain.Entities.Rental
{
    public class RoomDeposit
    {
        public Guid Id { get; set; }

        public Guid RentalRequestId { get; set; }

        public Guid RoomId { get; set; }

        public Guid TenantUserId { get; set; }

        public Guid LandlordUserId { get; set; }

        public decimal DepositAmount { get; set; }

        public string Currency { get; set; } = "VND";

        public RoomDepositStatus Status { get; set; }

        public DateTimeOffset? PaymentDeadlineAt { get; set; }

        public DateTimeOffset? PaidAt { get; set; }

        public DateTimeOffset? RefundedAt { get; set; }

        public DateTimeOffset? ForfeitedAt { get; set; }

        public decimal? RefundAmount { get; set; }

        public decimal? ForfeitedAmount { get; set; }

        public string? Note { get; set; }

        public Guid? PaymentTransferGroupId { get; set; }

        public Guid? RefundTransferGroupId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public RentalRequest RentalRequest { get; set; } = null!;

        public Room Room { get; set; } = null!;

        public User TenantUser { get; set; } = null!;

        public User LandlordUser { get; set; } = null!;

        public RentalContract? RentalContract { get; set; }
    }
}
