using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces.Media;

namespace SmartRentalPlatform.Api.Controllers.Media;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IMediaStorageService _mediaStorageService;

    public MediaController(IMediaStorageService mediaStorageService)
    {
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
