using System;
using System.Collections.Generic;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public class RoomingHouseReviewResponse
{
    public Guid Id { get; set; }
    public Guid RentalContractId { get; set; }
    public string? RoomNumber { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public Guid TenantUserId { get; set; }
    public string TenantDisplayName { get; set; } = string.Empty;
    public string? TenantAvatarUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? LandlordReply { get; set; }
    public DateTimeOffset? LandlordReplyCreatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsReported { get; set; }
    public string ModerationStatus { get; set; } = string.Empty;
    public string? ModerationReason { get; set; }
    public string? AiModerationProvider { get; set; }
    public string? AiModerationRiskLevel { get; set; }
    public string? AdminNote { get; set; }
    public List<PropertyImageResponse> Images { get; set; } = new();
}
