using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractAppendixChange
    {
        public Guid Id { get; set; }

        public Guid RentalContractAppendixId { get; set; }

        public ContractAppendixChangeType ChangeType { get; set; }

        public ContractAppendixTargetType TargetType { get; set; }

        public Guid? TargetId { get; set; }

        public string? FieldName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public int SortOrder { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public ContractAppendix RentalContractAppendix { get; set; } = null!;
    }
}
