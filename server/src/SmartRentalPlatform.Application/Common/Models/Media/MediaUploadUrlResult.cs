namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaUploadUrlResult
{
    public string Url { get; init; } = string.Empty;

    public string HttpMethod { get; init; } = "PUT";

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
