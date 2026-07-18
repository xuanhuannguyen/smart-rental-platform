using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Common;
using MediaRequestContracts = SmartRentalPlatform.Contracts.Media.Requests;
using SmartRentalPlatform.Contracts.Media.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Media;

namespace SmartRentalPlatform.Api.Controllers.Media;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private static readonly TimeSpan PrivateDownloadUrlTtl = TimeSpan.FromMinutes(5);

    private readonly ICurrentUserService _currentUserService;
    private readonly IAppDbContext _dbContext;
    private readonly IMediaAccessService _mediaAccessService;
    private readonly IMediaAssetService _mediaAssetService;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly IMediaWorkflowService _mediaWorkflowService;

    public MediaController(
        ICurrentUserService currentUserService,
        IMediaAccessService mediaAccessService,
        IMediaAssetService mediaAssetService,
        IMediaStorageService mediaStorageService,
        IMediaWorkflowService mediaWorkflowService,
        IAppDbContext dbContext)
    {
        _currentUserService = currentUserService;
        _mediaAccessService = mediaAccessService;
        _mediaAssetService = mediaAssetService;
        _mediaStorageService = mediaStorageService;
        _mediaWorkflowService = mediaWorkflowService;
        _dbContext = dbContext;
    }

    [HttpGet("public/{mediaAssetId:guid}")]
    public async Task<IActionResult> GetPublicAsset(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await _mediaAssetService.GetByIdAsync(mediaAssetId, cancellationToken);
        if (mediaAsset is null ||
            mediaAsset.Visibility != MediaVisibility.Public ||
            mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            return NotFound();
        }

        var stream = await _mediaStorageService.OpenReadAsync(mediaAsset.ObjectKey, cancellationToken);
        return File(stream, mediaAsset.ContentType, enableRangeProcessing: true);
    }

    [Authorize]
    [HttpGet("private/{mediaAssetId:guid}")]
    public async Task<IActionResult> GetPrivateObject(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để xem tệp riêng tư.");
        var result = await _mediaAccessService.OpenReadAsync(
            mediaAssetId,
            actorUserId,
            cancellationToken,
            new MediaAuditContext
            {
                Action = "View",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            });

        return File(result.Stream, result.ContentType, enableRangeProcessing: true);
    }

    [Authorize]
    [HttpGet("private/{mediaAssetId:guid}/download")]
    public async Task<IActionResult> DownloadPrivateObject(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để tải tệp riêng tư.");
        var result = await _mediaAccessService.OpenReadAsync(
            mediaAssetId,
            actorUserId,
            cancellationToken,
            BuildAuditContext("Download", "attachment"));

        return File(result.Stream, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }

    [Authorize]
    [HttpPost("upload-url")]
    public async Task<ActionResult<ApiResponse<MediaUploadSessionResponse>>> CreateUploadSession(
        [FromBody] MediaRequestContracts.CreateMediaUploadSessionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để tạo phiên upload.");
        var result = await _mediaWorkflowService.CreateUploadSessionAsync(
            new Application.Common.Models.Media.CreateMediaUploadSessionRequest
            {
                OriginalFileName = request.OriginalFileName,
                ContentType = request.ContentType,
                FileSize = request.FileSize,
                Scope = request.Scope,
                Visibility = request.Visibility
            },
            actorUserId,
            IsAdmin(),
            BuildAuditContext("CreateUploadSession", "upload-session"),
            cancellationToken);

        var uploadUrl = string.IsNullOrWhiteSpace(result.UploadUrl)
            ? Url.ActionLink(nameof(UploadPendingObject), values: new { mediaAssetId = result.MediaAssetId }) ?? $"/api/media/upload/{result.MediaAssetId}"
            : result.UploadUrl;

        return Ok(new ApiResponse<MediaUploadSessionResponse>
        {
            Success = true,
            Message = "Tạo upload session thành công.",
            Data = new MediaUploadSessionResponse(
                result.MediaAssetId,
                uploadUrl,
                result.HttpMethod,
                result.DeliveryMode,
                result.ExpiresAtUtc)
        });
    }

    [Authorize]
    [HttpPut("upload/{mediaAssetId:guid}")]
    public async Task<IActionResult> UploadPendingObject(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để upload tệp.");
        var mediaAsset = await EnsureCanManageMediaAssetAsync(mediaAssetId, actorUserId, cancellationToken);

        if (mediaAsset.Status == MediaStatus.Deleted)
        {
            return BadRequest("Media asset đã bị xóa mềm.");
        }

        MediaFileValidationPolicy.ValidateProxyUpload(
            mediaAsset.Scope,
            mediaAsset.OriginalFileName,
            mediaAsset.ContentType,
            mediaAsset.FileSize,
            Request.ContentType,
            Request.ContentLength);

        var uploadRequest = new MediaUploadRequest
        {
            Content = Request.Body,
            OriginalFileName = mediaAsset.OriginalFileName,
            ContentType = string.IsNullOrWhiteSpace(Request.ContentType) ? mediaAsset.ContentType : Request.ContentType,
            FileSize = Request.ContentLength ?? mediaAsset.FileSize,
            ObjectKey = mediaAsset.ObjectKey,
            Visibility = mediaAsset.Visibility
        };

        await _mediaStorageService.UploadAsync(uploadRequest, cancellationToken);
        await WriteAuditLogAsync(
            mediaAsset.Id,
            actorUserId,
            "UploadBinary",
            BuildAuditContext("UploadBinary", "upload-binary"),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("finalize")]
    public async Task<ActionResult<ApiResponse<MediaAssetActionResponse>>> FinalizeUpload(
        [FromBody] MediaRequestContracts.FinalizeMediaUploadRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để finalize upload.");
        var result = await _mediaWorkflowService.FinalizeUploadAsync(
            request.MediaAssetId,
            actorUserId,
            IsAdmin(),
            request.FileHash,
            BuildAuditContext("FinalizeUpload", "finalize"),
            cancellationToken);

        return Ok(new ApiResponse<MediaAssetActionResponse>
        {
            Success = true,
            Message = "Finalize upload thành công.",
            Data = new MediaAssetActionResponse(
                result.MediaAssetId,
                result.Status,
                result.ViewUrl,
                result.DownloadUrl)
        });
    }

    [Authorize]
    [HttpGet("{mediaAssetId:guid}/download-url")]
    [HttpGet("private/{mediaAssetId:guid}/download-url")]
    public async Task<ActionResult<PrivateMediaDownloadUrlResponse>> GetPrivateDownloadUrl(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để tải tệp riêng tư.");
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(PrivateDownloadUrlTtl);

        try
        {
            var url = await _mediaAccessService.GetDownloadUrlAsync(
                mediaAssetId,
                PrivateDownloadUrlTtl,
                actorUserId,
                cancellationToken,
                BuildAuditContext("GenerateDownloadUrl", "attachment"));

            return Ok(new PrivateMediaDownloadUrlResponse(url, expiresAtUtc, "signed-url"));
        }
        catch (NotSupportedException)
        {
            var fallbackUrl = PrivateMediaPathBuilder.Build(mediaAssetId, forceDownload: true);
            return Ok(new PrivateMediaDownloadUrlResponse(fallbackUrl, expiresAtUtc, "backend-route"));
        }
    }

    [Authorize]
    [HttpDelete("{mediaAssetId:guid}")]
    public async Task<ActionResult<ApiResponse<MediaAssetActionResponse>>> SoftDeleteMediaAsset(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập để xóa tệp.");
        var result = await _mediaWorkflowService.SoftDeleteAsync(
            mediaAssetId,
            actorUserId,
            IsAdmin(),
            BuildAuditContext("Delete", "soft-delete"),
            cancellationToken);

        return Ok(new ApiResponse<MediaAssetActionResponse>
        {
            Success = true,
            Message = "Xóa mềm media asset thành công.",
            Data = new MediaAssetActionResponse(
                result.MediaAssetId,
                result.Status,
                result.ViewUrl,
                result.DownloadUrl)
        });
    }

    private MediaAuditContext BuildAuditContext(string action, string disposition)
    {
        return new MediaAuditContext
        {
            Action = action,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                disposition,
                path = HttpContext.Request.Path.Value
            })
        };
    }

    private async Task<MediaAsset> EnsureCanManageMediaAssetAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await _mediaAssetService.GetByIdAsync(mediaAssetId, cancellationToken);
        if (mediaAsset is null)
        {
            throw new KeyNotFoundException($"Media asset '{mediaAssetId}' was not found.");
        }

        if (IsAdmin())
        {
            return mediaAsset;
        }

        if (!mediaAsset.OwnerUserId.HasValue || mediaAsset.OwnerUserId.Value != actorUserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to manage this media asset.");
        }

        return mediaAsset;
    }

    private bool IsAdmin()
    {
        return _currentUserService.Roles.Any(x => string.Equals(x, "Admin", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteAuditLogAsync(
        Guid mediaAssetId,
        Guid actorUserId,
        string action,
        MediaAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        _dbContext.MediaAuditLogs.Add(new Domain.Entities.Media.MediaAuditLog
        {
            Id = Guid.NewGuid(),
            MediaAssetId = mediaAssetId,
            ActorUserId = actorUserId,
            Action = action,
            IpAddress = auditContext.IpAddress,
            UserAgent = auditContext.UserAgent,
            Reason = auditContext.Reason,
            MetadataJson = auditContext.MetadataJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

}
