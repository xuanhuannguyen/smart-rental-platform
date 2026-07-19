using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Seed
{
    public sealed class DisplayCatalogSeederTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AppDbContext _context;

        public DisplayCatalogSeederTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task Seeder_ShouldBeIdempotentAndNotDuplicate()
        {
            await SeedPrerequisitesAsync();

            var storage = new RecordingMediaStorageService();
            var objectKeyFactory = new MockMediaObjectKeyFactory();
            var passwordService = new StubPasswordService();

            var runner = new DisplayCatalogSeedRunner(
                _context,
                storage,
                objectKeyFactory,
                passwordService);
            var version = $"test-catalog-v1-{Guid.NewGuid():N}";

            // First run: Seed 3 houses with 15 assets
            await runner.RunSeedAsync(
                targetHouseCount: 3,
                targetAssetCount: 15,
                uploadMedia: true,
                version: version);

            int initialHouses = await _context.RoomingHouses.CountAsync();
            int initialRooms = await _context.Rooms.CountAsync();
            int initialReviews = await _context.RoomingHouseReviews.CountAsync();
            int initialImages = await _context.PropertyImages.CountAsync();
            int initialAssets = await _context.MediaAssets.CountAsync();

            Assert.Equal(3, initialHouses);
            Assert.True(initialRooms >= 15); // 3 houses * 5-8 rooms
            Assert.Equal(15, initialReviews); // 3 houses * 5 reviews
            Assert.True(storage.UploadCount > 0);

            // Second run: should bypass adding and keep counts identical
            await runner.RunSeedAsync(
                targetHouseCount: 3,
                targetAssetCount: 15,
                uploadMedia: true,
                version: version);

            int secondHouses = await _context.RoomingHouses.CountAsync();
            int secondRooms = await _context.Rooms.CountAsync();
            int secondReviews = await _context.RoomingHouseReviews.CountAsync();
            int secondImages = await _context.PropertyImages.CountAsync();
            int secondAssets = await _context.MediaAssets.CountAsync();

            Assert.Equal(initialHouses, secondHouses);
            Assert.Equal(initialRooms, secondRooms);
            Assert.Equal(initialReviews, secondReviews);
            Assert.Equal(initialImages, secondImages);
            Assert.Equal(initialAssets, secondAssets);
        }

        [Fact]
        public async Task SeededData_ShouldHaveCorrectImageAssignmentsAndUsageCaps()
        {
            await SeedPrerequisitesAsync();

            var storage = new RecordingMediaStorageService();
            var objectKeyFactory = new MockMediaObjectKeyFactory();
            var passwordService = new StubPasswordService();

            var runner = new DisplayCatalogSeedRunner(
                _context,
                storage,
                objectKeyFactory,
                passwordService);
            var version = $"test-catalog-v2-{Guid.NewGuid():N}";

            await runner.RunSeedAsync(
                targetHouseCount: 3,
                targetAssetCount: 20,
                uploadMedia: true,
                version: version);

            // Validate that no house or room gallery has duplicate images
            var houses = await _context.RoomingHouses
                .Include(h => h.Images)
                .Include(h => h.Rooms)
                .ThenInclude(r => r.Images)
                .ToListAsync();

            foreach (var h in houses)
            {
                // House cover uses Exterior
                var cover = h.Images.FirstOrDefault(img => img.IsCover);
                Assert.NotNull(cover);
                Assert.NotNull(cover.MediaAssetId);
                var coverAsset = await _context.MediaAssets.FindAsync(cover.MediaAssetId.Value);
                Assert.NotNull(coverAsset);
                Assert.Equal(MediaScope.RoomingHouseImage, coverAsset.Scope);

                // House images uniqueness
                var houseAssetIds = h.Images.Select(img => img.MediaAssetId).ToList();
                Assert.Equal(houseAssetIds.Distinct().Count(), houseAssetIds.Count);

                foreach (var r in h.Rooms)
                {
                    // Room cover uses Room scope
                    var rCover = r.Images.FirstOrDefault(img => img.IsCover);
                    Assert.NotNull(rCover);
                    Assert.NotNull(rCover.MediaAssetId);
                    var rCoverAsset = await _context.MediaAssets.FindAsync(rCover.MediaAssetId.Value);
                    Assert.NotNull(rCoverAsset);
                    Assert.Equal(MediaScope.RoomImage, rCoverAsset.Scope);

                    // Room images uniqueness
                    var roomAssetIds = r.Images.Select(img => img.MediaAssetId).ToList();
                    Assert.Equal(roomAssetIds.Distinct().Count(), roomAssetIds.Count);
                }
            }

            // Usage cap validation: no single asset used more than 30 times
            var assetUsages = await _context.PropertyImages
                .Where(img => img.MediaAssetId.HasValue)
                .GroupBy(img => img.MediaAssetId!.Value)
                .Select(g => new { AssetId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var usage in assetUsages)
            {
                Assert.True(usage.Count <= 30, $"Asset {usage.AssetId} exceeded usage cap (count: {usage.Count})");
            }
        }

        [Fact]
        public async Task SeededReviews_ShouldBeApprovedAndHaveReplies()
        {
            await SeedPrerequisitesAsync();

            var storage = new RecordingMediaStorageService();
            var objectKeyFactory = new MockMediaObjectKeyFactory();
            var passwordService = new StubPasswordService();

            var runner = new DisplayCatalogSeedRunner(
                _context,
                storage,
                objectKeyFactory,
                passwordService);
            var version = $"test-catalog-v3-{Guid.NewGuid():N}";

            await runner.RunSeedAsync(
                targetHouseCount: 2,
                targetAssetCount: 15,
                uploadMedia: true,
                version: version);

            var reviews = await _context.RoomingHouseReviews.ToListAsync();
            Assert.Equal(10, reviews.Count); // 2 houses * 5 reviews

            foreach (var rev in reviews)
            {
                Assert.Equal(RoomingHouseReviewModerationStatus.Approved, rev.ModerationStatus);
                Assert.False(rev.IsHidden);
                if (string.IsNullOrWhiteSpace(rev.LandlordReply))
                {
                    Assert.Null(rev.LandlordReplyCreatedAt);
                }
                else
                {
                    Assert.NotNull(rev.LandlordReplyCreatedAt);
                }
            }

            var replyCounts = await _context.RoomingHouseReviews
                .GroupBy(x => x.RoomingHouseId)
                .Select(g => g.Count(x => x.LandlordReply != null && x.LandlordReply != ""))
                .ToListAsync();

            foreach (var replyCount in replyCounts)
            {
                Assert.InRange(replyCount, 3, 5);
            }
        }

        [Fact]
        public async Task SeededContractsAndOccupants_ShouldHaveCorrectStatusAndSnapshots()
        {
            await SeedPrerequisitesAsync();

            var storage = new RecordingMediaStorageService();
            var objectKeyFactory = new MockMediaObjectKeyFactory();
            var passwordService = new StubPasswordService();

            var runner = new DisplayCatalogSeedRunner(
                _context,
                storage,
                objectKeyFactory,
                passwordService);
            var version = $"test-catalog-v4-{Guid.NewGuid():N}";

            await runner.RunSeedAsync(
                targetHouseCount: 2,
                targetAssetCount: 15,
                uploadMedia: true,
                version: version);

            var contracts = await _context.RentalContracts
                .Include(c => c.Occupants)
                .ToListAsync();

            Assert.Equal(10, contracts.Count); // 2 houses * 5 reviews = 10 contracts

            int expiredCount = contracts.Count(c => c.Status == RentalContractStatus.Expired);
            int activeCount = contracts.Count(c => c.Status == RentalContractStatus.Active);

            // 80% (8) should be Expired, 20% (2) should be Active
            Assert.Equal(8, expiredCount);
            Assert.Equal(2, activeCount);

            foreach (var contract in contracts)
            {
                Assert.False(string.IsNullOrWhiteSpace(contract.RoomSnapshot));
                Assert.Contains("RoomNumber", contract.RoomSnapshot);
                Assert.Contains("MaxOccupants", contract.RoomSnapshot);

                // Each contract should have occupants
                Assert.NotEmpty(contract.Occupants);

                foreach (var occupant in contract.Occupants)
                {
                    if (contract.Status == RentalContractStatus.Expired)
                    {
                        Assert.Equal(ContractOccupantStatus.MoveOut, occupant.Status);
                        Assert.NotNull(occupant.MoveOutDate);
                    }
                    else
                    {
                        Assert.Equal(ContractOccupantStatus.Active, occupant.Status);
                        Assert.Null(occupant.MoveOutDate);
                    }
                }
            }
        }

        private async Task SeedPrerequisitesAsync()
        {
            var now = DateTimeOffset.UtcNow;
            
            // Seed target provinces and wards if they don't exist
            if (!await _context.AdministrativeProvinces.AnyAsync(p => p.Code == "01"))
            {
                var province = new AdministrativeProvince { Code = "01", Name = "Thành phố Hà Nội", IsActive = true };
                var ward = new AdministrativeWard { Code = "00001", ProvinceCode = "01", Name = "Phường Dịch Vọng Hậu", IsActive = true, Province = province };
                _context.AdministrativeProvinces.Add(province);
                _context.AdministrativeWards.Add(ward);
            }

            // Seed Roles if they don't exist
            if (!await _context.Roles.AnyAsync(r => r.Id == RoleSeed.TenantRoleId))
            {
                _context.Roles.Add(new Role { Id = RoleSeed.TenantRoleId, Name = RoleName.Tenant });
            }
            if (!await _context.Roles.AnyAsync(r => r.Id == RoleSeed.LandlordRoleId))
            {
                _context.Roles.Add(new Role { Id = RoleSeed.LandlordRoleId, Name = RoleName.Landlord });
            }

            // Seed Billing Service Types if they don't exist
            if (!await _context.BillingServiceTypes.AnyAsync())
            {
                var billing1 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Điện", SupportsMeterReading = true, IsActive = true };
                var billing2 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Nước", SupportsMeterReading = true, IsActive = true };
                var billing3 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Internet", SupportsMeterReading = false, IsActive = true };
                var billing4 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Rác", SupportsMeterReading = false, IsActive = true };
                _context.BillingServiceTypes.AddRange(billing1, billing2, billing3, billing4);
            }

            // Seed Amenities if they don't exist
            if (!await _context.Amenities.AnyAsync())
            {
                var amenity1 = new Amenity { Id = 1, Name = "Wifi", Scope = AmenityScope.Both, IsActive = true };
                var amenity2 = new Amenity { Id = 2, Name = "Parking", Scope = AmenityScope.House, IsActive = true };
                var amenity3 = new Amenity { Id = 3, Name = "Air Conditioning", Scope = AmenityScope.Room, IsActive = true };
                _context.Amenities.AddRange(amenity1, amenity2, amenity3);
            }

            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _connection.Dispose();
        }

        private sealed class StubPasswordService : IPasswordService
        {
            public string HashPassword(string password) => $"hashed_{password}";
            public bool VerifyPassword(string hashedPassword, string providedPassword) => hashedPassword == $"hashed_{providedPassword}";
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
                    BucketName = "test-media-bucket",
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

            public string GetBucketName() => "test-media-bucket";

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

        private sealed class MockMediaObjectKeyFactory : IMediaObjectKeyFactory
        {
            public MediaObjectKeyResult Create(
                MediaScope scope,
                MediaVisibility visibility,
                string originalFileName)
            {
                var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";
                return new MediaObjectKeyResult
                {
                    ObjectKey = $"public/tests/{storedName}",
                    StoredFileName = storedName
                };
            }
        }
    }
}
