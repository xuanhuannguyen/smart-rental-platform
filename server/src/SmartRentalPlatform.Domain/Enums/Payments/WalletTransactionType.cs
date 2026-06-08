namespace SmartRentalPlatform.Domain.Enums.Payments;

public enum WalletTransactionType
{
    WalletTopUp,
    DepositPayment,
    DepositReceive,
    InvoicePayment,
    InvoiceReceive,
    DepositRefundDebit,
    DepositRefundCredit,
    DepositForfeitRelease
}
