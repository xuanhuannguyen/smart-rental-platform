using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public sealed class RoomingHouseAiChatService : IRoomingHouseAiChatService
{
    private const int SearchPageSize = 5;
    private const int NearbyLimit = 6;

    private readonly IRoomingHouseQueryService queryService;
    private readonly IVietMapService vietMapService;
    private readonly IAiStructuredOutputService aiService;
    private readonly ILogger<RoomingHouseAiChatService> logger;

    public RoomingHouseAiChatService(
        IRoomingHouseQueryService queryService,
        IVietMapService vietMapService,
        IAiStructuredOutputService aiService,
        ILogger<RoomingHouseAiChatService> logger)
    {
        this.queryService = queryService;
        this.vietMapService = vietMapService;
        this.aiService = aiService;
        this.logger = logger;
    }

    public async Task<RoomingHouseAiChatResponse> ChatAsync(
        RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return new RoomingHouseAiChatResponse
            {
                Reply = "Bạn muốn mình tư vấn khu trọ theo khu vực, ngân sách hay hỏi thông tin khu trọ nào?",
                Intent = "clarify",
                Confidence = 1,
                FollowUpQuestions =
                {
                    "Bạn muốn tìm trọ gần khu vực nào?",
                    "Ngân sách mỗi tháng của bạn khoảng bao nhiêu?"
                },
                MissingInformation = { "message" }
            };
        }

        var plan = await CreatePlanAsync(request, cancellationToken)
            ?? CreateFallbackPlan(request);
        var detail = await LoadDetailIfNeededAsync(request, plan, cancellationToken);
        var searchResults = await SearchIfNeededAsync(request, plan, detail, cancellationToken);
        var nearbyPlaces = await SearchNearbyIfNeededAsync(request, plan, detail, cancellationToken);

        var usedSources = new List<string>();
        if (searchResults.Count > 0)
        {
            usedSources.Add("rooming_house_search");
        }

        if (detail is not null)
        {
            usedSources.Add("rooming_house_detail");
        }

        if (nearbyPlaces.Count > 0)
        {
            usedSources.Add("vietmap_nearby");
        }

        var compactContext = new
        {
            request.Message,
            request.Context,
            request.Mode,
            plan.Intent,
            plan.DetailFocus,
            roomingHouse = detail is null ? null : ToAiDetail(detail),
            roomingHouses = searchResults.Select(ToAiSearchItem).ToList(),
            nearbyPlaces
        };

        var aiAnswer = await CreateAnswerAsync(compactContext, cancellationToken);
        var fallbackReply = BuildFallbackReply(request, plan, detail, searchResults, nearbyPlaces);

        return new RoomingHouseAiChatResponse
        {
            Reply = FirstNotEmpty(aiAnswer?.Reply, fallbackReply) ?? fallbackReply,
            Intent = FirstNotEmpty(aiAnswer?.Intent, plan.Intent) ?? plan.Intent,
            Confidence = aiAnswer?.Confidence is > 0 ? aiAnswer.Confidence : plan.Confidence,
            AiAssisted = aiAnswer is not null || plan.AiAssisted,
            RoomingHouses = searchResults,
            NearbyPlaces = nearbyPlaces,
            FollowUpQuestions = NormalizeList(aiAnswer?.FollowUpQuestions).DefaultIfEmpty("Bạn muốn mình lọc kỹ hơn theo giá, vị trí hay tiện ích không?").Take(3).ToList(),
            MissingInformation = NormalizeList(aiAnswer?.MissingInformation).Take(4).ToList(),
            UsedSources = usedSources
        };
    }

    private async Task<ChatPlan?> CreatePlanAsync(
        RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                intent = new { type = "string", @enum = new[] { "search", "detail", "nearby", "compare", "clarify", "general" } },
                confidence = new { type = "number" },
                searchQuery = new { type = "string", nullable = true },
                detailFocus = new { type = "string", nullable = true },
                nearbyKeywords = new { type = "array", items = new { type = "string" } },
                radiusMeters = new { type = "integer" },
                needsRoomingHouseDetail = new { type = "boolean" }
            },
            required = new[] { "intent", "confidence", "nearbyKeywords", "radiusMeters", "needsRoomingHouseDetail" }
        };

        var instructions = """
        Bạn là AI router cho chatbot tư vấn tìm trọ.
        Phân loại câu hỏi thành một intent duy nhất.
        Nếu người dùng muốn tìm khu trọ/phòng, intent là search và searchQuery là câu hỏi đã làm sạch.
        Nếu hỏi chính sách, nội quy, giá, cọc, phòng trống, tiện ích của khu trọ hiện tại, intent là detail.
        Nếu hỏi quán ăn, chợ, cafe, siêu thị, trường, địa điểm xung quanh khu trọ, intent là nearby và nearbyKeywords là 1-3 từ khóa tiếng Việt.
        Nếu thiếu dữ kiện quan trọng, intent là clarify.
        Trả về JSON đúng schema, không giải thích thêm.
        """;

        try
        {
            var plan = await aiService.CreateJsonAsync<ChatPlan>(
                "rooming_house_chat_plan",
                schema,
                instructions,
                new
                {
                    request.Message,
                    request.Context,
                    request.RoomingHouseId,
                    request.Mode
                },
                cancellationToken);

            if (plan is not null)
            {
                plan.AiAssisted = true;
            }

            return plan;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI chat plan generation failed.");
            return null;
        }
    }

    private static ChatPlan CreateFallbackPlan(RoomingHouseAiChatRequest request)
    {
        var normalized = Normalize(request.Message);
        var isNearby = ContainsAny(normalized, "xung quanh", "gan day", "quan an", "cho", "cafe", "ca phe", "sieu thi", "tap hoa");
        var isDetail = request.RoomingHouseId.HasValue
            && ContainsAny(normalized, "chinh sach", "noi quy", "coc", "thanh toan", "gia", "phong", "tien ich", "khach", "gui xe", "yen tinh");

        return new ChatPlan
        {
            Intent = isNearby ? "nearby" : isDetail ? "detail" : "search",
            Confidence = 0.72m,
            SearchQuery = request.Message,
            DetailFocus = request.Message,
            NearbyKeywords = ExtractNearbyKeywords(normalized),
            RadiusMeters = 1500,
            NeedsRoomingHouseDetail = request.RoomingHouseId.HasValue,
            AiAssisted = false
        };
    }

    private async Task<RoomingHouseDetailResponse?> LoadDetailIfNeededAsync(
        RoomingHouseAiChatRequest request,
        ChatPlan plan,
        CancellationToken cancellationToken)
    {
        if (!request.RoomingHouseId.HasValue)
        {
            return null;
        }

        if (request.Context.Equals("detail", StringComparison.OrdinalIgnoreCase)
            || plan.NeedsRoomingHouseDetail
            || plan.Intent is "detail" or "nearby")
        {
            return await queryService.GetPublicByIdAsync(request.RoomingHouseId.Value, cancellationToken);
        }

        return null;
    }

    private async Task<List<RoomingHouseSearchItemResponse>> SearchIfNeededAsync(
        RoomingHouseAiChatRequest request,
        ChatPlan plan,
        RoomingHouseDetailResponse? detail,
        CancellationToken cancellationToken)
    {
        if (plan.Intent is not ("search" or "compare") && detail is not null)
        {
            return new List<RoomingHouseSearchItemResponse>();
        }

        var searchQuery = FirstNotEmpty(plan.SearchQuery, request.Message) ?? request.Message;
        var result = await queryService.SearchPublicAsync(
            new RoomingHouseSearchRequest
            {
                Q = searchQuery,
                Page = 1,
                PageSize = SearchPageSize,
                Sort = "relevance"
            },
            cancellationToken);

        if (result.Items.Count > 0)
        {
            return result.Items;
        }

        return await SearchAroundResolvedLocationAsync(searchQuery, cancellationToken);
    }

    private async Task<List<RoomingHouseSearchItemResponse>> SearchAroundResolvedLocationAsync(
        string searchQuery,
        CancellationToken cancellationToken)
    {
        foreach (var locationQuery in BuildLocationQueries(searchQuery))
        {
            try
            {
                var location = await vietMapService.SearchAddressAsync(locationQuery, cancellationToken);
                foreach (var radiusKm in new[] { 10m, 25m })
                {
                    var result = await queryService.SearchPublicAsync(
                        new RoomingHouseSearchRequest
                        {
                            CenterLat = location.Latitude,
                            CenterLng = location.Longitude,
                            RadiusKm = radiusKm,
                            Sort = "distanceAsc",
                            Page = 1,
                            PageSize = SearchPageSize
                        },
                        cancellationToken);

                    if (result.Items.Count > 0)
                    {
                        return result.Items;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI chat location fallback search failed for query {SearchQuery}.", locationQuery);
            }
        }

        var generalResult = await queryService.SearchPublicAsync(
            new RoomingHouseSearchRequest
            {
                Page = 1,
                PageSize = SearchPageSize,
                Sort = "relevance"
            },
            cancellationToken);

        return generalResult.Items;
    }

    private async Task<List<NearbyPlaceResponse>> SearchNearbyIfNeededAsync(
        RoomingHouseAiChatRequest request,
        ChatPlan plan,
        RoomingHouseDetailResponse? detail,
        CancellationToken cancellationToken)
    {
        if (plan.Intent != "nearby" || detail?.Latitude is null || detail.Longitude is null)
        {
            return new List<NearbyPlaceResponse>();
        }

        var keywords = NormalizeList(plan.NearbyKeywords)
            .DefaultIfEmpty("quán ăn")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var places = new List<NearbyPlaceResponse>();
        foreach (var keyword in keywords)
        {
            try
            {
                var items = await vietMapService.SearchNearbyPlacesAsync(
                    detail.Latitude.Value,
                    detail.Longitude.Value,
                    keyword,
                    plan.RadiusMeters <= 0 ? 1500 : plan.RadiusMeters,
                    NearbyLimit,
                    cancellationToken);
                places.AddRange(items);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "VietMap nearby search failed for keyword {Keyword}.", keyword);
            }
        }

        return places
            .GroupBy(x => $"{Normalize(x.Name)}|{Normalize(x.DisplayAddress ?? x.Address ?? string.Empty)}")
            .Select(x => x.First())
            .OrderBy(x => x.DistanceKm ?? decimal.MaxValue)
            .Take(8)
            .ToList();
    }

    private async Task<ChatAnswer?> CreateAnswerAsync(object compactContext, CancellationToken cancellationToken)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                reply = new { type = "string" },
                intent = new { type = "string" },
                confidence = new { type = "number" },
                followUpQuestions = new { type = "array", items = new { type = "string" } },
                missingInformation = new { type = "array", items = new { type = "string" } }
            },
            required = new[] { "reply", "intent", "confidence", "followUpQuestions", "missingInformation" }
        };

        var instructions = """
        Bạn là chatbot AI tư vấn tìm trọ cho Smart Rental Platform.
        Trả lời bằng tiếng Việt, thân thiện, chi tiết, có cấu trúc dễ đọc.
        Chỉ dùng dữ liệu trong Input JSON. Không tự bịa giá, phòng, chính sách hoặc địa điểm.
        Nếu có danh sách khu trọ, hãy giải thích vì sao phù hợp và nhắc người dùng xem các thẻ bên dưới.
        Nếu có dữ liệu khu trọ hiện tại, hãy trả lời theo chính sách, nội quy, phòng, tiện ích trong dữ liệu.
        Nếu có địa điểm VietMap, hãy nhóm câu trả lời theo loại địa điểm và nhắc khoảng cách khi có.
        Nếu thiếu dữ liệu, nói rõ thiếu gì và gợi ý câu hỏi tiếp theo.
        """;

        try
        {
            return await aiService.CreateJsonAsync<ChatAnswer>(
                "rooming_house_chat_answer",
                schema,
                instructions,
                compactContext,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI chat answer generation failed.");
            return null;
        }
    }

    private static object ToAiSearchItem(RoomingHouseSearchItemResponse item)
        => new
        {
            item.Id,
            item.Name,
            address = item.AddressDisplay,
            item.DistanceKm,
            item.AvailableRooms,
            item.TotalRooms,
            priceRange = FormatRange(item.MinMonthlyRent, item.MaxMonthlyRent, "VND/tháng"),
            areaRange = FormatRange(item.MinAreaM2, item.MaxAreaM2, "m2"),
            amenities = item.Amenities.Select(x => x.Name).Take(8).ToList()
        };

    private static object ToAiDetail(RoomingHouseDetailResponse detail)
    {
        var availableRooms = detail.Rooms
            .Where(room => room.Status.Equals("Available", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(room => new
            {
                room.RoomNumber,
                room.Floor,
                room.AreaM2,
                room.MaxOccupants,
                room.Status,
                rents = room.PriceTiers
                    .Where(tier => tier.IsActive)
                    .OrderBy(tier => tier.MonthlyRent)
                    .Select(tier => new
                    {
                        tier.OccupantCount,
                        tier.MonthlyRent
                    })
                    .Take(4)
                    .ToList(),
                amenities = room.Amenities.Select(x => x.Name).Take(8).ToList()
            })
            .ToList();

        return new
        {
            detail.Id,
            detail.Name,
            address = detail.AddressDisplay,
            detail.Description,
            detail.AvailableRooms,
            detail.TotalRooms,
            amenities = detail.Amenities.Select(x => x.Name).Take(12).ToList(),
            rooms = availableRooms,
            rentalPolicy = detail.RentalPolicy is null ? null : new
            {
                detail.RentalPolicy.MinRentalMonths,
                detail.RentalPolicy.MaxRentalMonths,
                detail.RentalPolicy.AllowShortTermRenewal,
                detail.RentalPolicy.RenewalNoticeDays,
                detail.RentalPolicy.DepositMonths,
                detail.RentalPolicy.DefaultPaymentDay
            },
            houseRule = detail.HouseRule is null ? null : new
            {
                detail.HouseRule.GeneralRules,
                detail.HouseRule.QuietHours,
                detail.HouseRule.SecurityPolicy,
                detail.HouseRule.CleaningPolicy,
                detail.HouseRule.GuestPolicy,
                detail.HouseRule.ParkingPolicy,
                detail.HouseRule.UtilityPolicy,
                detail.HouseRule.DamageCompensationPolicy,
                detail.HouseRule.AdditionalNotes
            }
        };
    }

    private static string BuildFallbackReply(
        RoomingHouseAiChatRequest request,
        ChatPlan plan,
        RoomingHouseDetailResponse? detail,
        List<RoomingHouseSearchItemResponse> searchResults,
        List<NearbyPlaceResponse> nearbyPlaces)
    {
        if (nearbyPlaces.Count > 0)
        {
            return $"Mình tìm thấy {nearbyPlaces.Count} địa điểm quanh {detail?.Name ?? "khu trọ này"}. Bạn có thể xem danh sách bên dưới, mình đã ưu tiên các nơi gần nhất trước.";
        }

        if (plan.Intent == "nearby" && detail is not null)
        {
            return detail.Latitude is null || detail.Longitude is null
                ? "Khu trọ này chưa có tọa độ nên mình chưa thể tìm quán ăn/chợ xung quanh bằng VietMap."
                : "Mình chưa tìm thấy địa điểm phù hợp quanh khu trọ này từ VietMap. Bạn thử hỏi cụ thể hơn như quán ăn, chợ, cafe hoặc siêu thị nhé.";
        }

        if (detail is not null)
        {
            var policy = detail.RentalPolicy is null
                ? "Chưa có chính sách thuê chi tiết."
                : $"Cọc {detail.RentalPolicy.DepositMonths:0.##} tháng, thuê tối thiểu {detail.RentalPolicy.MinRentalMonths} tháng, ngày thanh toán mặc định là ngày {detail.RentalPolicy.DefaultPaymentDay}.";
            return $"{detail.Name} hiện còn {detail.AvailableRooms}/{detail.TotalRooms} phòng. {policy} Tiện ích nổi bật: {string.Join(", ", detail.Amenities.Select(x => x.Name).Take(5))}.";
        }

        if (searchResults.Count > 0)
        {
            return $"Mình tìm thấy {searchResults.Count} khu trọ phù hợp với câu hỏi của bạn. Mình đã đặt các thẻ kết quả bên dưới để bạn xem giá, phòng trống và vị trí nhanh hơn.";
        }

        return "Mình chưa tìm thấy dữ liệu phù hợp. Bạn có thể nói rõ khu vực, ngân sách, số người ở hoặc tiện ích mong muốn để mình tìm kỹ hơn.";
    }

    private static string? FirstNotEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string FormatRange(decimal? min, decimal? max, string unit)
    {
        if (min is null && max is null)
        {
            return "chưa có dữ liệu";
        }

        if (min == max || max is null)
        {
            return $"{min:0.##} {unit}";
        }

        if (min is null)
        {
            return $"đến {max:0.##} {unit}";
        }

        return $"{min:0.##} - {max:0.##} {unit}";
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
        => values?
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList()
            ?? new List<string>();

    private static bool ContainsAny(string source, params string[] terms)
        => terms.Any(source.Contains);

    private static List<string> ExtractNearbyKeywords(string normalized)
    {
        var keywords = new List<string>();
        if (ContainsAny(normalized, "quan an", "an uong", "com", "bun", "pho"))
        {
            keywords.Add("quán ăn");
        }

        if (ContainsAny(normalized, "cho"))
        {
            keywords.Add("chợ");
        }

        if (ContainsAny(normalized, "cafe", "ca phe"))
        {
            keywords.Add("cafe");
        }

        if (ContainsAny(normalized, "sieu thi", "tap hoa"))
        {
            keywords.Add("siêu thị");
        }

        return keywords.Count > 0 ? keywords : new List<string> { "quán ăn", "chợ" };
    }

    private static List<string> BuildLocationQueries(string searchQuery)
    {
        var values = new List<string> { searchQuery };
        var normalized = Normalize(searchQuery);
        var cleaned = normalized
            .Replace("tim tro", string.Empty, StringComparison.Ordinal)
            .Replace("tim phong tro", string.Empty, StringComparison.Ordinal)
            .Replace("khu tro", string.Empty, StringComparison.Ordinal)
            .Replace("phong tro", string.Empty, StringComparison.Ordinal)
            .Replace("gan", string.Empty, StringComparison.Ordinal)
            .Replace("o dau", string.Empty, StringComparison.Ordinal)
            .Replace("cho toi", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            values.Add(cleaned);
        }

        if (normalized.Contains("fpt") && normalized.Contains("da nang"))
        {
            values.Add("Đại học FPT Đà Nẵng");
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        var replacements = new Dictionary<string, string>
        {
            ["á"] = "a", ["à"] = "a", ["ả"] = "a", ["ã"] = "a", ["ạ"] = "a", ["ă"] = "a", ["ắ"] = "a", ["ằ"] = "a", ["ẳ"] = "a", ["ẵ"] = "a", ["ặ"] = "a", ["â"] = "a", ["ấ"] = "a", ["ầ"] = "a", ["ẩ"] = "a", ["ẫ"] = "a", ["ậ"] = "a",
            ["é"] = "e", ["è"] = "e", ["ẻ"] = "e", ["ẽ"] = "e", ["ẹ"] = "e", ["ê"] = "e", ["ế"] = "e", ["ề"] = "e", ["ể"] = "e", ["ễ"] = "e", ["ệ"] = "e",
            ["í"] = "i", ["ì"] = "i", ["ỉ"] = "i", ["ĩ"] = "i", ["ị"] = "i",
            ["ó"] = "o", ["ò"] = "o", ["ỏ"] = "o", ["õ"] = "o", ["ọ"] = "o", ["ô"] = "o", ["ố"] = "o", ["ồ"] = "o", ["ổ"] = "o", ["ỗ"] = "o", ["ộ"] = "o", ["ơ"] = "o", ["ớ"] = "o", ["ờ"] = "o", ["ở"] = "o", ["ỡ"] = "o", ["ợ"] = "o",
            ["ú"] = "u", ["ù"] = "u", ["ủ"] = "u", ["ũ"] = "u", ["ụ"] = "u", ["ư"] = "u", ["ứ"] = "u", ["ừ"] = "u", ["ử"] = "u", ["ữ"] = "u", ["ự"] = "u",
            ["ý"] = "y", ["ỳ"] = "y", ["ỷ"] = "y", ["ỹ"] = "y", ["ỵ"] = "y",
            ["đ"] = "d"
        };

        foreach (var replacement in replacements)
        {
            normalized = normalized.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return normalized;
    }

    private sealed class ChatPlan
    {
        public string Intent { get; set; } = "general";

        public decimal Confidence { get; set; } = 0.7m;

        public string? SearchQuery { get; set; }

        public string? DetailFocus { get; set; }

        public List<string> NearbyKeywords { get; set; } = new();

        public int RadiusMeters { get; set; } = 1500;

        public bool NeedsRoomingHouseDetail { get; set; }

        public bool AiAssisted { get; set; }
    }

    private sealed class ChatAnswer
    {
        public string Reply { get; set; } = string.Empty;

        public string Intent { get; set; } = "general";

        public decimal Confidence { get; set; }

        public List<string> FollowUpQuestions { get; set; } = new();

        public List<string> MissingInformation { get; set; } = new();
    }
}
