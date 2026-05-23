namespace SmartRentalPlatform.Application.Common.Exceptions;

public class AppException : Exception
{
    public AppException(string errorCode, string message, int statusCode, object? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }

    public object? Details { get; }
}
