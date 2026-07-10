using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using System.Text.Json;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/media")]
public class AdminMediaController : ControllerBase
{
    private const string AdminViewAction = "View";
    private const string AdminDownloadAction = "Download";

    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaAccessService _mediaAccessService;
    private readonly IPrivateStorageService _privateStorageService;

    public AdminMediaController(
        ICurrentUserService currentUserService,
        IMediaAccessService mediaAccessService,
        IPrivateStorageService privateStorageService)
    {
        _currentUserService = currentUserService;
        _mediaAccessService = mediaAccessService;
        _privateStorageService = privateStorageService;
    }

    [HttpGet("private/{mediaAssetId:guid}")]
    public async Task<IActionResult> ViewPrivateFile(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập bằng tài khoản admin để xem tệp riêng tư.");
        var result = await _mediaAccessService.OpenReadAsync(
            mediaAssetId,
            adminId,
            cancellationToken,
            BuildAuditContext(AdminViewAction, "inline"));

        return File(result.Stream, result.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("private/{mediaAssetId:guid}/download")]
    public async Task<IActionResult> DownloadPrivateFile(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập bằng tài khoản admin để tải tệp riêng tư.");
        var result = await _mediaAccessService.OpenReadAsync(
            mediaAssetId,
            adminId,
            cancellationToken,
            BuildAuditContext(AdminDownloadAction, "attachment"));

        return File(result.Stream, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }

    [HttpGet("private")]
    public async Task<IActionResult> GetLegacyPrivateFile(
        [FromQuery] string objectKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(objectKey))
        {
            return BadRequest("Invalid object key.");
        }

        var stream = await _privateStorageService.OpenReadAsync(objectKey, cancellationToken);
        return File(stream, GuessContentType(objectKey));
    }

    private MediaAuditContext BuildAuditContext(string action, string disposition)
    {
        return new MediaAuditContext
        {
            Action = action,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            MetadataJson = JsonSerializer.Serialize(new
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
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
