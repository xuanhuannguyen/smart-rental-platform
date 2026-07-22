using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Media.Responses;
using System.Text.Json;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/media")]
public class AdminMediaController : ControllerBase
{
    private const string AdminViewAction = "View";
    private const string AdminDownloadAction = "Download";
    private static readonly TimeSpan PrivateDownloadUrlTtl = TimeSpan.FromMinutes(5);

    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaAccessService _mediaAccessService;

    public AdminMediaController(
        ICurrentUserService currentUserService,
        IMediaAccessService mediaAccessService)
    {
        _currentUserService = currentUserService;
        _mediaAccessService = mediaAccessService;
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

    [HttpGet("private/{mediaAssetId:guid}/download-url")]
    public async Task<ActionResult<PrivateMediaDownloadUrlResponse>> GetPrivateDownloadUrl(
        Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.GetRequiredUserId("Bạn cần đăng nhập bằng tài khoản admin để tải tệp riêng tư.");
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(PrivateDownloadUrlTtl);

        try
        {
            var url = await _mediaAccessService.GetDownloadUrlAsync(
                mediaAssetId,
                PrivateDownloadUrlTtl,
                adminId,
                cancellationToken,
                BuildAuditContext("GenerateDownloadUrl", "attachment"));

            return Ok(new PrivateMediaDownloadUrlResponse(url, expiresAtUtc, "signed-url"));
        }
        catch (NotSupportedException)
        {
            var fallbackUrl = AdminPrivateMediaPathBuilder.Build(mediaAssetId, forceDownload: true);
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
            MetadataJson = JsonSerializer.Serialize(new
            {
                disposition,
                path = HttpContext.Request.Path.Value
            })
        };
    }

}
