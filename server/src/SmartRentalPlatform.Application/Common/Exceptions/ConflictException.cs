namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 409, details)
    {
    }
}
