using SmartRentalPlatform.Contracts.PropertyImages.Responses;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public sealed class AdminReviewModerationItemResponse
{
    public Guid Id { get; set; }
    public Guid RoomingHouseId { get; set; }
    public string RoomingHouseName { get; set; } = string.Empty;
    public Guid TenantUserId { get; set; }
    public string TenantDisplayName { get; set; } = string.Empty;
    public string? TenantAvatarUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string ModerationStatus { get; set; } = string.Empty;
    public string? ModerationReason { get; set; }
    public string? AiModerationProvider { get; set; }
    public string? AiModerationRiskLevel { get; set; }
    public string? AiModerationCategories { get; set; }
    public string? AiContentComment { get; set; }
    public string? AiImageComment { get; set; }
    public DateTimeOffset? AiReviewedAt { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<PropertyImageResponse> Images { get; set; } = new();
}
