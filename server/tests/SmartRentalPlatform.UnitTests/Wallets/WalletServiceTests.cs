using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Domain.Enums.Users;
using SmartRentalPlatform.UnitTests.Common;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Wallets;

public class WalletServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakePaymentRowLockService _rowLockService;

    public WalletServiceTests()
    {
        _rowLockService = new FakePaymentRowLockService(_fixture.Context);
    }

    [Fact]
    public async Task GetMyWalletAsync_WhenUserActiveAndWalletMissing_CreatesWallet()
    {
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.GetMyWalletAsync(user.Id);

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(0m, result.Balance);
        Assert.Equal(0m, result.AvailableBalance);
        Assert.Equal(WalletAccountStatus.Active.ToString(), result.Status);
        Assert.Single(_fixture.Context.WalletAccounts.Where(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task GetOrCreateWalletAsync_WhenUserMissing_ThrowsNotFoundException()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetOrCreateWalletAsync(Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetOrCreateWalletAsync_WhenUserInactive_ThrowsForbiddenException()
    {
        var user = TestDataBuilder.BuildUser(status: UserStatus.Banned);
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetOrCreateWalletAsync(user.Id));
    }

    [Fact]
    public async Task CreditAndDebitAsync_UpdateBalanceAndCreateTransactions()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m);
        var service = CreateService();
        var relatedId = Guid.NewGuid();

        var credit = await service.CreditAsync(
            wallet.Id,
            500_000m,
            WalletTransactionType.WalletTopUp,
            new WalletTransactionMetadata
            {
                RelatedEntityType = "Payment",
                RelatedEntityId = relatedId,
                Description = "top up"
            });
        var debit = await service.DebitAsync(wallet.Id, 300_000m, WalletTransactionType.DepositPayment);

        Assert.Equal(1_200_000m, debit.Wallet.Balance);
        Assert.Equal(1_500_000m, credit.Transaction.BalanceAfter);
        Assert.Equal("Payment", credit.Transaction.RelatedEntityType);
        Assert.Equal(relatedId, credit.Transaction.RelatedEntityId);
        Assert.Equal(2, _fixture.Context.WalletTransactions.Count());
    }

    [Fact]
    public async Task DebitAsync_WhenAmountInvalid_ThrowsBadRequestException()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m);
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.DebitAsync(wallet.Id, 0m, WalletTransactionType.DepositPayment));
    }

    [Fact]
    public async Task DebitAsync_WhenInsufficientBalance_ThrowsConflictException()
    {
        var wallet = await SeedWalletAsync(balance: 100_000m);
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(
            () => service.DebitAsync(wallet.Id, 200_000m, WalletTransactionType.DepositPayment));
    }

    [Fact]
    public async Task IncreaseAndDecreaseReservedAsync_UpdateReservedBalance()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m);
        var service = CreateService();

        var increased = await service.IncreaseReservedAsync(wallet.Id, 400_000m, WalletTransactionType.DepositPayment);
        var decreased = await service.DecreaseReservedAsync(wallet.Id, 150_000m, WalletTransactionType.DepositRefundCredit);

        Assert.Equal(400_000m, increased.Wallet.ReservedBalance);
        Assert.Equal(250_000m, decreased.Wallet.ReservedBalance);
        Assert.Equal(750_000m, decreased.Wallet.AvailableBalance);
    }

    [Fact]
    public async Task TransferAsync_MovesBalanceBetweenWalletsWithSharedTransferGroup()
    {
        var source = await SeedWalletAsync(balance: 1_000_000m);
        var target = await SeedWalletAsync(balance: 200_000m);
        var transferGroupId = Guid.NewGuid();
        var service = CreateService();

        var result = await service.TransferAsync(
            source.Id,
            target.Id,
            300_000m,
            WalletTransactionType.DepositPayment,
            WalletTransactionType.DepositReceive,
            new WalletTransactionMetadata { TransferGroupId = transferGroupId, Description = "deposit" });

        Assert.Equal(transferGroupId, result.TransferGroupId);
        Assert.Equal(700_000m, source.Balance);
        Assert.Equal(500_000m, target.Balance);
        Assert.Equal(transferGroupId, result.DebitTransaction.TransferGroupId);
        Assert.Equal(transferGroupId, result.CreditTransaction.TransferGroupId);
    }

    [Fact]
    public async Task TransferAsync_WhenSameWallet_ThrowsBadRequestException()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m);
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.TransferAsync(
                wallet.Id,
                wallet.Id,
                100_000m,
                WalletTransactionType.DepositPayment,
                WalletTransactionType.DepositReceive));
    }

    [Fact]
    public async Task TransferFromReservedWithinTransactionAsync_ReleasesReservedAndTransfersAmount()
    {
        var source = await SeedWalletAsync(balance: 1_000_000m, reserved: 300_000m);
        var target = await SeedWalletAsync(balance: 0m);
        var service = CreateService();

        var result = await service.TransferFromReservedWithinTransactionAsync(
            source.Id,
            target.Id,
            amount: 300_000m,
            reservedAmountToRelease: 200_000m,
            WalletTransactionType.DepositRefundDebit,
            WalletTransactionType.DepositRefundCredit);

        Assert.Equal(700_000m, source.Balance);
        Assert.Equal(100_000m, source.ReservedBalance);
        Assert.Equal(300_000m, target.Balance);
        Assert.Equal(300_000m, result.CreditTransaction.BalanceAfter);
    }

    [Fact]
    public async Task TransferFromReservedWithinTransactionAsync_WhenReleaseAmountInvalid_ThrowsBadRequestException()
    {
        var source = await SeedWalletAsync(balance: 1_000_000m, reserved: 300_000m);
        var target = await SeedWalletAsync(balance: 0m);
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.TransferFromReservedWithinTransactionAsync(
                source.Id,
                target.Id,
                amount: 300_000m,
                reservedAmountToRelease: 400_000m,
                WalletTransactionType.DepositRefundDebit,
                WalletTransactionType.DepositRefundCredit));
    }

    [Fact]
    public async Task ReleaseReservedWithinTransactionAsync_WhenEnoughReserved_ReleasesReserved()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m, reserved: 250_000m);
        var service = CreateService();

        var result = await service.ReleaseReservedWithinTransactionAsync(
            wallet.Id,
            150_000m,
            WalletTransactionType.DepositRefundCredit);

        Assert.Equal(100_000m, result.Wallet.ReservedBalance);
        Assert.Equal(900_000m, result.Wallet.AvailableBalance);
    }

    [Fact]
    public async Task GetTransactionsAsync_ReturnsPagedTransactionsOrderedByNewest()
    {
        var wallet = await SeedWalletAsync(balance: 1_000_000m);
        _fixture.Context.WalletTransactions.AddRange(
            BuildTransaction(wallet, 100_000m, DateTimeOffset.UtcNow.AddMinutes(-3)),
            BuildTransaction(wallet, 200_000m, DateTimeOffset.UtcNow.AddMinutes(-2)),
            BuildTransaction(wallet, 300_000m, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.GetTransactionsAsync(wallet.UserId, page: 1, pageSize: 2);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal([300_000m, 200_000m], result.Items.Select(x => x.Amount));
    }

    private WalletService CreateService()
    {
        return new WalletService(_fixture.Context, _rowLockService);
    }

    private async Task<WalletAccount> SeedWalletAsync(decimal balance, decimal reserved = 0m)
    {
        var user = TestDataBuilder.BuildUser();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, balance);
        wallet.ReservedBalance = reserved;
        wallet.Status = WalletAccountStatus.Active;
        _fixture.Context.Users.Add(user);
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();
        return wallet;
    }

    private static WalletTransaction BuildTransaction(WalletAccount wallet, decimal amount, DateTimeOffset createdAt)
    {
        return new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletAccountId = wallet.Id,
            UserId = wallet.UserId,
            TransactionType = WalletTransactionType.WalletTopUp,
            Direction = WalletTransactionDirection.Credit,
            Amount = amount,
            BalanceBefore = 0m,
            BalanceAfter = amount,
            ReservedBalanceBefore = 0m,
            ReservedBalanceAfter = 0m,
            Status = WalletTransactionStatus.Succeeded,
            CreatedAt = createdAt
        };
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakePaymentRowLockService : IPaymentRowLockService
    {
        private readonly Microsoft.EntityFrameworkCore.DbContext _context;

        public FakePaymentRowLockService(Microsoft.EntityFrameworkCore.DbContext context)
        {
            _context = context;
        }

        public Task<WalletAccount?> LockWalletAccountAsync(Guid walletAccountId, CancellationToken cancellationToken = default)
        {
            return _context.Set<WalletAccount>().FirstOrDefaultAsync(x => x.Id == walletAccountId, cancellationToken);
        }

        public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Set<User>().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        }

        public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(null);
        public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<PaymentTransaction?>(null);
        public Task<BillingInvoice?> LockInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default) => Task.FromResult<BillingInvoice?>(null);
        public Task<RoomDeposit?> LockRoomDepositAsync(Guid roomDepositId, CancellationToken cancellationToken = default) => Task.FromResult<RoomDeposit?>(null);
        public Task<RentalContract?> LockRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default) => Task.FromResult<RentalContract?>(null);
    }
}
