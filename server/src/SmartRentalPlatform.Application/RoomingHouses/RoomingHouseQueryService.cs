using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RentalPolicies.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Responses;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

public partial class RoomingHouseQueryService : IRoomingHouseQueryService
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
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<RoomingHouseQueryService> logger;

    public RoomingHouseQueryService(
        IAppDbContext context,
        IRoomingHouseSearchParser searchParser,
        IEnumerable<IRoomingHouseSearchIntentEnricher> searchIntentEnrichers,
        IRoomingHouseRecommendationScorer recommendationScorer,
        IRoomingHouseRecommendationReranker recommendationReranker,
        IVietMapService vietMapService,
        IMemoryCache memoryCache,
        ILogger<RoomingHouseQueryService> logger)
    {
        this.context = context;
        this.searchParser = searchParser;
        this.searchIntentEnrichers = searchIntentEnrichers;
        this.recommendationScorer = recommendationScorer;
        this.recommendationReranker = recommendationReranker;
        this.vietMapService = vietMapService;
        this.memoryCache = memoryCache;
        this.logger = logger;
    }

    public async Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var houses = await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .Select(x => new RoomingHouseOnboardingSnapshot(
                x.Id,
                x.ApprovalStatus,
                x.UpdatedAt))
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

        var needsDetail = house.ApprovalStatus is not RoomingHouseApprovalStatus.Approved;

        return new RoomingHouseOnboardingResponse
        {
            Status = house.ApprovalStatus.ToString(),
            HasRoomingHouse = true,
            CanCreateDraft = CanCreateDraft(houses.Select(x => x.ApprovalStatus)),
            CanEdit = CanEditRejectedOrDraft(house.ApprovalStatus),
            CanSubmit = CanSubmit(house.ApprovalStatus),
            CanEnterLandlordDashboard = houses.Any(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved),
            RoomingHouseId = house.Id,
            RoomingHouse = needsDetail
                ? await GetByIdAsync(house.Id, cancellationToken)
                : null
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
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);

        var cacheKey = $"public-rooming-house-listing:{page}:{pageSize}";
        if (memoryCache.TryGetValue(cacheKey, out List<RoomingHouseListingResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var rawItems = await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                        x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null))
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                AddressDisplay = x.Ward != null && x.Province != null
                    ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                    : x.AddressDisplay,
                CoverImageMediaAssetId = x.Images
                    .Where(i => i.MediaAssetId.HasValue)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.MediaAssetId)
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
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var pageIds = rawItems.Select(x => x.Id).ToList();
        var amenityLookup = pageIds.Count == 0
            ? new Dictionary<Guid, List<AmenityResponse>>()
            : (await context.RoomingHouseAmenities
                .AsNoTracking()
                .Where(x => pageIds.Contains(x.RoomingHouseId))
                .Select(x => new
                {
                    x.RoomingHouseId,
                    Amenity = new AmenityResponse
                    {
                        Id = x.Amenity.Id,
                        Name = x.Amenity.Name,
                        Scope = x.Amenity.Scope.ToString(),
                        IconCode = x.Amenity.IconCode
                    }
                })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.RoomingHouseId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(a => a.Amenity).ToList());

        var result = rawItems
            .Select(x => new RoomingHouseListingResponse
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                    ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                    : null,
                AvailableRooms = x.AvailableRooms,
                MinMonthlyRent = x.MinMonthlyRent,
                MaxMonthlyRent = x.MaxMonthlyRent,
                MinAreaM2 = x.MinAreaM2,
                MaxAreaM2 = x.MaxAreaM2,
                Amenities = amenityLookup.GetValueOrDefault(x.Id) ?? new List<AmenityResponse>(),
                AverageRating = x.AverageRating,
                TotalReviews = x.TotalReviews,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        memoryCache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromSeconds(45)
            });

        return result;
    }

    public async Task<PagedResult<RoomingHouseSearchItemResponse>> SearchPublicAsync(
        RoomingHouseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var criteria = searchParser.Parse(request);
        await PrepareSearchCriteriaAsync(criteria, cancellationToken);
        var cacheKey = BuildPublicSearchCacheKey(criteria);
        if (memoryCache.TryGetValue(cacheKey, out PagedResult<RoomingHouseSearchItemResponse>? cachedResult) &&
            cachedResult is not null)
        {
            SetSearchMetadata(cachedResult, request, criteria);
            return cachedResult;
        }

        var result = await ExecutePublicSearchAsync(criteria, cancellationToken);
        SetSearchMetadata(result, request, criteria);
        LogSearchParse(request, criteria, result.TotalItems);

        if (result.TotalItems > 0 || string.IsNullOrWhiteSpace(request.Q))
        {
            CachePublicSearchResult(cacheKey, result);
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

            var finalResult = aiResult.TotalItems == 0 ? result : aiResult;
            CachePublicSearchResult(cacheKey, finalResult);
            return finalResult;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI search fallback failed.");
            CachePublicSearchResult(cacheKey, result);
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

    private static string BuildPublicSearchCacheKey(ParsedRoomingHouseSearchCriteria criteria)
    {
        static string JoinInts(IEnumerable<int> values) => string.Join(',', values.OrderBy(x => x));
        static string JoinGuids(IEnumerable<Guid> values) => string.Join(',', values.OrderBy(x => x));
        static string DecimalKey(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

        return string.Join('|',
            "public-rooming-house-search",
            RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty),
            criteria.ProvinceCode ?? string.Empty,
            criteria.WardCode ?? string.Empty,
            DecimalKey(criteria.MinPrice),
            DecimalKey(criteria.MaxPrice),
            DecimalKey(criteria.MinArea),
            DecimalKey(criteria.MaxArea),
            criteria.MinOccupants?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            JoinInts(criteria.AmenityIds),
            JoinInts(criteria.RoomAmenityIds),
            JoinInts(criteria.PreferredAmenityIds),
            JoinInts(criteria.PreferredRoomAmenityIds),
            JoinGuids(criteria.RecentRoomingHouseIds),
            DecimalKey(criteria.CenterLat),
            DecimalKey(criteria.CenterLng),
            DecimalKey(criteria.RadiusKm),
            criteria.Sort,
            criteria.Page.ToString(CultureInfo.InvariantCulture),
            criteria.PageSize.ToString(CultureInfo.InvariantCulture));
    }

    private void CachePublicSearchResult(
        string cacheKey,
        PagedResult<RoomingHouseSearchItemResponse> result)
    {
        memoryCache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromSeconds(45)
            });
    }

    private async Task<PagedResult<RoomingHouseSearchItemResponse>> ExecutePublicSearchAsync(
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (CanUseFastPublicSearch(criteria))
        {
            return await ExecuteFastPublicSearchAsync(criteria, cancellationToken);
        }

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
                ImageCount = x.Images.Count(i => i.MediaAssetId.HasValue),
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
                        ImageCount = r.Images.Count(i => i.MediaAssetId.HasValue)
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
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    AddressDisplay = x.Ward != null && x.Province != null
                        ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                        : x.AddressDisplay,
                    x.Latitude,
                    x.Longitude,
                    CoverImageMediaAssetId = x.Images
                        .Where(i => i.MediaAssetId.HasValue)
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.MediaAssetId)
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

            var materializedItems = rawItems
                .Select(x => new RoomingHouseSearchItemResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    AddressDisplay = x.AddressDisplay,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                        ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                        : null,
                    AvailableRooms = x.AvailableRooms,
                    TotalRooms = x.TotalRooms,
                    MinMonthlyRent = x.MinMonthlyRent,
                    MaxMonthlyRent = x.MaxMonthlyRent,
                    MinAreaM2 = x.MinAreaM2,
                    MaxAreaM2 = x.MaxAreaM2,
                    Amenities = x.Amenities,
                    AverageRating = x.AverageRating,
                    TotalReviews = x.TotalReviews,
                    CreatedAt = x.CreatedAt
                })
                .ToList();

            // Restore DistanceKm into the items and re-sort to match scored order
            foreach (var item in materializedItems)
            {
                if (distanceByHouseId.TryGetValue(item.Id, out var distance))
                {
                    item.DistanceKm = distance;
                }
            }

            var idOrder = pagedCandidates
                .Select((c, i) => (c.Id, Index: i))
                .ToDictionary(x => x.Id, x => x.Index);
            pageItems = materializedItems
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

    private async Task<PagedResult<RoomingHouseSearchItemResponse>> ExecuteFastPublicSearchAsync(
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var snapshotRows = await GetPublicSearchSnapshotAsync(cancellationToken);
        var filteredRows = ApplySnapshotFilters(snapshotRows, criteria).ToList();
        var normalizedKeyword = RoomingHouseSearchParser.Normalize(criteria.Keyword ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return ExecuteSnapshotKeywordSearch(filteredRows, criteria, normalizedKeyword);
        }

        var totalItems = filteredRows.Count;

        if (totalItems == 0)
        {
            return new PagedResult<RoomingHouseSearchItemResponse>
            {
                Items = new List<RoomingHouseSearchItemResponse>(),
                Page = criteria.Page,
                PageSize = criteria.PageSize,
                TotalItems = 0,
                TotalPages = 0,
                Metadata = new RoomingHouseSearchMetadataResponse
                {
                    AiAssisted = criteria.AiAssisted,
                    InterpretedQuery = criteria.InterpretedQuery,
                    RelaxedFields = criteria.RelaxedFields
                }
            };
        }

        var pageRows = ApplySnapshotSort(filteredRows, criteria)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        var items = MaterializeSnapshotSearchItems(pageRows, criteria);

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = items,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)criteria.PageSize),
            Metadata = new RoomingHouseSearchMetadataResponse
            {
                AiAssisted = criteria.AiAssisted,
                InterpretedQuery = criteria.InterpretedQuery,
                RelaxedFields = criteria.RelaxedFields
            }
        };
    }

    private async Task<PagedResult<RoomingHouseSearchItemResponse>> ExecuteSimpleFastPublicSearchAsync(
        IQueryable<RoomingHouse> baseQuery,
        ParsedRoomingHouseSearchCriteria criteria,
        int totalItems,
        CancellationToken cancellationToken)
    {
        var sortedQuery = ApplySimpleSearchSort(baseQuery, criteria);
        var pageRows = await BuildFastSearchProjection(sortedQuery
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize))
            .ToListAsync(cancellationToken);
        var items = await MaterializeFastSearchItemsAsync(pageRows, criteria, cancellationToken);

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = items,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)criteria.PageSize),
            Metadata = new RoomingHouseSearchMetadataResponse
            {
                AiAssisted = criteria.AiAssisted,
                InterpretedQuery = criteria.InterpretedQuery,
                RelaxedFields = criteria.RelaxedFields
            }
        };
    }

    private async Task<List<FastSearchRow>> GetPublicSearchSnapshotAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "public-rooming-house-search-snapshot";
        if (memoryCache.TryGetValue(cacheKey, out List<FastSearchRow>? cachedRows) &&
            cachedRows is not null)
        {
            return cachedRows;
        }

        var rows = await BuildFastSearchProjection(context.RoomingHouses
                .AsNoTracking()
                .Where(x => x.DeletedAt == null &&
                            x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                            x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                            x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null)))
            .ToListAsync(cancellationToken);

        var rowIds = rows.Select(x => x.Id).ToList();
        if (rowIds.Count > 0)
        {
            var houseAmenities = await context.RoomingHouseAmenities
                .AsNoTracking()
                .Where(x => rowIds.Contains(x.RoomingHouseId))
                .Select(x => new
                {
                    x.RoomingHouseId,
                    x.AmenityId,
                    Amenity = new AmenityResponse
                    {
                        Id = x.Amenity.Id,
                        Name = x.Amenity.Name,
                        Scope = x.Amenity.Scope.ToString(),
                        IconCode = x.Amenity.IconCode
                    }
                })
                .ToListAsync(cancellationToken);

            var roomAmenities = await context.RoomAmenities
                .AsNoTracking()
                .Where(x => rowIds.Contains(x.Room.RoomingHouseId) &&
                            x.Room.Status == RoomStatus.Available &&
                            x.Room.DeletedAt == null)
                .Select(x => new
                {
                    x.Room.RoomingHouseId,
                    x.AmenityId
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            var houseAmenityLookup = houseAmenities
                .GroupBy(x => x.RoomingHouseId)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToList());
            var roomAmenityLookup = roomAmenities
                .GroupBy(x => x.RoomingHouseId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(a => a.AmenityId).Distinct().ToHashSet());

            foreach (var row in rows)
            {
                if (houseAmenityLookup.TryGetValue(row.Id, out var amenities))
                {
                    row.AmenityIds = amenities.Select(x => x.AmenityId).Distinct().ToHashSet();
                    row.Amenities = amenities.Select(x => x.Amenity).ToList();
                }

                if (roomAmenityLookup.TryGetValue(row.Id, out var roomAmenityIds))
                {
                    row.RoomAmenityIds = roomAmenityIds;
                }
            }
        }

        memoryCache.Set(
            cacheKey,
            rows,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromSeconds(45)
            });

        return rows;
    }

    private static IEnumerable<FastSearchRow> ApplySnapshotFilters(
        IEnumerable<FastSearchRow> rows,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var filtered = rows;

        if (!string.IsNullOrWhiteSpace(criteria.ProvinceCode))
        {
            filtered = filtered.Where(x => x.ProvinceCode == criteria.ProvinceCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.WardCode))
        {
            filtered = filtered.Where(x => x.WardCode == criteria.WardCode);
        }

        if (criteria.MinPrice is not null)
        {
            filtered = filtered.Where(x => x.MaxMonthlyRent >= criteria.MinPrice);
        }

        if (criteria.MaxPrice is not null)
        {
            filtered = filtered.Where(x => x.MinMonthlyRent <= criteria.MaxPrice);
        }

        if (criteria.MinArea is not null)
        {
            filtered = filtered.Where(x => x.MaxAreaM2 >= criteria.MinArea);
        }

        if (criteria.MaxArea is not null)
        {
            filtered = filtered.Where(x => x.MinAreaM2 <= criteria.MaxArea);
        }

        if (criteria.MinOccupants is not null)
        {
            filtered = filtered.Where(x => x.MaxOccupants >= criteria.MinOccupants);
        }

        foreach (var amenityId in criteria.AmenityIds)
        {
            filtered = filtered.Where(x => x.AmenityIds.Contains(amenityId));
        }

        foreach (var amenityId in criteria.RoomAmenityIds)
        {
            filtered = filtered.Where(x => x.RoomAmenityIds.Contains(amenityId));
        }

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            var box = GeoSearchHelper.BuildBoundingBox(criteria.CenterLat.Value, criteria.CenterLng.Value, criteria.RadiusKm.Value);
            filtered = filtered.Where(x =>
                x.Latitude is not null &&
                x.Longitude is not null &&
                x.Latitude >= box.MinLat &&
                x.Latitude <= box.MaxLat &&
                x.Longitude >= box.MinLng &&
                x.Longitude <= box.MaxLng);
        }

        return filtered;
    }

    private PagedResult<RoomingHouseSearchItemResponse> ExecuteSnapshotKeywordSearch(
        List<FastSearchRow> rows,
        ParsedRoomingHouseSearchCriteria criteria,
        string normalizedKeyword)
    {
        var scoredRows = rows
            .Select(x => new FastSearchScoredRow
            {
                Row = x,
                KeywordScore = CalculateFastKeywordScore(x, normalizedKeyword),
                DistanceKm = CalculateFastDistanceKm(x, criteria)
            })
            .Where(x => x.KeywordScore > 0)
            .ToList();

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            scoredRows = scoredRows
                .Where(x => x.DistanceKm is not null && x.DistanceKm <= criteria.RadiusKm.Value)
                .ToList();
        }

        var totalItems = scoredRows.Count;
        var pageRows = ApplySnapshotKeywordSort(scoredRows, criteria)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => x.Row)
            .ToList();

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = MaterializeSnapshotSearchItems(pageRows, criteria),
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

    private static IEnumerable<FastSearchRow> ApplySnapshotSort(
        IEnumerable<FastSearchRow> rows,
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
            "newest" => rows.OrderByDescending(x => x.CreatedAt),
            "priceAsc" => rows.OrderBy(x => x.MinMonthlyRent ?? decimal.MaxValue).ThenByDescending(x => x.CreatedAt),
            "priceDesc" => rows.OrderByDescending(x => x.MaxMonthlyRent ?? decimal.MinValue).ThenByDescending(x => x.CreatedAt),
            "areaAsc" => rows.OrderBy(x => x.MinAreaM2 ?? decimal.MaxValue).ThenByDescending(x => x.CreatedAt),
            "areaDesc" => rows.OrderByDescending(x => x.MaxAreaM2 ?? decimal.MinValue).ThenByDescending(x => x.CreatedAt),
            "distanceAsc" when criteria.CenterLat is not null && criteria.CenterLng is not null => rows
                .OrderBy(x => CalculateFastDistanceKm(x, criteria) ?? decimal.MaxValue)
                .ThenByDescending(x => x.CreatedAt),
            _ => rows
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .ThenByDescending(x => x.CreatedAt)
        };
    }

    private static IEnumerable<FastSearchScoredRow> ApplySnapshotKeywordSort(
        IEnumerable<FastSearchScoredRow> rows,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var sort = criteria.Sort.Trim();
        if (sort == "relevance")
        {
            return rows
                .OrderByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.Row.AverageRating)
                .ThenByDescending(x => x.Row.TotalReviews)
                .ThenByDescending(x => x.Row.CreatedAt);
        }

        return sort switch
        {
            "newest" => rows.OrderByDescending(x => x.Row.CreatedAt),
            "priceAsc" => rows.OrderBy(x => x.Row.MinMonthlyRent ?? decimal.MaxValue).ThenByDescending(x => x.KeywordScore),
            "priceDesc" => rows.OrderByDescending(x => x.Row.MaxMonthlyRent ?? decimal.MinValue).ThenByDescending(x => x.KeywordScore),
            "areaAsc" => rows.OrderBy(x => x.Row.MinAreaM2 ?? decimal.MaxValue).ThenByDescending(x => x.KeywordScore),
            "areaDesc" => rows.OrderByDescending(x => x.Row.MaxAreaM2 ?? decimal.MinValue).ThenByDescending(x => x.KeywordScore),
            "distanceAsc" when criteria.CenterLat is not null && criteria.CenterLng is not null => rows
                .OrderBy(x => x.DistanceKm ?? decimal.MaxValue)
                .ThenByDescending(x => x.KeywordScore),
            _ => rows
                .OrderByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.Row.AverageRating)
                .ThenByDescending(x => x.Row.TotalReviews)
                .ThenByDescending(x => x.Row.CreatedAt)
        };
    }

    private static List<RoomingHouseSearchItemResponse> MaterializeSnapshotSearchItems(
        IEnumerable<FastSearchRow> rows,
        ParsedRoomingHouseSearchCriteria criteria)
        => rows
            .Select(x => new RoomingHouseSearchItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                    ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                    : null,
                AvailableRooms = x.AvailableRooms,
                TotalRooms = x.TotalRooms,
                MinMonthlyRent = x.MinMonthlyRent,
                MaxMonthlyRent = x.MaxMonthlyRent,
                MinAreaM2 = x.MinAreaM2,
                MaxAreaM2 = x.MaxAreaM2,
                Amenities = x.Amenities,
                AverageRating = x.AverageRating,
                TotalReviews = x.TotalReviews,
                DistanceKm = CalculateFastDistanceKm(x, criteria),
                CreatedAt = x.CreatedAt
            })
            .ToList();

    private async Task<PagedResult<RoomingHouseSearchItemResponse>> ExecuteFastKeywordPublicSearchAsync(
        IQueryable<RoomingHouse> baseQuery,
        ParsedRoomingHouseSearchCriteria criteria,
        string normalizedKeyword,
        CancellationToken cancellationToken)
    {
        var scoredRows = (await BuildFastKeywordProjection(baseQuery)
                .ToListAsync(cancellationToken))
            .Select(x => new FastKeywordScoredRow
            {
                Row = x,
                KeywordScore = CalculateFastKeywordScore(x, normalizedKeyword),
                DistanceKm = CalculateFastDistanceKm(x, criteria)
            })
            .Where(x => x.KeywordScore > 0)
            .ToList();

        if (criteria.CenterLat is not null && criteria.CenterLng is not null && criteria.RadiusKm is not null)
        {
            scoredRows = scoredRows
                .Where(x => x.DistanceKm is not null && x.DistanceKm <= criteria.RadiusKm.Value)
                .ToList();
        }

        var totalItems = scoredRows.Count;
        var pageIds = ApplyFastKeywordSort(scoredRows, criteria)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => x.Row.Id)
            .ToList();
        var pageRows = await LoadFastSearchRowsByIdsAsync(pageIds, cancellationToken);

        var items = await MaterializeFastSearchItemsAsync(pageRows, criteria, cancellationToken);

        return new PagedResult<RoomingHouseSearchItemResponse>
        {
            Items = items,
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

    private static IQueryable<FastKeywordRow> BuildFastKeywordProjection(IQueryable<RoomingHouse> query)
        => query.Select(x => new FastKeywordRow
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            AddressDisplay = x.Ward != null && x.Province != null
                ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                : x.AddressDisplay,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
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
            CreatedAt = x.CreatedAt
        });

    private static IQueryable<FastSearchRow> BuildFastSearchProjection(IQueryable<RoomingHouse> query)
        => query.Select(x => new FastSearchRow
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description,
            ProvinceCode = x.ProvinceCode,
            WardCode = x.WardCode,
            AddressDisplay = x.Ward != null && x.Province != null
                ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                : x.AddressDisplay,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            CoverImageMediaAssetId = x.Images
                .Where(i => i.MediaAssetId.HasValue)
                .OrderBy(i => i.SortOrder)
                .Select(i => i.MediaAssetId)
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
            MaxOccupants = x.Rooms
                .Where(r => r.Status == RoomStatus.Available && r.DeletedAt == null)
                .Select(r => (int?)r.MaxOccupants)
                .Max(),
            CreatedAt = x.CreatedAt
        });

    private async Task<List<FastSearchRow>> LoadFastSearchRowsByIdsAsync(
        List<Guid> pageIds,
        CancellationToken cancellationToken)
    {
        if (pageIds.Count == 0)
        {
            return new List<FastSearchRow>();
        }

        var rows = await BuildFastSearchProjection(context.RoomingHouses
                .AsNoTracking()
                .Where(x => pageIds.Contains(x.Id)))
            .ToListAsync(cancellationToken);
        var order = pageIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        return rows
            .OrderBy(x => order.GetValueOrDefault(x.Id, int.MaxValue))
            .ToList();
    }

    private async Task<List<RoomingHouseSearchItemResponse>> MaterializeFastSearchItemsAsync(
        List<FastSearchRow> pageRows,
        ParsedRoomingHouseSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var pageIds = pageRows.Select(x => x.Id).ToList();
        var amenityLookup = pageIds.Count == 0
            ? new Dictionary<Guid, List<AmenityResponse>>()
            : (await context.RoomingHouseAmenities
                .AsNoTracking()
                .Where(x => pageIds.Contains(x.RoomingHouseId))
                .Select(x => new
                {
                    x.RoomingHouseId,
                    Amenity = new AmenityResponse
                    {
                        Id = x.Amenity.Id,
                        Name = x.Amenity.Name,
                        Scope = x.Amenity.Scope.ToString(),
                        IconCode = x.Amenity.IconCode
                    }
                })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.RoomingHouseId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(a => a.Amenity).ToList());

        return pageRows
            .Select(x => new RoomingHouseSearchItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                    ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                    : null,
                AvailableRooms = x.AvailableRooms,
                TotalRooms = x.TotalRooms,
                MinMonthlyRent = x.MinMonthlyRent,
                MaxMonthlyRent = x.MaxMonthlyRent,
                MinAreaM2 = x.MinAreaM2,
                MaxAreaM2 = x.MaxAreaM2,
                Amenities = amenityLookup.GetValueOrDefault(x.Id) ?? new List<AmenityResponse>(),
                AverageRating = x.AverageRating,
                TotalReviews = x.TotalReviews,
                DistanceKm = CalculateFastDistanceKm(x, criteria),
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }

    private static IEnumerable<FastKeywordScoredRow> ApplyFastKeywordSort(
        IEnumerable<FastKeywordScoredRow> rows,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        var sort = criteria.Sort.Trim();
        if (sort == "relevance")
        {
            return rows
                .OrderByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.Row.AverageRating)
                .ThenByDescending(x => x.Row.TotalReviews)
                .ThenByDescending(x => x.Row.CreatedAt);
        }

        return sort switch
        {
            "newest" => rows.OrderByDescending(x => x.Row.CreatedAt),
            "priceAsc" => rows.OrderBy(x => x.Row.MinMonthlyRent ?? decimal.MaxValue).ThenByDescending(x => x.KeywordScore),
            "priceDesc" => rows.OrderByDescending(x => x.Row.MaxMonthlyRent ?? decimal.MinValue).ThenByDescending(x => x.KeywordScore),
            "areaAsc" => rows.OrderBy(x => x.Row.MinAreaM2 ?? decimal.MaxValue).ThenByDescending(x => x.KeywordScore),
            "areaDesc" => rows.OrderByDescending(x => x.Row.MaxAreaM2 ?? decimal.MinValue).ThenByDescending(x => x.KeywordScore),
            "distanceAsc" when criteria.CenterLat is not null && criteria.CenterLng is not null => rows
                .OrderBy(x => x.DistanceKm ?? decimal.MaxValue)
                .ThenByDescending(x => x.KeywordScore),
            _ => rows
                .OrderByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.Row.AverageRating)
                .ThenByDescending(x => x.Row.TotalReviews)
                .ThenByDescending(x => x.Row.CreatedAt)
        };
    }

    private static int CalculateFastKeywordScore(FastKeywordRow row, string normalizedKeyword)
        => CalculateFastKeywordScore(
            row.Name,
            row.AddressDisplay,
            row.Description,
            normalizedKeyword);

    private static int CalculateFastKeywordScore(FastSearchRow row, string normalizedKeyword)
        => CalculateFastKeywordScore(
            row.Name,
            row.AddressDisplay,
            row.Description,
            normalizedKeyword);

    private static int CalculateFastKeywordScore(
        string name,
        string addressDisplay,
        string? description,
        string normalizedKeyword)
    {
        var score = 0;
        var searchableHouseName = RoomingHouseSearchParser.Normalize(name);
        var searchableAddress = RoomingHouseSearchParser.Normalize(addressDisplay);
        var searchableDescription = RoomingHouseSearchParser.Normalize(description ?? string.Empty);

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

        var tokens = normalizedKeyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 3)
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

    private static decimal? CalculateFastDistanceKm(FastKeywordRow row, ParsedRoomingHouseSearchCriteria criteria)
    {
        if (criteria.CenterLat is null ||
            criteria.CenterLng is null ||
            row.Latitude is null ||
            row.Longitude is null)
        {
            return null;
        }

        return GeoSearchHelper.CalculateDistanceKm(
            criteria.CenterLat.Value,
            criteria.CenterLng.Value,
            row.Latitude.Value,
            row.Longitude.Value);
    }

    private static decimal? CalculateFastDistanceKm(FastSearchRow row, ParsedRoomingHouseSearchCriteria criteria)
    {
        if (criteria.CenterLat is null ||
            criteria.CenterLng is null ||
            row.Latitude is null ||
            row.Longitude is null)
        {
            return null;
        }

        return GeoSearchHelper.CalculateDistanceKm(
            criteria.CenterLat.Value,
            criteria.CenterLng.Value,
            row.Latitude.Value,
            row.Longitude.Value);
    }

    private static bool CanUseFastPublicSearch(ParsedRoomingHouseSearchCriteria criteria)
    {
        return criteria.PreferredAmenityIds.Count == 0 &&
               criteria.PreferredRoomAmenityIds.Count == 0 &&
               criteria.RecentRoomingHouseIds.Count == 0;
    }

    private static bool CanUseSimplePageIdSearch(ParsedRoomingHouseSearchCriteria criteria)
    {
        var sort = criteria.Sort.Trim();
        return sort is "" or "relevance" or "newest" or "distanceAsc";
    }

    private static IQueryable<RoomingHouse> ApplySimpleSearchSort(
        IQueryable<RoomingHouse> query,
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
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "distanceAsc" when criteria.CenterLat is not null && criteria.CenterLng is not null => query
                .OrderBy(x => Math.Abs((x.Latitude ?? 999m) - criteria.CenterLat.Value) +
                              Math.Abs((x.Longitude ?? 999m) - criteria.CenterLng.Value))
                .ThenByDescending(x => x.CreatedAt),
            _ => query
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .ThenByDescending(x => x.CreatedAt)
        };
    }

    private static IQueryable<FastSearchRow> ApplyFastSearchSort(
        IQueryable<FastSearchRow> query,
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
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "priceAsc" => query
                .OrderBy(x => x.MinMonthlyRent ?? decimal.MaxValue)
                .ThenByDescending(x => x.CreatedAt),
            "priceDesc" => query
                .OrderByDescending(x => x.MaxMonthlyRent ?? decimal.MinValue)
                .ThenByDescending(x => x.CreatedAt),
            "areaAsc" => query
                .OrderBy(x => x.MinAreaM2 ?? decimal.MaxValue)
                .ThenByDescending(x => x.CreatedAt),
            "areaDesc" => query
                .OrderByDescending(x => x.MaxAreaM2 ?? decimal.MinValue)
                .ThenByDescending(x => x.CreatedAt),
            "distanceAsc" when criteria.CenterLat is not null && criteria.CenterLng is not null => query
                .OrderBy(x => Math.Abs((x.Latitude ?? 999m) - criteria.CenterLat.Value) +
                              Math.Abs((x.Longitude ?? 999m) - criteria.CenterLng.Value))
                .ThenByDescending(x => x.CreatedAt),
            _ => query
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .ThenByDescending(x => x.CreatedAt)
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
        var cacheKey = $"public-rooming-house-detail:{roomingHouseId:N}";
        if (memoryCache.TryGetValue(cacheKey, out RoomingHouseDetailResponse? cachedResponse) &&
            cachedResponse is not null)
        {
            return cachedResponse;
        }

        var house = await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.Id == roomingHouseId &&
                        x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible)
            .Select(x => new RoomingHouseDetailResponse
            {
                Id = x.Id,
                LandlordUserId = x.LandlordUserId,
                Name = x.Name,
                Description = x.Description,
                AddressLine = x.AddressLine,
                ProvinceCode = x.ProvinceCode,
                WardCode = x.WardCode,
                AddressDisplay = x.Ward != null && x.Province != null
                    ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                    : x.AddressDisplay,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                GoogleMapUrl = x.GoogleMapUrl,
                ApprovalStatus = x.ApprovalStatus.ToString(),
                VisibilityStatus = x.VisibilityStatus.ToString(),
                RejectedReason = x.RejectedReason,
                ReviewedByAdminId = x.ReviewedByAdminId,
                ReviewedAt = x.ReviewedAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LegalDocument = null,
                Images = x.Images
                    .Where(image => image.MediaAssetId.HasValue)
                    .OrderByDescending(image => image.IsCover)
                    .ThenBy(image => image.SortOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => new PropertyImageResponse
                    {
                        Id = image.Id,
                        MediaAssetId = image.MediaAssetId,
                        Caption = image.Caption,
                        IsCover = image.IsCover,
                        SortOrder = image.SortOrder,
                        CreatedAt = image.CreatedAt
                    })
                    .ToList(),
                Amenities = x.RoomingHouseAmenities
                    .OrderBy(amenity => amenity.Amenity.Name)
                    .Select(amenity => new AmenityResponse
                    {
                        Id = amenity.Amenity.Id,
                        Name = amenity.Amenity.Name,
                        Scope = amenity.Amenity.Scope.ToString(),
                        IconCode = amenity.Amenity.IconCode
                    })
                    .ToList(),
                RentalPolicy = x.RentalPolicy == null
                    ? null
                    : new RentalPolicyResponse
                    {
                        Id = x.RentalPolicy.Id,
                        RoomingHouseId = x.RentalPolicy.RoomingHouseId,
                        MinRentalMonths = x.RentalPolicy.MinRentalMonths,
                        MaxRentalMonths = x.RentalPolicy.MaxRentalMonths,
                        AllowShortTermRenewal = x.RentalPolicy.AllowShortTermRenewal,
                        RenewalNoticeDays = x.RentalPolicy.RenewalNoticeDays,
                        DepositMonths = x.RentalPolicy.DepositMonths,
                        DefaultPaymentDay = x.RentalPolicy.DefaultPaymentDay,
                        IsActive = x.RentalPolicy.IsActive,
                        CreatedAt = x.RentalPolicy.CreatedAt,
                        UpdatedAt = x.RentalPolicy.UpdatedAt
                    },
                HouseRule = x.HouseRule == null
                    ? null
                    : new RoomingHouseRuleResponse
                    {
                        Id = x.HouseRule.Id,
                        RoomingHouseId = x.HouseRule.RoomingHouseId,
                        SourceType = x.HouseRule.SourceType.ToString(),
                        MediaAssetId = x.HouseRule.MediaAssetId,
                        GeneralRules = x.HouseRule.GeneralRules,
                        QuietHours = x.HouseRule.QuietHours,
                        SecurityPolicy = x.HouseRule.SecurityPolicy,
                        CleaningPolicy = x.HouseRule.CleaningPolicy,
                        GuestPolicy = x.HouseRule.GuestPolicy,
                        ParkingPolicy = x.HouseRule.ParkingPolicy,
                        UtilityPolicy = x.HouseRule.UtilityPolicy,
                        DamageCompensationPolicy = x.HouseRule.DamageCompensationPolicy,
                        AdditionalNotes = x.HouseRule.AdditionalNotes,
                        CreatedAt = x.HouseRule.CreatedAt,
                        UpdatedAt = x.HouseRule.UpdatedAt
                    },
                Rooms = new List<RoomResponse>(),
                ServicePrices = x.ServicePrices
                    .Where(price => price.IsActive)
                    .OrderBy(price => price.ServiceType.Name)
                    .Select(price => new RoomingHouseServicePriceResponse
                    {
                        Id = price.Id,
                        ServiceTypeId = price.ServiceTypeId,
                        ServiceTypeName = price.ServiceType.Name,
                        PricingUnit = price.PricingUnit.ToString(),
                        UnitPrice = price.UnitPrice,
                        Note = price.Note,
                        MeterUnitName = price.ServiceType.MeterUnitName,
                        IsActive = price.IsActive
                    })
                    .ToList(),
                TotalRooms = x.Rooms.Count(room => room.DeletedAt == null),
                AvailableRooms = x.Rooms.Count(room => room.Status == RoomStatus.Available && room.DeletedAt == null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (house is null)
        {
            return null;
        }

        foreach (var image in house.Images)
        {
            image.ImageUrl = image.MediaAssetId.HasValue
                ? PublicMediaPathBuilder.Build(image.MediaAssetId.Value)
                : string.Empty;
        }

        if (house.HouseRule?.MediaAssetId is Guid ruleMediaAssetId)
        {
            house.HouseRule.PdfUrl = PublicMediaPathBuilder.Build(ruleMediaAssetId);
        }

        memoryCache.Set(
            cacheKey,
            house,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

        return house;
    }

    public async Task<List<RoomingHouseResponse>> GetByLandlordAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var rawItems = await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.LandlordUserId,
                x.Name,
                AddressDisplay = x.Ward != null && x.Province != null
                    ? x.AddressLine + ", " + x.Ward.Name + ", " + x.Province.Name
                    : x.AddressDisplay,
                ApprovalStatus = x.ApprovalStatus,
                VisibilityStatus = x.VisibilityStatus,
                x.RejectedReason,
                x.CreatedAt,
                x.UpdatedAt,
                CoverImageMediaAssetId = x.Images
                    .Where(image => image.MediaAssetId.HasValue)
                    .OrderByDescending(image => image.IsCover)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.MediaAssetId)
                    .FirstOrDefault(),
                TotalRooms = x.Rooms.Count(room => room.DeletedAt == null),
                AvailableRooms = x.Rooms.Count(room =>
                    room.Status == RoomStatus.Available &&
                    room.DeletedAt == null),
                x.AverageRating,
                x.TotalReviews
            })
            .ToListAsync(cancellationToken);

        return rawItems
            .Select(x => new RoomingHouseResponse
            {
                Id = x.Id,
                LandlordUserId = x.LandlordUserId,
                Name = x.Name,
                AddressDisplay = x.AddressDisplay,
                ApprovalStatus = x.ApprovalStatus.ToString(),
                VisibilityStatus = x.VisibilityStatus.ToString(),
                RejectedReason = x.RejectedReason,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                CoverImageUrl = x.CoverImageMediaAssetId.HasValue
                    ? PublicMediaPathBuilder.Build(x.CoverImageMediaAssetId.Value)
                    : null,
                TotalRooms = x.TotalRooms,
                AvailableRooms = x.AvailableRooms,
                AverageRating = x.AverageRating,
                TotalReviews = x.TotalReviews
            })
            .ToList();
    }

    public async Task<RoomingHouseDetailResponse?> GetByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var house = await BuildRoomingHouseQuery()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        return house is null ? null : RoomingHouseReadModelMapper.ToDetailResponse(house);
    }

    private IQueryable<RoomingHouse> BuildRoomingHouseQuery()
    {
        return context.RoomingHouses
            .AsNoTracking()
            .AsSplitQuery()
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

    private IQueryable<RoomingHouse> BuildPublicRoomingHouseDetailQuery()
    {
        return context.RoomingHouses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.LegalDocument)
            .Include(x => x.RentalPolicy)
            .Include(x => x.HouseRule)
            .Include(x => x.Images)
            .Include(x => x.ServicePrices)
                .ThenInclude(x => x.ServiceType)
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

    private static bool CanEditRejectedOrDraft(RoomingHouseApprovalStatus status)
    {
        return status is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanSubmit(RoomingHouseApprovalStatus status)
    {
        return status is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanCreateDraft(IEnumerable<RoomingHouseApprovalStatus> statuses)
    {
        return !statuses.Any(status =>
            status is RoomingHouseApprovalStatus.Draft
                or RoomingHouseApprovalStatus.Pending
                or RoomingHouseApprovalStatus.Rejected);
    }

    private sealed record RoomingHouseOnboardingSnapshot(
        Guid Id,
        RoomingHouseApprovalStatus ApprovalStatus,
        DateTimeOffset UpdatedAt);

    private sealed record SearchLocationMatch(string Code, string? ProvinceCode, string Alias);

    private sealed class FastSearchRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string ProvinceCode { get; set; } = string.Empty;

        public string WardCode { get; set; } = string.Empty;

        public string AddressDisplay { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public Guid? CoverImageMediaAssetId { get; set; }

        public int AvailableRooms { get; set; }

        public int TotalRooms { get; set; }

        public double AverageRating { get; set; }

        public int TotalReviews { get; set; }

        public decimal? MinMonthlyRent { get; set; }

        public decimal? MaxMonthlyRent { get; set; }

        public decimal? MinAreaM2 { get; set; }

        public decimal? MaxAreaM2 { get; set; }

        public int? MaxOccupants { get; set; }

        public HashSet<int> AmenityIds { get; set; } = new();

        public HashSet<int> RoomAmenityIds { get; set; } = new();

        public List<AmenityResponse> Amenities { get; set; } = new();

        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class FastKeywordRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string AddressDisplay { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public double AverageRating { get; set; }

        public int TotalReviews { get; set; }

        public decimal? MinMonthlyRent { get; set; }

        public decimal? MaxMonthlyRent { get; set; }

        public decimal? MinAreaM2 { get; set; }

        public decimal? MaxAreaM2 { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }


    private sealed class FastKeywordScoredRow
    {
        public FastKeywordRow Row { get; set; } = new();

        public int KeywordScore { get; set; }

        public decimal? DistanceKm { get; set; }
    }

    private sealed class FastSearchScoredRow
    {
        public FastSearchRow Row { get; set; } = new();

        public int KeywordScore { get; set; }

        public decimal? DistanceKm { get; set; }
    }

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
