using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Invoice;

public class BillingServiceTests : IClassFixture<TestDatabaseFixture>
{
    private static readonly Guid ElectricServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid WaterServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly Guid InternetServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    private static readonly Guid TrashServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000004");

    private readonly TestDatabaseFixture _fixture;
    private readonly FakeBillingContractReadService _contractReadService;
    private readonly FakeInvoiceWalletPaymentService _walletPaymentService;

    public BillingServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _contractReadService = new FakeBillingContractReadService();
        _walletPaymentService = new FakeInvoiceWalletPaymentService();
    }

    [Fact]
    public async Task GetRoomBillingContextAsync_ShouldThrowNotFoundException_WhenNoActiveContractExists()
    {
        // Arrange
        var context = _fixture.Context;
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);

        context.Users.Add(landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRoomBillingContextAsync(landlord.Id, room.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetRoomBillingContextAsync_ShouldReturnContext_WhenActiveContractExists()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetRoomBillingContextAsync(seed.Landlord.Id, seed.Room.Id, CancellationToken.None);

        Assert.Equal(seed.Room.Id, result.RoomId);
        Assert.Equal(seed.Contract.Id, result.ContractId);
        Assert.Equal(seed.Tenant.Id, result.TenantUserId);
        Assert.Equal(seed.Contract.MonthlyRent, result.MonthlyRent);
    }

    [Fact]
    public async Task GetRoomInvoicePreviewAsync_ShouldThrowBadRequestException_WhenPeriodEndBeforeStart()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetRoomInvoicePreviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 1),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomInvoicePreviewAsync_ShouldThrowBadRequestException_WhenPeriodSpansMultipleMonths()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetRoomInvoicePreviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 1),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomInvoicePreviewAsync_ShouldThrowNotFoundException_WhenNoActiveContractExists()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRoomInvoicePreviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task GetRoomInvoicePreviewAsync_ShouldReturnPreviewWithMissingPricesBlockReason_WhenPricesAreMissing()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetRoomInvoicePreviewAsync(
            seed.Landlord.Id,
            seed.Room.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            CancellationToken.None);

        Assert.False(result.CanGenerate);
        Assert.NotNull(result.BlockReason);
        Assert.Equal(seed.Contract.Id, result.ContractId);
    }

    [Fact]
    public async Task GetRoomInvoicePreviewAsync_ShouldReturnGeneratablePreview_WhenAllPricesAreConfigured()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        AddDefaultServicePrices(seed.House.Id);
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetRoomInvoicePreviewAsync(
            seed.Landlord.Id,
            seed.Room.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            CancellationToken.None);

        Assert.True(result.CanGenerate, result.BlockReason);
        Assert.Null(result.BlockReason);
        Assert.Equal(seed.Contract.MonthlyRent, result.RentAmount);
        Assert.Equal(2, result.FixedServices.Count);
        Assert.Equal(2, result.MeteredServices.Count);
        Assert.Equal(result.RentAmount + result.FixedServiceAmount, result.TotalAmount);
    }

    [Fact]
    public async Task IssueInvoiceAsync_ShouldMarkDraftInvoiceAsIssued()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "issue-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "issue-landlord@example.com");
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

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.IssueInvoiceAsync(landlord.Id, invoice.Id, CancellationToken.None);

        Assert.Equal(InvoiceStatus.Issued.ToString(), result.Status);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.NotNull(invoice.SentAt);
        Assert.NotNull(invoice.IssueDate);

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task IssueInvoiceAsync_ShouldThrowBadRequestException_WhenInvoiceIsNotDraft()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: "issued-tenant@example.com");
        var landlord = TestDataBuilder.BuildUser(email: "issued-landlord@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id, status: InvoiceStatus.Issued);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.IssueInvoiceAsync(landlord.Id, invoice.Id, CancellationToken.None));

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetBillingServiceTypesAsync_ShouldReturnOnlyActiveTypesOrderedByName()
    {
        var context = _fixture.Context;
        context.BillingServiceTypes.AddRange(
            new BillingServiceType { Id = Guid.NewGuid(), Name = "Water", IsActive = true },
            new BillingServiceType { Id = Guid.NewGuid(), Name = "Electric", IsActive = true },
            new BillingServiceType { Id = Guid.NewGuid(), Name = "Inactive", IsActive = false });
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetBillingServiceTypesAsync(CancellationToken.None);

        Assert.Contains(result, x => x.Name == "Electric");
        Assert.Contains(result, x => x.Name == "Water");
        Assert.DoesNotContain(result, x => x.Name == "Inactive");
        Assert.Equal(result.OrderBy(x => x.Name).Select(x => x.Name), result.Select(x => x.Name));
    }

    [Fact]
    public async Task GetLandlordInvoicesAsync_ShouldReturnFilteredInvoices()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        var otherLandlord = TestDataBuilder.BuildUser(email: "other-landlord@example.com");
        var otherInvoice = TestDataBuilder.BuildInvoice(seed.Contract.Id, seed.Room.Id, seed.Tenant.Id, otherLandlord.Id, status: InvoiceStatus.Issued);
        otherInvoice.InvoiceNo = "INV-OTHER";
        context.Users.Add(otherLandlord);
        context.Invoices.Add(otherInvoice);
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetLandlordInvoicesAsync(seed.Landlord.Id, status: "Issued", search: seed.Invoice.InvoiceNo, contractId: seed.Contract.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(seed.Invoice.Id, result[0].Id);
    }

    [Fact]
    public async Task GetLandlordInvoiceAsync_ShouldThrowForbiddenException_WhenLandlordDoesNotOwnInvoice()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetLandlordInvoiceAsync(Guid.NewGuid(), seed.Invoice.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CancelInvoiceAsync_ShouldMarkInvoiceCancelledAndTrimReason()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.CancelInvoiceAsync(seed.Landlord.Id, seed.Invoice.Id, "  tenant moved out  ", CancellationToken.None);

        Assert.Equal(InvoiceStatus.Cancelled.ToString(), result.Status);
        Assert.Equal(InvoiceStatus.Cancelled, seed.Invoice.Status);
        Assert.Equal("tenant moved out", seed.Invoice.CancelReason);
        Assert.NotNull(seed.Invoice.CancelledAt);
    }

    [Fact]
    public async Task CancelInvoiceAsync_ShouldThrowBadRequestException_WhenInvoiceIsPaid()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Paid);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CancelInvoiceAsync(seed.Landlord.Id, seed.Invoice.Id, "reason", CancellationToken.None));
    }

    [Fact]
    public async Task GetMyInvoicesAsync_ShouldExcludeDraftInvoices()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        var draft = TestDataBuilder.BuildInvoice(seed.Contract.Id, seed.Room.Id, seed.Tenant.Id, seed.Landlord.Id, status: InvoiceStatus.Draft);
        draft.InvoiceNo = "INV-DRAFT";
        context.Invoices.Add(draft);
        await context.SaveChangesAsync();

        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.GetMyInvoicesAsync(seed.Tenant.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(seed.Invoice.Id, result[0].Id);
    }

    [Fact]
    public async Task GetMyInvoiceAsync_ShouldThrowNotFoundException_WhenInvoiceIsDraft()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Draft);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMyInvoiceAsync(seed.Tenant.Id, seed.Invoice.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldReturnUpdatedInvoice_WhenWalletPaymentSucceeds()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        _walletPaymentService.Result = new InvoiceWalletPaymentResult(true, Guid.NewGuid(), null);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        var result = await service.PayInvoiceAsync(seed.Tenant.Id, seed.Invoice.Id, CancellationToken.None);

        Assert.Equal(seed.Invoice.Id, result.Id);
    }

    [Fact]
    public async Task PayInvoiceAsync_ShouldThrowBadRequestException_WhenWalletPaymentFails()
    {
        var context = _fixture.Context;
        var seed = await SeedInvoiceGraphAsync(status: InvoiceStatus.Issued);
        _walletPaymentService.Result = new InvoiceWalletPaymentResult(false, null, "No balance");
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.PayInvoiceAsync(seed.Tenant.Id, seed.Invoice.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldThrowBadRequestException_WhenPeriodEndBeforeStart()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 10),
            new DateOnly(2026, 6, 1),
            0,
            null,
            []);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GenerateInvoiceWithReadingsAsync(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldThrowBadRequestException_WhenPeriodSpansMultipleMonths()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 1),
            0,
            null,
            []);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GenerateInvoiceWithReadingsAsync(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldThrowBadRequestException_WhenDiscountIsNegative()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            -1,
            null,
            []);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GenerateInvoiceWithReadingsAsync(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldThrowBadRequestException_WhenMeterReadingsIsNull()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            0,
            null,
            null!);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GenerateInvoiceWithReadingsAsync(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldCreateDraftInvoiceWithItems_WhenInputIsValid()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        AddDefaultServicePrices(seed.House.Id);
        await context.SaveChangesAsync();
        _contractReadService.ActiveContract = BuildContractSnapshot(seed, terminationDate: null, terminationType: null);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            seed.Contract.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            0,
            "January invoice",
            [
                new MeterReadingInput(ElectricServiceTypeId, 0, 100),
                new MeterReadingInput(WaterServiceTypeId, 0, 10)
            ]);

        var result = await service.GenerateInvoiceWithReadingsAsync(seed.Landlord.Id, request, CancellationToken.None);

        Assert.Equal(InvoiceStatus.Draft.ToString(), result.Status);
        Assert.Equal(seed.Contract.Id, result.ContractId);
        Assert.True(result.TotalAmount > seed.Contract.MonthlyRent);
        Assert.True(await context.Invoices.AnyAsync(x => x.Id == result.Id && x.Status == InvoiceStatus.Draft));
        Assert.Equal(5, await context.InvoiceItems.CountAsync(x => x.InvoiceId == result.Id));
        Assert.Equal(2, await context.MeterReadings.CountAsync(x => x.ContractId == seed.Contract.Id));
    }

    [Fact]
    public async Task GenerateInvoiceWithReadingsAsync_ShouldLinkProofMediaAsset_WhenProofMediaAssetIdExists()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        AddDefaultServicePrices(seed.House.Id);

        var proofAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = seed.Landlord.Id,
            BucketName = "test-bucket",
            ObjectKey = "private/meter-reading-images/2026/07/11/proof-electric.jpg",
            OriginalFileName = "proof-electric.jpg",
            StoredFileName = "proof-electric.jpg",
            ContentType = "image/jpeg",
            FileSize = 128,
            Scope = MediaScope.MeterReadingImage,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.MediaAssets.Add(proofAsset);
        await context.SaveChangesAsync();

        _contractReadService.ActiveContract = BuildContractSnapshot(seed, terminationDate: null, terminationType: null);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new GenerateInvoiceWithReadingsRequest(
            seed.Contract.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            0,
            "January invoice",
            [
                new MeterReadingInput(ElectricServiceTypeId, 0, 100, proofAsset.Id),
                new MeterReadingInput(WaterServiceTypeId, 0, 10)
            ]);

        var result = await service.GenerateInvoiceWithReadingsAsync(seed.Landlord.Id, request, CancellationToken.None);

        var reading = await context.MeterReadings
            .AsNoTracking()
            .FirstAsync(x => x.ContractId == seed.Contract.Id && x.ServiceTypeId == ElectricServiceTypeId);
        var meterItem = result.Items.First(x => x.MeterReadingId == reading.Id);

        Assert.Equal(proofAsset.Id, reading.ProofMediaAssetId);
        Assert.Equal(proofAsset.Id, meterItem.MeterReadingProofMediaAssetId);
        Assert.Equal(nameof(MeterReading), context.MediaAssets.Single(x => x.Id == proofAsset.Id).LinkedEntityType);
        Assert.Equal(reading.Id, context.MediaAssets.Single(x => x.Id == proofAsset.Id).LinkedEntityId);
    }

    [Fact]
    public async Task GetLandlordInvoiceAsync_ShouldThrowNotFoundException_WhenInvoiceDoesNotExist()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetLandlordInvoiceAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetTerminationInvoicePreviewAsync_ShouldThrowNotFoundException_WhenContractSnapshotMissing()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetTerminationInvoicePreviewAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetTerminationInvoicePreviewAsync_ShouldThrowConflictException_WhenContractHasNoTerminationDate()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        _contractReadService.Contract = BuildContractSnapshot(seed, terminationDate: null, terminationType: null);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<ConflictException>(() => service.GetTerminationInvoicePreviewAsync(seed.Landlord.Id, seed.Contract.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetTerminationInvoicePreviewAsync_ShouldThrowForbiddenException_WhenLandlordDoesNotOwnContract()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        _contractReadService.Contract = BuildContractSnapshot(seed, terminationDate: new DateOnly(2026, 5, 20), terminationType: ContractTerminationType.TenantUnilateral);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetTerminationInvoicePreviewAsync(Guid.NewGuid(), seed.Contract.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CreateNextTerminationInvoiceAsync_ShouldThrowConflictException_WhenContractHasNoTerminationDate()
    {
        var context = _fixture.Context;
        var seed = await SeedActiveContractGraphAsync();
        _contractReadService.Contract = BuildContractSnapshot(seed, terminationDate: null, terminationType: null);
        var service = new BillingService(context, _contractReadService, _walletPaymentService);
        var request = new CreateTerminationInvoiceRequest(0, null, []);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateNextTerminationInvoiceAsync(seed.Landlord.Id, seed.Contract.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateFinalInvoiceForTerminationAsync_ShouldThrowBadRequestException_WhenDiscountIsNegative()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateFinalInvoiceForTerminationAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 5, 20),
            -1,
            null,
            [],
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateFinalInvoiceForTerminationAsync_ShouldThrowBadRequestException_WhenMeterReadingsIsNull()
    {
        var context = _fixture.Context;
        var service = new BillingService(context, _contractReadService, _walletPaymentService);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateFinalInvoiceForTerminationAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 5, 20),
            0,
            null,
            null!,
            CancellationToken.None));
    }

    private async Task<InvoiceSeed> SeedInvoiceGraphAsync(InvoiceStatus status)
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: $"tenant-{Guid.NewGuid():N}@example.com");
        var landlord = TestDataBuilder.BuildUser(email: $"landlord-{Guid.NewGuid():N}@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id);
        var invoice = TestDataBuilder.BuildInvoice(contract.Id, room.Id, tenant.Id, landlord.Id, status: status);
        invoice.InvoiceNo = $"INV-{Guid.NewGuid():N}"[..20];
        invoice.TotalAmount = invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount;
        invoice.Room = room;
        invoice.Tenant = tenant;
        invoice.Landlord = landlord;
        invoice.RentalContract = contract;

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        return new InvoiceSeed(tenant, landlord, house, room, request, contract, invoice);
    }

    private async Task<ActiveContractSeed> SeedActiveContractGraphAsync()
    {
        var context = _fixture.Context;
        var tenant = TestDataBuilder.BuildUser(email: $"billing-tenant-{Guid.NewGuid():N}@example.com");
        var landlord = TestDataBuilder.BuildUser(email: $"billing-landlord-{Guid.NewGuid():N}@example.com");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var room = TestDataBuilder.BuildRoom(house.Id, status: RoomStatus.Occupied);
        var request = TestDataBuilder.BuildRentalRequest(tenant.Id, room.Id);
        var contract = TestDataBuilder.BuildRentalContract(request.Id, room.Id, tenant.Id, status: RentalContractStatus.Active);
        contract.StartDate = new DateOnly(2026, 1, 1);
        contract.EndDate = new DateOnly(2026, 12, 31);
        contract.ActivatedAt = DateTimeOffset.UtcNow.AddDays(-30);

        context.Users.AddRange(tenant, landlord);
        context.RoomingHouses.Add(house);
        context.Rooms.Add(room);
        context.RentalRequests.Add(request);
        context.RentalContracts.Add(contract);
        await context.SaveChangesAsync();

        return new ActiveContractSeed(tenant, landlord, house, room, request, contract);
    }

    private void AddDefaultServicePrices(Guid roomingHouseId)
    {
        var context = _fixture.Context;
        context.RoomingHouseServicePrices.AddRange(
            new RoomingHouseServicePrice
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                ServiceTypeId = ElectricServiceTypeId,
                PricingUnit = PricingUnit.MeterReading,
                UnitPrice = 3500,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsActive = true
            },
            new RoomingHouseServicePrice
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                ServiceTypeId = WaterServiceTypeId,
                PricingUnit = PricingUnit.MeterReading,
                UnitPrice = 15000,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsActive = true
            },
            new RoomingHouseServicePrice
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                ServiceTypeId = InternetServiceTypeId,
                PricingUnit = PricingUnit.PerMonth,
                UnitPrice = 100000,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsActive = true
            },
            new RoomingHouseServicePrice
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                ServiceTypeId = TrashServiceTypeId,
                PricingUnit = PricingUnit.PerPersonPerMonth,
                UnitPrice = 30000,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsActive = true
            });
    }

    private static BillingContractSnapshot BuildContractSnapshot(
        ActiveContractSeed seed,
        DateOnly? terminationDate,
        ContractTerminationType? terminationType)
    {
        return new BillingContractSnapshot(
            seed.Contract.Id,
            seed.Room.Id,
            seed.House.Id,
            seed.Tenant.Id,
            seed.Landlord.Id,
            seed.Contract.MonthlyRent,
            seed.Contract.PaymentDay,
            seed.Contract.StartDate,
            seed.Contract.EndDate,
            seed.Contract.Status,
            terminationDate,
            terminationType);
    }

    private sealed record InvoiceSeed(
        SmartRentalPlatform.Domain.Entities.Users.User Tenant,
        SmartRentalPlatform.Domain.Entities.Users.User Landlord,
        SmartRentalPlatform.Domain.Entities.Properties.RoomingHouse House,
        SmartRentalPlatform.Domain.Entities.Properties.Room Room,
        SmartRentalPlatform.Domain.Entities.Rental.RentalRequest Request,
        SmartRentalPlatform.Domain.Entities.RentalContracts.RentalContract Contract,
        SmartRentalPlatform.Domain.Entities.Billing.Invoice Invoice);

    private sealed record ActiveContractSeed(
        SmartRentalPlatform.Domain.Entities.Users.User Tenant,
        SmartRentalPlatform.Domain.Entities.Users.User Landlord,
        SmartRentalPlatform.Domain.Entities.Properties.RoomingHouse House,
        SmartRentalPlatform.Domain.Entities.Properties.Room Room,
        SmartRentalPlatform.Domain.Entities.Rental.RentalRequest Request,
        SmartRentalPlatform.Domain.Entities.RentalContracts.RentalContract Contract);
}

#region Fakes for BillingService
public class FakeBillingContractReadService : IBillingContractReadService
{
    public BillingContractSnapshot? ActiveContract { get; set; }
    public BillingContractSnapshot? Contract { get; set; }

    public Task<BillingContractSnapshot?> GetActiveContractAsync(Guid contractId, CancellationToken cancellationToken = default)
        => Task.FromResult(ActiveContract);

    public Task<BillingContractSnapshot?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default)
        => Task.FromResult(Contract);
}

public class FakeInvoiceWalletPaymentService : IInvoiceWalletPaymentService
{
    public InvoiceWalletPaymentResult Result { get; set; } = new(true, Guid.NewGuid(), null);

    public Task<InvoiceWalletPaymentResult> PayInvoiceAsync(Guid invoiceId, Guid tenantUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result);
}
#endregion
