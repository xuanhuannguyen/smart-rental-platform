namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaUploadSessionResult
{
    public Guid MediaAssetId { get; init; }

    public string? UploadUrl { get; init; }

    public string HttpMethod { get; init; } = "PUT";

    public string DeliveryMode { get; init; } = "backend-proxy";

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
