using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IFileStorageService fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        this.fileStorageService = fileStorageService;
    }

    [Authorize]
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ApiResponse<FileUploadResponse>>> UploadImage(
        [FromForm] UploadImageRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateImage(request.File);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var result = await fileStorageService.UploadImageAsync(
            new ImageUploadFile
            {
                Content = request.File.OpenReadStream(),
                FileName = request.File.FileName,
                ContentType = request.File.ContentType,
                Length = request.File.Length
            },
            request.Scope,
            cancellationToken);

        return Ok(new ApiResponse<FileUploadResponse>
        {
            Success = true,
            Message = "Image uploaded.",
            Data = result
        });
    }

    private static ApiErrorResponse? ValidateImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationError("Image file is required.", new { field = "file" });
        }

        if (file.Length > MaxImageSizeBytes)
        {
            return ValidationError("Image file must be 5MB or smaller.", new { field = "file" });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return ValidationError("Only JPG, PNG and WEBP images are allowed.", new { field = "file" });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return ValidationError("Unsupported image content type.", new { field = "file" });
        }

        return null;
    }

    private static ApiErrorResponse ValidationError(string message, object details)
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = ErrorCodes.ValidationError,
            Message = message,
            Details = details
        };
    }
}

public class UploadImageRequest
{
    public IFormFile File { get; set; } = default!;

    public FileUploadScope Scope { get; set; }
}
