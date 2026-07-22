using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaPermissionService
{
    Task<bool> CanViewAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default);

    Task<bool> CanDownloadAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default);

    Task<bool> CanDeleteAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken = default);
}
