using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Common;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Seed;

public sealed class LargeScaleRoomingHouseSeederTests : IDisposable
{
    private static readonly Guid DummyLandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000009999");
    private static readonly Guid LegacySearchMockLandlordId = Guid.Parse("90000000-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public LargeScaleRoomingHouseSeederTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestAppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateMediaBackedPropertyImagesForGeneratedMockHouses()
    {
        await SeedPrerequisitesAsync();

        var storage = new RecordingMediaStorageService();
        var objectKeyFactory = new SequencedMediaObjectKeyFactory();

        await LargeScaleRoomingHouseSeeder.SeedAsync(
            _context,
            storage,
            objectKeyFactory);
        await _context.SaveChangesAsync();

        var generatedHouses = await _context.RoomingHouses
            .Where(x => x.LandlordUserId == DummyLandlordUserId)
            .ToListAsync();
        var generatedHouse = Assert.Single(generatedHouses);
        Assert.Equal(
            LargeScaleRoomingHouseSeeder.TargetRoomingHouseCount,
            await _context.RoomingHouses.CountAsync());
        Assert.False(await _context.RoomingHouses.AnyAsync(x => x.LandlordUserId == LegacySearchMockLandlordId));

        var roomIds = await _context.Rooms
            .Where(x => x.RoomingHouseId == generatedHouse.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var propertyImages = await _context.PropertyImages
            .Where(x => x.RoomingHouseId == generatedHouse.Id || (x.RoomId.HasValue && roomIds.Contains(x.RoomId.Value)))
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
        var mediaAssetIds = propertyImages
            .Where(x => x.MediaAssetId.HasValue)
            .Select(x => x.MediaAssetId!.Value)
            .ToList();
        var mediaAssets = await _context.MediaAssets
            .Where(x => mediaAssetIds.Contains(x.Id))
            .ToListAsync();

        Assert.NotEmpty(roomIds);
        Assert.NotEmpty(propertyImages);
        Assert.All(propertyImages, image =>
        {
            Assert.True(image.MediaAssetId.HasValue);
            Assert.StartsWith("/api/media/public/", image.ImageUrl);
        });
        Assert.Equal(propertyImages.Count, mediaAssets.Count);
        Assert.Equal(propertyImages.Count, storage.UploadCount);
        Assert.All(mediaAssets, asset =>
        {
            Assert.Equal(MediaVisibility.Public, asset.Visibility);
            Assert.Equal(MediaStatus.Linked, asset.Status);
            Assert.Equal(nameof(PropertyImage), asset.LinkedEntityType);
            Assert.True(
                asset.Scope is MediaScope.RoomingHouseImage or MediaScope.RoomImage,
                $"Unexpected media scope '{asset.Scope}'.");
            Assert.Equal("image/png", asset.ContentType);
            Assert.True(asset.FileSize > 0);
        });
    }

    private async Task SeedPrerequisitesAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var ward = await _context.AdministrativeWards
            .Include(x => x.Province)
            .FirstAsync(x => x.IsActive && x.Province.IsActive);
        var province = ward.Province;
        var stableLandlord = TestDataBuilder.BuildUser(
            id: Guid.Parse("10000000-0000-0000-0000-000000000777"),
            email: "existing-mock-owner@test.com",
            displayName: "Existing Mock Owner");
        var legacySearchLandlord = TestDataBuilder.BuildUser(
            id: LegacySearchMockLandlordId,
            email: "legacy-search-mock@test.com",
            displayName: "Legacy Search Mock Owner");

        _context.Users.AddRange(stableLandlord, legacySearchLandlord);
        _context.RoomingHouses.Add(new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = legacySearchLandlord.Id,
            Landlord = legacySearchLandlord,
            Name = "Legacy Search Mock House",
            AddressLine = "1 Legacy Street",
            ProvinceCode = province.Code,
            WardCode = ward.Code,
            AddressDisplay = $"1 Legacy Street, {ward.Name}, {province.Name}",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
            Province = province,
            Ward = ward,
            CreatedAt = now,
            UpdatedAt = now
        });

        for (var index = 0; index < LargeScaleRoomingHouseSeeder.TargetRoomingHouseCount - 1; index++)
        {
            _context.RoomingHouses.Add(new RoomingHouse
            {
                Id = Guid.NewGuid(),
                LandlordUserId = stableLandlord.Id,
                Landlord = stableLandlord,
                Name = $"Existing House #{index + 1}",
                AddressLine = $"{index + 1} Nguyen Van Cu",
                ProvinceCode = province.Code,
                WardCode = ward.Code,
                AddressDisplay = $"{index + 1} Nguyen Van Cu, {ward.Name}, {province.Name}",
                ApprovalStatus = RoomingHouseApprovalStatus.Approved,
                VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
                Province = province,
                Ward = ward,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class RecordingMediaStorageService : IMediaStorageService
    {
        private readonly HashSet<string> _storedObjectKeys = new(StringComparer.Ordinal);

        public int UploadCount { get; private set; }

        public Task<MediaStoredObjectResult> UploadAsync(
            MediaUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            _storedObjectKeys.Add(request.ObjectKey);

            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "seed-media-bucket",
                ObjectKey = request.ObjectKey,
                StoredFileName = Path.GetFileName(request.ObjectKey)
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_storedObjectKeys.Contains(objectKey));

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(
            string objectKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public string GetBucketName() => "seed-media-bucket";

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            _storedObjectKeys.Remove(objectKey);
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(
            string objectKey,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SequencedMediaObjectKeyFactory : IMediaObjectKeyFactory
    {
        private int _sequence;

        public MediaObjectKeyResult Create(
            MediaScope scope,
            MediaVisibility visibility,
            string originalFileName)
        {
            var folder = scope == MediaScope.RoomingHouseImage
                ? "rooming-house-images"
                : "room-images";
            var fileName = $"{Interlocked.Increment(ref _sequence):D4}-{Path.GetFileName(originalFileName)}";

            return new MediaObjectKeyResult
            {
                ObjectKey = $"public/{folder}/2026/07/14/{fileName}",
                StoredFileName = fileName
            };
        }
    }
}
