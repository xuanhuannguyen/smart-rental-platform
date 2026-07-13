using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public class RoomingHouseReviewEligibilitySummaryResponse
{
    public bool IsEligible { get; set; }
    public Guid? ContractId { get; set; }
    public string? Reason { get; set; }
    public RoomingHouseReviewResponse? ExistingReview { get; set; }
    public List<ReviewableContractResponse> ReviewableContracts { get; set; } = new();
}

public class ReviewableContractResponse
{
    public Guid ContractId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanReview { get; set; }
    public string? ReviewStatus { get; set; }
    public Guid? ReviewId { get; set; }
    public RoomingHouseReviewResponse? Review { get; set; }
}
