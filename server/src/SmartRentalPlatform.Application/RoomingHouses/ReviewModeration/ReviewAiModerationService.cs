using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses.ReviewModeration;

public sealed class ReviewAiModerationService : IReviewAiModerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppDbContext context;
    private readonly IAiStructuredOutputService primaryAi;
    private readonly IBackupAiStructuredOutputService backupAi;
    private readonly IMediaAccessService mediaAccessService;
    private readonly ILogger<ReviewAiModerationService> logger;

    public ReviewAiModerationService(
        IAppDbContext context,
        IAiStructuredOutputService primaryAi,
        IBackupAiStructuredOutputService backupAi,
        IMediaAccessService mediaAccessService,
        ILogger<ReviewAiModerationService> logger)
    {
        this.context = context;
        this.primaryAi = primaryAi;
        this.backupAi = backupAi;
        this.mediaAccessService = mediaAccessService;
        this.logger = logger;
    }

    public async Task<int> ModeratePendingReviewsAsync(
        int batchSize = 20,
        CancellationToken cancellationToken = default)
    {
        var reviewIds = await context.RoomingHouseReviews
            .Where(x => !x.IsHidden && x.ModerationStatus == RoomingHouseReviewModerationStatus.PendingAiReview)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var reviewId in reviewIds)
        {
            await ModerateReviewAsync(reviewId, cancellationToken);
        }

        return reviewIds.Count;
    }

    public async Task ModerateReviewAsync(
        Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        var review = await context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .Include(x => x.TenantUser)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review is null || review.IsHidden || review.ModerationStatus != RoomingHouseReviewModerationStatus.PendingAiReview)
        {
            return;
        }

        var input = new
        {
            review.Id,
            roomingHouseName = review.RoomingHouse.Name,
            tenantName = review.TenantUser.DisplayName,
            review.Rating,
            review.Comment,
            imageCount = review.Images.Count(x => x.MediaAssetId.HasValue),
            images = review.Images
                .Where(x => x.MediaAssetId.HasValue)
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.Id,
                    x.MediaAssetId,
                    x.Caption,
                    x.SortOrder
                })
                .ToList(),
            createdAt = review.CreatedAt,
            updatedAt = review.UpdatedAt
        };

        var schema = new
        {
            type = "object",
            properties = new
            {
                decision = new { type = "string", @enum = new[] { "Approve", "NeedsAdminReview" } },
                riskLevel = new { type = "string", @enum = new[] { "None", "Low", "Medium", "High" } },
                categories = new { type = "array", items = new { type = "string" } },
                reason = new { type = "string" },
                contentComment = new { type = "string" },
                imageComment = new { type = "string" }
            },
            required = new[] { "decision", "riskLevel", "categories", "reason", "contentComment", "imageComment" }
        };

        var instructions = """
        Bạn là hệ thống kiểm duyệt đánh giá khu trọ. Hãy duyệt cả nội dung chữ và ảnh đính kèm nếu có.
        decision = Approve nếu review bình thường, đúng ngữ cảnh thuê trọ/phòng trọ/khu trọ, kể cả review tiêu cực nhưng diễn đạt phù hợp.
        decision = NeedsAdminReview nếu phần chữ hoặc ảnh có một trong các dấu hiệu:
        - xúc phạm/thù ghét, tục tĩu nặng, đe dọa, kích động bạo lực;
        - spam, quảng cáo, lừa đảo, nội dung không liên quan thuê trọ;
        - thông tin cá nhân nhạy cảm như CCCD/hộ chiếu, số tài khoản, số điện thoại, email riêng tư, địa chỉ cá nhân ngoài ngữ cảnh khu trọ;
        - ảnh nhạy cảm, khỏa thân, bạo lực, chất cấm, vũ khí, máu me;
        - ảnh có mặt người/biển số xe/tài liệu riêng tư quá rõ cần admin che hoặc xác minh;
        - cáo buộc nghiêm trọng cần admin xem lại;
        - ảnh không liên quan hoặc có watermark/logo quảng cáo app khác.
        Không đánh rớt chỉ vì review tiêu cực; tenant được quyền chê khu trọ nếu dùng ngôn ngữ phù hợp.
        reason, contentComment và imageComment phải viết bằng tiếng Việt, ngắn gọn, dễ hiểu cho admin.
        contentComment: nhận xét riêng về chữ/rating/bối cảnh nội dung.
        imageComment: nhận xét riêng về ảnh đính kèm; nếu không có ảnh hãy ghi "Không có ảnh đính kèm để đánh giá.".
        Trả JSON đúng schema, không giải thích ngoài JSON.
        """;

        var images = await LoadReviewImagesAsync(review, cancellationToken);
        var result = await TryModerateWithVertexAsync(schema, instructions, input, images, cancellationToken);

        if (result is null && images.Count == 0)
        {
            result = await TryModerateWithProviderAsync("DeepSeek", backupAi, schema, instructions, input, cancellationToken);
        }

        if (result is null)
        {
            ApplyPendingAdminReview(
                review,
                images.Count > 0 ? "VertexUnavailable" : "Unavailable",
                "High",
                images.Count > 0 ? ["vertex_unavailable_with_images"] : ["ai_unavailable"],
                images.Count > 0
                    ? "Vertex không phản hồi khi duyệt ảnh, cần admin duyệt thủ công."
                    : "AI không phản hồi hoặc không chắc chắn, cần admin duyệt.",
                null);
        }
        else if (IsApprove(result.Decision))
        {
            ApplyApproved(review, result);
        }
        else
        {
            ApplyPendingAdminReview(
                review,
                result.Provider,
                result.RiskLevel,
                result.Categories,
                result.Reason,
                result.RawJson);
        }

        await context.SaveChangesAsync(cancellationToken);
        await RoomingHouseRatingHelper.UpdateRatingAsync(context, review.RoomingHouseId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ModerationDecision?> TryModerateWithVertexAsync(
        object schema,
        string instructions,
        object input,
        IReadOnlyCollection<AiImageInput> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await primaryAi.CreateJsonWithImagesAsync<ModerationDecisionPayload>(
                "rooming_house_review_moderation",
                schema,
                instructions,
                input,
                images,
                cancellationToken);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Decision))
            {
                return null;
            }

            return new ModerationDecision
            {
                Provider = "Vertex",
                Decision = payload.Decision,
                RiskLevel = string.IsNullOrWhiteSpace(payload.RiskLevel) ? "Medium" : payload.RiskLevel.Trim(),
                Categories = payload.Categories ?? [],
                Reason = string.IsNullOrWhiteSpace(payload.Reason) ? "AI yêu cầu admin xem lại." : payload.Reason.Trim(),
                ContentComment = string.IsNullOrWhiteSpace(payload.ContentComment) ? null : payload.ContentComment.Trim(),
                ImageComment = string.IsNullOrWhiteSpace(payload.ImageComment) ? null : payload.ImageComment.Trim(),
                RawJson = Truncate(JsonSerializer.Serialize(payload, JsonOptions), 4000)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vertex review moderation failed.");
            return null;
        }
    }

    private async Task<List<AiImageInput>> LoadReviewImagesAsync(
        RoomingHouseReview review,
        CancellationToken cancellationToken)
    {
        var images = new List<AiImageInput>();

        foreach (var image in review.Images
                     .Where(x => x.MediaAssetId.HasValue)
                     .OrderBy(x => x.SortOrder)
                     .Take(4))
        {
            try
            {
                var mediaAssetId = image.MediaAssetId!.Value;
                var accessResult = await mediaAccessService.OpenReadAsync(
                    mediaAssetId,
                    review.TenantUserId,
                    cancellationToken);
                await using var stream = accessResult.Stream;
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                images.Add(new AiImageInput(
                    string.IsNullOrWhiteSpace(accessResult.DownloadFileName)
                        ? $"{image.Id:N}.jpg"
                        : accessResult.DownloadFileName,
                    accessResult.ContentType,
                    memory.ToArray()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not load review image {MediaAssetId} for AI moderation.", image.MediaAssetId);
            }
        }

        return images;
    }

    private async Task<ModerationDecision?> TryModerateWithProviderAsync(
        string provider,
        object aiService,
        object schema,
        string instructions,
        object input,
        CancellationToken cancellationToken)
    {
        try
        {
            ModerationDecisionPayload? payload = aiService switch
            {
                IAiStructuredOutputService primary => await primary.CreateJsonAsync<ModerationDecisionPayload>(
                    "rooming_house_review_moderation",
                    schema,
                    instructions,
                    input,
                    cancellationToken),
                IBackupAiStructuredOutputService backup => await backup.CreateJsonAsync<ModerationDecisionPayload>(
                    "rooming_house_review_moderation",
                    schema,
                    instructions,
                    input,
                    cancellationToken),
                _ => null
            };

            if (payload is null || string.IsNullOrWhiteSpace(payload.Decision))
            {
                return null;
            }

            return new ModerationDecision
            {
                Provider = provider,
                Decision = payload.Decision,
                RiskLevel = string.IsNullOrWhiteSpace(payload.RiskLevel) ? "Medium" : payload.RiskLevel.Trim(),
                Categories = payload.Categories ?? [],
                Reason = string.IsNullOrWhiteSpace(payload.Reason) ? "AI yêu cầu admin xem lại." : payload.Reason.Trim(),
                ContentComment = string.IsNullOrWhiteSpace(payload.ContentComment) ? null : payload.ContentComment.Trim(),
                ImageComment = string.IsNullOrWhiteSpace(payload.ImageComment) ? null : payload.ImageComment.Trim(),
                RawJson = Truncate(JsonSerializer.Serialize(payload, JsonOptions), 4000)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Provider} review moderation failed.", provider);
            return null;
        }
    }

    private static void ApplyApproved(RoomingHouseReview review, ModerationDecision decision)
    {
        review.ModerationStatus = RoomingHouseReviewModerationStatus.Approved;
        review.ModerationReason = decision.Reason;
        review.AiModerationProvider = decision.Provider;
        review.AiModerationRiskLevel = decision.RiskLevel;
        review.AiModerationCategories = string.Join(", ", decision.Categories);
        review.AiModerationJson = decision.RawJson;
        review.AiReviewedAt = DateTimeOffset.UtcNow;
        review.AdminNote = null;
        review.ReviewedByAdminId = null;
        review.AdminReviewedAt = null;
        review.IsHidden = false;
    }

    private static void ApplyPendingAdminReview(
        RoomingHouseReview review,
        string provider,
        string riskLevel,
        List<string> categories,
        string reason,
        string? rawJson)
    {
        review.ModerationStatus = RoomingHouseReviewModerationStatus.PendingAdminReview;
        review.ModerationReason = reason;
        review.AiModerationProvider = provider;
        review.AiModerationRiskLevel = riskLevel;
        review.AiModerationCategories = string.Join(", ", categories);
        review.AiModerationJson = rawJson;
        review.AiReviewedAt = DateTimeOffset.UtcNow;
        review.IsHidden = false;
    }

    private static bool IsApprove(string decision)
        => decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private sealed class ModerationDecisionPayload
    {
        public string Decision { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "Medium";
        public List<string> Categories { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string? ContentComment { get; set; }
        public string? ImageComment { get; set; }
    }

    private sealed class ModerationDecision
    {
        public string Provider { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "Medium";
        public List<string> Categories { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public string? ContentComment { get; set; }
        public string? ImageComment { get; set; }
        public string? RawJson { get; set; }
    }
}
