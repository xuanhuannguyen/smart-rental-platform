namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class StartESignEnvelopeRequest
{
    public bool AgreedToTerms { get; set; }
    public string? ReturnUrl { get; set; }
}
