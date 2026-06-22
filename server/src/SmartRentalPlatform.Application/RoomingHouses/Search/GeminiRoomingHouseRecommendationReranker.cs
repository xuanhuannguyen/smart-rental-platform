using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Options;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class GeminiRoomingHouseRecommendationReranker : IRoomingHouseRecommendationReranker
{
    private readonly IAiStructuredOutputService ai;
    private readonly GeminiOptions options;
    private readonly ILogger<GeminiRoomingHouseRecommendationReranker> logger;

    public GeminiRoomingHouseRecommendationReranker(
        IAiStructuredOutputService ai,
        IOptions<GeminiOptions> options,
        ILogger<GeminiRoomingHouseRecommendationReranker> logger)
    {
        this.ai = ai;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<RoomingHouseRecommendationRerankResult?> RerankAsync(
        GuestRoomingHouseRecommendationRequest request,
        IReadOnlyList<RoomingHouseRecommendationCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled ||
            !options.UseAiGuestRecommendations ||
            string.IsNullOrWhiteSpace(options.ApiKey) ||
            candidates.Count == 0)
        {
            logger.LogInformation(
                "AI recommendation reranker skipped. Enabled={Enabled}, UseAiGuestRecommendations={UseAiGuestRecommendations}, HasApiKey={HasApiKey}, CandidateCount={CandidateCount}",
                options.Enabled,
                options.UseAiGuestRecommendations,
                !string.IsNullOrWhiteSpace(options.ApiKey),
                candidates.Count);
            return null;
        }

        var candidateIds = candidates.Select(x => x.Id).ToHashSet();
        var input = new
        {
            behavior = new
            {
                request.RecentQueries,
                request.RecentRoomingHouseIds,
                request.ClickedRoomingHouseIds,
                request.PreferredAmenityIds,
                request.PreferredRoomAmenityIds,
                request.ProvinceCode,
                request.WardCode,
                request.MinPrice,
                request.MaxPrice,
                request.MinAreaM2,
                request.MaxAreaM2
            },
            candidates
        };

        try
        {
            var result = await ai.CreateJsonAsync<AiRecommendationResult>(
                "rooming_house_recommendations",
                BuildRecommendationSchema(),
                BuildRecommendationInstructions(),
                input,
                cancellationToken);

            if (result is null)
            {
                return null;
            }

            var rankedIds = result.RankedIds
                .Where(candidateIds.Contains)
                .Distinct()
                .ToList();
            var reasons = result.Reasons
                .Where(x => candidateIds.Contains(x.Id) && !string.IsNullOrWhiteSpace(x.Reason))
                .ToDictionary(x => x.Id, x => x.Reason.Trim());

            return rankedIds.Count == 0
                ? null
                : new RoomingHouseRecommendationRerankResult
                {
                    RankedIds = rankedIds,
                    Reasons = reasons
                };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI recommendation rerank failed.");
            return null;
        }
    }

    private static string BuildRecommendationInstructions()
        => """
        Bạn là hệ thống gợi ý khu trọ cho người thuê ở Việt Nam.
        Chỉ sắp xếp lại candidate được cung cấp, không tạo id mới.
        Ưu tiên khu vực, ngân sách, tiện ích và hành vi tìm kiếm/click gần đây.
        Lý do hiển thị bằng tiếng Việt, ngắn gọn, tự nhiên, tối đa 22 từ mỗi khu trọ.
        Trả JSON đúng schema.
        """;

    private static object BuildRecommendationSchema()
        => new
        {
            type = "object",
            properties = new
            {
                rankedIds = new
                {
                    type = "array",
                    items = new { type = "string", format = "uuid" }
                },
                reasons = new
                {
                    type = "array",
                    items = new
                {
                    type = "object",
                    properties = new
                    {
                            id = new { type = "string", format = "uuid" },
                            reason = new { type = "string" }
                        },
                        required = new[] { "id", "reason" }
                    }
                }
            },
            required = new[] { "rankedIds", "reasons" }
        };

    private sealed class AiRecommendationResult
    {
        public List<Guid> RankedIds { get; set; } = new();

        public List<AiRecommendationReason> Reasons { get; set; } = new();
    }

    private sealed class AiRecommendationReason
    {
        public Guid Id { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
