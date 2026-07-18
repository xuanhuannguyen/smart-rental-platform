using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomDeposits;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Contract;

public class RoomDepositServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeWalletService _walletService;
    private readonly FakePaymentRowLockService _rowLockService;

    public RoomDepositServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _walletService = new FakeWalletService();
        _rowLockService = new FakePaymentRowLockService();
    }

    [Fact]
    public async Task PayAsync_ShouldThrowForbiddenException_WhenTenantIdIsMismatch()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        var service = new RoomDepositService(context, _walletService, _rowLockService);
        var otherUserId = Guid.NewGuid(); // Mismatch User Id

        _rowLockService.LockRoomDepositAsyncFunc = (id) => Task.FromResult<RoomDeposit?>(deposit);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.PayAsync(otherUserId, deposit.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayAsync_ShouldThrowConflictException_WhenStatusIsNotPendingPayment()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id, status: RoomDepositStatus.Paid);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        var service = new RoomDepositService(context, _walletService, _rowLockService);

        _rowLockService.LockRoomDepositAsyncFunc = (id) => Task.FromResult<RoomDeposit?>(deposit);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.PayAsync(tenant.Id, deposit.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayAsync_ShouldReturnNull_WhenDepositDoesNotExist()
    {
        var context = _fixture.Context;
        var service = new RoomDepositService(context, _walletService, _rowLockService);

        var result = await service.PayAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireOverdueAsync_ShouldExpireDepositRequestAndReleaseReservedRoom()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "expire-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "expire-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Reserved);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id, status: RentalRequestStatus.Accepted);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id);
        deposit.PaymentDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        _rowLockService.LockRoomDepositAsyncFunc = _ => Task.FromResult<RoomDeposit?>(deposit);
        var service = new RoomDepositService(context, _walletService, _rowLockService);

        var expiredCount = await service.ExpireOverdueAsync(CancellationToken.None);

        Assert.Equal(1, expiredCount);
        Assert.Equal(RoomDepositStatus.Expired, deposit.Status);
        Assert.Equal(RentalRequestStatus.Expired, request.Status);
        Assert.Equal(RoomStatus.Available, room.Status);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayAsync_ShouldMarkDepositPaidAndCreateContract_WhenPendingPayment()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "pay-deposit-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "pay-deposit-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var policy = TestDataBuilder.BuildRentalPolicy(house.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Reserved);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id, status: RentalRequestStatus.Accepted);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id);
        var expectedTransferGroupId = Guid.NewGuid();

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.RentalPolicies.Add(policy);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        _walletService.TransferGroupId = expectedTransferGroupId;
        _rowLockService.LockRoomDepositAsyncFunc = _ => Task.FromResult<RoomDeposit?>(deposit);
        var service = new RoomDepositService(context, _walletService, _rowLockService);

        var result = await service.PayAsync(tenant.Id, deposit.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(RoomDepositStatus.Paid.ToString(), result!.Status);
        Assert.Equal(RoomDepositStatus.Paid, deposit.Status);
        Assert.Equal(expectedTransferGroupId, deposit.PaymentTransferGroupId);
        Assert.NotNull(deposit.PaidAt);
        Assert.Single(context.RentalContracts);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayAsync_ShouldThrowConflictException_WhenDepositIsExpired()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "expired-deposit-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "expired-deposit-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id);
        deposit.PaymentDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        var service = new RoomDepositService(context, _walletService, _rowLockService);

        await Assert.ThrowsAsync<ConflictException>(() => service.PayAsync(tenant.Id, deposit.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayAsync_ShouldThrowConflictException_WhenPaidDepositMissingContract()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "inconsistent-deposit-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "inconsistent-deposit-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var deposit = TestDataBuilder.BuildRoomDeposit(request.Id, room.Id, tenant.Id, landlord.Id, status: RoomDepositStatus.Paid);
        deposit.PaymentTransferGroupId = Guid.NewGuid();

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RoomDeposits.Add(deposit);
        await context.SaveChangesAsync();

        var service = new RoomDepositService(context, _walletService, _rowLockService);

        await Assert.ThrowsAsync<ConflictException>(() => service.PayAsync(tenant.Id, deposit.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }
}

#region Fakes for RoomDepositService
public class FakeWalletService : IWalletService
{
    public Guid TransferGroupId { get; set; } = Guid.NewGuid();

    public Task<WalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletResponse());

    public Task<WalletAccount> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletAccount { UserId = userId, Balance = 10000000 });

    public Task<PagedResult<WalletTransactionResponse>> GetTransactionsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<WalletTransactionResponse>
        {
            Items = new List<WalletTransactionResponse>(),
            TotalItems = 0,
            Page = page,
            PageSize = pageSize
        });

    public Task<WalletMutationResponse> CreditAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletMutationResponse> DebitAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletMutationResponse> IncreaseReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletMutationResponse> DecreaseReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletTransferResponse> TransferAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse());

    public Task<WalletTransferResponse> TransferWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse());

    public Task<WalletTransferResponse> TransferToReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse { TransferGroupId = TransferGroupId });

    public Task<WalletTransferResponse> TransferFromReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, decimal reservedAmountToRelease, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse());

    public Task<WalletMutationResponse> ReleaseReservedWithinTransactionAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletMutationResponse> DebitFromReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());
}

public class FakePaymentRowLockService : IPaymentRowLockService
{
    public Func<Guid, Task<RoomDeposit?>> LockRoomDepositAsyncFunc { get; set; } = _ => Task.FromResult<RoomDeposit?>(null);
    public Func<Guid, Task<WalletAccount?>> LockWalletAccountAsyncFunc { get; set; } = _ => Task.FromResult<WalletAccount?>(null);

    public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    public Task<WithdrawalRequest?> LockWithdrawalRequestByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        => Task.FromResult<WithdrawalRequest?>(null);

    public Task<WithdrawalRequest?> LockWithdrawalRequestByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<WithdrawalRequest?>(null);

    public Task<WalletAccount?> LockWalletAccountAsync(Guid walletAccountId, CancellationToken cancellationToken = default)
        => LockWalletAccountAsyncFunc(walletAccountId);

    public Task<BillingInvoice?> LockInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        => Task.FromResult<BillingInvoice?>(null);

    public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<User?>(null);

    public Task<RoomDeposit?> LockRoomDepositAsync(Guid roomDepositId, CancellationToken cancellationToken = default)
        => LockRoomDepositAsyncFunc(roomDepositId);

    public Task<RentalContract?> LockRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default)
        => Task.FromResult<RentalContract?>(null);
}
#endregion
