namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 429, details)
    {
    }
}
