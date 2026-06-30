namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class SignContractRequest
{
    public string Otp { get; set; } = default!;

    public string? SignatureText { get; set; }
}
