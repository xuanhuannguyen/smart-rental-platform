namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class ExternalServiceException : AppException
{
    public ExternalServiceException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 502, details)
    {
    }
}
