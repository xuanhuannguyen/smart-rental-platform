using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractOccupantDocument
    {
        public Guid Id { get; set; }

        public Guid RentalContractOccupantId { get; set; }

        public string DocumentType { get; set; } = string.Empty;

        public string? DocumentNumberMasked { get; set; }

        public string? DocumentNumberHash { get; set; }

        public string? DocumentNumberEncrypted { get; set; }

        public Guid? FrontMediaAssetId { get; set; }

        public Guid? BackMediaAssetId { get; set; }

        public Guid? ExtraMediaAssetId { get; set; }

        public string FrontImageObjectKey { get; set; } = string.Empty;

        public string? BackImageObjectKey { get; set; }

        public string? ExtraImageObjectKey { get; set; }

        public DateTimeOffset UploadedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public ContractOccupant RentalContractOccupant { get; set; } = null!;

        public MediaAsset? FrontMediaAsset { get; set; }

        public MediaAsset? BackMediaAsset { get; set; }

        public MediaAsset? ExtraMediaAsset { get; set; }
    }
}
