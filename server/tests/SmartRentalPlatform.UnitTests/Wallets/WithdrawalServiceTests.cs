using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Application.Wallets.Options;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Wallets;

public class WithdrawalServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakeWalletService _walletService;
    private readonly FakePayOSClient _payOSClient = new();
    private readonly FakeRowLockService _rowLockService;
    private readonly WithdrawalOptions _options = new() { FlatFee = 5000m };

    public WithdrawalServiceTests()
    {
        _rowLockService = new FakeRowLockService(_fixture.Context);
        _walletService = new FakeWalletService(_fixture.Context, _rowLockService);
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenUserHasNoApprovedKyc_ThrowsForbiddenException()
    {
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-1"));
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenInsufficientBalance_ThrowsConflictException()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 50000m); // Needs 100000 + 5000 fee
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-2"));
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenPayOSCreationFailsImmediately_SetsFailedAndRefundsReserved()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 200000m);
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();

        _payOSClient.CreatePayoutResultMock = new PayOSCreatePayoutResult
        {
            GatewayResponseCode = "99",
            GatewayResponseMessage = "Bank account invalid",
            PayoutId = null
        };

        var service = CreateService();

        var withdrawal = await service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-3");

        Assert.Equal(WithdrawalStatus.Failed, withdrawal.Status);
        Assert.Equal("Bank account invalid", withdrawal.Description);

        // Reserved balance should be 0 because it got refunded
        var updatedWallet = await _fixture.Context.WalletAccounts.FirstAsync(x => x.Id == wallet.Id);
        Assert.Equal(0m, updatedWallet.ReservedBalance);
        Assert.Equal(200000m, updatedWallet.Balance);
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenSuccessful_HoldsReservedBalanceAndReturnsProcessing()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 200000m);
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();

        _payOSClient.CreatePayoutResultMock = new PayOSCreatePayoutResult
        {
            GatewayResponseCode = "00",
            GatewayResponseMessage = "Success",
            PayoutId = "payos-txn-code"
        };

        var service = CreateService();

        var withdrawal = await service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-4");

        Assert.Equal(WithdrawalStatus.Processing, withdrawal.Status);
        Assert.Equal("payos-txn-code", withdrawal.ProviderTransactionCode);

        // Wallet reserved balance should be updated
        var updatedWallet = await _fixture.Context.WalletAccounts.FirstAsync(x => x.Id == wallet.Id);
        Assert.Equal(105000m, updatedWallet.ReservedBalance); // 100000 + 5000 fee
    }

    [Fact]
    public async Task RequestWithdrawalAsync_IdempotencyKey_ReturnsExistingRequest()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, 500000m);
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();

        _payOSClient.CreatePayoutResultMock = new PayOSCreatePayoutResult
        {
            GatewayResponseCode = "00",
            PayoutId = "payos-txn-code-1"
        };

        var service = CreateService();

        var first = await service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-duplicate");
        var second = await service.RequestWithdrawalAsync(user.Id, 100000m, "970415", "123456", "NGUYEN VAN A", "idemp-duplicate");

        Assert.Equal(first.Id, second.Id);
    }

    private WithdrawalService CreateService()
    {
        return new WithdrawalService(
            _fixture.Context,
            _walletService,
            _payOSClient,
            new FakeWithdrawalWebhookService(),
            _rowLockService,
            Options.Create(_options));
    }

    private async Task<User> SeedApprovedKycUserAsync()
    {
        var user = TestDataBuilder.BuildUser();
        var kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OcrFullName = "NGUYEN VAN A",
            Status = KycVerificationStatus.Approved
        };
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(kyc);
        await _fixture.Context.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private class FakePayOSClient : IPayOSClient
    {
        public PayOSCreatePayoutResult CreatePayoutResultMock { get; set; } = new() { PayoutId = "mock-id" };

        public Task<PayOSCreatePaymentResult> CreatePaymentAsync(PayOSCreatePaymentInput input, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PayOSCreatePayoutResult> CreatePayoutAsync(PayOSCreatePayoutInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreatePayoutResultMock);
        }

        public Task<PayOSPayoutDetailsResult> GetPayoutDetailsAsync(string payoutId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
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

    private class FakeWithdrawalWebhookService : IWithdrawalWebhookService
    {
        public Task ProcessWebhookAsync(
            string providerOrderCode,
            string status,
            string payload,
            string? signature,
            bool skipSignatureVerification = false,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
