using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/media")]
public class AdminMediaController : ControllerBase
{
    private readonly IPrivateStorageService _privateStorageService;

    public AdminMediaController(IPrivateStorageService privateStorageService)
    {
        _privateStorageService = privateStorageService;
    }

    [HttpGet("private")]
    public async Task<IActionResult> GetPrivateFile(
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
