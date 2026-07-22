using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class CreateMediaAssetRequest
{
    public Guid? OwnerUserId { get; init; }

    public string BucketName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public string OriginalFileName { get; init; } = string.Empty;

    public string StoredFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string? FileHash { get; init; }

    public MediaScope Scope { get; init; }

    public MediaVisibility Visibility { get; init; }

    public MediaStatus Status { get; init; }

    public string? LinkedEntityType { get; init; }

    public Guid? LinkedEntityId { get; init; }
}
