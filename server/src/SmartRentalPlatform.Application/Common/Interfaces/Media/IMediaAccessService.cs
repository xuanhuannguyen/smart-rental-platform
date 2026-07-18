using SmartRentalPlatform.Application.Common.Models.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaAccessService
{
    Task<MediaAccessResult> OpenReadAsync(
        Guid mediaAssetId,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null);

    Task<string> GetDownloadUrlAsync(
        Guid mediaAssetId,
        TimeSpan ttl,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null);
}
