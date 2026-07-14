namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class ContractOccupantDocumentRequest
{
    public string DocumentType { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public Guid? FrontMediaAssetId { get; set; }

    public Guid? BackMediaAssetId { get; set; }

    public Guid? ExtraMediaAssetId { get; set; }
}
