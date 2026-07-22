using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaUploadRequest
{
    public Stream Content { get; init; } = Stream.Null;

    public string OriginalFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string ObjectKey { get; init; } = string.Empty;

    public MediaVisibility Visibility { get; init; }
}
