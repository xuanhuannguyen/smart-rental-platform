using SmartRentalPlatform.Application.Common.Models.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaWorkflowService
{
    Task<MediaUploadSessionResult> CreateUploadSessionAsync(
        CreateMediaUploadSessionRequest request,
        Guid actorUserId,
        bool isAdmin,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default);

    Task<MediaFinalizeUploadResult> FinalizeUploadAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        bool isAdmin,
        string? fileHash = null,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default);

    Task<MediaFinalizeUploadResult> SoftDeleteAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        bool isAdmin,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default);
}
