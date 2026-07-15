using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Domain.Entities.Payments;

public class WithdrawalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.PendingApproval;
    
    // PayOS specific
    public string ProviderOrderCode { get; set; } = string.Empty;
    public string? ProviderTransactionCode { get; set; }
    public string BankBin { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public WalletAccount WalletAccount { get; set; } = null!;
}
