namespace SmartRentalPlatform.Contracts.Common;

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;

    public string ErrorCode { get; set; } = default!;

    public string Message { get; set; } = default!;

    public object? Details { get; set; }
}

// {
//   "success": false,
//   "errorCode": "INVALID_EMAIL_OR_PASSWORD",
//   "message": "Email hoặc mật khẩu không đúng.",
//   "details": null
// }
// {
//   "success": false,
//   "errorCode": "KYC_INVALID_STATUS",
//   "message": "Only PendingAdminReview KYC can be approved.",
//   "details": {
//     "currentStatus": "Approved"
//   }
// }