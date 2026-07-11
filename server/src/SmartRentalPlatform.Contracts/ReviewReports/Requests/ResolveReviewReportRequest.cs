namespace SmartRentalPlatform.Contracts.ReviewReports.Requests;

public class ResolveReviewReportRequest
{
    public bool HideReview { get; set; }
    public string? AdminNote { get; set; }
}
