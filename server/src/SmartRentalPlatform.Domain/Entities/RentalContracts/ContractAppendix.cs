using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractAppendix
    {
        public Guid Id { get; set; }

        public Guid RentalContractId { get; set; }

        public string AppendixNumber { get; set; } = string.Empty;

        public DateOnly EffectiveDate { get; set; }

        public ContractAppendixStatus Status { get; set; }

        public Guid CreatedByUserId { get; set; }

        public DateTimeOffset? ActivatedAt { get; set; }

        public string? StatusReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public RentalContract RentalContract { get; set; } = null!;

        public User CreatedByUser { get; set; } = null!;

        public ICollection<ContractAppendixChange> Changes { get; set; } = new List<ContractAppendixChange>();

        public ICollection<ContractFile> Files { get; set; } = new List<ContractFile>();

        public ICollection<ContractSignature> Signatures { get; set; } = new List<ContractSignature>();
    }
}
