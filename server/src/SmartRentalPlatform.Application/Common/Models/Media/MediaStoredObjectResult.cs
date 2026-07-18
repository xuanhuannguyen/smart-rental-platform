namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaStoredObjectResult
{
    public string ObjectKey { get; init; } = string.Empty;

    public string? PublicUrl { get; init; }

    public string BucketName { get; init; } = string.Empty;

    public string StoredFileName { get; init; } = string.Empty;
}
