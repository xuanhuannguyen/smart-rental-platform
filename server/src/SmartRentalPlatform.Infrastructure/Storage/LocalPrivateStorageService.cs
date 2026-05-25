using Microsoft.Extensions.Configuration;
using SmartRentalPlatform.Application.Abstractions;

namespace SmartRentalPlatform.Infrastructure.Storage;

public class LocalPrivateStorageService : IPrivateStorageService
{
    private readonly string _rootPath;

    public LocalPrivateStorageService(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:PrivateRootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "private-storage");
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
            throw new FileNotFoundException("Private storage object not found.", normalizedKey);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }
}
