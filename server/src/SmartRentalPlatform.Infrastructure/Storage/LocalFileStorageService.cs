using Microsoft.AspNetCore.Hosting;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Files;

namespace SmartRentalPlatform.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private static readonly Dictionary<FileUploadScope, string> FolderByScope = new()
    {
        [FileUploadScope.RoomingHouse] = "rooming-houses",
        [FileUploadScope.Room] = "rooms",
        [FileUploadScope.LegalDocument] = "legal-documents",
        [FileUploadScope.Avatar] = "avatars",
        [FileUploadScope.HouseRule] = "house-rules"
    };

    private readonly IWebHostEnvironment environment;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task<FileUploadResponse> UploadImageAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        var folder = FolderByScope[scope];
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
        var objectKey = $"{folder}/{fileName}";

        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var uploadDirectory = Path.Combine(webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadDirectory);

        var filePath = Path.Combine(uploadDirectory, fileName);
        await using var stream = File.Create(filePath);
        await file.Content.CopyToAsync(stream, cancellationToken);

        return new FileUploadResponse
        {
            ObjectKey = objectKey,
            Url = $"/uploads/{objectKey}"
        };
    }

    public Task<FileUploadResponse> UploadPdfAsync(
        ImageUploadFile file,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        return UploadFileAsync(file.Content, file.FileName, scope, cancellationToken);
    }

    public Task<FileUploadResponse> UploadPdfAsync(
        Stream content,
        string fileName,
        FileUploadScope scope,
        CancellationToken cancellationToken = default)
    {
        return UploadFileAsync(content, fileName, scope, cancellationToken);
    }

    private async Task<FileUploadResponse> UploadFileAsync(
        Stream content,
        string originalFileName,
        FileUploadScope scope,
        CancellationToken cancellationToken)
    {
        var folder = FolderByScope[scope];
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
        var objectKey = $"{folder}/{fileName}";

        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var uploadDirectory = Path.Combine(webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadDirectory);

        var filePath = Path.Combine(uploadDirectory, fileName);
        await using var stream = File.Create(filePath);
        await content.CopyToAsync(stream, cancellationToken);

        return new FileUploadResponse
        {
            ObjectKey = objectKey,
            Url = $"/uploads/{objectKey}"
        };
    }
}

