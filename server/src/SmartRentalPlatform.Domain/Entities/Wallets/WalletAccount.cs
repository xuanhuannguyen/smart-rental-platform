using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Domain.Entities.Wallets;

public class WalletAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedBalance { get; set; }
    public string Currency { get; set; } = "VND";
    public WalletAccountStatus Status { get; set; } = WalletAccountStatus.Active;

    public User User { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
