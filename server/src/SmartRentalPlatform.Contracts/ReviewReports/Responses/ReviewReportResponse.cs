using System;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

namespace SmartRentalPlatform.Contracts.ReviewReports.Responses;

public class ReviewReportResponse
{
    public Guid Id { get; set; }
    public Guid RoomingHouseReviewId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterDisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public RoomingHouseReviewResponse? Review { get; set; }
    public string RoomingHouseName { get; set; } = string.Empty;
}
