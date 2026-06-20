namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public class WalletResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
