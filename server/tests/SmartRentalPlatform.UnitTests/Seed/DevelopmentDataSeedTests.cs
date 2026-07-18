using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Seed;

public class DevelopmentDataSeedTests : IClassFixture<TestDatabaseFixture>
{
    private static readonly Guid ApprovedHouseId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private readonly TestDatabaseFixture _fixture;

    public DevelopmentDataSeedTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task EnsureApprovedHouseRuleSeedAsync_ShouldCreateLinkedPublicPdfAndStayIdempotent()
    {
        _fixture.Context.RoomingHouses.Add(new RoomingHouse
        {
            Id = ApprovedHouseId,
            LandlordUserId = LandlordUserId,
            Name = "Nhà trọ Hoa Sen",
            AddressLine = "123 Nguyen Van Cu",
            WardCode = "20285",
            ProvinceCode = "48",
            AddressDisplay = "123 Nguyen Van Cu, Hai Chau, Da Nang",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible
        });
        await _fixture.Context.SaveChangesAsync();

        var storage = new RecordingMediaStorageService();
        var objectKeyFactory = new FixedMediaObjectKeyFactory(
            "public/rooming-house-rule-pdfs/2026/07/14/approved-house-rule.pdf",
            "approved-house-rule.pdf");

        await DevelopmentDataSeed.EnsureApprovedHouseRuleSeedAsync(
            _fixture.Context,
            storage,
            objectKeyFactory);
        await _fixture.Context.SaveChangesAsync();

        await DevelopmentDataSeed.EnsureApprovedHouseRuleSeedAsync(
            _fixture.Context,
            storage,
            objectKeyFactory);
        await _fixture.Context.SaveChangesAsync();

        var rule = await _fixture.Context.RoomingHouseRules.SingleAsync(x => x.RoomingHouseId == ApprovedHouseId);
        var asset = await _fixture.Context.MediaAssets.SingleAsync(x => x.Id == rule.MediaAssetId);

        Assert.Equal(RoomingHouseRuleSourceType.FormGenerated, rule.SourceType);
        Assert.False(string.IsNullOrWhiteSpace(rule.GeneralRules));
        Assert.False(string.IsNullOrWhiteSpace(rule.QuietHours));
        Assert.False(string.IsNullOrWhiteSpace(rule.SecurityPolicy));
        Assert.Equal(LandlordUserId, asset.OwnerUserId);
        Assert.Equal(MediaScope.RoomingHouseRulePdf, asset.Scope);
        Assert.Equal(MediaVisibility.Public, asset.Visibility);
        Assert.Equal(MediaStatus.Linked, asset.Status);
        Assert.Equal(nameof(RoomingHouseRule), asset.LinkedEntityType);
        Assert.Equal(ApprovedHouseId, asset.LinkedEntityId);
        Assert.Equal("application/pdf", asset.ContentType);
        Assert.True(asset.FileSize > 0);
        Assert.Equal(1, storage.UploadCount);
        Assert.Single(storage.StoredObjectKeys);
        Assert.Single(await _fixture.Context.MediaAssets.ToListAsync());
    }

    private sealed class RecordingMediaStorageService : IMediaStorageService
    {
        private readonly HashSet<string> _storedObjectKeys = new(StringComparer.Ordinal);

        public int UploadCount { get; private set; }

        public IReadOnlyCollection<string> StoredObjectKeys => _storedObjectKeys;

        public Task<MediaStoredObjectResult> UploadAsync(
            MediaUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            _storedObjectKeys.Add(request.ObjectKey);

            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "dev-seed-bucket",
                ObjectKey = request.ObjectKey,
                PublicUrl = null,
                StoredFileName = Path.GetFileName(request.ObjectKey)
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_storedObjectKeys.Contains(objectKey));
        }

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaObjectMetadataResult
            {
                ObjectKey = objectKey,
                ContentType = "application/pdf",
                FileSize = 1
            });
        }

        public string GetBucketName()
        {
            return "dev-seed-bucket";
        }

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            _storedObjectKeys.Remove(objectKey);
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(
            string objectKey,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
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

        public MediaObjectKeyResult Create(
            MediaScope scope,
            MediaVisibility visibility,
            string originalFileName)
        {
            return new MediaObjectKeyResult
            {
                ObjectKey = _objectKey,
                StoredFileName = _storedFileName
            };
        }
    }
}
