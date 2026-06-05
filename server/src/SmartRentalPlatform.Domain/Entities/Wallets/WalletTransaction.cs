namespace SmartRentalPlatform.Domain.Entities.Wallets;

public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletAccountId { get; set; }
    public Guid UserId { get; set; }
    public Guid TransferGroupId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public WalletTransactionStatus Status { get; set; } = WalletTransactionStatus.Pending;

    public WalletAccount WalletAccount { get; set; } = null!;
}
