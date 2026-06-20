namespace SmartRentalPlatform.Contracts.Wallets.Responses;

public class WalletMutationResponse
{
    public WalletResponse Wallet { get; set; } = null!;
    public WalletTransactionResponse Transaction { get; set; } = null!;
}
