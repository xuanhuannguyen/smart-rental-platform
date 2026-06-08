using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence;

public class PaymentRowLockService : IPaymentRowLockService
{
    private readonly AppDbContext context;

    public PaymentRowLockService(AppDbContext context)
    {
        this.context = context;
    }

    public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(
        string providerOrderCode,
        CancellationToken cancellationToken = default)
    {
        return context.PaymentTransactions
            .FromSqlInterpolated($"SELECT * FROM payment_transactions WHERE provider_order_code = {providerOrderCode} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return context.PaymentTransactions
            .FromSqlInterpolated($"SELECT * FROM payment_transactions WHERE idempotency_key = {idempotencyKey} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WalletAccount?> LockWalletAccountAsync(
        Guid walletAccountId,
        CancellationToken cancellationToken = default)
    {
        return context.WalletAccounts
            .FromSqlInterpolated($"SELECT * FROM wallet_accounts WHERE id = {walletAccountId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }
}
