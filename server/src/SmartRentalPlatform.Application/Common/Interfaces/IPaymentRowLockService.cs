using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IPaymentRowLockService
{
    Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(
        string providerOrderCode,
        CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<WalletAccount?> LockWalletAccountAsync(
        Guid walletAccountId,
        CancellationToken cancellationToken = default);

    Task<Invoice?> LockInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<User?> LockUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RoomDeposit?> LockRoomDepositAsync(
        Guid roomDepositId,
        CancellationToken cancellationToken = default);

    Task<RentalContract?> LockRentalContractAsync(
        Guid contractId,
        CancellationToken cancellationToken = default);
}
