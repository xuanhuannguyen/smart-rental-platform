using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Payments.Responses;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Application.Payments;

public class PaymentWebhookService : IPaymentWebhookService
{
    private const string RelatedEntityTypePaymentTransaction = "PaymentTransaction";

    private readonly IAppDbContext context;
    private readonly IPayOSWebhookSignatureVerifier payOSSignatureVerifier;
    private readonly IPaymentRowLockService rowLockService;

    public PaymentWebhookService(
        IAppDbContext context,
        IPayOSWebhookSignatureVerifier payOSSignatureVerifier,
        IPaymentRowLockService rowLockService)
    {
        this.context = context;
        this.payOSSignatureVerifier = payOSSignatureVerifier;
        this.rowLockService = rowLockService;
    }

    public Task<PaymentWebhookProcessingResponse> ProcessPayOSWebhookAsync(
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        var parsed = PaymentWebhookPayload.Parse(rawPayload);
        var signatureStatus = payOSSignatureVerifier.Verify(rawPayload, signatureHeader)
            ? WebhookSignatureStatus.Valid
            : WebhookSignatureStatus.Invalid;

        return ProcessWebhookAsync(
            rawPayload,
            parsed,
            PaymentMethod.PayOS,
            signatureStatus,
            cancellationToken);
    }

    public Task<PaymentWebhookProcessingResponse> ProcessMockWebhookAsync(
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        var parsed = PaymentWebhookPayload.Parse(rawPayload);
        return ProcessWebhookAsync(
            rawPayload,
            parsed,
            PaymentMethod.Mock,
            WebhookSignatureStatus.SkippedForMock,
            cancellationToken);
    }

    private async Task<PaymentWebhookProcessingResponse> ProcessWebhookAsync(
        string rawPayload,
        PaymentWebhookPayload parsed,
        PaymentMethod paymentMethod,
        WebhookSignatureStatus signatureStatus,
        CancellationToken cancellationToken)
    {
        var rawPayloadHash = HashRawPayload(rawPayload);
        var duplicateLog = await context.PaymentWebhookLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RawPayloadHash == rawPayloadHash, cancellationToken);

        if (duplicateLog is not null)
        {
            return new PaymentWebhookProcessingResponse
            {
                PaymentTransactionId = duplicateLog.PaymentTransactionId,
                WebhookLogId = duplicateLog.Id,
                ProcessingStatus = WebhookProcessingStatus.Duplicate.ToString(),
                SignatureStatus = duplicateLog.SignatureStatus.ToString(),
                ProviderOrderCode = duplicateLog.ProviderOrderCode,
                Message = "Duplicate webhook payload ignored."
            };
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;
            var log = new PaymentWebhookLog
            {
                Id = Guid.NewGuid(),
                PaymentMethod = paymentMethod,
                ProviderEventId = parsed.ProviderEventId,
                ProviderOrderCode = parsed.ProviderOrderCode,
                ProviderTransactionCode = parsed.ProviderTransactionCode,
                IdempotencyKey = parsed.IdempotencyKey,
                RawPayload = rawPayload,
                RawPayloadHash = rawPayloadHash,
                SignatureStatus = signatureStatus,
                ProcessingStatus = WebhookProcessingStatus.Received,
                RetryCount = 0,
                ReceivedAt = now,
                CreatedAt = now
            };

            context.PaymentWebhookLogs.Add(log);

            if (signatureStatus == WebhookSignatureStatus.Invalid)
            {
                MarkLog(log, WebhookProcessingStatus.Failed, "Invalid PayOS webhook signature.");
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToResponse(log, null, "Invalid signature.");
            }

            var paymentTransaction = await LockPaymentTransactionAsync(parsed, cancellationToken);
            if (paymentTransaction is null)
            {
                MarkLog(log, WebhookProcessingStatus.Unmatched, "Payment transaction was not found.");
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToResponse(log, null, "Payment transaction was not found.");
            }

            log.PaymentTransactionId = paymentTransaction.Id;

            if (parsed.IsSuccess)
            {
                await ProcessSuccessAsync(log, paymentTransaction, parsed, cancellationToken);
            }
            else if (parsed.IsCancelled)
            {
                ProcessTerminalWithoutCredit(log, paymentTransaction, PaymentTransactionStatus.Cancelled, "Payment was cancelled by provider.");
            }
            else if (parsed.IsFailed)
            {
                ProcessTerminalWithoutCredit(log, paymentTransaction, PaymentTransactionStatus.Failed, "Payment failed at provider.");
            }
            else
            {
                MarkLog(log, WebhookProcessingStatus.Failed, "Unsupported webhook payment status.");
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ToResponse(log, paymentTransaction, log.ErrorMessage ?? "Webhook processed.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ProcessSuccessAsync(
        PaymentWebhookLog log,
        PaymentTransaction paymentTransaction,
        PaymentWebhookPayload parsed,
        CancellationToken cancellationToken)
    {
        if (paymentTransaction.Status == PaymentTransactionStatus.Succeeded)
        {
            MarkLog(log, WebhookProcessingStatus.Duplicate, "Payment transaction was already succeeded.");
            return;
        }

        if (paymentTransaction.Status != PaymentTransactionStatus.Pending)
        {
            MarkLog(log, WebhookProcessingStatus.Failed, "Payment transaction is not pending.");
            return;
        }

        if (parsed.Amount is null || parsed.Amount.Value != paymentTransaction.Amount)
        {
            MarkLog(log, WebhookProcessingStatus.Failed, "Webhook amount does not match payment transaction amount.");
            return;
        }

        var wallet = await rowLockService.LockWalletAccountAsync(paymentTransaction.WalletAccountId, cancellationToken);
        if (wallet is null)
        {
            MarkLog(log, WebhookProcessingStatus.Failed, "Wallet account was not found.");
            return;
        }

        if (wallet.Status != WalletAccountStatus.Active)
        {
            MarkLog(log, WebhookProcessingStatus.Failed, "Wallet account is not active.");
            return;
        }

        var balanceBefore = wallet.Balance;
        var reservedBefore = wallet.ReservedBalance;
        var balanceAfter = balanceBefore + paymentTransaction.Amount;

        wallet.Balance = balanceAfter;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        var walletTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            UserId = wallet.UserId,
            TransactionType = WalletTransactionType.WalletTopUp,
            Direction = WalletTransactionDirection.Credit,
            Amount = paymentTransaction.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReservedBalanceBefore = reservedBefore,
            ReservedBalanceAfter = reservedBefore,
            RelatedEntityType = RelatedEntityTypePaymentTransaction,
            RelatedEntityId = paymentTransaction.Id,
            Description = "Wallet top-up via PayOS.",
            Status = WalletTransactionStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.WalletTransactions.Add(walletTransaction);

        var now = DateTimeOffset.UtcNow;
        paymentTransaction.ProviderTransactionCode = parsed.ProviderTransactionCode ?? paymentTransaction.ProviderTransactionCode;
        paymentTransaction.GatewayResponseCode = parsed.GatewayResponseCode ?? paymentTransaction.GatewayResponseCode;
        paymentTransaction.GatewayResponseMessage = parsed.GatewayResponseMessage ?? paymentTransaction.GatewayResponseMessage;
        paymentTransaction.Status = PaymentTransactionStatus.Succeeded;
        paymentTransaction.PaidAt = now;
        paymentTransaction.ConfirmedAt = now;
        paymentTransaction.UpdatedAt = now;

        MarkLog(log, WebhookProcessingStatus.Processed, null);
    }

    private void ProcessTerminalWithoutCredit(
        PaymentWebhookLog log,
        PaymentTransaction paymentTransaction,
        PaymentTransactionStatus terminalStatus,
        string message)
    {
        if (paymentTransaction.Status == PaymentTransactionStatus.Succeeded)
        {
            MarkLog(log, WebhookProcessingStatus.Duplicate, "Payment transaction was already succeeded.");
            return;
        }

        if (paymentTransaction.Status == PaymentTransactionStatus.Pending)
        {
            paymentTransaction.Status = terminalStatus;
            paymentTransaction.FailedAt = terminalStatus == PaymentTransactionStatus.Failed
                ? DateTimeOffset.UtcNow
                : paymentTransaction.FailedAt;
            paymentTransaction.UpdatedAt = DateTimeOffset.UtcNow;
        }

        MarkLog(log, WebhookProcessingStatus.Processed, message);
    }

    private Task<PaymentTransaction?> LockPaymentTransactionAsync(
        PaymentWebhookPayload parsed,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(parsed.ProviderOrderCode))
        {
            return rowLockService.LockPaymentTransactionByProviderOrderCodeAsync(parsed.ProviderOrderCode, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(parsed.IdempotencyKey))
        {
            return rowLockService.LockPaymentTransactionByIdempotencyKeyAsync(parsed.IdempotencyKey, cancellationToken);
        }

        return Task.FromResult<PaymentTransaction?>(null);
    }

    private static void MarkLog(
        PaymentWebhookLog log,
        WebhookProcessingStatus status,
        string? errorMessage)
    {
        log.ProcessingStatus = status;
        log.ErrorMessage = errorMessage;
        log.ProcessedAt = DateTimeOffset.UtcNow;
    }

    private static PaymentWebhookProcessingResponse ToResponse(
        PaymentWebhookLog log,
        PaymentTransaction? paymentTransaction,
        string? message)
    {
        return new PaymentWebhookProcessingResponse
        {
            PaymentTransactionId = log.PaymentTransactionId,
            WebhookLogId = log.Id,
            ProcessingStatus = log.ProcessingStatus.ToString(),
            SignatureStatus = log.SignatureStatus.ToString(),
            PaymentStatus = paymentTransaction?.Status.ToString(),
            ProviderOrderCode = log.ProviderOrderCode,
            Message = message
        };
    }

    private static string HashRawPayload(string rawPayload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class PaymentWebhookPayload
    {
        public string? ProviderEventId { get; private init; }
        public string? ProviderOrderCode { get; private init; }
        public string? ProviderTransactionCode { get; private init; }
        public string? IdempotencyKey { get; private init; }
        public decimal? Amount { get; private init; }
        public string? GatewayResponseCode { get; private init; }
        public string? GatewayResponseMessage { get; private init; }
        public bool IsSuccess { get; private init; }
        public bool IsFailed { get; private init; }
        public bool IsCancelled { get; private init; }

        public static PaymentWebhookPayload Parse(string rawPayload)
        {
            try
            {
                using var document = JsonDocument.Parse(rawPayload);
                var root = document.RootElement;
                var data = TryGetObject(root, "data") ?? root;

                var status = FirstString(root, data, "status", "paymentStatus", "code");
                var success = FirstBool(root, data, "success", "isSuccess") == true
                    || IsAny(status, "00", "PAID", "SUCCESS", "SUCCEEDED", "SUCCESSFUL");
                var cancelled = IsAny(status, "CANCELLED", "CANCELED", "CANCEL");
                var failed = IsAny(status, "FAILED", "FAILURE", "ERROR")
                    || (!success && !cancelled && FirstBool(root, data, "success", "isSuccess") == false);

                return new PaymentWebhookPayload
                {
                    ProviderEventId = FirstString(root, data, "eventId", "providerEventId", "webhookId"),
                    ProviderOrderCode = FirstString(root, data, "providerOrderCode", "orderCode"),
                    ProviderTransactionCode = FirstString(root, data, "providerTransactionCode", "transactionCode", "paymentLinkId", "reference"),
                    IdempotencyKey = FirstString(root, data, "idempotencyKey"),
                    Amount = FirstDecimal(root, data, "amount"),
                    GatewayResponseCode = FirstString(root, data, "code"),
                    GatewayResponseMessage = FirstString(root, data, "desc", "message"),
                    IsSuccess = success,
                    IsFailed = failed,
                    IsCancelled = cancelled
                };
            }
            catch (JsonException)
            {
                return new PaymentWebhookPayload
                {
                    GatewayResponseMessage = "Webhook payload was not valid JSON.",
                    IsFailed = true
                };
            }
        }

        private static JsonElement? TryGetObject(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Object
                    ? value
                    : null;
        }

        private static string? FirstString(JsonElement root, JsonElement data, params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetString(data, name) ?? GetString(root, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static bool? FirstBool(JsonElement root, JsonElement data, params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetBool(data, name) ?? GetBool(root, name);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static bool? GetBool(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        private static decimal? FirstDecimal(JsonElement root, JsonElement data, params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetDecimal(data, name) ?? GetDecimal(root, name);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static decimal? GetDecimal(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
                JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static bool IsAny(string? value, params string[] expected)
        {
            return !string.IsNullOrWhiteSpace(value)
                && expected.Any(x => string.Equals(value, x, StringComparison.OrdinalIgnoreCase));
        }
    }
}
