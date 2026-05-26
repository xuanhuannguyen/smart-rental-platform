namespace SmartRentalPlatform.Contracts.Common;

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;

    public string ErrorCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public object? Details { get; set; }

    public static ApiErrorResponse Create(
        string errorCode,
        string message,
        object? details = null)
    {
        return new ApiErrorResponse
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message,
            Details = details
        };
    }
}