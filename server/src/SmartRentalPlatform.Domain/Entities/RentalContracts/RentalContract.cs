using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class RentalContract
    {
        public Guid Id { get; set; }

        public Guid RentalRequestId { get; set; }

        public Guid RoomDepositId { get; set; }

        public Guid RoomId { get; set; }

        public Guid MainTenantUserId { get; set; }

        public string ContractNumber { get; set; } = string.Empty;

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        public decimal MonthlyRent { get; set; }

        public decimal DepositAmount { get; set; }

        public int PaymentDay { get; set; }

        public RentalContractStatus Status { get; set; }

        public string? RoomSnapshot { get; set; }

        public DateTimeOffset? SignatureDeadlineAt { get; set; }

        public DateTimeOffset? ActivatedAt { get; set; }

        public string? StatusReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public RentalRequest RentalRequest { get; set; } = null!;

        public RoomDeposit RoomDeposit { get; set; } = null!;

        public Room Room { get; set; } = null!;

        public User MainTenantUser { get; set; } = null!;

        public ICollection<ContractOccupant> Occupants { get; set; } = new List<ContractOccupant>();

        public ICollection<ContractAppendix> Appendices { get; set; } = new List<ContractAppendix>();

        public ICollection<ContractFile> Files { get; set; } = new List<ContractFile>();

        public ICollection<ContractSignature> Signatures { get; set; } = new List<ContractSignature>();
    }
}
