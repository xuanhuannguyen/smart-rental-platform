using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Contracts.Media.Responses;

public sealed record MediaAssetActionResponse(
    Guid MediaAssetId,
    MediaStatus Status,
    string? ViewUrl,
    string? DownloadUrl);
