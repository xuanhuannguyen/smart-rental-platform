using System.Text;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Media;
using SmartRentalPlatform.Infrastructure.Storage;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Media;

public class MediaBackedFileStorageServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MediaBackedFileStorageServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task UploadImageAsync_ShouldStorePublicMediaAndCreateMediaAsset()
    {
        var storage = new RecordingMediaStorageService();
        var service = new MediaBackedFileStorageService(
            new FixedMediaObjectKeyFactory("public/room-images/2026/07/09/room.jpg", "room.jpg"),
            storage,
            new MediaAssetService(_fixture.Context),
            new FakeCurrentUserService(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("room-image"));
        var response = await service.UploadImageAsync(
            new ImageUploadFile
            {
                Content = stream,
                FileName = "room.jpg",
                ContentType = "image/jpeg",
                Length = stream.Length
            },
            FileUploadScope.Room);

        Assert.Equal("public/room-images/2026/07/09/room.jpg", response.ObjectKey);
        Assert.Equal("/uploads/public/room-images/2026/07/09/room.jpg", response.Url);
        Assert.NotNull(storage.LastRequest);
        Assert.Equal(MediaVisibility.Public, storage.LastRequest!.Visibility);

        var asset = await new MediaAssetService(_fixture.Context).GetByObjectKeyAsync(response.ObjectKey);
        Assert.NotNull(asset);
        Assert.Equal(MediaScope.RoomImage, asset!.Scope);
        Assert.Equal(MediaStatus.Uploaded, asset.Status);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), asset.OwnerUserId);
        Assert.Equal(stream.Length, asset.FileSize);
    }

    [Fact]
    public async Task UploadPdfAsync_StreamOverload_ShouldMapHouseRuleScopeAndPersistFileSize()
    {
        var storage = new RecordingMediaStorageService();
        var service = new MediaBackedFileStorageService(
            new FixedMediaObjectKeyFactory("public/rooming-house-rule-pdfs/2026/07/09/rule.pdf", "rule.pdf"),
            storage,
            new MediaAssetService(_fixture.Context),
            new FakeCurrentUserService(null));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf-content"));
        var response = await service.UploadPdfAsync(stream, "house-rule.pdf", FileUploadScope.HouseRule);

        Assert.Equal("public/rooming-house-rule-pdfs/2026/07/09/rule.pdf", response.ObjectKey);

        var asset = await new MediaAssetService(_fixture.Context).GetByObjectKeyAsync(response.ObjectKey);
        Assert.NotNull(asset);
        Assert.Equal(MediaScope.RoomingHouseRulePdf, asset!.Scope);
        Assert.Equal("application/pdf", asset.ContentType);
        Assert.Equal(11, asset.FileSize);
    }

    [Fact]
    public async Task UploadPdfAsync_ShouldStorePrivateChatAttachmentAndCreateMediaAsset()
    {
        var storage = new RecordingMediaStorageService();
        var service = new MediaBackedFileStorageService(
            new FixedMediaObjectKeyFactory("private/chat-attachments/2026/07/11/chat.pdf", "chat.pdf"),
            storage,
            new MediaAssetService(_fixture.Context),
            new FakeCurrentUserService(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("chat-attachment"));
        var response = await service.UploadPdfAsync(
            new ImageUploadFile
            {
                Content = stream,
                FileName = "chat.pdf",
                ContentType = "application/pdf",
                Length = stream.Length
            },
            FileUploadScope.ChatAttachment);

        Assert.Equal("private/chat-attachments/2026/07/11/chat.pdf", response.ObjectKey);
        Assert.NotNull(response.MediaAssetId);
        Assert.Equal($"/api/media/private/{response.MediaAssetId}", response.Url);
        Assert.NotNull(storage.LastRequest);
        Assert.Equal(MediaVisibility.Private, storage.LastRequest!.Visibility);

        var asset = await new MediaAssetService(_fixture.Context).GetByObjectKeyAsync(response.ObjectKey);
        Assert.NotNull(asset);
        Assert.Equal(MediaScope.ChatAttachment, asset!.Scope);
        Assert.Equal(MediaVisibility.Private, asset.Visibility);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), asset.OwnerUserId);
    }

    private sealed class RecordingMediaStorageService : IMediaStorageService
    {
        public MediaUploadRequest? LastRequest { get; private set; }

        public string GetBucketName()
        {
            return "local-media";
        }

        public Task<MediaStoredObjectResult> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "local-media",
                ObjectKey = request.ObjectKey,
                PublicUrl = $"/uploads/{request.ObjectKey}",
                StoredFileName = Path.GetFileName(request.ObjectKey)
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaObjectMetadataResult
            {
                ObjectKey = objectKey,
                ContentType = "application/pdf",
                FileSize = 11
            });
        }

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaUploadUrlResult
            {
                Url = $"/upload/{objectKey}",
                HttpMethod = "PUT",
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl)
            });
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedMediaObjectKeyFactory : IMediaObjectKeyFactory
    {
        private readonly string _objectKey;
        private readonly string _storedFileName;

        public FixedMediaObjectKeyFactory(string objectKey, string storedFileName)
        {
            _objectKey = objectKey;
            _storedFileName = storedFileName;
        }

        public MediaObjectKeyResult Create(MediaScope scope, MediaVisibility visibility, string originalFileName)
        {
            return new MediaObjectKeyResult
            {
                ObjectKey = _objectKey,
                StoredFileName = _storedFileName
            };
        }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid? userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => Array.Empty<string>();

        public bool IsAuthenticated => UserId.HasValue;
    }
}
