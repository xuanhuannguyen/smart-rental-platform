namespace SmartRentalPlatform.Domain.Enums.Kyc;

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

