namespace SmartRentalPlatform.Contracts.Common;

public static class ErrorCodes
{
    // Common
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string ForbiddenAdminOnly = "FORBIDDEN_ADMIN_ONLY";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string InvalidStatus = "INVALID_STATUS";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";

    // Auth
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string GoogleAccountExists = "GOOGLE_ACCOUNT_EXISTS";
    public const string InvalidEmailOrPassword = "INVALID_EMAIL_OR_PASSWORD";
    public const string EmailVerificationRequired = "EMAIL_VERIFICATION_REQUIRED";
    public const string UserLocked = "USER_LOCKED";
    public const string UserBanned = "USER_BANNED";
    public const string UserDeleted = "USER_DELETED";
    public const string TokenInvalid = "TOKEN_INVALID";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string RefreshTokenReuseDetected = "REFRESH_TOKEN_REUSE_DETECTED";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpResendTooSoon = "OTP_RESEND_TOO_SOON";
    public const string GoogleIdTokenInvalid = "GOOGLE_ID_TOKEN_INVALID";

    // Profile / Role / KYC
    public const string ProfileIncomplete = "PROFILE_INCOMPLETE";
    public const string KycRequired = "KYC_REQUIRED";
    public const string KycNotFound = "KYC_NOT_FOUND";
    public const string KycInvalidStatus = "KYC_INVALID_STATUS";
    public const string KycRejectReasonRequired = "KYC_REJECT_REASON_REQUIRED";
    public const string KycAlreadyApproved = "KYC_ALREADY_APPROVED";
    public const string KycPendingAdminReview = "KYC_PENDING_ADMIN_REVIEW";
    public const string FrontImageRequired = "FRONT_IMAGE_REQUIRED";
    public const string BackImageRequired = "BACK_IMAGE_REQUIRED";
    public const string SelfieRequired = "SELFIE_REQUIRED";
    public const string EkycDocumentFailed = "EKYC_DOCUMENT_FAILED";
    public const string AccountNotActive = "ACCOUNT_NOT_ACTIVE";
    public const string RoleAlreadyExists = "ROLE_ALREADY_EXISTS";
    public const string RoleGrantFailed = "ROLE_GRANT_FAILED";

    // Property / Room
    public const string HouseNotFound = "HOUSE_NOT_FOUND";
    public const string HouseInvalidStatus = "HOUSE_INVALID_STATUS";
    public const string HouseRejectReasonRequired = "HOUSE_REJECT_REASON_REQUIRED";
    public const string HouseNotApproved = "HOUSE_NOT_APPROVED";
    public const string HouseNotPublic = "HOUSE_NOT_PUBLIC";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string RoomDuplicateNumber = "ROOM_DUPLICATE_NUMBER";
    public const string RoomInvalidStatus = "ROOM_INVALID_STATUS";
    public const string RoomNotAvailable = "ROOM_NOT_AVAILABLE";
    public const string NoAvailableRoom = "NO_AVAILABLE_ROOM";
    public const string AmenityNotFound = "AMENITY_NOT_FOUND";
    public const string ImageInvalidOwner = "IMAGE_INVALID_OWNER";
    public const string PriceTierInvalid = "PRICE_TIER_INVALID";

    // Lease Policy
    public const string LeasePolicyRequired = "LEASE_POLICY_REQUIRED";
    public const string LeasePolicyInvalid = "LEASE_POLICY_INVALID";
}
