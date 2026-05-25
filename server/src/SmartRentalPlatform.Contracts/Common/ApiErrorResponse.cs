namespace SmartRentalPlatform.Contracts.Common;

public class ApiErrorResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public object? Details { get; set; }

    public static ApiErrorResponse Create(string code, string message, object? details = null) =>
        new()
        {
            Success = false,
            Message = message,
            Code = code,
            Details = details
        };
}
