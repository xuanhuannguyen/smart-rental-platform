namespace SmartRentalPlatform.Contracts.Wallets.Requests;

public class CreatePayOSTopUpRequest
{
    public decimal Amount { get; set; }
    public string? ReturnUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
}
