namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaObjectMetadataResult
{
    public string ObjectKey { get; init; } = string.Empty;

    public string? ContentType { get; init; }

    public long FileSize { get; init; }
}
