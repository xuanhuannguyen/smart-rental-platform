namespace SmartRentalPlatform.Application.Wallets.Options;

public class WithdrawalOptions
{
    public const string SectionName = "Withdrawal";
    public decimal FlatFee { get; set; } = 5000m;
}
