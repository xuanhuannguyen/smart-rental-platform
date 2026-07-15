using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Requests;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Payments;

public class PayOSTopUpServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakeWalletService _walletService = new();
    private readonly FakePayOSClient _payOSClient = new();

    [Fact]
    public async Task CreateTopUpAsync_WhenAmountBelowMinimum_ThrowsBadRequestException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateTopUpAsync(Guid.NewGuid(), new CreatePayOSTopUpRequest { Amount = 9_999m }));
    }

    [Fact]
    public async Task CreateTopUpAsync_WhenUserMissing_ThrowsNotFoundException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateTopUpAsync(Guid.NewGuid(), new CreatePayOSTopUpRequest { Amount = 100_000m }));
    }

    [Fact]
    public async Task CreateTopUpAsync_WhenKycNotApproved_ThrowsForbiddenException()
    {
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.CreateTopUpAsync(user.Id, new CreatePayOSTopUpRequest { Amount = 100_000m }));
    }

    [Fact]
    public async Task CreateTopUpAsync_WhenValid_CreatesPendingPaymentAndCallsPayOS()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, balance: 0m);
        _walletService.Wallet = wallet;
        _fixture.Context.WalletAccounts.Add(wallet);
        await _fixture.Context.SaveChangesAsync();
        _payOSClient.Result = new PayOSCreatePaymentResult
        {
            ProviderTransactionCode = "payos-txn",
            CheckoutUrl = "https://pay.local/checkout",
            QrCode = "qr",
            GatewayResponseCode = "00",
            GatewayResponseMessage = "created",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        var service = CreateService();

        var result = await service.CreateTopUpAsync(user.Id, new CreatePayOSTopUpRequest
        {
            Amount = 250_000m,
            IdempotencyKey = "first",
            Note = "This note is intentionally longer than twenty five characters",
            ReturnUrl = "https://app.local/return?x=1#frag",
            CancelUrl = "https://app.local/cancel"
        });

        Assert.Equal(250_000m, result.Amount);
        Assert.Equal(PaymentTransactionStatus.Pending.ToString(), result.Status);
        Assert.Equal("https://pay.local/checkout", result.PaymentUrl);
        Assert.Single(_fixture.Context.PaymentTransactions);
        Assert.NotNull(_payOSClient.LastInput);
        Assert.Equal(250_000m, _payOSClient.LastInput.Amount);
        Assert.Equal("This note is intentionall", _payOSClient.LastInput.Description);
        Assert.Contains("paymentTransactionId=", _payOSClient.LastInput.ReturnUrl);
        Assert.Contains("#frag", _payOSClient.LastInput.ReturnUrl);
    }

    [Fact]
    public async Task CreateTopUpAsync_WhenIdempotencyKeyAlreadyExistsWithSameAmount_ReturnsExistingPayment()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, balance: 0m);
        _walletService.Wallet = wallet;
        var existing = BuildPayment(user.Id, wallet.Id, amount: 300_000m, idempotencyKey: $"wallet-topup:{user.Id:N}:same");
        existing.ProviderCheckoutUrl = "https://pay.local/existing";
        _fixture.Context.WalletAccounts.Add(wallet);
        _fixture.Context.PaymentTransactions.Add(existing);
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.CreateTopUpAsync(user.Id, new CreatePayOSTopUpRequest
        {
            Amount = 300_000m,
            IdempotencyKey = "same"
        });

        Assert.Equal(existing.Id, result.PaymentTransactionId);
        Assert.Equal("https://pay.local/existing", result.PaymentUrl);
        Assert.Null(_payOSClient.LastInput);
    }

    [Fact]
    public async Task CreateTopUpAsync_WhenIdempotencyKeyExistsWithDifferentAmount_ThrowsConflictException()
    {
        var user = await SeedApprovedKycUserAsync();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id, balance: 0m);
        _walletService.Wallet = wallet;
        _fixture.Context.WalletAccounts.Add(wallet);
        _fixture.Context.PaymentTransactions.Add(BuildPayment(user.Id, wallet.Id, amount: 300_000m, idempotencyKey: $"wallet-topup:{user.Id:N}:same"));
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateTopUpAsync(user.Id, new CreatePayOSTopUpRequest
            {
                Amount = 400_000m,
                IdempotencyKey = "same"
            }));
    }

    [Fact]
    public async Task GetTopUpHistoryAsync_ReturnsOnlyUsersTopUpsPagedByNewest()
    {
        var user = TestDataBuilder.BuildUser();
        var otherUser = TestDataBuilder.BuildUser();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id);
        var otherWallet = TestDataBuilder.BuildWalletAccount(otherUser.Id);
        _fixture.Context.Users.AddRange(user, otherUser);
        _fixture.Context.WalletAccounts.AddRange(wallet, otherWallet);
        _fixture.Context.PaymentTransactions.AddRange(
            BuildPayment(user.Id, wallet.Id, 100_000m, "a", createdAt: DateTimeOffset.UtcNow.AddMinutes(-3)),
            BuildPayment(user.Id, wallet.Id, 200_000m, "b", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            BuildPayment(user.Id, wallet.Id, 300_000m, "c", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1)),
            BuildPayment(otherUser.Id, otherWallet.Id, 999_000m, "d", createdAt: DateTimeOffset.UtcNow));
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.GetTopUpHistoryAsync(user.Id, page: 1, pageSize: 2);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal([300_000m, 200_000m], result.Items.Select(x => x.Amount));
    }

    [Fact]
    public async Task GetTopUpAsync_WhenTopUpBelongsToUser_ReturnsDetail()
    {
        var user = TestDataBuilder.BuildUser();
        var wallet = TestDataBuilder.BuildWalletAccount(user.Id);
        var payment = BuildPayment(user.Id, wallet.Id, 123_000m, "detail");
        _fixture.Context.Users.Add(user);
        _fixture.Context.WalletAccounts.Add(wallet);
        _fixture.Context.PaymentTransactions.Add(payment);
        await _fixture.Context.SaveChangesAsync();
        var service = CreateService();

        var result = await service.GetTopUpAsync(user.Id, payment.Id);

        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(123_000m, result.Amount);
        Assert.Equal(PaymentMethod.PayOS.ToString(), result.PaymentMethod);
    }

    [Fact]
    public async Task GetTopUpAsync_WhenMissing_ThrowsNotFoundException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetTopUpAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    private PayOSTopUpService CreateService()
    {
        return new PayOSTopUpService(_fixture.Context, _walletService, _payOSClient);
    }

    private async Task<User> SeedApprovedKycUserAsync()
    {
        var user = TestDataBuilder.BuildUser();
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            CitizenIdHash = Guid.NewGuid().ToString("N"),
            Status = KycVerificationStatus.Approved,
            SubmittedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();
        return user;
    }

    private static PaymentTransaction BuildPayment(
        Guid userId,
        Guid walletId,
        decimal amount,
        string idempotencyKey,
        DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;

        return new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            WalletAccountId = walletId,
            PayerUserId = userId,
            IdempotencyKey = idempotencyKey,
            Amount = amount,
            Currency = "VND",
            PaymentPurpose = PaymentPurpose.WalletTopUp,
            PaymentMethod = PaymentMethod.PayOS,
            ProviderOrderCode = $"order-{Guid.NewGuid():N}",
            ProviderCheckoutUrl = $"https://pay.local/{Guid.NewGuid():N}",
            Status = PaymentTransactionStatus.Pending,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakePayOSClient : IPayOSClient
    {
        public PayOSCreatePaymentInput? LastInput { get; private set; }

        public PayOSCreatePaymentResult Result { get; set; } = new()
        {
            CheckoutUrl = "https://pay.local/default",
            QrCode = "qr",
            GatewayResponseCode = "00",
            GatewayResponseMessage = "created"
        };

        public Task<PayOSCreatePaymentResult> CreatePaymentAsync(PayOSCreatePaymentInput input, CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(Result);
        }

        public Task<PayOSCreatePayoutResult> CreatePayoutAsync(PayOSCreatePayoutInput input, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PayOSPayoutDetailsResult> GetPayoutDetailsAsync(string providerOrderCode, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeWalletService : IWalletService
    {
        public WalletAccount? Wallet { get; set; }

        public Task<WalletAccount> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Wallet ?? throw new InvalidOperationException("Wallet was not seeded."));
        }

        public Task<WalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<WalletTransactionResponse>> GetTransactionsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> CreditAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> DebitAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> IncreaseReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> DecreaseReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> DebitFromReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletTransferResponse> TransferAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletTransferResponse> TransferWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletTransferResponse> TransferToReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletTransferResponse> TransferFromReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, decimal reservedAmountToRelease, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WalletMutationResponse> ReleaseReservedWithinTransactionAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
