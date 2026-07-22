using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Media;

public class MediaWorkflowService : IMediaWorkflowService
{
    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromMinutes(15);

    private readonly IAppDbContext _dbContext;
    private readonly IMediaAssetService _mediaAssetService;
    private readonly IMediaObjectKeyFactory _mediaObjectKeyFactory;
    private readonly IMediaPermissionService _mediaPermissionService;
    private readonly IMediaStorageService _mediaStorageService;

    public MediaWorkflowService(
        IAppDbContext dbContext,
        IMediaAssetService mediaAssetService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        IMediaPermissionService mediaPermissionService,
        IMediaStorageService mediaStorageService)
    {
        _dbContext = dbContext;
        _mediaAssetService = mediaAssetService;
        _mediaObjectKeyFactory = mediaObjectKeyFactory;
        _mediaPermissionService = mediaPermissionService;
        _mediaStorageService = mediaStorageService;
    }

    public async Task<MediaUploadSessionResult> CreateUploadSessionAsync(
        CreateMediaUploadSessionRequest request,
        Guid actorUserId,
        bool isAdmin,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateUploadSessionRequest(request);
        MediaFileValidationPolicy.ValidateDeclaredUpload(
            request.Scope,
            request.OriginalFileName,
            request.ContentType,
            request.FileSize);

        var objectKey = _mediaObjectKeyFactory.Create(
            request.Scope,
            request.Visibility,
            request.OriginalFileName);

        var mediaAsset = await _mediaAssetService.CreateAsync(
            new CreateMediaAssetRequest
            {
                OwnerUserId = actorUserId,
                BucketName = _mediaStorageService.GetBucketName(),
                ObjectKey = objectKey.ObjectKey,
                OriginalFileName = request.OriginalFileName.Trim(),
                StoredFileName = objectKey.StoredFileName,
                ContentType = request.ContentType.Trim(),
                FileSize = request.FileSize,
                Scope = request.Scope,
                Visibility = request.Visibility,
                Status = MediaStatus.PendingUpload
            },
            cancellationToken);

        try
        {
            var uploadUrl = await _mediaStorageService.GetUploadUrlAsync(
                mediaAsset.ObjectKey,
                mediaAsset.ContentType,
                UploadUrlTtl,
                cancellationToken);

            await WriteAuditLogAsync(
                mediaAsset.Id,
                actorUserId,
                "CreateUploadSession",
                auditContext,
                cancellationToken);

            return new MediaUploadSessionResult
            {
                MediaAssetId = mediaAsset.Id,
                UploadUrl = uploadUrl.Url,
                HttpMethod = uploadUrl.HttpMethod,
                DeliveryMode = "signed-upload-url",
                ExpiresAtUtc = uploadUrl.ExpiresAtUtc
            };
        }
        catch (NotSupportedException)
        {
            await WriteAuditLogAsync(
                mediaAsset.Id,
                actorUserId,
                "CreateUploadSession",
                auditContext,
                cancellationToken);

            return new MediaUploadSessionResult
            {
                MediaAssetId = mediaAsset.Id,
                HttpMethod = "PUT",
                DeliveryMode = "backend-proxy",
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(UploadUrlTtl)
            };
        }
    }

    public async Task<MediaFinalizeUploadResult> FinalizeUploadAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        bool isAdmin,
        string? fileHash = null,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        var mediaAsset = await GetManagedMediaAssetAsync(mediaAssetId, actorUserId, isAdmin, cancellationToken);

        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            throw new BadRequestException(ErrorCodes.InvalidStatus, "Tệp này đã bị xóa mềm và không thể finalize.");
        }

        var exists = await _mediaStorageService.ExistsAsync(mediaAsset.ObjectKey, cancellationToken);
        if (!exists)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Không tìm thấy object đã upload trong storage. Hãy upload nội dung trước khi finalize.");
        }

        var storedMetadata = await _mediaStorageService.GetObjectMetadataAsync(mediaAsset.ObjectKey, cancellationToken);
        MediaFileValidationPolicy.ValidateStoredObject(
            mediaAsset.Scope,
            mediaAsset.OriginalFileName,
            mediaAsset.ContentType,
            mediaAsset.FileSize,
            storedMetadata);

        mediaAsset.FileHash = string.IsNullOrWhiteSpace(fileHash) ? mediaAsset.FileHash : fileHash.Trim();
        mediaAsset.Status = mediaAsset.LinkedEntityId.HasValue ? MediaStatus.Linked : MediaStatus.Uploaded;
        mediaAsset.UpdatedAt = DateTimeOffset.UtcNow;

        await WriteAuditLogAsync(
            mediaAsset.Id,
            actorUserId,
            "FinalizeUpload",
            auditContext,
            cancellationToken,
            saveChanges: false);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildActionResult(mediaAsset);
    }

    public async Task<MediaFinalizeUploadResult> SoftDeleteAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        bool isAdmin,
        MediaAuditContext? auditContext = null,
        CancellationToken cancellationToken = default)
    {
        var mediaAsset = await _dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy media asset.");

        var canDelete = isAdmin || await _mediaPermissionService.CanDeleteAsync(actorUserId, mediaAsset, cancellationToken);
        if (!canDelete)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền xóa media asset này.");
        }

        if (mediaAsset.Status != MediaStatus.Deleted)
        {
            var now = DateTimeOffset.UtcNow;
            mediaAsset.Status = MediaStatus.Deleted;
            mediaAsset.DeletedAt = now;
            mediaAsset.UpdatedAt = now;
        }

        await WriteAuditLogAsync(
            mediaAsset.Id,
            actorUserId,
            "Delete",
            auditContext,
            cancellationToken,
            saveChanges: false);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildActionResult(mediaAsset);
    }

    private async Task<MediaAsset> GetManagedMediaAssetAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await _dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy media asset.");

        if (isAdmin)
        {
            return mediaAsset;
        }

        if (!mediaAsset.OwnerUserId.HasValue || mediaAsset.OwnerUserId.Value != actorUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền thao tác media asset này.");
        }

        return mediaAsset;
    }

    private async Task WriteAuditLogAsync(
        Guid mediaAssetId,
        Guid? actorUserId,
        string action,
        MediaAuditContext? auditContext,
        CancellationToken cancellationToken,
        bool saveChanges = true)
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

        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static MediaFinalizeUploadResult BuildActionResult(MediaAsset mediaAsset)
    {
        var viewUrl = mediaAsset.Visibility == MediaVisibility.Public
            ? PublicMediaPathBuilder.Build(mediaAsset.Id)
            : PrivateMediaPathBuilder.Build(mediaAsset.Id);
        var downloadUrl = mediaAsset.Visibility == MediaVisibility.Public
            ? PublicMediaPathBuilder.Build(mediaAsset.Id)
            : PrivateMediaPathBuilder.Build(mediaAsset.Id, forceDownload: true);

        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            viewUrl = null;
            downloadUrl = null;
        }

        return new MediaFinalizeUploadResult
        {
            MediaAssetId = mediaAsset.Id,
            Status = mediaAsset.Status,
            ViewUrl = viewUrl,
            DownloadUrl = downloadUrl
        };
    }

    private static void ValidateCreateUploadSessionRequest(CreateMediaUploadSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Tên file gốc là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Content-Type là bắt buộc.");
        }

        if (request.FileSize <= 0)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Kích thước file phải lớn hơn 0.");
        }
    }
}
