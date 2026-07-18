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
    private readonly IEnumerable<IRoomingHouseSearchIntentEnricher> searchIntentEnrichers;
    private readonly IRoomingHouseRecommendationScorer recommendationScorer;
    private readonly IRoomingHouseRecommendationReranker recommendationReranker;
    private readonly IVietMapService vietMapService;
    private readonly ILogger<RoomingHouseQueryService> logger;

    public RoomingHouseQueryService(
        IAppDbContext context,
        IRoomingHouseSearchParser searchParser,
        IEnumerable<IRoomingHouseSearchIntentEnricher> searchIntentEnrichers,
        IRoomingHouseRecommendationScorer recommendationScorer,
        IRoomingHouseRecommendationReranker recommendationReranker,
        IVietMapService vietMapService,
        ILogger<RoomingHouseQueryService> logger)
    {
        this.context = context;
        this.searchParser = searchParser;
        this.searchIntentEnrichers = searchIntentEnrichers;
        this.recommendationScorer = recommendationScorer;
        this.recommendationReranker = recommendationReranker;
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

    public async Task<List<RoomingHouseListingResponse>> GetPublicListingAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                        x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RoomingHouseListingResponse
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.Ward != null && x.Province != null
                    ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                    : x.AddressDisplay,
                CoverImageUrl = x.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                AvailableRooms = x.Rooms.Count(r => r.Status == RoomStatus.Available && r.DeletedAt == null),
                AverageRating = x.AverageRating,
                TotalReviews = x.TotalReviews,
                MinMonthlyRent = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                    .SelectMany(r => r.PriceTiers)
                    .Where(p => p.IsActive)
                    .Select(p => (decimal?)p.MonthlyRent)
                    .Min(),
                MaxMonthlyRent = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                    .SelectMany(r => r.PriceTiers)
                    .Where(p => p.IsActive)
                    .Select(p => (decimal?)p.MonthlyRent)
                    .Max(),
                MinAreaM2 = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                    .Select(r => (decimal?)r.AreaM2)
                    .Min(),
                MaxAreaM2 = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                    .Select(r => (decimal?)r.AreaM2)
                    .Max(),
                Amenities = x.RoomingHouseAmenities
                    .Select(a => new AmenityResponse
                    {
                        Id = a.Amenity.Id,
                        Name = a.Amenity.Name,
                        Scope = a.Amenity.Scope.ToString(),
                        IconCode = a.Amenity.IconCode
                    })
                    .ToList(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<RoomingHouseSearchItemResponse>> SearchPublicAsync(
        RoomingHouseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = searchParser.Parse(request);
        await PrepareSearchCriteriaAsync(criteria, cancellationToken);
        var result = await ExecutePublicSearchAsync(criteria, cancellationToken);
        SetSearchMetadata(result, request, criteria);
        LogSearchParse(request, criteria, result.TotalItems);

        if (result.TotalItems > 0 || string.IsNullOrWhiteSpace(request.Q))
        {
            return result;
        }

        try
        {
            var aiCriteria = searchParser.Parse(request);
            var normalizedQuery = new QueryNormalizer().Normalize(request.Q);
            var intentContext = new RoomingHouseSearchIntentContext(request, normalizedQuery, aiCriteria);
            foreach (var enricher in searchIntentEnrichers)
            {
                await enricher.EnrichAsync(intentContext, cancellationToken);
            }

            if (!aiCriteria.AiAssisted)
            {
                return result;
            }

            await PrepareSearchCriteriaAsync(aiCriteria, cancellationToken);
            var aiResult = await ExecutePublicSearchAsync(aiCriteria, cancellationToken);
            SetSearchMetadata(aiResult, request, aiCriteria);
            LogSearchParse(request, aiCriteria, aiResult.TotalItems);

            return aiResult.TotalItems == 0 ? result : aiResult;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI search fallback failed.");
            return result;
        }
    }

    private async Task PrepareSearchCriteriaAsync(
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
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
    }

    private async Task<PagedResult<RoomingHouseSearchItemResponse>> ExecutePublicSearchAsync(
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        // Phase 1: Build the query with WHERE filters (no .Include(), no entity loading)
        var baseQuery = BuildSearchBaseQuery(criteria);

        // Phase 2: Load ONLY the columns needed for scoring via .Select() projection.
        // This generates a single SQL query with only the required columns — no full entity graph.
        var candidateData = await baseQuery
            .Select(x => new RoomingHouseSearchCandidateData
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                AddressLine = x.AddressLine,
                Description = x.Description,
                ProvinceCode = x.ProvinceCode,
                WardCode = x.WardCode,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                UpdatedAt = x.UpdatedAt,
                CreatedAt = x.CreatedAt,
                ProvinceName = x.Province.Name,
                WardName = x.Ward.Name,
                ImageCount = x.Images.Count,
                HasVerifiedKyc = x.Landlord.KycVerifications.Any(k => k.Status == KycVerificationStatus.Approved),
                ReviewedAt = x.ReviewedAt,
                HouseAmenities = x.RoomingHouseAmenities
                    .Select(a => new CandidateAmenity
                    {
                        Id = a.Amenity.Id,
                        Name = a.Amenity.Name
                    })
                    .ToList(),
                AvailableRooms = x.Rooms
                    .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                    .Select(r => new CandidateRoom
                    {
                        Id = r.Id,
                        RoomNumber = r.RoomNumber,
                        Floor = r.Floor,
                        AreaM2 = r.AreaM2,
                        MaxOccupants = r.MaxOccupants,
                        ActivePrices = r.PriceTiers
                            .Where(p => p.IsActive)
                            .Select(p => p.MonthlyRent)
                            .ToList(),
                        RoomAmenities = r.RoomAmenities
                            .Select(ra => new CandidateAmenity
                            {
                                Id = ra.Amenity.Id,
                                Name = ra.Amenity.Name
                            })
                            .ToList(),
                        ImageCount = r.Images.Count
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var normalizedKeyword = RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty);

        // Phase 3: Score each candidate in memory on the lightweight DTO
        var scored = candidateData
            .Select(c => BuildSearchProjection(c, criteria, normalizedKeyword))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        // Phase 4: Filter by keyword score and distance, then sort
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            scored = scored
                .Where(x => x.KeywordScore > 0)
                .ToList();
        }

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            scored = scored
                .Where(x => x.DistanceKm <= criteria.RadiusKm)
                .ToList();
        }

        scored = ApplySearchSort(scored, criteria).ToList();

        var totalItems = scored.Count;
        var pagedCandidates = scored
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        // Phase 5: Load only the page's items with .Select() projection
        var pageItemIds = pagedCandidates.Select(x => x.Id).ToList();
        List<RoomingHouseSearchItemResponse> pageItems;
        if (pageItemIds.Count > 0)
        {
            // Restore DistanceKm from scored candidates
            var distanceByHouseId = pagedCandidates
                .Where(x => x.DistanceKm is not null)
                .ToDictionary(x => x.Id, x => x.DistanceKm);

            var rawItems = await context.RoomingHouses
                .AsNoTracking()
                .Where(x => pageItemIds.Contains(x.Id))
                .Select(x => new RoomingHouseSearchItemResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    AddressDisplay = x.Ward != null && x.Province != null
                        ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                        : x.AddressDisplay,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    CoverImageUrl = x.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    AvailableRooms = x.Rooms.Count(r => r.Status == RoomStatus.Available && r.DeletedAt == null),
                    TotalRooms = x.Rooms.Count(r => r.DeletedAt == null),
                    AverageRating = x.AverageRating,
                    TotalReviews = x.TotalReviews,
                    MinMonthlyRent = x.Rooms
                        .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                        .SelectMany(r => r.PriceTiers)
                        .Where(p => p.IsActive)
                        .Select(p => (decimal?)p.MonthlyRent)
                        .Min(),
                    MaxMonthlyRent = x.Rooms
                        .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                        .SelectMany(r => r.PriceTiers)
                        .Where(p => p.IsActive)
                        .Select(p => (decimal?)p.MonthlyRent)
                        .Max(),
                    MinAreaM2 = x.Rooms
                        .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                        .Select(r => (decimal?)r.AreaM2)
                        .Min(),
                    MaxAreaM2 = x.Rooms
                        .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null && r.AreaM2 != null)
                        .Select(r => (decimal?)r.AreaM2)
                        .Max(),
                    Amenities = x.RoomingHouseAmenities
                        .Select(a => new AmenityResponse
                        {
                            Id = a.Amenity.Id,
                            Name = a.Amenity.Name,
                            Scope = a.Amenity.Scope.ToString(),
                            IconCode = a.Amenity.IconCode
                        })
                        .ToList(),
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            // Restore DistanceKm into the items and re-sort to match scored order
            foreach (var item in rawItems)
            {
                if (distanceByHouseId.TryGetValue(item.Id, out var distance))
                {
                    item.DistanceKm = distance;
                }
            }

            var idOrder = pagedCandidates
                .Select((c, i) => (c.Id, Index: i))
                .ToDictionary(x => x.Id, x => x.Index);
            pageItems = rawItems
                .OrderBy(x => idOrder.GetValueOrDefault(x.Id, int.MaxValue))
                .ToList();
        }
        else
        {
            pageItems = new List<RoomingHouseSearchItemResponse>();
        }

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = pageItems,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)criteria.PageSize),
            Metadata = new RoomingHouseSearchMetadataResponse
            {
                AiAssisted = criteria.AiAssisted,
                InterpretedQuery = criteria.InterpretedQuery,
                RelaxedFields = criteria.RelaxedFields
            }
        };
    }

    /// <summary>
    /// Builds the base query with all WHERE filters applied but NO .Include().
    /// The heavy data is loaded via .Select() projection instead.
    /// </summary>
    private IQueryable<RoomingHouse> BuildSearchBaseQuery(ParsedRoomingHouseSearchCriteria criteria)
    {
        var query = context.RoomingHouses
            .AsNoTracking()
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

        return query;
    }

    private static void SetSearchMetadata(
        PagedResult<RoomingHouseSearchItemResponse> result,
        RoomingHouseSearchRequest request,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        result.Metadata = new RoomingHouseSearchMetadataResponse
        {
            AiAssisted = criteria.AiAssisted,
            OriginalQuery = request.Q,
            InterpretedQuery = criteria.InterpretedQuery,
            RelaxedFields = criteria.RelaxedFields
        };
    }

    public async Task<RoomingHouseRecommendationResponse> GetGuestRecommendationsAsync(
        GuestRoomingHouseRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Min(24, Math.Max(1, request.PageSize));
        var candidatePageSize = Math.Min(48, Math.Max(pageSize * 3, pageSize));
        var candidateCriteria = new ParsedRoomingHouseSearchCriteria
        {
            ProvinceCode = NormalizeEmpty(request.ProvinceCode),
            WardCode = NormalizeEmpty(request.WardCode),
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinAreaM2,
            MaxArea = request.MaxAreaM2,
            AmenityIds = request.PreferredAmenityIds.Distinct().ToList(),
            RoomAmenityIds = request.PreferredRoomAmenityIds.Distinct().ToList(),
            PreferredAmenityIds = request.PreferredAmenityIds.Distinct().ToList(),
            PreferredRoomAmenityIds = request.PreferredRoomAmenityIds.Distinct().ToList(),
            RecentRoomingHouseIds = request.RecentRoomingHouseIds
                .Concat(request.ClickedRoomingHouseIds)
                .Distinct()
                .ToList(),
            Sort = "relevance",
            Page = 1,
            PageSize = candidatePageSize
        };

        ValidateSearchRequest(candidateCriteria);
        var candidateResult = await ExecutePublicSearchAsync(candidateCriteria, cancellationToken);
        var candidateItems = candidateResult.Items.Count == 0
            ? await LoadDefaultRecommendationCandidatesAsync(pageSize, candidatePageSize, cancellationToken)
            : await BackfillRecommendationCandidatesAsync(
                candidateResult.Items,
                pageSize,
                candidatePageSize,
                cancellationToken);

        if (candidateItems.Count == 0)
        {
            return new RoomingHouseRecommendationResponse
            {
                FallbackReason = "Không có khu trọ còn phòng để gợi ý."
            };
        }

        var candidates = candidateItems
            .Select(ToRecommendationCandidate)
            .ToList();
        var rerankResult = await recommendationReranker.RerankAsync(request, candidates, cancellationToken);
        if (rerankResult is null)
        {
            return new RoomingHouseRecommendationResponse
            {
                Items = candidateItems.Take(pageSize).ToList(),
                Reasons = BuildRuleBasedRecommendationReasons(candidateItems.Take(pageSize)),
                AiAssisted = false,
                FallbackReason = "AI chưa khả dụng, dùng xếp hạng mặc định."
            };
        }

        var itemById = candidateItems.ToDictionary(x => x.Id);
        var rankedItems = rerankResult.RankedIds
            .Where(itemById.ContainsKey)
            .Select(id => itemById[id])
            .Concat(candidateItems.Where(item => !rerankResult.RankedIds.Contains(item.Id)))
            .Take(pageSize)
            .ToList();

        return new RoomingHouseRecommendationResponse
        {
            Items = rankedItems,
            Reasons = rerankResult.Reasons
                .Where(x => rankedItems.Any(item => item.Id == x.Key))
                .ToDictionary(x => x.Key, x => x.Value),
            AiAssisted = true
        };
    }

    private async Task<List<RoomingHouseSearchItemResponse>> BackfillRecommendationCandidatesAsync(
        IReadOnlyList<RoomingHouseSearchItemResponse> primaryItems,
        int pageSize,
        int candidatePageSize,
        CancellationToken cancellationToken)
    {
        if (primaryItems.Count >= pageSize)
        {
            return primaryItems.ToList();
        }

        var fallbackItems = await LoadDefaultRecommendationCandidatesAsync(
            pageSize,
            candidatePageSize,
            cancellationToken);
        return primaryItems
            .Concat(fallbackItems.Where(item => primaryItems.All(existing => existing.Id != item.Id)))
            .Take(Math.Max(pageSize, primaryItems.Count))
            .ToList();
    }

    private async Task<List<RoomingHouseSearchItemResponse>> LoadDefaultRecommendationCandidatesAsync(
        int pageSize,
        int candidatePageSize,
        CancellationToken cancellationToken)
    {
        var fallbackCriteria = new ParsedRoomingHouseSearchCriteria
        {
            Sort = "relevance",
            Page = 1,
            PageSize = Math.Max(pageSize, candidatePageSize)
        };

        ValidateSearchRequest(fallbackCriteria);
        var fallbackResult = await ExecutePublicSearchAsync(fallbackCriteria, cancellationToken);
        return fallbackResult.Items;
    }

    private static RoomingHouseRecommendationCandidate ToRecommendationCandidate(
        RoomingHouseSearchItemResponse item)
        => new()
        {
            Id = item.Id,
            Name = item.Name,
            AddressDisplay = item.AddressDisplay,
            DistanceKm = item.DistanceKm,
            MinMonthlyRent = item.MinMonthlyRent,
            MaxMonthlyRent = item.MaxMonthlyRent,
            MinAreaM2 = item.MinAreaM2,
            MaxAreaM2 = item.MaxAreaM2,
            AvailableRooms = item.AvailableRooms,
            TotalRooms = item.TotalRooms,
            HasCoverImage = !string.IsNullOrWhiteSpace(item.CoverImageUrl),
            CreatedAt = item.CreatedAt,
            Amenities = item.Amenities.Select(x => x.Name).ToList()
        };

    private static Dictionary<Guid, string> BuildRuleBasedRecommendationReasons(
        IEnumerable<RoomingHouseSearchItemResponse> items)
        => items.ToDictionary(
            x => x.Id,
            x =>
            {
                var parts = new List<string>();

                if (x.AvailableRooms > 0)
                {
                    var ratio = x.TotalRooms > 0 ? (double)x.AvailableRooms / x.TotalRooms : 0;
                    parts.Add(ratio > 0.5
                        ? $"Còn {x.AvailableRooms}/{x.TotalRooms} phòng trống, nhiều lựa chọn."
                        : $"Còn {x.AvailableRooms} phòng trống.");
                }

                if (x.MinMonthlyRent is not null)
                {
                    var rentDisplay = x.MaxMonthlyRent is not null && x.MinMonthlyRent != x.MaxMonthlyRent
                        ? $"{x.MinMonthlyRent:N0} - {x.MaxMonthlyRent:N0} đ"
                        : $"{x.MinMonthlyRent:N0} đ";
                    parts.Add($"Giá thuê từ {rentDisplay}/tháng.");
                }

                if (x.MinAreaM2 is not null)
                {
                    parts.Add($"Diện tích từ {x.MinAreaM2:F0}m².");
                }

                if (x.Amenities.Count > 0)
                {
                    var topAmenities = x.Amenities.Take(3).Select(a => a.Name);
                    parts.Add($"Tiện ích: {string.Join(", ", topAmenities)}.");
                }

                return parts.Count > 0
                    ? string.Join(" ", parts)
                    : "Khu trọ còn phòng và phù hợp để bạn tham khảo thêm.";
            });

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
            criteria.RecentRoomingHouseIds,
            criteria.PreferredAmenityIds,
            criteria.PreferredRoomAmenityIds,
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

    private IQueryable<RoomingHouse> BuildRoomingHouseQuery()
    {
        return context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.LegalDocument)
            .Include(x => x.RentalPolicy)
            .Include(x => x.HouseRule)
            .Include(x => x.Images)
            .Include(x => x.ServicePrices)
                .ThenInclude(x => x.ServiceType)
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

    /// <summary>
    /// Lightweight scored candidate used for filtering, sorting, and pagination.
    /// Does NOT hold the full response DTO — that is loaded separately for page items only.
    /// </summary>
    private sealed record ScoredCandidate(
        Guid Id,
        int RelevanceScore,
        int KeywordScore,
        decimal? DistanceKm,
        decimal? MinMonthlyRent,
        decimal? MaxMonthlyRent,
        decimal? MinAreaM2,
        decimal? MaxAreaM2,
        DateTimeOffset CreatedAt);
}
