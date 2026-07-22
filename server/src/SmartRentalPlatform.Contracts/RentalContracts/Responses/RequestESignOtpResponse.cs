namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class RequestESignOtpResponse
{
    public int Method { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MaskedDestination { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}
