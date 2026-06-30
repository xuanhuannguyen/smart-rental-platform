using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.UnitTests.Common;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Payments;

public class PaymentWebhookServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakePayOSWebhookSignatureVerifier _signatureVerifier = new();
    private readonly FakePaymentRowLockService _rowLockService;

    public PaymentWebhookServiceTests()
    {
        _rowLockService = new FakePaymentRowLockService(_fixture.Context);
    }

    [Fact]
    public async Task ProcessMockWebhookAsync_WhenSuccess_CreditsWalletAndMarksPaymentSucceeded()
    {
        var (wallet, payment) = await SeedPendingTopUpAsync(amount: 500_000m);
        var service = CreateService();
        var payload = BuildPayload(payment, "Succeeded", amount: 500_000m);

        var result = await service.ProcessMockWebhookAsync(payload);

        Assert.Equal(WebhookProcessingStatus.Processed.ToString(), result.ProcessingStatus);
        Assert.Equal(PaymentTransactionStatus.Succeeded.ToString(), result.PaymentStatus);
        Assert.Equal(500_000m, wallet.Balance);
        Assert.Equal(PaymentTransactionStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.PaidAt);
        var walletTransaction = Assert.Single(_fixture.Context.WalletTransactions);
        Assert.Equal(WalletTransactionType.WalletTopUp, walletTransaction.TransactionType);
        Assert.Equal(payment.Id, walletTransaction.RelatedEntityId);
    }

    [Fact]
    public async Task ProcessMockWebhookAsync_WhenPayloadRepeated_ReturnsDuplicate()
    {
        var (_, payment) = await SeedPendingTopUpAsync(amount: 200_000m);
        var service = CreateService();
        var payload = BuildPayload(payment, "Succeeded", amount: 200_000m);

        await service.ProcessMockWebhookAsync(payload);
        var duplicate = await service.ProcessMockWebhookAsync(payload);

        Assert.Equal(WebhookProcessingStatus.Duplicate.ToString(), duplicate.ProcessingStatus);
        Assert.Equal("Duplicate webhook payload ignored.", duplicate.Message);
    }

    [Theory]
    [InlineData("Failed", PaymentTransactionStatus.Failed)]
    [InlineData("Cancelled", PaymentTransactionStatus.Cancelled)]
    public async Task ProcessMockWebhookAsync_WhenTerminalWithoutSuccess_DoesNotCreditWallet(
        string status,
        PaymentTransactionStatus expectedStatus)
    {
        var (wallet, payment) = await SeedPendingTopUpAsync(amount: 300_000m);
        var service = CreateService();

        var result = await service.ProcessMockWebhookAsync(BuildPayload(payment, status, amount: 300_000m));

        Assert.Equal(WebhookProcessingStatus.Processed.ToString(), result.ProcessingStatus);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(_fixture.Context.WalletTransactions);
    }

    [Fact]
    public async Task ProcessMockWebhookAsync_WhenPaymentTransactionMissing_ReturnsUnmatched()
    {
        var service = CreateService();
        var payload = """
            {"providerOrderCode":"missing-order","amount":100000,"status":"Succeeded","success":true}
            """;

        var result = await service.ProcessMockWebhookAsync(payload);

        Assert.Equal(WebhookProcessingStatus.Unmatched.ToString(), result.ProcessingStatus);
        Assert.Null(result.PaymentTransactionId);
    }

    [Fact]
    public async Task ProcessPayOSWebhookAsync_WhenSignatureInvalid_StoresFailedLog()
    {
        var (_, payment) = await SeedPendingTopUpAsync(amount: 100_000m);
        _signatureVerifier.IsValid = false;
        var service = CreateService();

        var result = await service.ProcessPayOSWebhookAsync(BuildPayload(payment, "Succeeded", 100_000m), "bad-signature");

        Assert.Equal(WebhookProcessingStatus.Failed.ToString(), result.ProcessingStatus);
        Assert.Equal(WebhookSignatureStatus.Invalid.ToString(), result.SignatureStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task ProcessMockWebhookAsync_WhenAmountMismatch_MarksLogFailedAndLeavesPaymentPending()
    {
        var (wallet, payment) = await SeedPendingTopUpAsync(amount: 500_000m);
        var service = CreateService();

        var result = await service.ProcessMockWebhookAsync(BuildPayload(payment, "Succeeded", amount: 400_000m));

        Assert.Equal(WebhookProcessingStatus.Failed.ToString(), result.ProcessingStatus);
        Assert.Equal(PaymentTransactionStatus.Pending, payment.Status);
        Assert.Equal(0m, wallet.Balance);
        Assert.Empty(_fixture.Context.WalletTransactions);
    }

    [Fact]
    public async Task ProcessMockWebhookAsync_WhenPaymentAlreadySucceeded_ReturnsDuplicateForTransaction()
    {
        var (wallet, payment) = await SeedPendingTopUpAsync(amount: 500_000m);
        wallet.Balance = 500_000m;
        payment.Status = PaymentTransactionStatus.Succeeded;
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.ProcessMockWebhookAsync(BuildPayload(payment, "Succeeded", amount: 500_000m));

        Assert.Equal(WebhookProcessingStatus.Duplicate.ToString(), result.ProcessingStatus);
        Assert.Equal(PaymentTransactionStatus.Succeeded.ToString(), result.PaymentStatus);
        Assert.Empty(_fixture.Context.WalletTransactions);
    }

    [Fact]
    public async Task MockPaymentService_SimulateFailedAsync_BuildsPayloadAndProcessesWebhook()
    {
        var (_, payment) = await SeedPendingTopUpAsync(amount: 120_000m);
        var webhookService = CreateService();
        var mockService = new MockPaymentService(_fixture.Context, webhookService);

        var result = await mockService.SimulateFailedAsync(payment.Id, null);

        Assert.Equal(WebhookProcessingStatus.Processed.ToString(), result.ProcessingStatus);
        Assert.Equal(PaymentTransactionStatus.Failed, payment.Status);
    }

    private PaymentWebhookService CreateService()
    {
        return new PaymentWebhookService(_fixture.Context, _signatureVerifier, _rowLockService);
    }

    private async Task<(WalletAccount Wallet, PaymentTransaction Payment)> SeedPendingTopUpAsync(decimal amount)
    {
        var user = TestDataBuilder.BuildUser();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, balance: 0m);
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            PayerUserId = user.Id,
            IdempotencyKey = $"idem-{Guid.NewGuid():N}",
            Amount = amount,
            Currency = "VND",
            PaymentPurpose = PaymentPurpose.WalletTopUp,
            PaymentMethod = PaymentMethod.PayOS,
            ProviderOrderCode = $"order-{Guid.NewGuid():N}",
            Status = PaymentTransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _fixture.Context.Users.Add(user);
        _fixture.Context.WalletAccounts.Add(wallet);
        _fixture.Context.PaymentTransactions.Add(payment);
        await _fixture.Context.SaveChangesAsync();
        return (wallet, payment);
    }

    private static string BuildPayload(PaymentTransaction payment, string status, decimal amount)
    {
        var success = string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            ? "true"
            : "false";

        return $$"""
            {
              "providerEventId": "evt-{{Guid.NewGuid():N}}",
              "providerOrderCode": "{{payment.ProviderOrderCode}}",
              "providerTransactionCode": "txn-{{Guid.NewGuid():N}}",
              "idempotencyKey": "{{payment.IdempotencyKey}}",
              "amount": {{amount}},
              "status": "{{status}}",
              "success": {{success}},
              "code": "{{(success == "true" ? "00" : "FAILED")}}",
              "desc": "unit test"
            }
            """;
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakePayOSWebhookSignatureVerifier : IPayOSWebhookSignatureVerifier
    {
        public bool IsValid { get; set; } = true;

        public bool Verify(string rawPayload, string? signatureHeader)
        {
            return IsValid;
        }
    }

    private sealed class FakePaymentRowLockService : IPaymentRowLockService
    {
        private readonly DbContext _context;

        public FakePaymentRowLockService(DbContext context)
        {
            _context = context;
        }

        public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        {
            return _context.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.ProviderOrderCode == providerOrderCode, cancellationToken);
        }

        public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return _context.Set<PaymentTransaction>().FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public Task<WalletAccount?> LockWalletAccountAsync(Guid walletAccountId, CancellationToken cancellationToken = default)
        {
            return _context.Set<WalletAccount>().FirstOrDefaultAsync(x => x.Id == walletAccountId, cancellationToken);
        }

        public Task<BillingInvoice?> LockInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default) => Task.FromResult<BillingInvoice?>(null);
        public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<RoomDeposit?> LockRoomDepositAsync(Guid roomDepositId, CancellationToken cancellationToken = default) => Task.FromResult<RoomDeposit?>(null);
        public Task<RentalContract?> LockRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default) => Task.FromResult<RentalContract?>(null);
    }
}
