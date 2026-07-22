namespace SmartRentalPlatform.Application.RentalContracts;

public enum ContractPreviewAudience
{
    LandlordReview = 1,
    TenantReview = 2
}

public sealed record ContractReviewImage(
    string Label,
    byte[]? Content,
    string MissingMessage);

public sealed record ContractReviewAttachment(
    string PersonName,
    string PersonRole,
    string DocumentType,
    string? DocumentNumber,
    IReadOnlyList<ContractReviewImage> Images);
