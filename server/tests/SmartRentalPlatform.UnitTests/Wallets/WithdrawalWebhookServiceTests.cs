using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Wallets;

public class WithdrawalWebhookServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakeWalletService _walletService;
    private readonly FakeRowLockService _rowLockService;
    private readonly FakePayOSSignatureVerifier _signatureVerifier = new();
    private readonly TestLogger<WithdrawalWebhookService> _logger = new();

    public WithdrawalWebhookServiceTests()
    {
        _rowLockService = new FakeRowLockService(_fixture.Context);
        _walletService = new FakeWalletService(_fixture.Context, _rowLockService);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenStatusSucceeded_UpdatesStatusToSucceededAndDebitsReserved()
    {
        // Arrange
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);

        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 200000m);
        wallet.ReservedBalance = 105000m; // 100000 + 5000 fee
        _fixture.Context.WalletAccounts.Add(wallet);

        var request = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            Amount = 100000m,
            Fee = 5000m,
            Status = WithdrawalStatus.Processing,
            ProviderOrderCode = "order-suc-111",
            IdempotencyKey = "idemp-suc"
        };
        _fixture.Context.WithdrawalRequests.Add(request);
        await _fixture.Context.SaveChangesAsync();

        _signatureVerifier.IsValid = true;
        var service = CreateService();

        // Act
        await service.ProcessWebhookAsync(
            "order-suc-111",
            "SUCCEEDED",
            "{}",
            "valid_sig",
            skipSignatureVerification: false);

        // Assert
        var updatedRequest = await _fixture.Context.WithdrawalRequests.FirstAsync(x => x.Id == request.Id);
        Assert.Equal(WithdrawalStatus.Succeeded, updatedRequest.Status);

        var updatedWallet = await _fixture.Context.WalletAccounts.FirstAsync(x => x.Id == wallet.Id);
        // Balance before: 200000. Succeeded withdrawal debits 105000 from balance and reserved.
        // Balance after: 95000. Reserved balance after: 0.
        Assert.Equal(0m, updatedWallet.ReservedBalance);
        Assert.Equal(95000m, updatedWallet.Balance);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenStatusFailed_UpdatesStatusToFailedAndRefundsReserved()
    {
        // Arrange
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);

        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 200000m);
        wallet.ReservedBalance = 105000m; // 100000 + 5000 fee
        _fixture.Context.WalletAccounts.Add(wallet);

        var request = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            Amount = 100000m,
            Fee = 5000m,
            Status = WithdrawalStatus.Processing,
            ProviderOrderCode = "order-fail-222",
            IdempotencyKey = "idemp-fail"
        };
        _fixture.Context.WithdrawalRequests.Add(request);
        await _fixture.Context.SaveChangesAsync();

        _signatureVerifier.IsValid = true;
        var service = CreateService();

        // Act
        await service.ProcessWebhookAsync(
            "order-fail-222",
            "FAILED",
            "{}",
            "valid_sig",
            skipSignatureVerification: false);

        // Assert
        var updatedRequest = await _fixture.Context.WithdrawalRequests.FirstAsync(x => x.Id == request.Id);
        Assert.Equal(WithdrawalStatus.Failed, updatedRequest.Status);
        Assert.Contains("failed", updatedRequest.Description);

        var updatedWallet = await _fixture.Context.WalletAccounts.FirstAsync(x => x.Id == wallet.Id);
        // Balance before: 200000. Failed withdrawal refunds 105000.
        // Balance after: 200000. Reserved balance after: 0.
        Assert.Equal(0m, updatedWallet.ReservedBalance);
        Assert.Equal(200000m, updatedWallet.Balance);
    }

    private WithdrawalWebhookService CreateService()
    {
        return new WithdrawalWebhookService(
            _fixture.Context,
            _walletService,
            _rowLockService,
            _signatureVerifier,
            _logger);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private class FakePayOSSignatureVerifier : IPayOSWebhookSignatureVerifier
    {
        public bool IsValid { get; set; } = true;

        public bool Verify(string rawPayload, string? signatureHeader) => IsValid;
        public bool VerifyPayment(string rawPayload, string? signatureHeader) => IsValid;
        public bool VerifyPayout(string rawPayload, string? signatureHeader) => IsValid;
    }

    private class FakeWalletService : WalletService
    {
        public FakeWalletService(IAppDbContext context, IPaymentRowLockService rowLockService)
            : base(context, rowLockService)
        {
        }
    }

    private class FakeRowLockService : IPaymentRowLockService
    {
        private readonly IAppDbContext _context;

        public FakeRowLockService(IAppDbContext context) => _context = context;

        public Task<WalletAccount?> LockWalletAccountAsync(Guid walletAccountId, CancellationToken cancellationToken = default)
        {
            return _context.WalletAccounts.FirstOrDefaultAsync(x => x.Id == walletAccountId, cancellationToken);
        }

        public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        }

        public Task<WithdrawalRequest?> LockWithdrawalRequestByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return _context.WithdrawalRequests.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public Task<WithdrawalRequest?> LockWithdrawalRequestByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        {
            return _context.WithdrawalRequests.FirstOrDefaultAsync(x => x.ProviderOrderCode == providerOrderCode, cancellationToken);
        }

        public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BillingInvoice?> LockInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RoomDeposit?> LockRoomDepositAsync(Guid roomDepositId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RentalContract?> LockRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
