using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Invoice;

public class MeterReadingInputResolverTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public MeterReadingInputResolverTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task ResolveAsync_DuplicateServiceInput_ShouldThrowBadRequestException()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new MeterReadingInputResolver(context);
        var contractId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();

        var period = new ResolvedBillingPeriod(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            30, 30, true);

        var requestMeterReadings = new List<MeterReadingInput>
        {
            new MeterReadingInput(serviceTypeId, 100, 150, null),
            new MeterReadingInput(serviceTypeId, 150, 200, null)
        };

        var serviceTypeById = new Dictionary<Guid, BillingServiceType>();
        var prices = new List<RoomingHouseServicePrice>();

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => resolver.ResolveAsync(
            contractId, period, requestMeterReadings, serviceTypeById, prices, false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_MissingFirstPreviousReading_ShouldThrowBadRequestException()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new MeterReadingInputResolver(context);
        var contractId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();

        var period = new ResolvedBillingPeriod(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            30, 30, true);

        var requestMeterReadings = new List<MeterReadingInput>
        {
            new MeterReadingInput(serviceTypeId, null, 150, null)
        };

        var serviceType = new BillingServiceType
        {
            Id = serviceTypeId,
            Name = "Water",
            IsActive = true,
            SupportsMeterReading = true,
            MeterUnitName = "m3"
        };
        var serviceTypeById = new Dictionary<Guid, BillingServiceType> { [serviceTypeId] = serviceType };

        var price = new RoomingHouseServicePrice
        {
            ServiceTypeId = serviceTypeId,
            PricingUnit = PricingUnit.MeterReading,
            UnitPrice = 20000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };
        var prices = new List<RoomingHouseServicePrice> { price };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => resolver.ResolveAsync(
            contractId, period, requestMeterReadings, serviceTypeById, prices, false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_CurrentReadingLessThanPreviousReading_ShouldThrowBadRequestException()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new MeterReadingInputResolver(context);
        var contractId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();

        var period = new ResolvedBillingPeriod(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            30, 30, true);

        var requestMeterReadings = new List<MeterReadingInput>
        {
            new MeterReadingInput(serviceTypeId, 150, 100, null)
        };

        var serviceType = new BillingServiceType
        {
            Id = serviceTypeId,
            Name = "Electricity",
            IsActive = true,
            SupportsMeterReading = true,
            MeterUnitName = "kWh"
        };
        var serviceTypeById = new Dictionary<Guid, BillingServiceType> { [serviceTypeId] = serviceType };

        var price = new RoomingHouseServicePrice
        {
            ServiceTypeId = serviceTypeId,
            PricingUnit = PricingUnit.MeterReading,
            UnitPrice = 3500m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };
        var prices = new List<RoomingHouseServicePrice> { price };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => resolver.ResolveAsync(
            contractId, period, requestMeterReadings, serviceTypeById, prices, false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_OverlappingPeriod_ShouldThrowConflictException()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new MeterReadingInputResolver(context);
        var contractId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();

        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, start, end, 30, 30, true);

        var requestMeterReadings = new List<MeterReadingInput>
        {
            new MeterReadingInput(serviceTypeId, 100, 150, null)
        };

        var serviceType = new BillingServiceType
        {
            Id = serviceTypeId,
            Name = "Water",
            IsActive = true,
            SupportsMeterReading = true,
            MeterUnitName = "m3"
        };
        var serviceTypeById = new Dictionary<Guid, BillingServiceType> { [serviceTypeId] = serviceType };

        var price = new RoomingHouseServicePrice
        {
            ServiceTypeId = serviceTypeId,
            PricingUnit = PricingUnit.MeterReading,
            UnitPrice = 20000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };
        var prices = new List<RoomingHouseServicePrice> { price };

        // Seed overlapping reading in DB
        var overlappingInvoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            RoomId = roomId,
            TenantUserId = Guid.NewGuid(),
            LandlordUserId = Guid.NewGuid(),
            InvoiceNo = "INV-001",
            BillingPeriodStart = start,
            BillingPeriodEnd = end,
            RentAmount = 5000000,
            Status = InvoiceStatus.Draft
        };

        var overlappingReading = new MeterReading
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ContractId = contractId,
            ServiceTypeId = serviceTypeId,
            BillingPeriodStart = start,
            BillingPeriodEnd = end,
            PreviousReading = 100,
            CurrentReading = 150,
            Consumption = 50
        };

        var item = new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = overlappingInvoice.Id,
            Invoice = overlappingInvoice,
            MeterReadingId = overlappingReading.Id,
            MeterReading = overlappingReading,
            ItemType = InvoiceItemType.Service,
            Description = "Water Test",
            Quantity = 50,
            UnitPrice = 20000,
            Amount = 1000000
        };

        overlappingReading.InvoiceItems.Add(item);

        context.Invoices.Add(overlappingInvoice);
        context.MeterReadings.Add(overlappingReading);
        await context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => resolver.ResolveAsync(
            contractId, period, requestMeterReadings, serviceTypeById, prices, false, CancellationToken.None));
    }
}
