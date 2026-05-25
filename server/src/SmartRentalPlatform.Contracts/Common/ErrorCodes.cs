namespace SmartRentalPlatform.Contracts.Common;

public static class ErrorCodes
{
    public const string Unauthorized = "UNAUTHORIZED";
    public const string AccountNotActive = "ACCOUNT_NOT_ACTIVE";
    public const string KycAlreadyApproved = "KYC_ALREADY_APPROVED";
    public const string KycPendingAdminReview = "KYC_PENDING_ADMIN_REVIEW";
    public const string FrontImageRequired = "FRONT_IMAGE_REQUIRED";
    public const string BackImageRequired = "BACK_IMAGE_REQUIRED";
    public const string SelfieRequired = "SELFIE_REQUIRED";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string EkycDocumentFailed = "EKYC_DOCUMENT_FAILED";
}
