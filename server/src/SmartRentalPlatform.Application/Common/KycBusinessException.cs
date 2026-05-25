namespace SmartRentalPlatform.Application.Common;

public class KycBusinessException : Exception
{
    public KycBusinessException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
