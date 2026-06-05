namespace SmartRentalPlatform.Application.Billing;

public class PendingInvoiceWalletPaymentService : IInvoiceWalletPaymentService
{
    public Task<InvoiceWalletPaymentResult> PayInvoiceAsync(
        Guid invoiceId,
        Guid tenantUserId,
        Guid landlordUserId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InvoiceWalletPaymentResult(
            false,
            null,
            "InvoiceWalletPaymentService chua duoc cau hinh boi module Vi."));
    }
}
