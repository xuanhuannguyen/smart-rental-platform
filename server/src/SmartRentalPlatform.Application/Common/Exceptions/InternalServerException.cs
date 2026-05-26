namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class InternalServerException : AppException
{
    public InternalServerException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 500, details)
    {
    }
}
