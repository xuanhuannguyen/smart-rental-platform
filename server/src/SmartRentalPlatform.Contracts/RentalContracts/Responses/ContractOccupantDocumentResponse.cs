namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractOccupantDocumentResponse
{
    public Guid Id { get; set; }

    public Guid ContractOccupantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string? DocumentNumberMasked { get; set; }

    public string FrontImageObjectKey { get; set; } = string.Empty;

    public string? BackImageObjectKey { get; set; }

    public string? ExtraImageObjectKey { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
}
