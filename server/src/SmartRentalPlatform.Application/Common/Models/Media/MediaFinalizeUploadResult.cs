using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaFinalizeUploadResult
{
    public Guid MediaAssetId { get; init; }

    public MediaStatus Status { get; init; }

    public string? ViewUrl { get; init; }

    public string? DownloadUrl { get; init; }
}
