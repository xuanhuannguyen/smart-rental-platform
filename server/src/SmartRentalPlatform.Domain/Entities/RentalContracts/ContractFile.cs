using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractFile
    {
        public Guid Id { get; set; }

        public Guid RentalContractId { get; set; }

        public Guid? RentalContractAppendixId { get; set; }

        public Guid? MediaAssetId { get; set; }

        public ContractFileVariant FileVariant { get; set; } = ContractFileVariant.Raw;

        public DateTimeOffset CreatedAt { get; set; }

        public RentalContract RentalContract { get; set; } = null!;

        public ContractAppendix? RentalContractAppendix { get; set; }

        public MediaAsset? MediaAsset { get; set; }
    }
}
