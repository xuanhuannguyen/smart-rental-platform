using System;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;

public class RoomingHouseReviewEligibilitySummaryResponse
{
    public bool IsEligible { get; set; }
    public Guid? ContractId { get; set; }
    public RoomingHouseReviewResponse? ExistingReview { get; set; }
}
