namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractOccupantDocumentResponse
{
    public Guid Id { get; set; }

    public Guid ContractOccupantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string? DocumentNumberMasked { get; set; }

    public Guid? FrontMediaAssetId { get; set; }

    public Guid? BackMediaAssetId { get; set; }

    public Guid? ExtraMediaAssetId { get; set; }

    public string FrontImageUrl { get; set; } = string.Empty;

    public string? BackImageUrl { get; set; }

    public string? ExtraImageUrl { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
}
