using System;
using System.Collections.Generic;
using System.Linq;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Invoice;

public class BillingInvoiceBuilderTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public BillingInvoiceBuilderTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public void AddRentInvoiceItem_FullMonth_ShouldAddCorrectItem()
    {
        // Arrange
        var context = _fixture.Context;
        var builder = new BillingInvoiceBuilder(context);
        var invoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice();
        var monthlyRent = 5000000m;
        var rentAmount = 5000000m;
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, start, end, 30, 30, true);

        // Act
        builder.AddRentInvoiceItem(invoice, monthlyRent, rentAmount, period, DateTimeOffset.UtcNow);

        // Assert
        Assert.Single(invoice.Items);
        var item = invoice.Items.First();
        Assert.Equal(InvoiceItemType.Rent, item.ItemType);
        Assert.Equal(1.0m, item.Quantity);
        Assert.Equal(monthlyRent, item.UnitPrice);
        Assert.Equal(rentAmount, item.Amount);
    }

    [Fact]
    public void AddRentInvoiceItem_PartialMonth_ShouldAddCorrectProratedItem()
    {
        // Arrange
        var context = _fixture.Context;
        var builder = new BillingInvoiceBuilder(context);
        var invoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice();
        var monthlyRent = 6000000m;
        var rentAmount = 3000000m; // 15 days of 30 days
        var start = new DateOnly(2026, 6, 16);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 15, 30, false);

        // Act
        builder.AddRentInvoiceItem(invoice, monthlyRent, rentAmount, period, DateTimeOffset.UtcNow);

        // Assert
        Assert.Single(invoice.Items);
        var item = invoice.Items.First();
        Assert.Equal(0.5m, item.Quantity); // 15 / 30
        Assert.Equal(monthlyRent, item.UnitPrice);
        Assert.Equal(rentAmount, item.Amount);
    }

    [Fact]
    public void AddFixedServiceInvoiceItems_PerMonthAndPerPerson_ShouldAddCorrectItems()
    {
        // Arrange
        var context = _fixture.Context;
        var builder = new BillingInvoiceBuilder(context);
        var invoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice();

        var serviceType1 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Internet", IsActive = true };
        var serviceType2 = new BillingServiceType { Id = Guid.NewGuid(), Name = "Trash", IsActive = true };
        var serviceTypeById = new Dictionary<Guid, BillingServiceType>
        {
            [serviceType1.Id] = serviceType1,
            [serviceType2.Id] = serviceType2
        };

        var prices = new List<RoomingHouseServicePrice>
        {
            new() { ServiceTypeId = serviceType1.Id, PricingUnit = PricingUnit.PerMonth, UnitPrice = 100000m, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new() { ServiceTypeId = serviceType2.Id, PricingUnit = PricingUnit.PerPersonPerMonth, UnitPrice = 50000m, EffectiveFrom = new DateOnly(2026, 1, 1) }
        };

        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, start, end, 30, 30, true);
        var occupantCount = 2;

        // Act
        builder.AddFixedServiceInvoiceItems(invoice, prices, serviceTypeById, period, occupantCount, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(200000m, invoice.ServiceAmount); // 100k + (50k * 2)
    }

    [Fact]
    public void AddMeteredServiceInvoiceItems_ShouldCalculateConsumptionAndAmount()
    {
        // Arrange
        var context = _fixture.Context;
        var builder = new BillingInvoiceBuilder(context);
        var invoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice();

        var serviceType = new BillingServiceType { Id = Guid.NewGuid(), Name = "Electricity", IsActive = true };
        var price = new RoomingHouseServicePrice { ServiceTypeId = serviceType.Id, PricingUnit = PricingUnit.MeterReading, UnitPrice = 3500m };
        var meteredInputs = new List<ResolvedMeterReadingInput>
        {
            new ResolvedMeterReadingInput(serviceType, price, 100m, 150m, null)
        };

        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, start, end, 30, 30, true);

        // Act
        builder.AddMeteredServiceInvoiceItems(invoice, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), period, meteredInputs, DateTimeOffset.UtcNow);

        // Assert
        Assert.Single(invoice.Items);
        var item = invoice.Items.First();
        Assert.Equal(50m, item.Quantity); // 150 - 100
        Assert.Equal(3500m, item.UnitPrice);
        Assert.Equal(175000m, item.Amount); // 50 * 3500
        Assert.Equal(175000m, invoice.UtilityAmount);
    }

    [Fact]
    public void CalculateAndValidateInvoiceTotal_NegativeTotalAmount_ShouldThrowBadRequestException()
    {
        // Arrange
        var invoice = new SmartRentalPlatform.Domain.Entities.Billing.Invoice
        {
            RentAmount = 1000000m,
            UtilityAmount = 0m,
            ServiceAmount = 0m,
            DiscountAmount = 1500000m // Discount exceeds total rent amount
        };

        // Act & Assert
        Assert.Throws<BadRequestException>(() => BillingInvoiceBuilder.CalculateAndValidateInvoiceTotal(invoice));
    }
}
