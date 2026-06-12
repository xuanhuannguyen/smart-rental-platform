namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class ContractOccupantDocumentRequest
{
    public string DocumentType { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public string FrontImageObjectKey { get; set; } = string.Empty;

    public string? BackImageObjectKey { get; set; }

    public string? ExtraImageObjectKey { get; set; }
}
