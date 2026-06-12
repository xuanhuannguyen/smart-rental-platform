using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractFile
    {
        public Guid Id { get; set; }

        public Guid RentalContractId { get; set; }

        public Guid? RentalContractAppendixId { get; set; }

        public string StorageObjectKey { get; set; } = string.Empty;

        public ContractFileVariant FileVariant { get; set; } = ContractFileVariant.Raw;

        public string? FileUrl { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public RentalContract RentalContract { get; set; } = null!;

        public ContractAppendix? RentalContractAppendix { get; set; }
    }
}
