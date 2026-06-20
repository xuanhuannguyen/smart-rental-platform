namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Wallets;

// The richer Payments.WalletAccount model owns the shared wallet_accounts table.
// Keep this marker type so the develop-side namespace remains source-compatible,
// but do not register a second EF entity mapping for the same physical table.
internal static class WalletAccountConfiguration
{
}
