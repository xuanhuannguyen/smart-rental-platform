using SmartRentalPlatform.Application.Common.Exceptions;

namespace SmartRentalPlatform.Application.Common;

public class KycBusinessException : AppException
{
    public KycBusinessException(string code, string message, int statusCode = 400)
        : base(code, message, statusCode)
    {
    }
}
