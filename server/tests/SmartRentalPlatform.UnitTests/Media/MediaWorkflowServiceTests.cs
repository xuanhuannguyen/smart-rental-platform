using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Infrastructure.Media;
using SmartRentalPlatform.Infrastructure.Storage;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Media;

public class MediaWorkflowServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MediaWorkflowServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task CreateUploadSessionAsync_ShouldCreatePendingAssetAndSignedUploadUrl()
    {
        var actorUserId = Guid.NewGuid();
        var service = CreateService(new SignedUploadMediaStorageService());

        var result = await service.CreateUploadSessionAsync(
            new CreateMediaUploadSessionRequest
            {
                OriginalFileName = "avatar.png",
                ContentType = "image/png",
                FileSize = 2048,
                Scope = MediaScope.Avatar,
                Visibility = MediaVisibility.Private
            },
            actorUserId,
            isAdmin: false);

        Assert.NotEqual(Guid.Empty, result.MediaAssetId);
        Assert.Equal("signed-upload-url", result.DeliveryMode);
        Assert.Equal("PUT", result.HttpMethod);
        Assert.NotNull(result.UploadUrl);

        var asset = await _fixture.Context.MediaAssets.FindAsync(result.MediaAssetId);
        Assert.NotNull(asset);
        Assert.Equal(MediaStatus.PendingUpload, asset!.Status);
        Assert.Equal(actorUserId, asset.OwnerUserId);
        Assert.StartsWith("private/avatars/", asset.ObjectKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FinalizeUploadAsync_ShouldMarkUploadedAndWriteAudit()
    {
        var actorUserId = Guid.NewGuid();
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = actorUserId,
            BucketName = "unit-test-bucket",
            ObjectKey = "private/avatars/2026/07/12/avatar.png",
            OriginalFileName = "avatar.png",
            StoredFileName = "avatar.png",
            ContentType = "image/png",
            FileSize = 123,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.PendingUpload,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _fixture.Context.MediaAssets.Add(asset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(new SignedUploadMediaStorageService());

        var result = await service.FinalizeUploadAsync(asset.Id, actorUserId, isAdmin: false, fileHash: "hash-123");

        Assert.Equal(MediaStatus.Uploaded, result.Status);

        var stored = await _fixture.Context.MediaAssets.FindAsync(asset.Id);
        Assert.NotNull(stored);
        Assert.Equal(MediaStatus.Uploaded, stored!.Status);
        Assert.Equal("hash-123", stored.FileHash);

        var audit = Assert.Single(_fixture.Context.MediaAuditLogs);
        Assert.Equal("FinalizeUpload", audit.Action);
    }

    [Fact]
    public async Task CreateUploadSessionAsync_WithInvalidContentType_ShouldThrowValidation()
    {
        var actorUserId = Guid.NewGuid();
        var service = CreateService(new SignedUploadMediaStorageService());

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateUploadSessionAsync(
            new CreateMediaUploadSessionRequest
            {
                OriginalFileName = "avatar.exe",
                ContentType = "application/octet-stream",
                FileSize = 2048,
                Scope = MediaScope.Avatar,
                Visibility = MediaVisibility.Private
            },
            actorUserId,
            isAdmin: false));
    }

    [Fact]
    public async Task FinalizeUploadAsync_WhenStoredMetadataMismatch_ShouldThrowValidation()
    {
        var actorUserId = Guid.NewGuid();
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = actorUserId,
            BucketName = "unit-test-bucket",
            ObjectKey = "private/avatars/2026/07/12/avatar.png",
            OriginalFileName = "avatar.png",
            StoredFileName = "avatar.png",
            ContentType = "image/png",
            FileSize = 123,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.PendingUpload,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _fixture.Context.MediaAssets.Add(asset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(new SignedUploadMediaStorageService
        {
            Metadata = new MediaObjectMetadataResult
            {
                ObjectKey = asset.ObjectKey,
                ContentType = "image/png",
                FileSize = 999
            }
        });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.FinalizeUploadAsync(asset.Id, actorUserId, isAdmin: false, fileHash: "hash-123"));
    }

    [Fact]
    public async Task SoftDeleteAsync_ForNonOwner_ShouldThrowForbidden()
    {
        var ownerUserId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            BucketName = "unit-test-bucket",
            ObjectKey = "private/avatars/2026/07/12/avatar.png",
            OriginalFileName = "avatar.png",
            StoredFileName = "avatar.png",
            ContentType = "image/png",
            FileSize = 123,
            Scope = MediaScope.Avatar,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _fixture.Context.MediaAssets.Add(asset);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService(new SignedUploadMediaStorageService());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.SoftDeleteAsync(asset.Id, actorUserId, isAdmin: false));
    }

    private MediaWorkflowService CreateService(IMediaStorageService mediaStorageService)
    {
        return new MediaWorkflowService(
            _fixture.Context,
            new MediaAssetService(_fixture.Context),
            new MediaObjectKeyFactory(),
            new DefaultMediaPermissionService(_fixture.Context),
            mediaStorageService);
    }

    private sealed class SignedUploadMediaStorageService : IMediaStorageService
    {
        public MediaObjectMetadataResult? Metadata { get; init; }

        public Task<MediaStoredObjectResult> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "unit-test-bucket",
                ObjectKey = request.ObjectKey,
                StoredFileName = request.OriginalFileName
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Metadata ?? new MediaObjectMetadataResult
            {
                ObjectKey = objectKey,
                ContentType = "image/png",
                FileSize = 123
            });
        }

        public string GetBucketName()
        {
            return "unit-test-bucket";
        }

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaUploadUrlResult
            {
                Url = $"https://example.test/{objectKey}",
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
            return Task.FromResult($"https://example.test/download/{objectKey}");
        }
    }
}
