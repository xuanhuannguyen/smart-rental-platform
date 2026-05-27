namespace SmartRentalPlatform.Domain.Enums;

public enum KycVerificationStatus
{
    Pending,
    PendingEkyc,
    EkycFailed,
    PendingAdminReview,
    Approved,
    Rejected,
    Cancelled
}
