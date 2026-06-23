using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Contracts.Rooms.Responses;
using SmartRentalPlatform.Contracts.RoomPriceTiers.Responses;

namespace SmartRentalPlatform.Application.RoomingHouses;

public sealed class RoomingHouseAiChatService : IRoomingHouseAiChatService
{
    private const int SearchPageSize = 5;
    private const int NearbyLimit = 6;
    private const decimal DefaultChatSearchRadiusKm = 10m;
    private const int DefaultNearbyRadiusMeters = 2000;

    private readonly IRoomingHouseQueryService queryService;
    private readonly IVietMapService vietMapService;
    private readonly IAiStructuredOutputService aiService;
    private readonly IConversationCacheService conversationCache;
    private readonly ILogger<RoomingHouseAiChatService> logger;

    public RoomingHouseAiChatService(
        IRoomingHouseQueryService queryService,
        IVietMapService vietMapService,
        IAiStructuredOutputService aiService,
        IConversationCacheService conversationCache,
        ILogger<RoomingHouseAiChatService> logger)
    {
        this.queryService = queryService;
        this.vietMapService = vietMapService;
        this.aiService = aiService;
        this.conversationCache = conversationCache;
        this.logger = logger;
    }

    public async Task<RoomingHouseAiChatResponse> ChatAsync(
        RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ChatInternalAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI chat service failed for message: {Message}", request.Message);
            return new RoomingHouseAiChatResponse
            {
                Reply = "Mình đang gặp sự cố xử lý câu hỏi. Bạn thử lại sau nhé.",
                Intent = "general",
                Confidence = 1,
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString("N"),
                FollowUpQuestions = { "Bạn có thể hỏi lại bằng cách khác không?" },
                UsedSources = []
            };
        }
    }

    private async Task<RoomingHouseAiChatResponse> ChatInternalAsync(
        RoomingHouseAiChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return new RoomingHouseAiChatResponse
            {
                Reply = "Bạn muốn mình tư vấn khu trọ theo khu vực, ngân sách hay hỏi thông tin khu trọ nào?",
                Intent = "clarify",
                Confidence = 1,
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString("N"),
                FollowUpQuestions =
                {
                    "Bạn muốn tìm trọ gần khu vực nào?",
                    "Ngân sách mỗi tháng của bạn khoảng bao nhiêu?"
                },
                MissingInformation = { "message" },
                UsedSources = []
            };
        }

        // Resolve conversation ID for multi-turn memory
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
        var history = ResolveConversationHistory(request, conversationId);

        var plan = await CreatePlanAsync(request, history, cancellationToken)
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
            intent = plan.Intent,
            detailFocus = plan.DetailFocus,
            conversationHistory = history.Select(h => new { h.Role, h.Text }).ToList(),
            roomingHouse = detail is null ? null : ToAiDetail(detail),
            roomingHouses = searchResults.Select(ToAiSearchItem).ToList(),
            nearbyPlaces
        };

        var aiAnswer = await CreateAnswerAsync(compactContext, cancellationToken);
        var fallbackReply = BuildFallbackReply(request, plan, detail, searchResults, nearbyPlaces);

        var response = new RoomingHouseAiChatResponse
        {
            Reply = FirstNotEmpty(aiAnswer?.Reply, fallbackReply) ?? fallbackReply,
            Intent = FirstNotEmpty(aiAnswer?.Intent, plan.Intent) ?? plan.Intent,
            Confidence = aiAnswer?.Confidence is > 0 ? aiAnswer.Confidence : plan.Confidence,
            AiAssisted = aiAnswer is not null || plan.AiAssisted,
            ConversationId = conversationId,
            RoomingHouses = searchResults,
            NearbyPlaces = nearbyPlaces,
            FollowUpQuestions = NormalizeList(aiAnswer?.FollowUpQuestions)
                .DefaultIfEmpty("Bạn muốn mình lọc kỹ hơn theo giá, vị trí hay tiện ích không?")
                .Take(3)
                .ToList(),
            MissingInformation = NormalizeList(aiAnswer?.MissingInformation).Take(4).ToList(),
            UsedSources = usedSources
        };

        // Persist this turn into conversation memory
        conversationCache.Append(
            conversationId,
            new ConversationMessage("user", message),
            new ConversationMessage("assistant", response.Reply));

        return response;
    }

    private async Task<ChatPlan?> CreatePlanAsync(
        RoomingHouseAiChatRequest request,
        IReadOnlyList<ConversationMessage> history,
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
                needsRoomingHouseDetail = new { type = "boolean" },
                minPrice = new { type = "number", nullable = true },
                maxPrice = new { type = "number", nullable = true },
                locationName = new { type = "string", nullable = true }
            },
            required = new[] { "intent", "confidence", "nearbyKeywords", "radiusMeters", "needsRoomingHouseDetail" }
        };

        var hasHistory = history.Count > 0;
        var instructions = """
        Bạn là AI router cho chatbot tư vấn tìm trọ.
        Phân loại câu hỏi thành một intent duy nhất.
        Nếu người dùng muốn tìm khu trọ/phòng, intent là search và searchQuery là câu hỏi đã làm sạch.
        Nếu người dùng nói tìm trọ gần một địa điểm cụ thể như gần trường, gần chợ, gần siêu thị, gần bệnh viện, gần khu công nghiệp, gần công ty..., hãy đặt locationName là tên địa điểm đó (ví dụ "Đại học FPT Đà Nẵng", "chợ Hòa Khánh", "Khu công nghiệp Hòa Khánh") và searchQuery giữ nguyên câu hỏi. intent vẫn là search.
        Nếu context là detail và người dùng hỏi chính sách, nội quy, giá, cọc, phòng trống, tiện ích của khu trọ hiện tại, intent là detail.
        Nếu context là detail và người dùng hỏi quán ăn, chợ, cafe, siêu thị, trường học, đại học, ATM, bệnh viện, nhà thuốc, trạm xe buýt hoặc địa điểm xung quanh khu trọ, intent là nearby và nearbyKeywords là 1-3 từ khóa tiếng Việt.
        Nếu người dùng nói bán kính như 500m, 2km, 10km, hãy đổi radiusMeters tương ứng; nếu không nói thì để 2000.
        Nếu thiếu dữ kiện quan trọng, intent là clarify.
        Nếu có minPrice/maxPrice từ câu hỏi (ví dụ "dưới 3 triệu" => maxPrice=3000000), hãy trích xuất chúng.
        Trả về JSON đúng schema, không giải thích thêm.
        """;

        try
        {
            var input = new
            {
                request.Message,
                request.Context,
                request.RoomingHouseId,
                conversationHistory = hasHistory
                    ? history.Select(h => new { h.Role, h.Text }).Take(6).ToList()
                    : null
            };

            var plan = await aiService.CreateJsonAsync<ChatPlan>(
                "rooming_house_chat_plan",
                schema,
                instructions,
                input,
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
        var isNearby = ContainsAny(normalized,
            "xung quanh", "quanh", "lan canh", "gan day", "gan khu", "gan nha", "gan tro",
            "quan an", "cho", "cafe", "ca phe",
            "sieu thi", "tap hoa", "nha hang", "phong gym", "benh vien",
            "truong hoc", "truong", "cao dang", "mau giao",
            "tiem thuoc", "coffee", "atm", "ngan hang", "benh vien", "phong kham",
            "tram xe buyt", "bus", "cong vien", "giat ui", "tien ich quanh");
        var isDetail = request.RoomingHouseId.HasValue
            && ContainsAny(normalized, "chinh sach", "noi quy", "coc", "thanh toan",
                "gia", "phong", "tien ich", "khach", "gui xe", "yen tinh");

        var plan = new ChatPlan
        {
            Intent = isNearby ? "nearby" : isDetail ? "detail" : "search",
            Confidence = 0.72m,
            SearchQuery = request.Message,
            DetailFocus = request.Message,
            NearbyKeywords = ExtractNearbyKeywords(normalized),
            RadiusMeters = GetRequestedRadiusMeters(request.Message, DefaultNearbyRadiusMeters),
            NeedsRoomingHouseDetail = request.RoomingHouseId.HasValue,
            AiAssisted = false
        };

        // Extract price hints from message text in fallback mode
        ExtractPriceFromMessage(request.Message, plan);

        return plan;
    }

    private static void ExtractPriceFromMessage(string message, ChatPlan plan)
    {
        var normalized = Normalize(message);

        // Match patterns like "duoi 3 trieu", "tren 5 trieu", "3 - 5 trieu", "khoang 4 trieu"
        var duoiMatch = Regex.Match(
            normalized, @"duoi\s+(\d+(?:[\.,]\d+)?)\s*trieu",
            RegexOptions.CultureInvariant);
        if (duoiMatch.Success && decimal.TryParse(duoiMatch.Groups[1].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var maxVal))
        {
            plan.MaxPrice = maxVal * 1_000_000m;
        }

        var trenMatch = Regex.Match(
            normalized, @"tren\s+(\d+(?:[\.,]\d+)?)\s*trieu",
            RegexOptions.CultureInvariant);
        if (trenMatch.Success && decimal.TryParse(trenMatch.Groups[1].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var minVal))
        {
            plan.MinPrice = minVal * 1_000_000m;
        }

        var rangeMatch = Regex.Match(
            normalized, @"(\d+(?:[\.,]\d+)?)\s*(?:[-–—]|den)\s*(\d+(?:[\.,]\d+)?)\s*trieu",
            RegexOptions.CultureInvariant);
        if (rangeMatch.Success
            && decimal.TryParse(rangeMatch.Groups[1].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var rangeMin)
            && decimal.TryParse(rangeMatch.Groups[2].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var rangeMax))
        {
            plan.MinPrice = rangeMin * 1_000_000m;
            plan.MaxPrice = rangeMax * 1_000_000m;
        }
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

        // Use Gemini's extracted locationName first; fall back to keyword-based detection
        if (!string.IsNullOrWhiteSpace(plan.LocationName))
        {
            var aiLocationResults = await SearchByLocationNameAsync(
                plan.LocationName,
                radiusKm: GetRequestedRadiusKm(request.Message, searchQuery),
                cancellationToken);

            if (aiLocationResults.Count > 0)
            {
                return aiLocationResults;
            }

            logger.LogInformation(
                "Gemini location '{Location}' returned 0 houses. Falling back.",
                plan.LocationName);
        }

        var shouldResolveLocationFirst = ShouldResolveLocationFirst(request.Message)
            || ShouldResolveLocationFirst(searchQuery);
        if (shouldResolveLocationFirst)
        {
            var locationResults = await SearchAroundResolvedLocationAsync(
                searchQuery,
                radiusKm: GetRequestedRadiusKm(request.Message, searchQuery),
                allowGeneralFallback: false,
                cancellationToken);

            if (locationResults.Count > 0)
            {
                return locationResults;
            }

            // Location-based search returned 0 results — fall back to text search
            logger.LogInformation(
                "Location search returned 0 results for query '{Query}'. Falling back to text search.",
                searchQuery);
        }

        var searchRequest = new RoomingHouseSearchRequest
        {
            Q = searchQuery,
            Page = 1,
            PageSize = SearchPageSize,
            Sort = "relevance"
        };

        // Map structured price filters from AI plan into the search request
        if (plan.MinPrice is > 0)
        {
            searchRequest.MinPrice = NormalizePrice(plan.MinPrice.Value);
        }

        if (plan.MaxPrice is > 0)
        {
            searchRequest.MaxPrice = NormalizePrice(plan.MaxPrice.Value);
        }

        var result = await queryService.SearchPublicAsync(searchRequest, cancellationToken);

        if (result.Items.Count > 0)
        {
            return result.Items;
        }

        // Text search returned 0 — fall back to general listing
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

    /// <summary>
    /// Normalize price value: values less than 100,000 are treated as millions.
    /// </summary>
    private static decimal NormalizePrice(decimal value)
        => value < 100_000m ? value * 1_000_000m : value;

    private async Task<List<RoomingHouseSearchItemResponse>> SearchAroundResolvedLocationAsync(
        string searchQuery,
        decimal radiusKm,
        bool allowGeneralFallback,
        CancellationToken cancellationToken)
    {
        foreach (var locationQuery in BuildLocationQueries(searchQuery))
        {
            try
            {
                var location = await vietMapService.SearchAddressAsync(locationQuery, cancellationToken);
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI chat location fallback search failed for query {SearchQuery}.", locationQuery);
            }
        }

        if (!allowGeneralFallback)
        {
            return new List<RoomingHouseSearchItemResponse>();
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

    private async Task<List<RoomingHouseSearchItemResponse>> SearchByLocationNameAsync(
        string locationName,
        decimal radiusKm,
        CancellationToken cancellationToken)
    {
        try
        {
            var location = await vietMapService.SearchAddressAsync(locationName, cancellationToken);
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

            return result.Items;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI chat search by location name '{Location}' failed.", locationName);
            return new List<RoomingHouseSearchItemResponse>();
        }
    }

    private async Task<List<NearbyPlaceResponse>> SearchNearbyIfNeededAsync(
        RoomingHouseAiChatRequest request,
        ChatPlan plan,
        RoomingHouseDetailResponse? detail,
        CancellationToken cancellationToken)
    {
        if (plan.Intent != "nearby")
        {
            return new List<NearbyPlaceResponse>();
        }

        // If we have detail coordinates, search around the rooming house
        if (detail?.Latitude is not null && detail.Longitude is not null)
        {
            return await SearchNearbyAtLocationAsync(
                detail.Latitude.Value,
                detail.Longitude.Value,
                plan,
                cancellationToken);
        }

        // Nearby without coordinates: try to geocode a location from the message
        var searchQuery = FirstNotEmpty(plan.SearchQuery, request.Message) ?? request.Message;
        foreach (var locationQuery in BuildLocationQueries(searchQuery))
        {
            try
            {
                var location = await vietMapService.SearchAddressAsync(locationQuery, cancellationToken);
                return await SearchNearbyAtLocationAsync(
                    location.Latitude,
                    location.Longitude,
                    plan,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nearby location geocode failed for {LocationQuery}.", locationQuery);
            }
        }

        return new List<NearbyPlaceResponse>();
    }

    private async Task<List<NearbyPlaceResponse>> SearchNearbyAtLocationAsync(
        decimal latitude,
        decimal longitude,
        ChatPlan plan,
        CancellationToken cancellationToken)
    {
        var keywords = NormalizeList(plan.NearbyKeywords)
            .DefaultIfEmpty("quán ăn")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        var places = new List<NearbyPlaceResponse>();
        foreach (var keyword in keywords)
        {
            try
            {
                var items = await vietMapService.SearchNearbyPlacesAsync(
                    latitude,
                    longitude,
                    keyword,
                    NormalizeNearbyRadiusMeters(plan.RadiusMeters),
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
        Format reply bắt buộc:
        - Mở đầu 1 câu ngắn, tối đa 20 từ.
        - Nếu có từ 2 khu trọ/địa điểm trở lên, dùng danh sách đánh số, mỗi mục trên một dòng riêng theo dạng "1. Tên: lý do ngắn".
        - Không viết danh sách dồn trên cùng một dòng.
        - Kết thúc bằng 1 câu gợi ý ngắn về thẻ kết quả hoặc câu hỏi tiếp theo.
        - Tránh đoạn văn dài quá 2 câu liên tiếp.
        Chỉ dùng dữ liệu trong Input JSON. Không tự bịa giá, phòng, chính sách hoặc địa điểm.
        Nếu có danh sách khu trọ, hãy giải thích vì sao phù hợp và nhắc người dùng xem các thẻ bên dưới.
        Nếu có dữ liệu khu trọ hiện tại, hãy trả lời theo chính sách, nội quy, phòng, tiện ích trong dữ liệu.
        Nếu có địa điểm VietMap, hãy nhóm câu trả lời theo loại địa điểm như trường học, quán ăn, chợ, siêu thị và nhắc khoảng cách khi có.
        Khi ở trang chi tiết khu trọ, luôn xem roomingHouse là khu trọ hiện tại mà người dùng đang hỏi.
        Nếu có conversationHistory, hãy dùng nó để hiểu ngữ cảnh câu hỏi tiếp theo.
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
            return $"Mình tìm thấy {nearbyPlaces.Count} địa điểm quanh {detail?.Name ?? "khu vực này"}. Bạn có thể xem danh sách bên dưới, mình đã ưu tiên các nơi gần nhất trước.";
        }

        if (plan.Intent == "nearby" && detail is not null)
        {
            return detail.Latitude is null || detail.Longitude is null
                ? "Khu trọ này chưa có tọa độ nên mình chưa thể tìm tiện ích xung quanh bằng VietMap."
                : "Mình chưa tìm thấy địa điểm phù hợp quanh khu trọ này từ VietMap. Bạn thử hỏi cụ thể hơn như trường học, quán ăn, chợ, cafe, siêu thị hoặc ATM nhé.";
        }

        if (detail is not null)
        {
            var policy = detail.RentalPolicy is null
                ? "Chưa có chính sách thuê chi tiết."
                : $"Cọc {detail.RentalPolicy.DepositMonths:0.##} tháng, thuê tối thiểu {detail.RentalPolicy.MinRentalMonths} tháng, ngày thanh toán mặc định là ngày {detail.RentalPolicy.DefaultPaymentDay}.";
            var amenities = detail.Amenities.Count > 0
                ? $" Tiện ích nổi bật: {string.Join(", ", detail.Amenities.Select(x => x.Name).Take(5))}."
                : " Chủ trọ chưa cập nhật tiện ích nổi bật.";
            var priceInfo = ExtractPriceSummary(detail);
            return $"{detail.Name} hiện còn {detail.AvailableRooms}/{detail.TotalRooms} phòng. {priceInfo}{policy}{amenities} Bạn có thể hỏi tiếp về nội quy, tiền cọc hoặc trường học/quán ăn xung quanh.";
        }

        if (searchResults.Count > 0)
        {
            if (plan.Intent == "compare")
            {
                return $"Mình tìm thấy {searchResults.Count} khu trọ để bạn so sánh. Xem các thẻ bên dưới để so sánh giá, phòng trống và vị trí nhé.";
            }

            return $"Mình tìm thấy {searchResults.Count} khu trọ phù hợp với câu hỏi của bạn. Mình đã đặt các thẻ kết quả bên dưới để bạn xem giá, phòng trống và vị trí nhanh hơn.";
        }

        return "Mình chưa tìm thấy dữ liệu phù hợp. Bạn có thể nói rõ khu vực, ngân sách, số người ở hoặc tiện ích mong muốn để mình tìm kỹ hơn.";
    }

    private static string ExtractPriceSummary(RoomingHouseDetailResponse detail)
    {
        var allActivePrices = detail.Rooms
            .Where(r => r.Status.Equals("Available", StringComparison.OrdinalIgnoreCase))
            .SelectMany(r => r.PriceTiers)
            .Where(t => t.IsActive)
            .Select(t => t.MonthlyRent)
            .ToList();

        if (allActivePrices.Count == 0)
        {
            return string.Empty;
        }

        var minPrice = allActivePrices.Min();
        var maxPrice = allActivePrices.Max();
        var formatPrice = (decimal p) => p >= 1_000_000
            ? $"{p / 1_000_000:0.#} triệu"
            : $"{p:N0} đ";

        if (minPrice == maxPrice)
        {
            return $"Giá từ {formatPrice(minPrice)}/tháng. ";
        }

        return $"Giá từ {formatPrice(minPrice)} - {formatPrice(maxPrice)}/tháng. ";
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

    private IReadOnlyList<ConversationMessage> ResolveConversationHistory(
        RoomingHouseAiChatRequest request,
        string conversationId)
    {
        var serverHistory = conversationCache.GetHistory(conversationId);
        if (serverHistory.Count > 0)
        {
            return serverHistory.TakeLast(12).ToList();
        }

        return request.ChatHistory
            .Select(message => new ConversationMessage(
                NormalizeHistoryRole(message.Role),
                message.Text.Trim()))
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .TakeLast(12)
            .ToList();
    }

    private static string NormalizeHistoryRole(string? role)
        => role?.Trim().Equals("user", StringComparison.OrdinalIgnoreCase) == true
            ? "user"
            : "assistant";

    private static bool ContainsAny(string source, params string[] terms)
        => terms.Any(source.Contains);

    private static List<string> ExtractNearbyKeywords(string normalized)
    {
        var keywords = new List<string>();
        if (ContainsAny(normalized, "quan an", "an uong", "com", "bun", "pho", "nha hang", "mon an", "an gi", "do an"))
        {
            keywords.Add("quán ăn");
        }

        if (ContainsAny(normalized, "cho", "cho dan sinh", "cho gan"))
        {
            keywords.Add("chợ");
        }

        if (ContainsAny(normalized, "cafe", "ca phe", "coffee"))
        {
            keywords.Add("cafe");
        }

        if (ContainsAny(normalized, "sieu thi", "tap hoa", "bach hoa", "cua hang tien loi", "circle k", "mini mart"))
        {
            keywords.Add("siêu thị");
        }

        if (ContainsAny(normalized, "phong gym", "gym", "tap gym", "the duc"))
        {
            keywords.Add("phòng gym");
        }

        if (ContainsAny(normalized, "benh vien", "phong kham", "kham benh", "y te"))
        {
            keywords.Add("bệnh viện");
        }

        if (ContainsAny(normalized, "truong hoc", "truong", "dai hoc", "cao dang", "mau giao", "hoc vien", "fpt", "bach khoa", "duy tan"))
        {
            keywords.Add("trường học");
        }

        if (ContainsAny(normalized, "tiem thuoc", "thuoc tay"))
        {
            keywords.Add("nhà thuốc");
        }

        if (ContainsAny(normalized, "atm", "ngan hang", "rut tien"))
        {
            keywords.Add("ATM");
        }

        if (ContainsAny(normalized, "tram xe buyt", "xe buyt", "bus"))
        {
            keywords.Add("trạm xe buýt");
        }

        if (ContainsAny(normalized, "cong vien", "the thao ngoai troi"))
        {
            keywords.Add("công viên");
        }

        if (ContainsAny(normalized, "giat ui", "giat say", "giat la", "laundry"))
        {
            keywords.Add("giặt ủi");
        }

        if (ContainsAny(normalized, "tien ich", "xung quanh co gi", "quanh do co gi", "gan do co gi"))
        {
            keywords.AddRange(new[] { "quán ăn", "chợ", "siêu thị", "trường học" });
        }

        return keywords.Count > 0 ? keywords : new List<string> { "quán ăn", "chợ" };
    }

    private static List<string> BuildLocationQueries(string searchQuery)
    {
        var values = new List<string>();
        var normalized = Normalize(searchQuery);
        if (normalized.Contains("fpt"))
        {
            values.Add("Đại học FPT Đà Nẵng");
            values.Add("FPT University Đà Nẵng");
        }

        var rawCleaned = CleanLocationQuery(searchQuery);
        if (!string.IsNullOrWhiteSpace(rawCleaned))
        {
            values.Add(rawCleaned);
        }

        values.Add(searchQuery);

        var cleaned = normalized
            .Replace("tim tro", string.Empty, StringComparison.Ordinal)
            .Replace("tim phong tro", string.Empty, StringComparison.Ordinal)
            .Replace("tim", string.Empty, StringComparison.Ordinal)
            .Replace("khu tro", string.Empty, StringComparison.Ordinal)
            .Replace("phong tro", string.Empty, StringComparison.Ordinal)
            .Replace("nha tro", string.Empty, StringComparison.Ordinal)
            .Replace("can ho", string.Empty, StringComparison.Ordinal)
            .Replace("gan", string.Empty, StringComparison.Ordinal)
            .Replace("xung quanh", string.Empty, StringComparison.Ordinal)
            .Replace("quanh", string.Empty, StringComparison.Ordinal)
            .Replace("o dau", string.Empty, StringComparison.Ordinal)
            .Replace("cho toi", string.Empty, StringComparison.Ordinal)
            .Replace("co", string.Empty, StringComparison.Ordinal)
            .Replace("nay", string.Empty, StringComparison.Ordinal)
            .Replace("nao", string.Empty, StringComparison.Ordinal)
            .Replace("khong", string.Empty, StringComparison.Ordinal)
            .Replace("vay", string.Empty, StringComparison.Ordinal)
            .Replace("xin", string.Empty, StringComparison.Ordinal)
            .Replace("hoi", string.Empty, StringComparison.Ordinal)
            .Replace("tu van", string.Empty, StringComparison.Ordinal)
            .Replace("tu", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            values.Add(cleaned);
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldResolveLocationFirst(string? value)
    {
        var normalized = Normalize(value);
        // Use word boundary matching to avoid false positives like "ngan" matching "gan"
        return Regex.IsMatch(
            normalized,
            @"\bgan\b|\bxung quanh\b|\bquanh\b|\bcach\b|\bgan khu vuc\b",
            RegexOptions.CultureInvariant);
    }

    private static decimal GetRequestedRadiusKm(params string?[] values)
    {
        foreach (var value in values)
        {
            var radiusKm = ExtractRadiusKm(value);
            if (radiusKm is not null)
            {
                return Math.Clamp(radiusKm.Value, 0.5m, 30m);
            }
        }

        return DefaultChatSearchRadiusKm;
    }

    private static int GetRequestedRadiusMeters(string? value, int defaultRadiusMeters)
    {
        var radiusKm = ExtractRadiusKm(value);
        if (radiusKm is null)
        {
            return defaultRadiusMeters;
        }

        return NormalizeNearbyRadiusMeters((int)Math.Round(radiusKm.Value * 1000m));
    }

    private static int NormalizeNearbyRadiusMeters(int radiusMeters)
    {
        if (radiusMeters <= 0)
        {
            return DefaultNearbyRadiusMeters;
        }

        return Math.Clamp(radiusMeters, 300, 30_000);
    }

    private static decimal? ExtractRadiusKm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Normalize(value);
        var match = Regex.Match(
            normalized,
            @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>km|kilomet|kilometer|met|meter|m)\b",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var rawValue = match.Groups["value"].Value.Replace(',', '.');
        if (!decimal.TryParse(
                rawValue,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var distance))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value;
        return unit is "m" or "met" or "meter"
            ? distance / 1000m
            : distance;
    }

    private static string CleanLocationQuery(string value)
    {
        var cleaned = value.Trim();
        cleaned = Regex.Replace(
            cleaned,
            @"\b\d+(?:[\.,]\d+)?\s*(?:km|kilomet|kilometer|m|met|meter)\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var removablePhrases = new[]
        {
            "Tìm phòng trọ", "tìm phòng trọ",
            "Tìm khu trọ", "tìm khu trọ",
            "Tìm nhà trọ", "tìm nhà trọ",
            "Tìm trọ", "tìm trọ",
            "Tìm", "tìm",
            "phòng trọ", "khu trọ", "nhà trọ", "trọ",
            "gần", "Gần",
            "xung quanh", "Xung quanh",
            "quanh", "Quanh",
            "ở đâu", "cho tôi"
        };

        foreach (var phrase in removablePhrases)
        {
            cleaned = cleaned.Replace(phrase, " ", StringComparison.Ordinal);
        }

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public string? LocationName { get; set; }
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
