using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Application.Wallets;

public class WalletService : IWalletService
{
    private const string DefaultCurrency = "VND";
    private const int MaxPageSize = 100;

    private readonly IAppDbContext context;
    private readonly IPaymentRowLockService rowLockService;

    public WalletService(
        IAppDbContext context,
        IPaymentRowLockService rowLockService)
    {
        this.context = context;
        this.rowLockService = rowLockService;
    }

    public async Task<WalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await GetOrCreateWalletAsync(userId, cancellationToken);
        return ToWalletResponse(wallet);
    }

    public async Task<WalletAccount> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await LoadUserByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException(ErrorCodes.NotFound, "User not found.");
            }

            if (user.Status != UserStatus.Active)
            {
                throw new ForbiddenException(
                    ErrorCodes.AccountNotActive,
                    "User account is not active.",
                    new { user.Status });
            }

            var existing = await LoadWalletByUserIdAsync(userId, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }

            var now = DateTimeOffset.UtcNow;
            var wallet = new WalletAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Balance = 0,
                ReservedBalance = 0,
                Currency = DefaultCurrency,
                Status = WalletAccountStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.WalletAccounts.Add(wallet);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return wallet;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PagedResult<WalletTransactionResponse>> GetTransactionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = context.WalletTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToWalletTransactionResponse(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<WalletTransactionResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public Task<WalletMutationResponse> CreditAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return MutateWalletAsync(
            walletAccountId,
            amount,
            transactionType,
            WalletTransactionDirection.Credit,
            balanceDelta: amount,
            reservedBalanceDelta: 0,
            metadata,
            cancellationToken);
    }

    public Task<WalletMutationResponse> DebitAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return MutateWalletAsync(
            walletAccountId,
            amount,
            transactionType,
            WalletTransactionDirection.Debit,
            balanceDelta: -amount,
            reservedBalanceDelta: 0,
            metadata,
            cancellationToken);
    }

    public Task<WalletMutationResponse> IncreaseReservedAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return MutateWalletAsync(
            walletAccountId,
            amount,
            transactionType,
            WalletTransactionDirection.Credit,
            balanceDelta: 0,
            reservedBalanceDelta: amount,
            metadata,
            cancellationToken);
    }

    public Task<WalletMutationResponse> DecreaseReservedAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return MutateWalletAsync(
            walletAccountId,
            amount,
            transactionType,
            WalletTransactionDirection.Debit,
            balanceDelta: 0,
            reservedBalanceDelta: -amount,
            metadata,
            cancellationToken);
    }

    private async Task<WalletMutationResponse> MutateWalletAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionDirection direction,
        decimal balanceDelta,
        decimal reservedBalanceDelta,
        WalletTransactionMetadata? metadata,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var wallet = await rowLockService.LockWalletAccountAsync(walletAccountId, cancellationToken);
            if (wallet is null)
            {
                throw new NotFoundException(ErrorCodes.NotFound, "Wallet account not found.");
            }

            var walletTransaction = ApplyWalletMutation(
                wallet,
                amount,
                transactionType,
                direction,
                balanceDelta,
                reservedBalanceDelta,
                metadata);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new WalletMutationResponse
            {
                Wallet = ToWalletResponse(wallet),
                Transaction = ToWalletTransactionResponse(walletTransaction)
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<WalletTransferResponse> TransferAsync(
        Guid sourceWalletAccountId,
        Guid targetWalletAccountId,
        decimal amount,
        WalletTransactionType debitTransactionType,
        WalletTransactionType creditTransactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAmount(amount);

        if (sourceWalletAccountId == targetWalletAccountId)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Source and target wallets must be different.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var firstWalletId = sourceWalletAccountId.CompareTo(targetWalletAccountId) <= 0
                ? sourceWalletAccountId
                : targetWalletAccountId;
            var secondWalletId = firstWalletId == sourceWalletAccountId
                ? targetWalletAccountId
                : sourceWalletAccountId;

            var firstWallet = await rowLockService.LockWalletAccountAsync(firstWalletId, cancellationToken);
            var secondWallet = await rowLockService.LockWalletAccountAsync(secondWalletId, cancellationToken);

            var sourceWallet = firstWallet?.Id == sourceWalletAccountId ? firstWallet : secondWallet;
            var targetWallet = firstWallet?.Id == targetWalletAccountId ? firstWallet : secondWallet;

            if (sourceWallet is null)
            {
                throw new NotFoundException(ErrorCodes.NotFound, "Source wallet account not found.");
            }

            if (targetWallet is null)
            {
                throw new NotFoundException(ErrorCodes.NotFound, "Target wallet account not found.");
            }

            var transferGroupId = metadata?.TransferGroupId ?? Guid.NewGuid();
            var debitMetadata = CopyMetadata(metadata, transferGroupId, "DevTestTransfer");
            var creditMetadata = CopyMetadata(metadata, transferGroupId, "DevTestTransfer");

            var debitTransaction = ApplyWalletMutation(
                sourceWallet,
                amount,
                debitTransactionType,
                WalletTransactionDirection.Debit,
                balanceDelta: -amount,
                reservedBalanceDelta: 0,
                debitMetadata);

            var creditTransaction = ApplyWalletMutation(
                targetWallet,
                amount,
                creditTransactionType,
                WalletTransactionDirection.Credit,
                balanceDelta: amount,
                reservedBalanceDelta: 0,
                creditMetadata);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new WalletTransferResponse
            {
                TransferGroupId = transferGroupId,
                DebitTransaction = ToWalletTransactionResponse(debitTransaction),
                CreditTransaction = ToWalletTransactionResponse(creditTransaction)
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private WalletTransaction ApplyWalletMutation(
        WalletAccount wallet,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionDirection direction,
        decimal balanceDelta,
        decimal reservedBalanceDelta,
        WalletTransactionMetadata? metadata)
    {
        EnsureWalletActive(wallet);

        var balanceBefore = wallet.Balance;
        var reservedBefore = wallet.ReservedBalance;
        var balanceAfter = balanceBefore + balanceDelta;
        var reservedAfter = reservedBefore + reservedBalanceDelta;

        ValidateWalletInvariants(balanceAfter, reservedAfter);

        wallet.Balance = balanceAfter;
        wallet.ReservedBalance = reservedAfter;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            UserId = wallet.UserId,
            TransferGroupId = metadata?.TransferGroupId,
            TransactionType = transactionType,
            Direction = direction,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReservedBalanceBefore = reservedBefore,
            ReservedBalanceAfter = reservedAfter,
            RelatedEntityType = metadata?.RelatedEntityType,
            RelatedEntityId = metadata?.RelatedEntityId,
            Description = metadata?.Description,
            Status = WalletTransactionStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.WalletTransactions.Add(walletTransaction);
        return walletTransaction;
    }

    private static WalletTransactionMetadata CopyMetadata(
        WalletTransactionMetadata? metadata,
        Guid transferGroupId,
        string relatedEntityType)
    {
        return new WalletTransactionMetadata
        {
            TransferGroupId = transferGroupId,
            RelatedEntityType = metadata?.RelatedEntityType ?? relatedEntityType,
            RelatedEntityId = metadata?.RelatedEntityId,
            Description = metadata?.Description
        };
    }

    private Task<User?> LoadUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return context.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    private Task<WalletAccount?> LoadWalletByIdAsync(Guid walletAccountId, CancellationToken cancellationToken)
    {
        return context.WalletAccounts
            .FirstOrDefaultAsync(x => x.Id == walletAccountId, cancellationToken);
    }

    private Task<WalletAccount?> LoadWalletByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return context.WalletAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Wallet amount must be greater than zero.");
        }
    }

    private static void EnsureWalletActive(WalletAccount wallet)
    {
        if (wallet.Status != WalletAccountStatus.Active)
        {
            throw new ConflictException(
                ErrorCodes.InvalidStatus,
                "Wallet account is not active.",
                new { wallet.Status });
        }
    }

    private static void ValidateWalletInvariants(decimal balance, decimal reservedBalance)
    {
        if (balance < 0)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Wallet balance cannot be negative.");
        }

        if (reservedBalance < 0)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Reserved balance cannot be negative.");
        }

        if (reservedBalance > balance)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Reserved balance cannot exceed wallet balance.");
        }
    }

    private static WalletResponse ToWalletResponse(WalletAccount wallet)
    {
        return new WalletResponse
        {
            Id = wallet.Id,
            UserId = wallet.UserId,
            Balance = wallet.Balance,
            ReservedBalance = wallet.ReservedBalance,
            AvailableBalance = wallet.Balance - wallet.ReservedBalance,
            Currency = wallet.Currency,
            Status = wallet.Status.ToString(),
            CreatedAt = wallet.CreatedAt,
            UpdatedAt = wallet.UpdatedAt
        };
    }

    private static WalletTransactionResponse ToWalletTransactionResponse(WalletTransaction transaction)
    {
        return new WalletTransactionResponse
        {
            Id = transaction.Id,
            WalletAccountId = transaction.WalletAccountId,
            UserId = transaction.UserId,
            TransferGroupId = transaction.TransferGroupId,
            TransactionType = transaction.TransactionType.ToString(),
            Direction = transaction.Direction.ToString(),
            Amount = transaction.Amount,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            ReservedBalanceBefore = transaction.ReservedBalanceBefore,
            ReservedBalanceAfter = transaction.ReservedBalanceAfter,
            RelatedEntityType = transaction.RelatedEntityType,
            RelatedEntityId = transaction.RelatedEntityId,
            Description = transaction.Description,
            Status = transaction.Status.ToString(),
            CreatedAt = transaction.CreatedAt
        };
    }
}
