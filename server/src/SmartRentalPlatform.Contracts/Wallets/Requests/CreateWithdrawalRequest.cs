namespace SmartRentalPlatform.Contracts.Wallets.Requests;

public sealed record CreateWithdrawalRequest(
    decimal Amount,
    string BankBin,
    string AccountNumber,
    string AccountName
);
