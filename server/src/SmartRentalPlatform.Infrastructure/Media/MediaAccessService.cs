using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Media;

public class MediaAccessService : IMediaAccessService
{
    private readonly IAppDbContext _dbContext;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly IMediaPermissionService _mediaPermissionService;

    public MediaAccessService(
        IAppDbContext dbContext,
        IMediaStorageService mediaStorageService,
        IMediaPermissionService mediaPermissionService)
    {
        _dbContext = dbContext;
        _mediaStorageService = mediaStorageService;
        _mediaPermissionService = mediaPermissionService;
    }

    public async Task<MediaAccessResult> OpenReadAsync(
        Guid mediaAssetId,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null)
    {
        var mediaAsset = await GetMediaAssetAsync(mediaAssetId, cancellationToken);
        try
        {
            await EnsureCanAccessAsync(actorUserId, mediaAsset, isDownload: false, cancellationToken);
        }
        catch (ForbiddenException)
        {
            await WriteAuditLogAsync(
                mediaAsset.Id,
                actorUserId,
                string.IsNullOrWhiteSpace(auditContext?.Action) ? "ViewDenied" : $"{auditContext.Action}Denied",
                auditContext,
                cancellationToken);
            throw;
        }

        var stream = await _mediaStorageService.OpenReadAsync(mediaAsset.ObjectKey, cancellationToken);
        await WriteAuditLogAsync(
            mediaAsset.Id,
            actorUserId,
            string.IsNullOrWhiteSpace(auditContext?.Action) ? "View" : auditContext.Action,
            auditContext,
            cancellationToken);

        return new MediaAccessResult
        {
            MediaAsset = mediaAsset,
            Stream = stream,
            ContentType = mediaAsset.ContentType,
            DownloadFileName = mediaAsset.OriginalFileName
        };
    }

    public async Task<string> GetDownloadUrlAsync(
        Guid mediaAssetId,
        TimeSpan ttl,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null)
    {
        var mediaAsset = await GetMediaAssetAsync(mediaAssetId, cancellationToken);
        try
        {
            await EnsureCanAccessAsync(actorUserId, mediaAsset, isDownload: true, cancellationToken);
        }
        catch (ForbiddenException)
        {
            await WriteAuditLogAsync(
                mediaAsset.Id,
                actorUserId,
                string.IsNullOrWhiteSpace(auditContext?.Action) ? "GenerateDownloadUrlDenied" : $"{auditContext.Action}Denied",
                auditContext,
                cancellationToken);
            throw;
        }

        var url = await _mediaStorageService.GetDownloadUrlAsync(mediaAsset.ObjectKey, ttl, cancellationToken);
        await WriteAuditLogAsync(
            mediaAsset.Id,
            actorUserId,
            string.IsNullOrWhiteSpace(auditContext?.Action) ? "GenerateDownloadUrl" : auditContext.Action,
            auditContext,
            cancellationToken);
        return url;
    }

    private async Task<MediaAsset> GetMediaAssetAsync(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        var mediaAsset = await _dbContext.MediaAssets.FindAsync([mediaAssetId], cancellationToken);
        if (mediaAsset is null)
        {
            throw new KeyNotFoundException($"Media asset '{mediaAssetId}' was not found.");
        }

        return mediaAsset;
    }

    private async Task EnsureCanAccessAsync(
        Guid? actorUserId,
        MediaAsset mediaAsset,
        bool isDownload,
        CancellationToken cancellationToken)
    {
        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Media asset đã bị xóa mềm.");
        }

        if (mediaAsset.Status == MediaStatus.PendingUpload)
        {
            throw new ForbiddenException(ErrorCodes.InvalidStatus, "Media asset chưa hoàn tất upload.");
        }

        if (mediaAsset.Visibility == MediaVisibility.Public)
        {
            return;
        }

        var allowed = isDownload
            ? await _mediaPermissionService.CanDownloadAsync(actorUserId, mediaAsset, cancellationToken)
            : await _mediaPermissionService.CanViewAsync(actorUserId, mediaAsset, cancellationToken);

        if (!allowed)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền truy cập media asset này.");
        }
    }

    private async Task WriteAuditLogAsync(
        Guid mediaAssetId,
        Guid? actorUserId,
        string action,
        MediaAuditContext? auditContext,
        CancellationToken cancellationToken)
    {
        _dbContext.MediaAuditLogs.Add(new MediaAuditLog
        {
            Id = Guid.NewGuid(),
            MediaAssetId = mediaAssetId,
            ActorUserId = actorUserId,
            Action = action,
            IpAddress = auditContext?.IpAddress,
            UserAgent = auditContext?.UserAgent,
            Reason = auditContext?.Reason,
            MetadataJson = auditContext?.MetadataJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
