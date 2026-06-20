namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public class WalletTransactionResponse
{
    public Guid Id { get; set; }
    public Guid WalletAccountId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TransferGroupId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal ReservedBalanceBefore { get; set; }
    public decimal ReservedBalanceAfter { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
