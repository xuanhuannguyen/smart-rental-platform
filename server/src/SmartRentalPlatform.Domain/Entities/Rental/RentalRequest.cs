using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Rental;

namespace SmartRentalPlatform.Domain.Entities.Rental
{
    public class RentalRequest
    {
        public Guid Id { get; set; }

        public Guid RoomId { get; set; }

        public Guid TenantUserId { get; set; }

        public Guid? ApprovedByLandlordId { get; set; }

        public DateOnly DesiredStartDate { get; set; }

        public DateOnly ExpectedEndDate { get; set; }

        public int ExpectedOccupantCount { get; set; }

        public decimal MonthlyRentSnapshot { get; set; }

        public decimal DepositAmountSnapshot { get; set; }

        public string? TenantNote { get; set; }

        public RentalRequestStatus Status { get; set; }

        public DateTimeOffset? RespondedAt { get; set; }

        public string? RejectedReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public Room Room { get; set; } = null!;

        public User TenantUser { get; set; } = null!;

        public User? ApprovedByLandlord { get; set; }

        public RoomDeposit? RoomDeposit { get; set; }

        public RentalContract? RentalContract { get; set; }
    }
}
