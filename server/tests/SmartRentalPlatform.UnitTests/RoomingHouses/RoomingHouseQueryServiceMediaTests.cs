using SmartRentalPlatform.Application.Common.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Contracts.Locations;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Common;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.RoomingHouses;

public class RoomingHouseQueryServiceMediaTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public RoomingHouseQueryServiceMediaTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task GetPublicListingAsync_ShouldBuildCoverFromMediaAssetIdInsteadOfLegacyImageUrl()
    {
        var mediaAssetId = await SeedPublicHouseWithLegacyAndMediaImagesAsync();
        var service = CreateService();

        var result = await service.GetPublicListingAsync();

        var item = Assert.Single(result);
        Assert.Equal(PublicMediaPathBuilder.Build(mediaAssetId), item.CoverImageUrl);
    }

    [Fact]
    public async Task SearchPublicAsync_ShouldBuildCoverFromMediaAssetIdInsteadOfLegacyImageUrl()
    {
        var mediaAssetId = await SeedPublicHouseWithLegacyAndMediaImagesAsync();
        var service = CreateService();

        var result = await service.SearchPublicAsync(new RoomingHouseSearchRequest
        {
            Page = 1,
            PageSize = 12
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(PublicMediaPathBuilder.Build(mediaAssetId), item.CoverImageUrl);
    }

    private async Task<Guid> SeedPublicHouseWithLegacyAndMediaImagesAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var province = new AdministrativeProvince
        {
            Code = "UT-P-MEDIA",
            Name = "Tỉnh test media",
            Type = ProvinceType.City,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var ward = new AdministrativeWard
        {
            Code = "UT-W-MEDIA",
            ProvinceCode = province.Code,
            Name = "Phường test media",
            Type = WardType.Ward,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Province = province
        };
        var landlord = TestDataBuilder.BuildUser(email: "listing-media@test.com", displayName: "Listing Media");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Nhà trọ Media");
        house.AddressLine = "123 Nguyen Van Cu";
        house.AddressDisplay = "123 Nguyen Van Cu, Phường test media, Tỉnh test media";
        house.ProvinceCode = province.Code;
        house.WardCode = ward.Code;
        house.Province = province;
        house.Ward = ward;
        house.Landlord = landlord;
        house.Latitude = 16.0678m;
        house.Longitude = 108.2208m;

        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        room.RoomingHouse = house;
        room.AreaM2 = 24m;
        room.MaxOccupants = 2;
        room.PriceTiers.Add(new RoomPriceTier
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            OccupantCount = 1,
            MonthlyRent = 3_200_000m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = landlord.Id,
            BucketName = "test-bucket",
            ObjectKey = "public/rooming-house-images/2026/07/14/test-house-cover.png",
            OriginalFileName = "test-house-cover.png",
            StoredFileName = "test-house-cover.png",
            ContentType = "image/png",
            FileSize = 68,
            Scope = MediaScope.RoomingHouseImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(PropertyImage),
            LinkedEntityId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var legacyImage = new PropertyImage
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            ImageUrl = "https://legacy.example.com/cover.jpg",
            Caption = "Legacy cover",
            IsCover = true,
            SortOrder = 0,
            CreatedAt = now,
            RoomingHouse = house
        };
        var mediaImage = new PropertyImage
        {
            Id = mediaAsset.LinkedEntityId!.Value,
            RoomingHouseId = house.Id,
            MediaAssetId = mediaAsset.Id,
            ImageUrl = PublicMediaPathBuilder.Build(mediaAsset.Id),
            Caption = "Media cover",
            IsCover = true,
            SortOrder = 1,
            CreatedAt = now,
            RoomingHouse = house,
            MediaAsset = mediaAsset
        };

        house.Images.Add(legacyImage);
        house.Images.Add(mediaImage);
        house.Rooms.Add(room);

        _fixture.Context.AdministrativeProvinces.Add(province);
        _fixture.Context.AdministrativeWards.Add(ward);
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.Add(mediaAsset);
        _fixture.Context.PropertyImages.AddRange(legacyImage, mediaImage);
        await _fixture.Context.SaveChangesAsync();

        return mediaAsset.Id;
    }

    private RoomingHouseQueryService CreateService()
    {
        return new RoomingHouseQueryService(
            _fixture.Context,
            new PassthroughSearchParser(),
            [],
            new ZeroBehaviorScoreScorer(),
            new NoopReranker(),
            new NoopVietMapService(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RoomingHouseQueryService>.Instance);
    }

    private sealed class PassthroughSearchParser : IRoomingHouseSearchParser
    {
        public ParsedRoomingHouseSearchCriteria Parse(RoomingHouseSearchRequest request)
            => ParsedRoomingHouseSearchCriteria.FromRequest(request);
    }

    private sealed class ZeroBehaviorScoreScorer : IRoomingHouseRecommendationScorer
    {
        public int CalculateBehaviorScore(RoomingHouseSearchCandidate candidate) => 0;
    }

    private sealed class NoopReranker : IRoomingHouseRecommendationReranker
    {
        public Task<RoomingHouseRecommendationRerankResult?> RerankAsync(
            GuestRoomingHouseRecommendationRequest request,
            IReadOnlyList<RoomingHouseRecommendationCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<RoomingHouseRecommendationRerankResult?>(null);
        }
    }

    private sealed class NoopVietMapService : IVietMapService
    {
        public Task<LocationSearchResponse> SearchAddressAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<LocationSuggestionResponse>> SuggestAddressesAsync(
            string text,
            int limit = 5,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<NearbyPlaceResponse>> SearchNearbyPlacesAsync(
            decimal latitude,
            decimal longitude,
            string keyword,
            int radiusMeters = 1500,
            int limit = 6,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
