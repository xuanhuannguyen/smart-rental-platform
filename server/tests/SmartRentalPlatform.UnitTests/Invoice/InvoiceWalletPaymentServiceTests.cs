using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Wallets.Responses;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Invoice;

public class InvoiceWalletPaymentServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeWalletService _walletService;
    private readonly FakePaymentRowLockService _rowLockService;

    public InvoiceWalletPaymentServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _walletService = new FakeWalletService();
        _rowLockService = new FakePaymentRowLockService();
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldThrowForbiddenException_WhenTenantIdIsMismatch()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new InvoiceWalletPaymentService(context, _walletService, _rowLockService);
        var otherUserId = Guid.NewGuid(); // Mismatch User Id

        _rowLockService.LockInvoiceAsyncFunc = (id) => Task.FromResult<SmartRentalPlatform.Domain.Entities.Billing.Invoice?>(invoice);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.PayInvoiceAsync(invoice.Id, otherUserId, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldThrowBadRequestException_WhenInvoiceIsDraft()
    {
        // Arrange
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id, status: InvoiceStatus.Draft);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new InvoiceWalletPaymentService(context, _walletService, _rowLockService);

        _rowLockService.LockInvoiceAsyncFunc = (id) => Task.FromResult<SmartRentalPlatform.Domain.Entities.Billing.Invoice?>(invoice);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => service.PayInvoiceAsync(invoice.Id, tenant.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldThrowNotFoundException_WhenInvoiceDoesNotExist()
    {
        var context = _fixture.Context;
        var service = new InvoiceWalletPaymentService(context, _walletService, _rowLockService);

        await Assert.ThrowsAsync<NotFoundException>(() => service.PayInvoiceAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldMarkPaidAndStoreTransferGroup_WhenInvoiceIsIssued()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "pay-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "pay-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id, status: InvoiceStatus.Issued);
        invoice.TotalAmount = 3000000;
        var expectedTransferGroupId = Guid.NewGuid();

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        _walletService.TransferGroupId = expectedTransferGroupId;
        _rowLockService.LockInvoiceAsyncFunc = _ => Task.FromResult<SmartRentalPlatform.Domain.Entities.Billing.Invoice?>(invoice);
        var service = new InvoiceWalletPaymentService(context, _walletService, _rowLockService);

        var result = await service.PayInvoiceAsync(invoice.Id, tenant.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedTransferGroupId, result.TransferGroupId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(expectedTransferGroupId, invoice.WalletTransferGroupId);
        Assert.NotNull(invoice.PaidAt);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldReturnSuccess_WhenInvoiceAlreadyPaid()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "paid-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "paid-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var transferGroupId = Guid.NewGuid();
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id, status: InvoiceStatus.Paid);
        invoice.WalletTransferGroupId = transferGroupId;

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new InvoiceWalletPaymentService(context, _walletService, _rowLockService);

        var result = await service.PayInvoiceAsync(invoice.Id, tenant.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(transferGroupId, result.TransferGroupId);

        context.ChangeTracker.Clear();
    }
}

#region Fakes for InvoiceWalletPaymentService
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
        => Task.FromResult(new WalletTransferResponse { TransferGroupId = TransferGroupId });

    public Task<WalletTransferResponse> TransferToReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse());

    public Task<WalletTransferResponse> TransferFromReservedWithinTransactionAsync(Guid sourceWalletAccountId, Guid targetWalletAccountId, decimal amount, decimal reservedAmountToRelease, WalletTransactionType debitTransactionType, WalletTransactionType creditTransactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletTransferResponse());

    public Task<WalletMutationResponse> ReleaseReservedWithinTransactionAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());

    public Task<WalletMutationResponse> DebitFromReservedAsync(Guid walletAccountId, decimal amount, WalletTransactionType transactionType, WalletTransactionMetadata? metadata = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new WalletMutationResponse());
}

public class FakePaymentRowLockService : IPaymentRowLockService
{
    public Func<Guid, Task<SmartRentalPlatform.Domain.Entities.Billing.Invoice?>> LockInvoiceAsyncFunc { get; set; } = _ => Task.FromResult<SmartRentalPlatform.Domain.Entities.Billing.Invoice?>(null);

    public Task<PaymentTransaction?> LockPaymentTransactionByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    public Task<PaymentTransaction?> LockPaymentTransactionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentTransaction?>(null);

    public Task<WithdrawalRequest?> LockWithdrawalRequestByProviderOrderCodeAsync(string providerOrderCode, CancellationToken cancellationToken = default)
        => Task.FromResult<WithdrawalRequest?>(null);

    public Task<WithdrawalRequest?> LockWithdrawalRequestByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<WithdrawalRequest?>(null);

    public Task<WalletAccount?> LockWalletAccountAsync(Guid walletAccountId, CancellationToken cancellationToken = default)
        => Task.FromResult<WalletAccount?>(null);

    public Task<SmartRentalPlatform.Domain.Entities.Billing.Invoice?> LockInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        => LockInvoiceAsyncFunc(invoiceId);

    public Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<User?>(null);

    public Task<RoomDeposit?> LockRoomDepositAsync(Guid roomDepositId, CancellationToken cancellationToken = default)
        => Task.FromResult<RoomDeposit?>(null);

    public Task<RentalContract?> LockRentalContractAsync(Guid contractId, CancellationToken cancellationToken = default)
        => Task.FromResult<RentalContract?>(null);
}
#endregion
