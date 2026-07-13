using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Users;
using SmartRentalPlatform.Infrastructure.MediaMigration;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.MediaMigration;

public class LegacyMediaMigrationReadinessServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public LegacyMediaMigrationReadinessServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task BuildReportAsync_ShouldMatchLegacyObjectKeysAndIgnoreExternalAvatarUrls()
    {
        var mediaAssetId = Guid.NewGuid();
        _fixture.Context.MediaAssets.Add(new MediaAsset
        {
            Id = mediaAssetId,
            BucketName = "test-bucket",
            ObjectKey = "public/rooming-house-images/legacy.jpg",
            OriginalFileName = "legacy.jpg",
            StoredFileName = "legacy.jpg",
            ContentType = "image/jpeg",
            FileSize = 123,
            Scope = MediaScope.RoomingHouseImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.PropertyImages.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            ObjectKey = "/api/media/public/public/rooming-house-images/legacy.jpg",
            ImageUrl = "/api/media/public/public/rooming-house-images/legacy.jpg",
            CreatedAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "external-avatar@test.com",
            NormalizedEmail = "EXTERNAL-AVATAR@TEST.COM",
            DisplayName = "External Avatar",
            AvatarUrl = "https://lh3.googleusercontent.com/avatar.png",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = new LegacyMediaMigrationReadinessService(_fixture.Context);
        var report = await service.BuildReportAsync(new LegacyMediaMigrationReadinessOptions());

        var propertyImages = Assert.Single(report.Modules, x => x.Module == "PropertyImages");
        Assert.Equal(1, propertyImages.LegacyReferences);
        Assert.Equal(1, propertyImages.MissingMediaAssetLinks);
        Assert.Equal(1, propertyImages.MatchingMediaAssetsByObjectKey);
        Assert.Equal(mediaAssetId, propertyImages.Samples.Single().MatchingMediaAssetId);
        Assert.DoesNotContain(report.Modules, x => x.Module == "Avatars");
    }

    [Fact]
    public async Task BuildReportAsync_WithStorageCheck_ShouldReportMissingStorageObjects()
    {
        _fixture.Context.PropertyImages.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            ObjectKey = "public/rooming-house-images/missing.jpg",
            ImageUrl = "/api/media/public/public/rooming-house-images/missing.jpg",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = new LegacyMediaMigrationReadinessService(
            _fixture.Context,
            new FakeMediaStorageService(presentObjectKeys: []));

        var report = await service.BuildReportAsync(new LegacyMediaMigrationReadinessOptions
        {
            CheckStorage = true
        });

        var propertyImages = Assert.Single(report.Modules, x => x.Module == "PropertyImages");
        Assert.True(propertyImages.StorageChecked);
        Assert.Equal(1, propertyImages.StorageMissing);
        Assert.Equal("Missing", propertyImages.Samples.Single().StorageStatus);
    }

    [Fact]
    public async Task BackfillAsync_DryRun_ShouldPlanCreateAndLinkWithoutMutatingDatabase()
    {
        var propertyImageId = Guid.NewGuid();
        _fixture.Context.PropertyImages.Add(new PropertyImage
        {
            Id = propertyImageId,
            ObjectKey = "/uploads/public/rooming-house-images/legacy-backfill.jpg",
            ImageUrl = "/api/media/public/public/rooming-house-images/legacy-backfill.jpg",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = new LegacyMediaMigrationReadinessService(_fixture.Context);
        var report = await service.BackfillAsync(new LegacyMediaBackfillOptions
        {
            DryRun = true
        });

        var propertyImages = Assert.Single(report.Modules, x => x.Module == "PropertyImages");
        Assert.Equal(1, propertyImages.Candidates);
        Assert.Equal(1, propertyImages.PlannedCreates);
        Assert.Equal(1, propertyImages.PlannedLinks);
        Assert.Equal(0, propertyImages.CreatedMediaAssets);
        Assert.Equal(0, propertyImages.LinkedLegacyRows);
        Assert.Equal("Link", propertyImages.Samples.Single().Action);
        Assert.Empty(_fixture.Context.MediaAssets);
        Assert.Null(_fixture.Context.PropertyImages.Single(x => x.Id == propertyImageId).MediaAssetId);
    }

    [Fact]
    public async Task BackfillAsync_DryRun_ShouldPlanLinkToExistingMediaAssetWithoutCreate()
    {
        var mediaAssetId = Guid.NewGuid();
        _fixture.Context.MediaAssets.Add(new MediaAsset
        {
            Id = mediaAssetId,
            BucketName = "test-bucket",
            ObjectKey = "public/rooming-house-images/existing-backfill.jpg",
            OriginalFileName = "existing-backfill.jpg",
            StoredFileName = "existing-backfill.jpg",
            ContentType = "image/jpeg",
            FileSize = 123,
            Scope = MediaScope.RoomingHouseImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _fixture.Context.PropertyImages.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            ObjectKey = "/uploads/public/rooming-house-images/existing-backfill.jpg",
            ImageUrl = "/api/media/public/public/rooming-house-images/existing-backfill.jpg",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = new LegacyMediaMigrationReadinessService(_fixture.Context);
        var report = await service.BackfillAsync(new LegacyMediaBackfillOptions
        {
            DryRun = true
        });

        var propertyImages = Assert.Single(report.Modules, x => x.Module == "PropertyImages");
        Assert.Equal(1, propertyImages.Candidates);
        Assert.Equal(0, propertyImages.PlannedCreates);
        Assert.Equal(1, propertyImages.PlannedLinks);
        Assert.Equal(0, propertyImages.CreatedMediaAssets);
        Assert.Equal(0, propertyImages.LinkedLegacyRows);
        Assert.Empty(propertyImages.Samples);
    }

    private sealed class FakeMediaStorageService : IMediaStorageService
    {
        private readonly HashSet<string> _presentObjectKeys;

        public FakeMediaStorageService(IEnumerable<string> presentObjectKeys)
        {
            _presentObjectKeys = new HashSet<string>(presentObjectKeys, StringComparer.OrdinalIgnoreCase);
        }

        public Task<MediaStoredObjectResult> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_presentObjectKeys.Contains(objectKey));
        }

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(string objectKey, string contentType, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public string GetBucketName()
        {
            return "test-bucket";
        }
    }
}
