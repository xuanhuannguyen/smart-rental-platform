using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public partial class RoomingHouseQueryService
{
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

        ValidatePriceBounds(criteria.MinPrice, criteria.MaxPrice);
        ValidateAreaBounds(criteria.MinArea, criteria.MaxArea);
        NormalizeSearchRanges(criteria);
    }

    private static void ValidatePriceBounds(decimal? minPrice, decimal? maxPrice)
    {
        if (minPrice is < 0m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giá thuê tối thiểu không được âm.",
                new { field = nameof(minPrice) });
        }

        if (maxPrice is < 0m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giá thuê tối đa không được âm.",
                new { field = nameof(maxPrice) });
        }

        if (minPrice is not null && minPrice < 500_000m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giá thuê tối thiểu phải từ 500.000₫ trở lên.",
                new { field = nameof(minPrice), value = minPrice });
        }

        if (maxPrice is not null && maxPrice > 30_000_000m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giá thuê tối đa không được vượt quá 30.000.000₫.",
                new { field = nameof(maxPrice), value = maxPrice });
        }
    }

    private static void ValidateAreaBounds(decimal? minArea, decimal? maxArea)
    {
        if (minArea is < 0m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Diện tích tối thiểu không được âm.",
                new { field = nameof(minArea) });
        }

        if (maxArea is < 0m)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Diện tích tối đa không được âm.",
                new { field = nameof(maxArea) });
        }

        if (minArea is not null && maxArea is not null && minArea > maxArea)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Diện tích tối thiểu không được lớn hơn diện tích tối đa.",
                new { field = nameof(minArea), minArea, maxArea });
        }
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

    private static string? NormalizeEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ScoredCandidate? BuildSearchProjection(
        RoomingHouseSearchCandidateData candidate,
        ParsedRoomingHouseSearchCriteria criteria,
        string normalizedKeyword)
    {
        // Filter rooms matching search criteria in memory
        var matchingRooms = candidate.AvailableRooms
            .Where(room => RoomMatchesSearchCriteria(room, criteria))
            .ToList();

        if (matchingRooms.Count == 0)
        {
            return null;
        }

        var activePrices = matchingRooms
            .SelectMany(x => x.ActivePrices)
            .ToList();

        var areas = matchingRooms
            .Where(x => x.AreaM2 is not null)
            .Select(x => x.AreaM2!.Value)
            .ToList();

        decimal? distanceKm = null;
        if (criteria.CenterLat is not null &&
            criteria.CenterLng is not null &&
            candidate.Latitude is not null &&
            candidate.Longitude is not null)
        {
            distanceKm = GeoSearchHelper.CalculateDistanceKm(
                criteria.CenterLat.Value,
                criteria.CenterLng.Value,
                candidate.Latitude.Value,
                candidate.Longitude.Value);
        }

        var keywordScore = CalculateKeywordScore(candidate, matchingRooms, normalizedKeyword);
        var ruleScore = CalculateRelevanceScore(candidate, matchingRooms, criteria);
        var behaviorScore = recommendationScorer.CalculateBehaviorScore(
            new RoomingHouseSearchCandidate(candidate, criteria));

        return new ScoredCandidate(
            candidate.Id,
            ruleScore + keywordScore + behaviorScore,
            keywordScore,
            distanceKm,
            activePrices.Count == 0 ? null : activePrices.Min(),
            activePrices.Count == 0 ? null : activePrices.Max(),
            areas.Count == 0 ? null : areas.Min(),
            areas.Count == 0 ? null : areas.Max(),
            candidate.CreatedAt);
    }

    private static int CalculateRelevanceScore(
        RoomingHouseSearchCandidateData candidate,
        List<CandidateRoom> matchingRooms,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var score = 0;

        score += CalculateLocationScore(candidate, criteria);
        score += CalculatePriceScore(matchingRooms, criteria);
        score += CalculateAmenityScore(candidate, matchingRooms, criteria);
        score += CalculateFreshnessScore(candidate.UpdatedAt);
        score += CalculateTrustScore(candidate);
        score += CalculateImageScore(candidate, matchingRooms);
        score -= CalculateQualityPenalty(candidate);

        return score;
    }

    private static int CalculateKeywordScore(
        RoomingHouseSearchCandidateData candidate,
        List<CandidateRoom> matchingRooms,
        string normalizedKeyword)
    {
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return 0;
        }

        var score = 0;
        var searchableHouseName = RoomingHouseSearchParser.Normalize(candidate.Name);
        var searchableAddress = RoomingHouseSearchParser.Normalize(BuildAddressDisplay(candidate));
        var searchableDescription = RoomingHouseSearchParser.Normalize(candidate.Description ?? string.Empty);
        var searchableHouseAmenities = RoomingHouseSearchParser.Normalize(
            string.Join(" ", candidate.HouseAmenities.Select(x => x.Name)));
        var searchableRoomAmenities = RoomingHouseSearchParser.Normalize(
            string.Join(" ", matchingRooms.SelectMany(x => x.RoomAmenities).Select(x => x.Name)));
        var searchableRoomNumbers = RoomingHouseSearchParser.Normalize(
            string.Join(" ", matchingRooms.Select(x => x.RoomNumber)));

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

    private static int CalculateLocationScore(RoomingHouseSearchCandidateData candidate, ParsedRoomingHouseSearchCriteria criteria)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(criteria.WardCode) && candidate.WardCode == criteria.WardCode)
        {
            score += 50;
        }

        if (!string.IsNullOrWhiteSpace(criteria.ProvinceCode) && candidate.ProvinceCode == criteria.ProvinceCode)
        {
            score += 25;
        }

        if (criteria.RadiusKm is not null && criteria.CenterLat is not null && criteria.CenterLng is not null)
        {
            var distanceKm = candidate.Latitude is null || candidate.Longitude is null
                ? (decimal?)null
                : GeoSearchHelper.CalculateDistanceKm(
                    criteria.CenterLat.Value,
                    criteria.CenterLng.Value,
                    candidate.Latitude.Value,
                    candidate.Longitude.Value);

            if (distanceKm is not null)
            {
                score += distanceKm <= 1m ? 35 : distanceKm <= 3m ? 26 : distanceKm <= 5m ? 18 : 8;
            }
        }

        return score;
    }

    private static int CalculatePriceScore(List<CandidateRoom> matchingRooms, ParsedRoomingHouseSearchCriteria criteria)
    {
        var activePrices = matchingRooms
            .SelectMany(room => room.ActivePrices)
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
        RoomingHouseSearchCandidateData candidate,
        List<CandidateRoom> matchingRooms,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var matchedHouseAmenities = criteria.AmenityIds.Count(id =>
            candidate.HouseAmenities.Any(amenity => amenity.Id == id));

        var matchedRoomAmenities = criteria.RoomAmenityIds.Count(id =>
            matchingRooms.Any(room => room.RoomAmenities.Any(amenity => amenity.Id == id)));

        return (matchedHouseAmenities + matchedRoomAmenities) * 14;
    }

    private static int CalculateFreshnessScore(DateTimeOffset updatedAt)
    {
        var ageDays = (DateTimeOffset.UtcNow - updatedAt).TotalDays;
        return ageDays <= 7 ? 20 : ageDays <= 30 ? 12 : ageDays <= 60 ? 6 : 0;
    }

    private static int CalculateTrustScore(RoomingHouseSearchCandidateData candidate)
    {
        var score = 15;
        if (candidate.HasVerifiedKyc)
        {
            score += 20;
        }

        if (candidate.ReviewedAt is not null)
        {
            score += 10;
        }

        return score;
    }

    private static int CalculateImageScore(RoomingHouseSearchCandidateData candidate, List<CandidateRoom> matchingRooms)
    {
        var imageCount = candidate.ImageCount + matchingRooms.Sum(room => room.ImageCount);
        return imageCount >= 6 ? 15 : imageCount >= 3 ? 10 : imageCount > 0 ? 5 : 0;
    }

    private static int CalculateQualityPenalty(RoomingHouseSearchCandidateData candidate)
    {
        var penalty = 0;

        if (candidate.ImageCount == 0)
        {
            penalty += 30;
        }

        if (string.IsNullOrWhiteSpace(candidate.Description) || candidate.Description.Trim().Length < 50)
        {
            penalty += 15;
        }

        if (candidate.Latitude is null || candidate.Longitude is null)
        {
            penalty += 10;
        }

        return penalty;
    }

    private static bool RoomMatchesSearchCriteria(CandidateRoom room, ParsedRoomingHouseSearchCriteria criteria)
    {
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
            criteria.RoomAmenityIds.Any(id => room.RoomAmenities.All(amenity => amenity.Id != id)))
        {
            return false;
        }

        if (criteria.MinPrice is null && criteria.MaxPrice is null)
        {
            return true;
        }

        return room.ActivePrices.Any(price =>
            (criteria.MinPrice is null || price >= criteria.MinPrice) &&
            (criteria.MaxPrice is null || price <= criteria.MaxPrice));
    }

    private static List<ScoredCandidate> ApplySearchSort(
        List<ScoredCandidate> candidates,
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
            "newest" => [.. candidates.OrderByDescending(x => x.CreatedAt)],
            "priceAsc" => [.. candidates.OrderBy(x => x.MinMonthlyRent ?? decimal.MaxValue)],
            "priceDesc" => [.. candidates.OrderByDescending(x => x.MaxMonthlyRent ?? decimal.MinValue)],
            "areaAsc" => [.. candidates.OrderBy(x => x.MinAreaM2 ?? decimal.MaxValue)],
            "areaDesc" => [.. candidates.OrderByDescending(x => x.MaxAreaM2 ?? decimal.MinValue)],
            "distanceAsc" => [.. candidates.OrderBy(x => x.DistanceKm ?? decimal.MaxValue)],
            _ => [.. candidates
                .OrderByDescending(x => x.RelevanceScore)
                .ThenByDescending(x => x.CreatedAt)]
        };
    }

    private static string BuildAddressDisplay(RoomingHouseSearchCandidateData candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.WardName) &&
            !string.IsNullOrWhiteSpace(candidate.ProvinceName))
        {
            return $"{candidate.AddressLine}, {candidate.WardName}, {candidate.ProvinceName}";
        }

        return candidate.AddressDisplay;
    }

}
