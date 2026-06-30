using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractOccupant
    {
        public Guid Id { get; set; }

        public Guid RentalContractId { get; set; }

        public Guid? UserId { get; set; }

        public Guid? GuardianOccupantId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public DateOnly DateOfBirth { get; set; }

        public string? RelationshipToMainTenant { get; set; }

        public DateOnly MoveInDate { get; set; }

        public DateOnly? MoveOutDate { get; set; }

        public ContractOccupantStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public RentalContract RentalContract { get; set; } = null!;

        public User? User { get; set; }

        public ContractOccupant? GuardianOccupant { get; set; }

        public ICollection<ContractOccupant> Dependents { get; set; } = new List<ContractOccupant>();

        public ICollection<ContractOccupantDocument> Documents { get; set; } = new List<ContractOccupantDocument>();
    }
}
