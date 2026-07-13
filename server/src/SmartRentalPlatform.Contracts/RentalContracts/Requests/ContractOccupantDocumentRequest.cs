namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class ContractOccupantDocumentRequest
{
    public string DocumentType { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public Guid? FrontMediaAssetId { get; set; }

    public string FrontImageObjectKey { get; set; } = string.Empty;

    public Guid? BackMediaAssetId { get; set; }

    public string? BackImageObjectKey { get; set; }

    public Guid? ExtraMediaAssetId { get; set; }

    public string? ExtraImageObjectKey { get; set; }
}
