namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public sealed record WithdrawalRequestResponse
{
    public Guid Id { get; init; }
    public Guid WalletAccountId { get; init; }
    public decimal Amount { get; init; }
    public decimal Fee { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProviderOrderCode { get; init; } = string.Empty;
    public string BankBin { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
