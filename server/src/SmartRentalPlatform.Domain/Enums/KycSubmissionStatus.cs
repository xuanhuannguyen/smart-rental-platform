namespace SmartRentalPlatform.Domain.Enums;

public enum KycSubmissionStatus
{
    Draft = 0,
    PendingEkyc = 1,
    EkycFailed = 2,
    PendingAdminReview = 3,
    Approved = 4,
    Rejected = 5,
    Cancelled = 6
}