namespace SmartRentalPlatform.Domain.Entities.Media;

public class MediaAuditLog
{
    public Guid Id { get; set; }

    public Guid MediaAssetId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Reason { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public MediaAsset MediaAsset { get; set; } = null!;
}
