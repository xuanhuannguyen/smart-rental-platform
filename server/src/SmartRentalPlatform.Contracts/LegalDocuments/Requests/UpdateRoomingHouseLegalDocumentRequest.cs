namespace SmartRentalPlatform.Contracts.LegalDocuments.Requests;

public class UpdateRoomingHouseLegalDocumentRequest
{
    public string DocumentType { get; set; } = string.Empty;
    public Guid? FrontMediaAssetId { get; set; }
    public Guid? BackMediaAssetId { get; set; }
    public Guid? ExtraMediaAssetId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
}
