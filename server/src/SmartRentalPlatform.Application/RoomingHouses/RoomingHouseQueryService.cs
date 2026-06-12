using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseQueryService : IRoomingHouseQueryService
{
    private static readonly string[] LocationNamePrefixes =
    [
        "thanh pho ",
        "tp ",
        "tinh ",
        "phuong ",
        "xa ",
        "thi tran ",
        "dac khu "
    ];

    private readonly IAppDbContext context;
    private readonly IRoomingHouseSearchParser searchParser;
    private readonly IVietMapService vietMapService;
    private readonly ILogger<RoomingHouseQueryService> logger;

    public RoomingHouseQueryService(
        IAppDbContext context,
        IRoomingHouseSearchParser searchParser,
        IVietMapService vietMapService,
        ILogger<RoomingHouseQueryService> logger)
    {
        this.context = context;
        this.searchParser = searchParser;
        this.vietMapService = vietMapService;
        this.logger = logger;
    }

    public async Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var houses = await BuildRoomingHouseQuery()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var house = houses
            .OrderBy(x => GetOnboardingPriority(x.ApprovalStatus))
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (house is null)
        {
            return new RoomingHouseOnboardingResponse
            {
                Status = RoomingHouseOnboardingStatus.None,
                HasRoomingHouse = false,
                CanCreateDraft = true,
                CanEdit = false,
                CanSubmit = false,
                CanEnterLandlordDashboard = false
            };
        }

        return new RoomingHouseOnboardingResponse
        {
            Status = house.ApprovalStatus.ToString(),
            HasRoomingHouse = true,
            CanCreateDraft = CanCreateDraft(houses),
            CanEdit = CanEditRejectedOrDraft(house),
            CanSubmit = CanSubmit(house),
            CanEnterLandlordDashboard = houses.Any(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved),
            RoomingHouseId = house.Id,
            RoomingHouse = RoomingHouseReadModelMapper.ToDetailResponse(house)
        };
    }

    public async Task<List<RoomingHouseDetailResponse>> GetPublicAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var houses = await context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.Images)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.PriceTiers)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.Images)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.RoomAmenities)
                    .ThenInclude(x => x.Amenity)
            .Where(x => x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                        x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return houses.Select(RoomingHouseReadModelMapper.ToDetailResponse).ToList();
    }

    public async Task<PagedResult<RoomingHouseSearchItemResponse>> SearchPublicAsync(
        RoomingHouseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = searchParser.Parse(request);
        await ApplyAdministrativeLocationCriteriaAsync(criteria, cancellationToken);
        ValidateSearchRequest(criteria);

        if ((criteria.CenterLat is null || criteria.CenterLng is null) &&
            !string.IsNullOrWhiteSpace(criteria.PlaceText))
        {
            var location = await vietMapService.SearchAddressAsync(criteria.PlaceText, cancellationToken);
            criteria.CenterLat = location.Latitude;
            criteria.CenterLng = location.Longitude;
            criteria.RadiusKm = GeoSearchHelper.NormalizeRadius(criteria.RadiusKm, hasPlaceText: true);
        }

        var query = context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.Images)
            .Include(x => x.Landlord)
                .ThenInclude(x => x.KycVerifications)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.PriceTiers)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.RoomAmenities)
                    .ThenInclude(x => x.Amenity)
            .Where(x => x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                        x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null));

        if (!string.IsNullOrWhiteSpace(criteria.ProvinceCode))
        {
            query = query.Where(x => x.ProvinceCode == criteria.ProvinceCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.WardCode))
        {
            query = query.Where(x => x.WardCode == criteria.WardCode);
        }

        if (criteria.MinPrice is not null)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.PriceTiers.Any(p => p.IsActive && p.MonthlyRent >= criteria.MinPrice)));
        }

        if (criteria.MaxPrice is not null)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.PriceTiers.Any(p => p.IsActive && p.MonthlyRent <= criteria.MaxPrice)));
        }

        if (criteria.MinArea is not null)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.AreaM2 >= criteria.MinArea));
        }

        if (criteria.MaxArea is not null)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.AreaM2 <= criteria.MaxArea));
        }

        if (criteria.MinOccupants is not null)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.MaxOccupants >= criteria.MinOccupants));
        }

        foreach (var amenityId in criteria.AmenityIds)
        {
            query = query.Where(x => x.RoomingHouseAmenities.Any(a => a.AmenityId == amenityId));
        }

        foreach (var amenityId in criteria.RoomAmenityIds)
        {
            query = query.Where(x => x.Rooms.Any(r =>
                r.Status == RoomStatus.Available &&
                r.DeletedAt == null &&
                r.RoomAmenities.Any(a => a.AmenityId == amenityId)));
        }

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            var box = GeoSearchHelper.BuildBoundingBox(criteria.CenterLat.Value, criteria.CenterLng.Value, criteria.RadiusKm.Value);
            query = query.Where(x =>
                x.Latitude != null &&
                x.Longitude != null &&
                x.Latitude >= box.MinLat &&
                x.Latitude <= box.MaxLat &&
                x.Longitude >= box.MinLng &&
                x.Longitude <= box.MaxLng);
        }

        var houses = await query.ToListAsync(cancellationToken);
        var normalizedKeyword = RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty);

        var projected = houses
            .Select(house => BuildSearchProjection(house, criteria, normalizedKeyword))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            projected = projected
                .Where(x => x.KeywordScore > 0)
                .ToList();
        }

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            projected = projected
                .Where(x => x.Item.DistanceKm <= criteria.RadiusKm)
                .ToList();
        }

        projected = ApplySearchSort(projected, criteria).ToList();

        var totalItems = projected.Count;
        LogSearchParse(request, criteria, totalItems);
        var pageItems = projected
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => x.Item)
            .ToList();

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = pageItems,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)criteria.PageSize)
        };
    }

    private void LogSearchParse(
        RoomingHouseSearchRequest request,
        ParsedRoomingHouseSearchCriteria criteria,
        int resultCount)
    {
        var entry = new
        {
            RawQuery = request.Q ?? string.Empty,
            RemainingKeyword = criteria.Keyword ?? string.Empty,
            criteria.ProvinceCode,
            criteria.WardCode,
            criteria.MinPrice,
            criteria.MaxPrice,
            criteria.MinArea,
            criteria.MaxArea,
            criteria.MinOccupants,
            criteria.AmenityIds,
            criteria.RoomAmenityIds,
            criteria.CenterLat,
            criteria.CenterLng,
            criteria.RadiusKm,
            criteria.Sort,
            ResultCount = resultCount,
            ZeroResult = resultCount == 0
        };

        logger.LogInformation("SearchParsed {@Entry}", entry);
        if (resultCount == 0 && !string.IsNullOrWhiteSpace(request.Q))
        {
            logger.LogWarning(
                "ZeroResultQuery {RawQuery} RemainingKeyword {RemainingKeyword}",
                request.Q,
                criteria.Keyword);
        }
    }

    public async Task<RoomingHouseDetailResponse?> GetPublicByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var house = await BuildRoomingHouseQuery()
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.DeletedAt == null &&
                     x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                     x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible,
                cancellationToken);

        return house is null ? null : RoomingHouseReadModelMapper.ToDetailResponse(house);
    }

    public async Task<List<RoomingHouseResponse>> GetByLandlordAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var houses = await BuildRoomingHouseQuery()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return houses.Select(RoomingHouseReadModelMapper.ToResponse).ToList();
    }

    public async Task<RoomingHouseDetailResponse?> GetByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var house = await BuildRoomingHouseQuery()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        return house is null ? null : RoomingHouseReadModelMapper.ToDetailResponse(house);
    }

    private static void ValidateSearchRequest(ParsedRoomingHouseSearchCriteria criteria)
    {
        if (criteria.Page < 1)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Trang tìm kiếm phải lớn hơn hoặc bằng 1.",
                new { field = nameof(criteria.Page) });
        }

        if (criteria.PageSize is < 1 or > 48)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Kích thước trang phải nằm trong khoảng từ 1 đến 48.",
                new { field = nameof(criteria.PageSize) });
        }

        GeoSearchHelper.ValidateCoordinates(criteria.CenterLat, criteria.CenterLng);
        criteria.RadiusKm = GeoSearchHelper.NormalizeRadius(
            criteria.RadiusKm,
            hasPlaceText: !string.IsNullOrWhiteSpace(criteria.PlaceText));
        NormalizeSearchRanges(criteria);
    }

    private async Task ApplyAdministrativeLocationCriteriaAsync(
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return;
        }

        var provinceMatch = await FindProvinceMatchAsync(normalizedKeyword, cancellationToken);
        if (provinceMatch is not null)
        {
            criteria.ProvinceCode ??= provinceMatch.Code;
            criteria.Keyword = RemoveSearchPhrase(normalizedKeyword, provinceMatch.Alias);
            normalizedKeyword = RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return;
        }

        var wardMatch = await FindWardMatchAsync(normalizedKeyword, criteria.ProvinceCode, cancellationToken);
        if (wardMatch is not null)
        {
            criteria.WardCode ??= wardMatch.Code;
            criteria.ProvinceCode ??= wardMatch.ProvinceCode;
            criteria.Keyword = RemoveSearchPhrase(normalizedKeyword, wardMatch.Alias);
        }
    }

    private async Task<SearchLocationMatch?> FindProvinceMatchAsync(
        string normalizedKeyword,
        CancellationToken cancellationToken)
    {
        var provinces = await context.AdministrativeProvinces
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Code, x.Name })
            .ToListAsync(cancellationToken);

        return provinces
            .SelectMany(province => BuildLocationAliases(province.Name)
                .Select(alias => new SearchLocationMatch(province.Code, null, alias)))
            .Where(match => ContainsSearchPhrase(normalizedKeyword, match.Alias))
            .OrderByDescending(match => match.Alias.Length)
            .FirstOrDefault();
    }

    private async Task<SearchLocationMatch?> FindWardMatchAsync(
        string normalizedKeyword,
        string? provinceCode,
        CancellationToken cancellationToken)
    {
        var wardsQuery = context.AdministrativeWards
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            wardsQuery = wardsQuery.Where(x => x.ProvinceCode == provinceCode);
        }

        var wards = await wardsQuery
            .Select(x => new { x.Code, x.ProvinceCode, x.Name })
            .ToListAsync(cancellationToken);

        var matches = wards
            .SelectMany(ward => BuildLocationAliases(ward.Name)
                .Where(alias => alias.Length >= 4)
                .Select(alias => new SearchLocationMatch(ward.Code, ward.ProvinceCode, alias)))
            .Where(match => ContainsSearchPhrase(normalizedKeyword, match.Alias))
            .OrderByDescending(match => match.Alias.Length)
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        var longestAlias = matches[0].Alias;
        var topMatches = matches
            .Where(match => match.Alias == longestAlias)
            .ToList();

        return !string.IsNullOrWhiteSpace(provinceCode) || topMatches.Select(x => x.Code).Distinct().Count() == 1
            ? topMatches[0]
            : null;
    }

    private static IEnumerable<string> BuildLocationAliases(string name)
    {
        var normalizedName = RoomingHouseSearchParser.Normalize(name);
        var aliases = new HashSet<string>(StringComparer.Ordinal)
        {
            normalizedName
        };

        foreach (var prefix in LocationNamePrefixes)
        {
            if (normalizedName.StartsWith(prefix, StringComparison.Ordinal))
            {
                aliases.Add(normalizedName[prefix.Length..].Trim());
            }
        }

        return aliases.Where(alias => alias.Length >= 3);
    }

    private static void NormalizeSearchRanges(ParsedRoomingHouseSearchCriteria criteria)
    {
        criteria.MinPrice = NormalizeRentalPrice(criteria.MinPrice);
        criteria.MaxPrice = NormalizeRentalPrice(criteria.MaxPrice);

        if (criteria.MinPrice is not null && criteria.MaxPrice is not null && criteria.MinPrice > criteria.MaxPrice)
        {
            (criteria.MinPrice, criteria.MaxPrice) = (criteria.MaxPrice, criteria.MinPrice);
        }

        if (criteria.MinArea is not null && criteria.MaxArea is not null && criteria.MinArea > criteria.MaxArea)
        {
            (criteria.MinArea, criteria.MaxArea) = (criteria.MaxArea, criteria.MinArea);
        }
    }

    private static decimal? NormalizeRentalPrice(decimal? value)
    {
        if (value is null or <= 0m)
        {
            return value;
        }

        return value < 100_000m ? value * 1_000_000m : value;
    }

    private static RoomingHouseSearchProjection? BuildSearchProjection(
        RoomingHouse house,
        ParsedRoomingHouseSearchCriteria criteria,
        string normalizedKeyword)
    {
        var availableRooms = house.Rooms
            .Where(room => RoomMatchesSearchCriteria(room, criteria))
            .ToList();

        if (availableRooms.Count == 0)
        {
            return null;
        }

        var activePrices = availableRooms
            .SelectMany(x => x.PriceTiers)
            .Where(x => x.IsActive)
            .Select(x => x.MonthlyRent)
            .ToList();

        var areas = availableRooms
            .Where(x => x.AreaM2 is not null)
            .Select(x => x.AreaM2!.Value)
            .ToList();

        decimal? distanceKm = null;
        if (criteria.CenterLat is not null &&
            criteria.CenterLng is not null &&
            house.Latitude is not null &&
            house.Longitude is not null)
        {
            distanceKm = GeoSearchHelper.CalculateDistanceKm(
                criteria.CenterLat.Value,
                criteria.CenterLng.Value,
                house.Latitude.Value,
                house.Longitude.Value);
        }

        var item = new RoomingHouseSearchItemResponse
        {
            Id = house.Id,
            Name = house.Name,
            AddressDisplay = BuildAddressDisplay(house),
            Latitude = house.Latitude,
            Longitude = house.Longitude,
            DistanceKm = distanceKm,
            CoverImageUrl = house.Images.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.IsCover)?.ImageUrl
                ?? house.Images.OrderBy(x => x.SortOrder).FirstOrDefault()?.ImageUrl,
            AvailableRooms = availableRooms.Count,
            TotalRooms = house.Rooms.Count(x => x.DeletedAt == null),
            MinMonthlyRent = activePrices.Count == 0 ? null : activePrices.Min(),
            MaxMonthlyRent = activePrices.Count == 0 ? null : activePrices.Max(),
            MinAreaM2 = areas.Count == 0 ? null : areas.Min(),
            MaxAreaM2 = areas.Count == 0 ? null : areas.Max(),
            Amenities = house.RoomingHouseAmenities
                .Select(x => new AmenityResponse
                {
                    Id = x.Amenity.Id,
                    Name = x.Amenity.Name,
                    Scope = x.Amenity.Scope.ToString(),
                    IconCode = x.Amenity.IconCode
                })
                .ToList(),
            CreatedAt = house.CreatedAt
        };

        var keywordScore = CalculateKeywordScore(house, availableRooms, normalizedKeyword);
        var ruleScore = CalculateRelevanceScore(house, availableRooms, criteria);

        return new RoomingHouseSearchProjection(item, ruleScore + keywordScore, keywordScore);
    }

    private static int CalculateRelevanceScore(
        RoomingHouse house,
        List<Room> availableRooms,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var score = 0;

        score += CalculateLocationScore(house, criteria);
        score += CalculatePriceScore(availableRooms, criteria);
        score += CalculateAmenityScore(house, availableRooms, criteria);
        score += CalculateFreshnessScore(house.UpdatedAt);
        score += CalculateTrustScore(house);
        score += CalculateImageScore(house, availableRooms);
        score -= CalculateQualityPenalty(house);

        return score;
    }

    private static int CalculateKeywordScore(RoomingHouse house, List<Room> availableRooms, string normalizedKeyword)
    {
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return 0;
        }

        var score = 0;
        var searchableHouseName = RoomingHouseSearchParser.Normalize(house.Name);
        var searchableAddress = RoomingHouseSearchParser.Normalize(BuildAddressDisplay(house));
        var searchableDescription = RoomingHouseSearchParser.Normalize(house.Description ?? string.Empty);
        var searchableHouseAmenities = RoomingHouseSearchParser.Normalize(
            string.Join(" ", house.RoomingHouseAmenities.Select(x => x.Amenity.Name)));
        var searchableRoomAmenities = RoomingHouseSearchParser.Normalize(
            string.Join(" ", availableRooms.SelectMany(x => x.RoomAmenities).Select(x => x.Amenity.Name)));
        var searchableRoomNumbers = RoomingHouseSearchParser.Normalize(
            string.Join(" ", availableRooms.Select(x => x.RoomNumber)));

        if (ContainsSearchPhrase(searchableHouseName, normalizedKeyword))
        {
            score += 35;
        }

        if (ContainsSearchPhrase(searchableAddress, normalizedKeyword))
        {
            score += 25;
        }

        if (ContainsSearchPhrase(searchableDescription, normalizedKeyword))
        {
            score += 12;
        }

        if (ContainsSearchPhrase(searchableHouseAmenities, normalizedKeyword))
        {
            score += 10;
        }

        if (ContainsSearchPhrase(searchableRoomAmenities, normalizedKeyword))
        {
            score += 10;
        }

        if (ContainsSearchPhrase(searchableRoomNumbers, normalizedKeyword))
        {
            score += 8;
        }

        var tokens = normalizedKeyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2)
            .Distinct()
            .ToList();
        foreach (var token in tokens)
        {
            if (ContainsSearchPhrase(searchableHouseName, token))
            {
                score += 8;
            }

            if (ContainsSearchPhrase(searchableAddress, token))
            {
                score += 6;
            }

            if (ContainsSearchPhrase(searchableDescription, token))
            {
                score += 3;
            }
        }

        return score;
    }

    private static bool ContainsSearchPhrase(string searchableText, string normalizedKeyword)
    {
        if (string.IsNullOrWhiteSpace(searchableText) || string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return false;
        }

        return Regex.IsMatch(
            searchableText,
            $@"(^|\s){Regex.Escape(normalizedKeyword)}(\s|$)",
            RegexOptions.CultureInvariant);
    }

    private static string RemoveSearchPhrase(string normalizedText, string normalizedPhrase)
    {
        if (string.IsNullOrWhiteSpace(normalizedText) || string.IsNullOrWhiteSpace(normalizedPhrase))
        {
            return normalizedText;
        }

        var removed = Regex.Replace(
            normalizedText,
            $@"(^|\s){Regex.Escape(normalizedPhrase)}(\s|$)",
            " ",
            RegexOptions.CultureInvariant);

        return Regex.Replace(removed, @"\s+", " ").Trim();
    }

    private static int CalculateLocationScore(RoomingHouse house, ParsedRoomingHouseSearchCriteria criteria)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(criteria.WardCode) && house.WardCode == criteria.WardCode)
        {
            score += 50;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ProvinceCode) && house.ProvinceCode == criteria.ProvinceCode)
        {
            score += 25;
        }

        if (criteria.RadiusKm is not null && criteria.CenterLat is not null && criteria.CenterLng is not null)
        {
            var distanceKm = house.Latitude is null || house.Longitude is null
                ? (decimal?)null
                : GeoSearchHelper.CalculateDistanceKm(
                    criteria.CenterLat.Value,
                    criteria.CenterLng.Value,
                    house.Latitude.Value,
                    house.Longitude.Value);

            if (distanceKm is not null)
            {
                score += distanceKm <= 1m ? 35 : distanceKm <= 3m ? 26 : distanceKm <= 5m ? 18 : 8;
            }
        }

        return score;
    }

    private static int CalculatePriceScore(List<Room> availableRooms, ParsedRoomingHouseSearchCriteria criteria)
    {
        var activePrices = availableRooms
            .SelectMany(room => room.PriceTiers)
            .Where(price => price.IsActive)
            .Select(price => price.MonthlyRent)
            .ToList();

        if (activePrices.Count == 0)
        {
            return 0;
        }

        if (criteria.MinPrice is null && criteria.MaxPrice is null)
        {
            return 10;
        }

        var min = criteria.MinPrice ?? 0m;
        var max = criteria.MaxPrice ?? decimal.MaxValue;
        var matchedPrices = activePrices.Where(price => price >= min && price <= max).ToList();
        if (matchedPrices.Count == 0)
        {
            return 0;
        }

        var score = 30;
        if (criteria.MaxPrice is not null)
        {
            var cheapest = matchedPrices.Min();
            var budgetRoom = criteria.MaxPrice.Value - cheapest;
            if (budgetRoom >= criteria.MaxPrice.Value * 0.15m)
            {
                score += 8;
            }
        }

        if (criteria.MinPrice is not null && criteria.MaxPrice is not null)
        {
            var middle = (criteria.MinPrice.Value + criteria.MaxPrice.Value) / 2m;
            var closestPrice = matchedPrices
                .OrderBy(price => Math.Abs(price - middle))
                .First();
            if (Math.Abs(closestPrice - middle) <= middle * 0.2m)
            {
                score += 6;
            }
        }

        return score;
    }

    private static int CalculateAmenityScore(
        RoomingHouse house,
        List<Room> availableRooms,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var matchedHouseAmenities = criteria.AmenityIds.Count(id =>
            house.RoomingHouseAmenities.Any(amenity => amenity.AmenityId == id));

        var matchedRoomAmenities = criteria.RoomAmenityIds.Count(id =>
            availableRooms.Any(room => room.RoomAmenities.Any(amenity => amenity.AmenityId == id)));

        return (matchedHouseAmenities + matchedRoomAmenities) * 14;
    }

    private static int CalculateFreshnessScore(DateTimeOffset updatedAt)
    {
        var ageDays = (DateTimeOffset.UtcNow - updatedAt).TotalDays;
        return ageDays <= 7 ? 20 : ageDays <= 30 ? 12 : ageDays <= 60 ? 6 : 0;
    }

    private static int CalculateTrustScore(RoomingHouse house)
    {
        var score = 15;
        if (house.Landlord.KycVerifications.Any(x => x.Status == KycVerificationStatus.Approved))
        {
            score += 20;
        }

        if (house.ReviewedAt is not null)
        {
            score += 10;
        }

        return score;
    }

    private static int CalculateImageScore(RoomingHouse house, List<Room> availableRooms)
    {
        var imageCount = house.Images.Count + availableRooms.Sum(room => room.Images.Count);
        return imageCount >= 6 ? 15 : imageCount >= 3 ? 10 : imageCount > 0 ? 5 : 0;
    }

    private static int CalculateQualityPenalty(RoomingHouse house)
    {
        var penalty = 0;

        if (house.Images.Count == 0)
        {
            penalty += 30;
        }

        if (string.IsNullOrWhiteSpace(house.Description) || house.Description.Trim().Length < 50)
        {
            penalty += 15;
        }

        if (house.Latitude is null || house.Longitude is null)
        {
            penalty += 10;
        }

        return penalty;
    }

    private static bool RoomMatchesSearchCriteria(Room room, ParsedRoomingHouseSearchCriteria criteria)
    {
        if (room.Status != RoomStatus.Available || room.DeletedAt is not null)
        {
            return false;
        }

        if (criteria.MinArea is not null && (room.AreaM2 is null || room.AreaM2 < criteria.MinArea))
        {
            return false;
        }

        if (criteria.MaxArea is not null && (room.AreaM2 is null || room.AreaM2 > criteria.MaxArea))
        {
            return false;
        }

        if (criteria.MinOccupants is not null && room.MaxOccupants < criteria.MinOccupants)
        {
            return false;
        }

        if (criteria.RoomAmenityIds.Count > 0 &&
            criteria.RoomAmenityIds.Any(id => room.RoomAmenities.All(amenity => amenity.AmenityId != id)))
        {
            return false;
        }

        if (criteria.MinPrice is null && criteria.MaxPrice is null)
        {
            return true;
        }

        return room.PriceTiers
            .Where(price => price.IsActive)
            .Any(price =>
                (criteria.MinPrice is null || price.MonthlyRent >= criteria.MinPrice) &&
                (criteria.MaxPrice is null || price.MonthlyRent <= criteria.MaxPrice));
    }

    private static IEnumerable<RoomingHouseSearchProjection> ApplySearchSort(
        IEnumerable<RoomingHouseSearchProjection> projected,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var sort = criteria.Sort.Trim();
        if (sort == "relevance" &&
            criteria.CenterLat is not null &&
            criteria.CenterLng is not null &&
            criteria.RadiusKm is not null)
        {
            sort = "distanceAsc";
        }

        return sort switch
        {
            "newest" => projected.OrderByDescending(x => x.Item.CreatedAt),
            "priceAsc" => projected.OrderBy(x => x.Item.MinMonthlyRent ?? decimal.MaxValue),
            "priceDesc" => projected.OrderByDescending(x => x.Item.MaxMonthlyRent ?? decimal.MinValue),
            "areaAsc" => projected.OrderBy(x => x.Item.MinAreaM2 ?? decimal.MaxValue),
            "areaDesc" => projected.OrderByDescending(x => x.Item.MaxAreaM2 ?? decimal.MinValue),
            "distanceAsc" => projected.OrderBy(x => x.Item.DistanceKm ?? decimal.MaxValue),
            _ => projected
                .OrderByDescending(x => x.RelevanceScore)
                .ThenByDescending(x => x.Item.CreatedAt)
        };
    }

    private static string BuildAddressDisplay(RoomingHouse house)
    {
        if (!string.IsNullOrWhiteSpace(house.Ward?.Name) &&
            !string.IsNullOrWhiteSpace(house.Province?.Name))
        {
            return $"{house.AddressLine}, {house.Ward.Name}, {house.Province.Name}";
        }

        return house.AddressDisplay;
    }

    private IQueryable<RoomingHouse> BuildRoomingHouseQuery()
    {
        return context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.LegalDocument)
            .Include(x => x.HouseRule)
            .Include(x => x.RentalPolicy)
            .Include(x => x.Images)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.PriceTiers)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.Images)
            .Include(x => x.Rooms)
                .ThenInclude(x => x.RoomAmenities)
                    .ThenInclude(x => x.Amenity)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity);
    }

    private static int GetOnboardingPriority(RoomingHouseApprovalStatus status)
    {
        return status switch
        {
            RoomingHouseApprovalStatus.Draft => 0,
            RoomingHouseApprovalStatus.Rejected => 1,
            RoomingHouseApprovalStatus.Pending => 2,
            RoomingHouseApprovalStatus.Approved => 3,
            _ => 4
        };
    }

    private static bool CanEditRejectedOrDraft(RoomingHouse house)
    {
        return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanSubmit(RoomingHouse house)
    {
        return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanCreateDraft(IEnumerable<RoomingHouse> houses)
    {
        return !houses.Any(x =>
            x.ApprovalStatus is RoomingHouseApprovalStatus.Draft
                or RoomingHouseApprovalStatus.Pending
                or RoomingHouseApprovalStatus.Rejected);
    }

    private sealed record SearchLocationMatch(string Code, string? ProvinceCode, string Alias);

    private sealed record RoomingHouseSearchProjection(
        RoomingHouseSearchItemResponse Item,
        int RelevanceScore,
        int KeywordScore);
}
