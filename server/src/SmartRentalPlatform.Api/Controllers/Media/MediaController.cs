using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Media.Responses;

namespace SmartRentalPlatform.Api.Controllers.Media;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private static readonly TimeSpan PrivateDownloadUrlTtl = TimeSpan.FromMinutes(5);

    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaAccessService _mediaAccessService;
    private readonly IMediaStorageService _mediaStorageService;

    public MediaController(
        ICurrentUserService currentUserService,
        IMediaAccessService mediaAccessService,
        IMediaStorageService mediaStorageService)
    {
        _currentUserService = currentUserService;
        _mediaAccessService = mediaAccessService;
        _mediaStorageService = mediaStorageService;
    }

    [HttpGet("public/{**objectKey}")]
    public async Task<IActionResult> GetPublicObject(
        string objectKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey) ||
            !objectKey.Replace('\\', '/').TrimStart('/').StartsWith("public/", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var stream = await _mediaStorageService.OpenReadAsync(objectKey, cancellationToken);
        var contentType = GuessContentType(objectKey);
        var fileName = Path.GetFileName(objectKey);

        return File(stream, contentType, fileName, enableRangeProcessing: true);
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

    private static string GuessContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
