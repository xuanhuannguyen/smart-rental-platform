namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractRenderOptions
{
    public ContractPreviewAudience? PreviewAudience { get; init; }

    public string ViewerMode { get; init; } = string.Empty;

    public bool ShowFullDocumentNumbers { get; init; }

    public IReadOnlyCollection<Guid>? VisibleOccupantIds { get; init; }

    public IReadOnlyDictionary<Guid, string?> UserDocumentNumbersByUserId { get; init; } =
        new Dictionary<Guid, string?>();

    public IReadOnlyDictionary<Guid, string?> OccupantDocumentNumbersByDocumentId { get; init; } =
        new Dictionary<Guid, string?>();

    public IReadOnlyList<ContractReviewAttachment> ReviewAttachments { get; init; } =
        Array.Empty<ContractReviewAttachment>();
}
