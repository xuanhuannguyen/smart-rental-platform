using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.UnitTests.RoomingHouses.Search;

public class RoomingHouseSearchUtilityTests
{
    [Fact]
    public void Parse_WhenQueryHasStructuredTerms_ExtractsCriteriaAndKeyword()
    {
        var parser = new RoomingHouseSearchParser();

        var result = parser.Parse(new RoomingHouseSearchRequest
        {
            Q = "Tìm phòng gần Đại học FPT Đà Nẵng bán kính 2.5km giá 2tr5-4tr từ 20m2 đến 35m2 cho 2 người có wifi máy lạnh",
            Sort = " newest ",
            AmenityIds = [1, 1, 2],
            RoomAmenityIds = [3, 3]
        });

        Assert.Equal("48", result.ProvinceCode);
        Assert.Equal("Đại học FPT Đà Nẵng", result.PlaceText);
        Assert.Equal(2_500_000m, result.MinPrice);
        Assert.Equal(4_000_000m, result.MaxPrice);
        Assert.Equal(20m, result.MinArea);
        Assert.Equal(35m, result.MaxArea);
        Assert.Equal(2, result.MinOccupants);
        Assert.Equal(2.5m, result.RadiusKm);
        Assert.Equal("newest", result.Sort);
        Assert.Equal([1, 2], result.AmenityIds.Take(2));
        Assert.Contains(result.RoomAmenityIds, id => id > 0);
        Assert.DoesNotContain("wifi", result.Keyword ?? string.Empty);
    }

    [Fact]
    public void Parse_WhenRequestAlreadyHasValues_DoesNotOverrideExplicitFilters()
    {
        var parser = new RoomingHouseSearchParser();

        var result = parser.Parse(new RoomingHouseSearchRequest
        {
            Q = "phòng ở Hà Nội dưới 3 triệu",
            ProvinceCode = "48",
            MaxPrice = 2_000_000m,
            Page = 2,
            PageSize = 5
        });

        Assert.Equal("48", result.ProvinceCode);
        Assert.Equal(2_000_000m, result.MaxPrice);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
    }

    [Theory]
    [InlineData("Sai Gon", "sai gon")]
    [InlineData("Đà Nẵng", "da nang")]
    [InlineData("  Phòng   trọ  ", "phong tro")]
    public void Normalize_RemovesDiacriticsAndCompactsWhitespace(string input, string expected)
    {
        Assert.Equal(expected, RoomingHouseSearchParser.Normalize(input));
    }

    [Fact]
    public void GeoSearchHelper_CalculatesDistanceAndBoundingBox()
    {
        var distance = GeoSearchHelper.CalculateDistanceKm(16.071m, 108.225m, 16.075m, 108.23m);
        var box = GeoSearchHelper.BuildBoundingBox(16.071m, 108.225m, 3m);

        Assert.InRange(distance, 0.6m, 0.8m);
        Assert.True(box.MinLat < 16.071m);
        Assert.True(box.MaxLat > 16.071m);
        Assert.True(box.MinLng < 108.225m);
        Assert.True(box.MaxLng > 108.225m);
    }

    [Fact]
    public void GeoSearchHelper_ValidateCoordinates_ThrowsForInvalidLatitudeLongitudeOrPartialPair()
    {
        Assert.Throws<BadRequestException>(() => GeoSearchHelper.ValidateCoordinates(-91m, 108m));
        Assert.Throws<BadRequestException>(() => GeoSearchHelper.ValidateCoordinates(16m, 181m));
        Assert.Throws<BadRequestException>(() => GeoSearchHelper.ValidateCoordinates(16m, null));
    }

    [Fact]
    public void GeoSearchHelper_NormalizeRadius_AppliesDefaultAndValidatesRange()
    {
        Assert.Null(GeoSearchHelper.NormalizeRadius(null, hasPlaceText: false));
        Assert.Equal(GeoSearchHelper.DefaultRadiusKm, GeoSearchHelper.NormalizeRadius(null, hasPlaceText: true));
        Assert.Equal(5m, GeoSearchHelper.NormalizeRadius(5m, hasPlaceText: true));
        Assert.Throws<BadRequestException>(() => GeoSearchHelper.NormalizeRadius(0.1m, hasPlaceText: true));
        Assert.Throws<BadRequestException>(() => GeoSearchHelper.NormalizeRadius(31m, hasPlaceText: true));
    }

    [Fact]
    public void RuleBasedScorer_AppliesRecentPenaltyAndPreferenceBonuses()
    {
        var houseId = Guid.NewGuid();
        var scorer = new RuleBasedRoomingHouseRecommendationScorer();
        var candidate = new RoomingHouseSearchCandidate(
            new RoomingHouseSearchCandidateData
            {
                Id = houseId,
                ImageCount = 3,
                HasVerifiedKyc = true,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                HouseAmenities =
                [
                    new CandidateAmenity { Id = 1, Name = "Camera" },
                    new CandidateAmenity { Id = 2, Name = "Parking" }
                ],
                AvailableRooms =
                [
                    BuildRoom(3),
                    BuildRoom(3),
                    BuildRoom(4),
                    BuildRoom(5)
                ]
            },
            new ParsedRoomingHouseSearchCriteria
            {
                RecentRoomingHouseIds = [Guid.NewGuid(), houseId],
                PreferredAmenityIds = [1, 99],
                PreferredRoomAmenityIds = [3, 5]
            });

        var score = scorer.CalculateBehaviorScore(candidate);

        Assert.Equal(62, score);
    }

    [Fact]
    public void RuleBasedScorer_GivesSmallerFreshnessBonusForOlderHouse()
    {
        var scorer = new RuleBasedRoomingHouseRecommendationScorer();
        var candidate = new RoomingHouseSearchCandidate(
            new RoomingHouseSearchCandidateData
            {
                Id = Guid.NewGuid(),
                ImageCount = 1,
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-20)
            },
            new ParsedRoomingHouseSearchCriteria());

        var score = scorer.CalculateBehaviorScore(candidate);

        Assert.Equal(11, score);
    }

    [Fact]
    public async Task NoopIntentEnricher_CompletesWithoutMutatingContext()
    {
        var request = new RoomingHouseSearchRequest { Q = "query" };
        var context = new RoomingHouseSearchIntentContext(
            request,
            new QueryNormalizer().Normalize(request.Q),
            ParsedRoomingHouseSearchCriteria.FromRequest(request));
        var enricher = new NoopRoomingHouseSearchIntentEnricher();

        await enricher.EnrichAsync(context);

        Assert.False(context.Criteria.AiAssisted);
        Assert.Null(context.Criteria.InterpretedQuery);
    }

    private static CandidateRoom BuildRoom(int amenityId)
    {
        return new CandidateRoom
        {
            Id = Guid.NewGuid(),
            RoomNumber = amenityId.ToString(),
            RoomAmenities = [new CandidateAmenity { Id = amenityId, Name = $"Amenity {amenityId}" }]
        };
    }
}
