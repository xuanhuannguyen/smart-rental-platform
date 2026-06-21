using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Application.Payments;

public class PayOSTopUpService : IPayOSTopUpService
{
    private const decimal MinimumTopUpAmount = 10_000m;
    private const decimal MaximumTopUpAmount = 50_000_000m;
    private const int MaxIdempotencyKeyLength = 120;
    private const string DefaultCurrency = "VND";
    private static readonly TimeSpan PaymentExpiry = TimeSpan.FromMinutes(15);

    private readonly IAppDbContext context;
    private readonly IWalletService walletService;
    private readonly IPayOSClient payOSClient;

    public PayOSTopUpService(
        IAppDbContext context,
        IWalletService walletService,
        IPayOSClient payOSClient)
    {
        this.context = context;
        this.walletService = walletService;
        this.payOSClient = payOSClient;
    }

    public async Task<CreatePayOSTopUpResponse> CreateTopUpAsync(
        Guid userId,
        CreatePayOSTopUpRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAmount(request.Amount);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

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

        var hasApprovedKyc = await context.KycVerifications
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId && x.Status == KycVerificationStatus.Approved,
                cancellationToken);

        if (!hasApprovedKyc)
        {
            throw new ForbiddenException(
                ErrorCodes.KycRequired,
                "Approved KYC is required before creating a wallet top-up.");
        }

        var wallet = await walletService.GetOrCreateWalletAsync(userId, cancellationToken);
        if (wallet.Status != WalletAccountStatus.Active)
        {
            throw new ConflictException(
                ErrorCodes.InvalidStatus,
                "Wallet account is not active.",
                new { wallet.Status });
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey, userId);
        var existing = await context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.PayerUserId == userId && x.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            if (existing.Amount != request.Amount)
            {
                throw new ConflictException(
                    ErrorCodes.ValidationError,
                    "Idempotency key was already used for a different top-up amount.");
            }

            return ToResponse(existing);
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var transactionClosed = false;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.Add(PaymentExpiry);
            var paymentTransactionId = Guid.NewGuid();
            var providerOrderCode = await GenerateProviderOrderCodeAsync(cancellationToken);

            var paymentTransaction = new PaymentTransaction
            {
                Id = paymentTransactionId,
                WalletAccountId = wallet.Id,
                PayerUserId = userId,
                IdempotencyKey = idempotencyKey,
                Amount = request.Amount,
                Currency = DefaultCurrency,
                PaymentPurpose = PaymentPurpose.WalletTopUp,
                PaymentMethod = PaymentMethod.PayOS,
                ProviderOrderCode = providerOrderCode,
                Status = PaymentTransactionStatus.Pending,
                ExpiresAt = expiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PaymentTransactions.Add(paymentTransaction);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                transactionClosed = true;
                var existingAfterRace = await LoadExistingTopUpAsync(userId, idempotencyKey, cancellationToken);
                if (existingAfterRace is not null)
                {
                    EnsureExistingTopUpMatches(existingAfterRace, request.Amount);
                    return ToResponse(existingAfterRace);
                }

                throw;
            }

            var payOSResult = await payOSClient.CreatePaymentAsync(
                new PayOSCreatePaymentInput
                {
                    PaymentTransactionId = paymentTransactionId,
                    PayerUserId = userId,
                    ProviderOrderCode = providerOrderCode,
                    Amount = request.Amount,
                    Currency = DefaultCurrency,
                    Description = BuildDescription(request.Note),
                    ReturnUrl = AppendPaymentTransactionId(request.ReturnUrl, paymentTransactionId),
                    CancelUrl = AppendPaymentTransactionId(request.CancelUrl, paymentTransactionId),
                    ExpiresAt = expiresAt
                },
                cancellationToken);

            paymentTransaction.ProviderTransactionCode = payOSResult.ProviderTransactionCode;
            paymentTransaction.ProviderCheckoutUrl = payOSResult.CheckoutUrl;
            paymentTransaction.ProviderQrCode = payOSResult.QrCode;
            paymentTransaction.GatewayResponseCode = payOSResult.GatewayResponseCode;
            paymentTransaction.GatewayResponseMessage = payOSResult.GatewayResponseMessage;
            paymentTransaction.ExpiresAt = payOSResult.ExpiresAt ?? expiresAt;
            paymentTransaction.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            transactionClosed = true;

            return ToResponse(paymentTransaction);
        }
        catch
        {
            if (!transactionClosed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task<PagedResult<WalletTopUpHistoryResponse>> GetTopUpHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.PayerUserId == userId && x.PaymentPurpose == PaymentPurpose.WalletTopUp)
            .OrderByDescending(x => x.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WalletTopUpHistoryResponse
            {
                Id = x.Id,
                Amount = x.Amount,
                Currency = x.Currency,
                PaymentMethod = x.PaymentMethod.ToString(),
                ProviderOrderCode = x.ProviderOrderCode,
                ProviderCheckoutUrl = x.ProviderCheckoutUrl,
                Status = x.Status.ToString(),
                ExpiresAt = x.ExpiresAt,
                PaidAt = x.PaidAt,
                FailedAt = x.FailedAt,
                ConfirmedAt = x.ConfirmedAt,
                CreatedAt = x.CreatedAt,
                GatewayResponseCode = x.GatewayResponseCode,
                GatewayResponseMessage = x.GatewayResponseMessage
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<WalletTopUpHistoryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<WalletTopUpHistoryResponse> GetTopUpAsync(
        Guid userId,
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        var topUp = await context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Id == paymentTransactionId &&
                x.PayerUserId == userId &&
                x.PaymentPurpose == PaymentPurpose.WalletTopUp)
            .Select(x => new WalletTopUpHistoryResponse
            {
                Id = x.Id,
                Amount = x.Amount,
                Currency = x.Currency,
                PaymentMethod = x.PaymentMethod.ToString(),
                ProviderOrderCode = x.ProviderOrderCode,
                ProviderCheckoutUrl = x.ProviderCheckoutUrl,
                Status = x.Status.ToString(),
                ExpiresAt = x.ExpiresAt,
                PaidAt = x.PaidAt,
                FailedAt = x.FailedAt,
                ConfirmedAt = x.ConfirmedAt,
                CreatedAt = x.CreatedAt,
                GatewayResponseCode = x.GatewayResponseCode,
                GatewayResponseMessage = x.GatewayResponseMessage
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (topUp is null)
        {
            throw new NotFoundException(ErrorCodes.NotFound, "Wallet top-up transaction was not found.");
        }

        return topUp;
    }

    public Task<int> ExpireOverduePendingTopUpsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return context.PaymentTransactions
            .Where(x => x.PaymentPurpose == PaymentPurpose.WalletTopUp &&
                x.Status == PaymentTransactionStatus.Pending &&
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, PaymentTransactionStatus.Expired)
                    .SetProperty(x => x.GatewayResponseMessage, "Payment request expired.")
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Top-up amount must be greater than zero.");
        }

        if (decimal.Truncate(amount) != amount)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Top-up amount must be an integer VND amount.");
        }

        if (amount < MinimumTopUpAmount)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Minimum top-up amount is {MinimumTopUpAmount:N0} VND.");
        }

        if (amount > MaximumTopUpAmount)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                $"Maximum top-up amount is {MaximumTopUpAmount:N0} VND.");
        }
    }

    private static string NormalizeIdempotencyKey(string? idempotencyKey, Guid userId)
    {
        var prefix = $"wallet-topup:{userId:N}:";
        var trimmed = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return $"{prefix}{Guid.NewGuid():N}";
        }

        if (trimmed.Length <= MaxIdempotencyKeyLength - prefix.Length)
        {
            return $"{prefix}{trimmed}";
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(trimmed));
        return $"{prefix}{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }

    private async Task<string> GenerateProviderOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100, 999)}";
            var exists = await context.PaymentTransactions
                .AsNoTracking()
                .AnyAsync(x => x.ProviderOrderCode == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N")[..18];
    }

    private static string BuildDescription(string? note)
    {
        var description = string.IsNullOrWhiteSpace(note)
            ? "Wallet top-up"
            : note.Trim();

        return description.Length <= 25 ? description : description[..25];
    }

    private static string? AppendPaymentTransactionId(string? url, Guid paymentTransactionId)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        const string parameterName = "paymentTransactionId";
        var fragmentIndex = url.IndexOf('#', StringComparison.Ordinal);
        var baseUrl = fragmentIndex >= 0 ? url[..fragmentIndex] : url;
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";

        return $"{baseUrl}{separator}{parameterName}={paymentTransactionId:D}{fragment}";
    }

    private static CreatePayOSTopUpResponse ToResponse(PaymentTransaction paymentTransaction)
    {
        return new CreatePayOSTopUpResponse
        {
            PaymentTransactionId = paymentTransaction.Id,
            Amount = paymentTransaction.Amount,
            Status = paymentTransaction.Status.ToString(),
            ProviderOrderCode = paymentTransaction.ProviderOrderCode,
            PaymentUrl = paymentTransaction.ProviderCheckoutUrl,
            QrCode = paymentTransaction.ProviderQrCode,
            ExpiredAt = paymentTransaction.ExpiresAt ?? paymentTransaction.CreatedAt
        };
    }

    private Task<PaymentTransaction?> LoadExistingTopUpAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return context.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.PayerUserId == userId && x.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void EnsureExistingTopUpMatches(PaymentTransaction existing, decimal amount)
    {
        if (existing.Amount != amount)
        {
            throw new ConflictException(
                ErrorCodes.ValidationError,
                "Idempotency key was already used for a different top-up amount.");
        }
    }
}
