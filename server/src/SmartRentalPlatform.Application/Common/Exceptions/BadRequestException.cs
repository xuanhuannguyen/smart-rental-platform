namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class BadRequestException : AppException
{
    public BadRequestException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 400, details)
    {
    }
}