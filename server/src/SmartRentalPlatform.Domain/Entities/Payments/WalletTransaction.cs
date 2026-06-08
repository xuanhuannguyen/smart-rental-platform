using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Domain.Entities.Payments;

public class WalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletAccountId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TransferGroupId { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public WalletTransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal ReservedBalanceBefore { get; set; }
    public decimal ReservedBalanceAfter { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? Description { get; set; }
    public WalletTransactionStatus Status { get; set; } = WalletTransactionStatus.Succeeded;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public WalletAccount WalletAccount { get; set; } = null!;
    public User User { get; set; } = null!;
}
