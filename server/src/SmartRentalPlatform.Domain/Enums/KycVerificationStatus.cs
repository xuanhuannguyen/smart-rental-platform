namespace SmartRentalPlatform.Domain.Enums;

public enum KycVerificationStatus
{
    Pending,
    PendingAdminReview,
    EkycFailed,
    Approved,
    Rejected
}
