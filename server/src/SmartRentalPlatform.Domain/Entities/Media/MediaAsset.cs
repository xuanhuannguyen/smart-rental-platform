using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Domain.Entities.Media;

public class MediaAsset
{
    public Guid Id { get; set; }

    public Guid? OwnerUserId { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string? FileHash { get; set; }

    public MediaScope Scope { get; set; }

    public MediaVisibility Visibility { get; set; } = MediaVisibility.Private;

    public MediaStatus Status { get; set; } = MediaStatus.PendingUpload;

    public string? LinkedEntityType { get; set; }

    public Guid? LinkedEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<MediaAuditLog> AuditLogs { get; set; } = new List<MediaAuditLog>();
}
