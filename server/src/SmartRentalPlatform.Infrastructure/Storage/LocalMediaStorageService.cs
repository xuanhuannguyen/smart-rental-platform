using Microsoft.AspNetCore.Hosting;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;

namespace SmartRentalPlatform.Infrastructure.Storage;

public class LocalMediaStorageService : IMediaStorageService
{
    private const string LocalBucketName = "local-media";

    private readonly string _uploadsRootPath;

    public LocalMediaStorageService(IWebHostEnvironment environment)
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        _uploadsRootPath = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(_uploadsRootPath);
    }

    public async Task<MediaStoredObjectResult> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(request.ObjectKey);
        var fullPath = GetFullPath(normalizedObjectKey);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = File.Create(fullPath);
        await request.Content.CopyToAsync(output, cancellationToken);

        return new MediaStoredObjectResult
        {
            BucketName = LocalBucketName,
            ObjectKey = normalizedObjectKey,
            PublicUrl = normalizedObjectKey.StartsWith("public/", StringComparison.OrdinalIgnoreCase)
                ? $"/uploads/{normalizedObjectKey}"
                : null,
            StoredFileName = Path.GetFileName(normalizedObjectKey)
        };
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        var fullPath = GetFullPath(normalizedObjectKey);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Storage object not found.", normalizedObjectKey);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        return Task.FromResult(File.Exists(GetFullPath(normalizedObjectKey)));
    }

    public Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        var fullPath = GetFullPath(normalizedObjectKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<string> GetDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        if (normalizedObjectKey.StartsWith("private/", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Private media download URLs are not supported by local media storage.");
        }

        return Task.FromResult($"/uploads/{normalizedObjectKey}");
    }

    private string GetFullPath(string normalizedObjectKey)
    {
        return Path.Combine(_uploadsRootPath, normalizedObjectKey.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').Trim().TrimStart('/');
    }
}
