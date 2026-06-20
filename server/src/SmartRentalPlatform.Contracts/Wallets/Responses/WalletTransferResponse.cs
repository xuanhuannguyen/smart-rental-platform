namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public class WalletTransferResponse
{
    public Guid TransferGroupId { get; set; }
    public WalletTransactionResponse DebitTransaction { get; set; } = null!;
    public WalletTransactionResponse CreditTransaction { get; set; } = null!;
}
