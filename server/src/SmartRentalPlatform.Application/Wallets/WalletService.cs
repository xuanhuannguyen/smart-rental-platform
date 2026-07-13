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
        var isExternalTransaction = context.HasActiveTransaction;
        var transaction = isExternalTransaction ? null : await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await rowLockService.LockUserAsync(userId, cancellationToken);
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
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
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
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            return wallet;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
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
            WalletTransactionDirection.Debit,
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
            WalletTransactionDirection.Credit,
            balanceDelta: 0,
            reservedBalanceDelta: -amount,
            metadata,
            cancellationToken);
    }

    public Task<WalletMutationResponse> DebitFromReservedAsync(
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

        var isExternalTransaction = context.HasActiveTransaction;
        var transaction = isExternalTransaction ? null : await context.BeginTransactionAsync(cancellationToken);

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
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            return new WalletMutationResponse
            {
                Wallet = ToWalletResponse(wallet),
                Transaction = ToWalletTransactionResponse(walletTransaction)
            };
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
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
        var isExternalTransaction = context.HasActiveTransaction;
        var transaction = isExternalTransaction ? null : await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await TransferWithinTransactionAsync(
                sourceWalletAccountId,
                targetWalletAccountId,
                amount,
                debitTransactionType,
                creditTransactionType,
                metadata,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<WalletTransferResponse> TransferWithinTransactionAsync(
        Guid sourceWalletAccountId,
        Guid targetWalletAccountId,
        decimal amount,
        WalletTransactionType debitTransactionType,
        WalletTransactionType creditTransactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return await TransferWithinTransactionCoreAsync(
            sourceWalletAccountId,
            targetWalletAccountId,
            amount,
            debitTransactionType,
            creditTransactionType,
            sourceReservedBalanceDelta: 0,
            targetReservedBalanceDelta: 0,
            metadata,
            cancellationToken);
    }

    public async Task<WalletTransferResponse> TransferToReservedWithinTransactionAsync(
        Guid sourceWalletAccountId,
        Guid targetWalletAccountId,
        decimal amount,
        WalletTransactionType debitTransactionType,
        WalletTransactionType creditTransactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return await TransferWithinTransactionCoreAsync(
            sourceWalletAccountId,
            targetWalletAccountId,
            amount,
            debitTransactionType,
            creditTransactionType,
            sourceReservedBalanceDelta: 0,
            targetReservedBalanceDelta: amount,
            metadata,
            cancellationToken);
    }

    public async Task<WalletTransferResponse> TransferFromReservedWithinTransactionAsync(
        Guid sourceWalletAccountId,
        Guid targetWalletAccountId,
        decimal amount,
        decimal reservedAmountToRelease,
        WalletTransactionType debitTransactionType,
        WalletTransactionType creditTransactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (reservedAmountToRelease < 0 || reservedAmountToRelease > amount)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Reserved amount to release must be between zero and the transfer amount.");
        }

        return await TransferWithinTransactionCoreAsync(
            sourceWalletAccountId,
            targetWalletAccountId,
            amount,
            debitTransactionType,
            creditTransactionType,
            sourceReservedBalanceDelta: -reservedAmountToRelease,
            targetReservedBalanceDelta: 0,
            metadata,
            cancellationToken);
    }

    public async Task<WalletMutationResponse> ReleaseReservedWithinTransactionAsync(
        Guid walletAccountId,
        decimal amount,
        WalletTransactionType transactionType,
        WalletTransactionMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAmount(amount);

        var wallet = await rowLockService.LockWalletAccountAsync(walletAccountId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Wallet account not found.");

        EnsureWalletActive(wallet);
        if (wallet.ReservedBalance < amount)
        {
            throw new ConflictException(
                ErrorCodes.WalletInsufficientReservedBalance,
                "Số dư đang giữ trong ví không đủ để tất toán tiền cọc.",
                new { walletAccountId, wallet.ReservedBalance, requiredAmount = amount });
        }

        var transferGroupId = metadata?.TransferGroupId ?? Guid.NewGuid();
        var releaseMetadata = CopyMetadata(metadata, transferGroupId, "WalletReserveRelease");
        var walletTransaction = ApplyWalletMutation(
            wallet,
            amount,
            transactionType,
            WalletTransactionDirection.Debit,
            balanceDelta: 0,
            reservedBalanceDelta: -amount,
            releaseMetadata);

        return new WalletMutationResponse
        {
            Wallet = ToWalletResponse(wallet),
            Transaction = ToWalletTransactionResponse(walletTransaction)
        };
    }

    private async Task<WalletTransferResponse> TransferWithinTransactionCoreAsync(
        Guid sourceWalletAccountId,
        Guid targetWalletAccountId,
        decimal amount,
        WalletTransactionType debitTransactionType,
        WalletTransactionType creditTransactionType,
        decimal sourceReservedBalanceDelta,
        decimal targetReservedBalanceDelta,
        WalletTransactionMetadata? metadata,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount);

        if (sourceWalletAccountId == targetWalletAccountId)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Source and target wallets must be different.");
        }

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

        EnsureWalletActive(sourceWallet);
        EnsureWalletActive(targetWallet);

        var reservedAmountToRelease = -sourceReservedBalanceDelta;
        if (reservedAmountToRelease > sourceWallet.ReservedBalance)
        {
            throw new ConflictException(
                ErrorCodes.WalletInsufficientReservedBalance,
                "Số dư đang giữ trong ví không đủ để hoàn tiền cọc.",
                new
                {
                    sourceWalletAccountId,
                    sourceWallet.ReservedBalance,
                    requiredReservedAmount = reservedAmountToRelease
                });
        }

        var availableBalance = sourceWallet.Balance - sourceWallet.ReservedBalance;
        var requiredAvailableBalance = amount - reservedAmountToRelease;
        if (availableBalance < requiredAvailableBalance)
        {
            throw new ConflictException(
                ErrorCodes.WalletInsufficientBalance,
                "Số dư khả dụng trong ví không đủ để thực hiện giao dịch.",
                new
                {
                    sourceWalletAccountId,
                    availableBalance,
                    requiredAmount = requiredAvailableBalance
                });
        }

        var transferGroupId = metadata?.TransferGroupId ?? Guid.NewGuid();
        var debitMetadata = CopyMetadata(metadata, transferGroupId, "WalletTransfer");
        var creditMetadata = CopyMetadata(metadata, transferGroupId, "WalletTransfer");

        var debitTransaction = ApplyWalletMutation(
            sourceWallet,
            amount,
            debitTransactionType,
            WalletTransactionDirection.Debit,
            balanceDelta: -amount,
            reservedBalanceDelta: sourceReservedBalanceDelta,
            debitMetadata);

        var creditTransaction = ApplyWalletMutation(
            targetWallet,
            amount,
            creditTransactionType,
            WalletTransactionDirection.Credit,
            balanceDelta: amount,
            reservedBalanceDelta: targetReservedBalanceDelta,
            creditMetadata);

        return new WalletTransferResponse
        {
            TransferGroupId = transferGroupId,
            DebitTransaction = ToWalletTransactionResponse(debitTransaction),
            CreditTransaction = ToWalletTransactionResponse(creditTransaction)
        };
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
