using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Storage;

namespace SmartRentalPlatform.UnitTests.Media;

public sealed class LocalMediaStorageServiceTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly LocalMediaStorageService _service;

    public LocalMediaStorageServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "smart-rental-media-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
        _service = new LocalMediaStorageService(new FakeWebHostEnvironment(_rootDirectory));
    }

    [Fact]
    public async Task UploadAsync_ThenOpenReadAndDelete_ShouldRoundTripContent()
    {
        var contentBytes = Encoding.UTF8.GetBytes("hello media");
        await using var input = new MemoryStream(contentBytes);

        var upload = await _service.UploadAsync(new MediaUploadRequest
        {
            Content = input,
            OriginalFileName = "hello.txt",
            ContentType = "text/plain",
            FileSize = contentBytes.Length,
            ObjectKey = "private/test/hello.txt",
            Visibility = MediaVisibility.Private
        });

        Assert.Equal("local-media", upload.BucketName);
        Assert.Null(upload.PublicUrl);
        Assert.True(await _service.ExistsAsync(upload.ObjectKey));

        string text;
        await using (var output = await _service.OpenReadAsync(upload.ObjectKey))
        using (var reader = new StreamReader(output, Encoding.UTF8))
        {
            text = await reader.ReadToEndAsync();
        }

        Assert.Equal("hello media", text);

        await _service.DeleteAsync(upload.ObjectKey);
        Assert.False(await _service.ExistsAsync(upload.ObjectKey));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ForPublicObject_ShouldReturnUploadsPath()
    {
        var url = await _service.GetDownloadUrlAsync("public/room-images/2026/07/09/file.jpg", TimeSpan.FromMinutes(5));
        Assert.Equal("/uploads/public/room-images/2026/07/09/file.jpg", url);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string rootDirectory)
        {
            ContentRootPath = rootDirectory;
            WebRootPath = Path.Combine(rootDirectory, "wwwroot");
            Directory.CreateDirectory(WebRootPath);
            ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
            WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        }

        public string ApplicationName { get; set; } = "Tests";

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
