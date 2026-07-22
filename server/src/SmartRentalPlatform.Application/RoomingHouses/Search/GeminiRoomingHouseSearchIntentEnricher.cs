using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Options;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class GeminiRoomingHouseSearchIntentEnricher : IRoomingHouseSearchIntentEnricher
{
    private const decimal MinimumConfidence = 0.45m;

    private readonly IAppDbContext context;
    private readonly IAiStructuredOutputService ai;
    private readonly GeminiOptions options;
    private readonly ILogger<GeminiRoomingHouseSearchIntentEnricher> logger;

    public GeminiRoomingHouseSearchIntentEnricher(
        IAppDbContext context,
        IAiStructuredOutputService ai,
        IOptions<GeminiOptions> options,
        ILogger<GeminiRoomingHouseSearchIntentEnricher> logger)
    {
        this.context = context;
        this.ai = ai;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task EnrichAsync(
        RoomingHouseSearchIntentContext searchContext,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled ||
            !options.UseAiSearchFallback ||
            !options.HasCredential() ||
            string.IsNullOrWhiteSpace(searchContext.Request.Q))
        {
            return;
        }

        var amenities = await context.Amenities
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.Id, x.Name, Scope = x.Scope.ToString() })
            .ToListAsync(cancellationToken);

        var validAmenityIds = amenities.Select(x => x.Id).ToHashSet();
        var input = new
        {
            rawQuery = searchContext.Request.Q,
            normalizedQuery = searchContext.NormalizedQuery.WithoutDiacritics,
            currentCriteria = new
            {
                searchContext.Criteria.Keyword,
                searchContext.Criteria.PlaceText,
                searchContext.Criteria.ProvinceCode,
                searchContext.Criteria.WardCode,
                searchContext.Criteria.MinPrice,
                searchContext.Criteria.MaxPrice,
                searchContext.Criteria.MinArea,
                searchContext.Criteria.MaxArea,
                searchContext.Criteria.MinOccupants,
                searchContext.Criteria.AmenityIds,
                searchContext.Criteria.RoomAmenityIds,
                searchContext.Criteria.RadiusKm
            },
            amenities
        };

        try
        {
            var result = await ai.CreateJsonAsync<AiSearchIntentResult>(
                "rooming_house_search_intent",
                BuildSearchIntentSchema(),
                BuildSearchIntentInstructions(),
                input,
                cancellationToken);

            if (result is null || result.Confidence < MinimumConfidence)
            {
                return;
            }

            ApplyResult(searchContext.Criteria, result, validAmenityIds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI search intent enrichment failed.");
        }
    }

    private static void ApplyResult(
        ParsedRoomingHouseSearchCriteria criteria,
        AiSearchIntentResult result,
        HashSet<int> validAmenityIds)
    {
        criteria.PlaceText = FirstNonBlank(result.PlaceText, criteria.PlaceText);
        criteria.ProvinceCode = FirstNonBlank(criteria.ProvinceCode, result.ProvinceCode);
        criteria.WardCode = FirstNonBlank(criteria.WardCode, result.WardCode);
        criteria.Keyword = FirstNonBlank(result.RelaxedKeyword, criteria.Keyword);
        criteria.MinPrice ??= PositiveDecimal(result.MinPrice);
        criteria.MaxPrice ??= PositiveDecimal(result.MaxPrice);
        criteria.MinArea ??= PositiveDecimal(result.MinArea);
        criteria.MaxArea ??= PositiveDecimal(result.MaxArea);
        criteria.MinOccupants ??= result.MinOccupants is > 0 ? result.MinOccupants : null;
        criteria.RadiusKm ??= PositiveDecimal(result.RadiusKm);

        AddValidIds(criteria.AmenityIds, result.AmenityIds, validAmenityIds);
        AddValidIds(criteria.RoomAmenityIds, result.RoomAmenityIds, validAmenityIds);

        criteria.AiAssisted = true;
        criteria.InterpretedQuery = FirstNonBlank(result.InterpretedQuery, criteria.InterpretedQuery);
        criteria.RelaxedFields = result.RelaxedFields
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? FirstNonBlank(string? first, string? second)
        => string.IsNullOrWhiteSpace(first) ? second : first.Trim();

    private static decimal? PositiveDecimal(decimal? value)
        => value is > 0m ? value : null;

    private static void AddValidIds(List<int> target, IEnumerable<int> values, HashSet<int> validIds)
    {
        foreach (var id in values.Where(validIds.Contains))
        {
            if (!target.Contains(id))
            {
                target.Add(id);
            }
        }
    }

    private static string BuildSearchIntentInstructions()
        => """
        Bạn là bộ phân tích intent tìm khu trọ tại Việt Nam.
        Chỉ trích xuất điều kiện từ dữ liệu đầu vào, không bịa địa điểm hoặc tiện ích.
        Chỉ trả amenity id có trong danh sách amenities. Nếu không chắc, dùng null hoặc mảng rỗng.
        relaxedKeyword là phần từ khóa còn lại sau khi bỏ giá, diện tích, số người, địa điểm và tiện ích.
        Trả JSON đúng schema, lý giải ngắn bằng tiếng Việt trong interpretedQuery.
        """;

    private static object BuildSearchIntentSchema()
        => new
        {
            type = "object",
            properties = new
            {
                placeText = NullableString(),
                provinceCode = NullableString(),
                wardCode = NullableString(),
                minPrice = NullableNumber(),
                maxPrice = NullableNumber(),
                minArea = NullableNumber(),
                maxArea = NullableNumber(),
                minOccupants = NullableInteger(),
                amenityIds = IntegerArray(),
                roomAmenityIds = IntegerArray(),
                radiusKm = NullableNumber(),
                relaxedKeyword = NullableString(),
                interpretedQuery = NullableString(),
                relaxedFields = StringArray(),
                confidence = new { type = "number", minimum = 0, maximum = 1 }
            },
            required = new[]
            {
                "placeText",
                "provinceCode",
                "wardCode",
                "minPrice",
                "maxPrice",
                "minArea",
                "maxArea",
                "minOccupants",
                "amenityIds",
                "roomAmenityIds",
                "radiusKm",
                "relaxedKeyword",
                "interpretedQuery",
                "relaxedFields",
                "confidence"
            }
        };

    private static object NullableString()
        => new { type = "string", nullable = true };

    private static object NullableNumber()
        => new { type = "number", nullable = true };

    private static object NullableInteger()
        => new { type = "integer", nullable = true };

    private static object IntegerArray()
        => new { type = "array", items = new { type = "integer" } };

    private static object StringArray()
        => new { type = "array", items = new { type = "string" } };

    private sealed class AiSearchIntentResult
    {
        public string? PlaceText { get; set; }

        public string? ProvinceCode { get; set; }

        public string? WardCode { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public decimal? MinArea { get; set; }

        public decimal? MaxArea { get; set; }

        public int? MinOccupants { get; set; }

        public List<int> AmenityIds { get; set; } = new();

        public List<int> RoomAmenityIds { get; set; } = new();

        public decimal? RadiusKm { get; set; }

        public string? RelaxedKeyword { get; set; }

        public string? InterpretedQuery { get; set; }

        public List<string> RelaxedFields { get; set; } = new();

        public decimal Confidence { get; set; }
    }
}
