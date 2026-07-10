using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;

namespace SmartRentalPlatform.Api.Controllers.Media;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
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
