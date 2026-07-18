using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaAccessResult
{
    public MediaAsset MediaAsset { get; init; } = null!;

    public Stream Stream { get; init; } = Stream.Null;

    public string ContentType { get; init; } = string.Empty;

    public string DownloadFileName { get; init; } = string.Empty;
}
