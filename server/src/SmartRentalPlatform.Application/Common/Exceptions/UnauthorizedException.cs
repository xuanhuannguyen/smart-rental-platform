namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 401, details)
    {
    }
}