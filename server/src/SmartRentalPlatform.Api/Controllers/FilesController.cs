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
            Message = "Tải ảnh lên thành công.",
            Data = result
        });
    }

    private static ApiErrorResponse? ValidateImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationError("Vui lòng chọn ảnh cần tải lên.", new { field = "file" });
        }

        if (file.Length > MaxImageSizeBytes)
        {
            return ValidationError("Dung lượng ảnh không được vượt quá 5MB.", new { field = "file" });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return ValidationError("Chỉ cho phép tải ảnh JPG, PNG hoặc WEBP.", new { field = "file" });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return ValidationError("Định dạng nội dung ảnh không được hỗ trợ.", new { field = "file" });
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
