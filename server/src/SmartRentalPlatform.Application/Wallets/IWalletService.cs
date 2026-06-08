using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Application.Wallets;

public interface IWalletService
{
    Task<WalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<WalletAccount> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<WalletTransactionResponse>> GetTransactionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<WalletMutationResponse> CreditAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    Task<WalletMutationResponse> DebitAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    Task<WalletMutationResponse> IncreaseReservedAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    Task<WalletMutationResponse> DecreaseReservedAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default);
}
