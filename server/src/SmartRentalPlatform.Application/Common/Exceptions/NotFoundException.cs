namespace SmartRentalPlatform.Application.Common.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string errorCode, string message, object? details = null)
        : base(errorCode, message, 404, details)
    {
    }
}
