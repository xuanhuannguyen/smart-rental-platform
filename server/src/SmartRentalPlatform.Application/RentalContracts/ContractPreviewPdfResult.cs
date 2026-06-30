namespace SmartRentalPlatform.Application.RentalContracts;

public sealed record ContractPreviewPdfResult(
    byte[] Content,
    string ContentType,
    string FileName);
