using Microsoft.AspNetCore.Hosting;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Storage;

/// <summary>
/// Lưu trữ file private (KYC) cùng thư mục với public uploads (wwwroot/uploads/).
/// Tất cả file đều ở một chỗ, dễ quản lý backup.
/// KYC ảnh vẫn được bảo vệ vì chỉ serve qua admin endpoint có [Authorize].
/// </summary>
public class LocalPrivateStorageService : IPrivateStorageService
{
    private readonly string _rootPath;

    public LocalPrivateStorageService(IWebHostEnvironment environment)
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        _rootPath = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(
        Stream content,
        string contentType,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = objectKey.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.Combine(_rootPath, normalizedKey.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return normalizedKey;
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = objectKey.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.Combine(_rootPath, normalizedKey.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Storage object not found.", normalizedKey);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }
}
