using System;
using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Invoice;

public class BillingPeriodResolverTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public BillingPeriodResolverTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public void ResolveWithinContract_FullMonth_ShouldReturnCorrectPeriod()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractStart = new DateOnly(2026, 1, 1);
        var contractEnd = new DateOnly(2026, 12, 31);
        var requestedMonth = new DateOnly(2026, 6, 1);

        // Act
        var result = resolver.ResolveWithinContract(contractStart, contractEnd, requestedMonth);

        // Assert
        Assert.True(result.IsFullMonth);
        Assert.Equal(new DateOnly(2026, 6, 1), result.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), result.End);
        Assert.Equal(30, result.BillableDays);
        Assert.Equal(30, result.DaysInMonth);
    }

    [Fact]
    public void ResolveWithinContract_PartialFirstMonth_ShouldReturnCorrectPeriod()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractStart = new DateOnly(2026, 6, 15);
        var contractEnd = new DateOnly(2026, 12, 31);
        var requestedMonth = new DateOnly(2026, 6, 1);

        // Act
        var result = resolver.ResolveWithinContract(contractStart, contractEnd, requestedMonth);

        // Assert
        Assert.False(result.IsFullMonth);
        Assert.Equal(new DateOnly(2026, 6, 15), result.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), result.End);
        Assert.Equal(16, result.BillableDays);
        Assert.Equal(30, result.DaysInMonth);
    }

    [Fact]
    public void ResolveWithinContract_PartialFinalMonth_ShouldReturnCorrectPeriod()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractStart = new DateOnly(2026, 1, 1);
        var contractEnd = new DateOnly(2026, 6, 10);
        var requestedMonth = new DateOnly(2026, 6, 1);

        // Act
        var result = resolver.ResolveWithinContract(contractStart, contractEnd, requestedMonth);

        // Assert
        Assert.False(result.IsFullMonth);
        Assert.Equal(new DateOnly(2026, 6, 1), result.Start);
        Assert.Equal(new DateOnly(2026, 6, 10), result.End);
        Assert.Equal(10, result.BillableDays);
        Assert.Equal(30, result.DaysInMonth);
    }

    [Fact]
    public void ResolveWithinContract_OutsideContract_ShouldThrowBadRequestException()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractStart = new DateOnly(2026, 1, 1);
        var contractEnd = new DateOnly(2026, 5, 31);
        var requestedMonth = new DateOnly(2026, 6, 1);

        // Act & Assert
        Assert.Throws<BadRequestException>(() => resolver.ResolveWithinContract(contractStart, contractEnd, requestedMonth));
    }

    [Fact]
    public async Task ResolveInvoicePeriodContextAsync_FuturePeriod_ShouldReturnBlockReason()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractId = Guid.NewGuid();
        var contractStart = new DateOnly(2026, 1, 1);
        var contractEnd = new DateOnly(2100, 12, 31);

        // Target a future date (assuming today is before year 2100)
        var futureMonth = new DateOnly(2100, 1, 1);

        // Act
        var result = await resolver.ResolveInvoicePeriodContextAsync(
            contractId,
            contractStart,
            contractEnd,
            futureMonth,
            billingPeriodEndOverride: null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result.BlockReason);
        Assert.Contains("Kỳ hóa đơn chưa kết thúc", result.BlockReason);
    }

    [Fact]
    public async Task InvoicePeriodExistsAsync_ShouldReturnTrue_WhenInvoiceExists()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractId = Guid.NewGuid();
        var start = new DateOnly(2026, 6, 1);
        var end = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(start, end, start, end, 30, 30, true);

        context.Invoices.Add(new SmartRentalPlatform.Domain.Entities.Billing.Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            RoomId = Guid.NewGuid(),
            TenantUserId = Guid.NewGuid(),
            LandlordUserId = Guid.NewGuid(),
            InvoiceNo = "INV-TEST-001",
            BillingPeriodStart = start,
            BillingPeriodEnd = end,
            RentAmount = 5000000,
            Status = InvoiceStatus.Draft
        });
        await context.SaveChangesAsync();

        // Act
        var exists = await resolver.InvoicePeriodExistsAsync(contractId, period, CancellationToken.None);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task GetInvoiceGenerationBlockReasonAsync_MissingPreviousMonthInvoice_ShouldReturnBlockReason()
    {
        // Arrange
        var context = _fixture.Context;
        var resolver = new BillingPeriodResolver(context);
        var contractId = Guid.NewGuid();
        var contractStart = new DateOnly(2026, 4, 1);
        var contractEnd = new DateOnly(2026, 12, 31);

        // Request for June 2026, but April and May are missing invoices
        var requestedStart = new DateOnly(2026, 6, 1);
        var requestedEnd = new DateOnly(2026, 6, 30);
        var period = new ResolvedBillingPeriod(requestedStart, requestedEnd, requestedStart, requestedEnd, 30, 30, true);

        // Act
        var blockReason = await resolver.GetInvoiceGenerationBlockReasonAsync(
            contractId,
            contractStart,
            contractEnd,
            period,
            billingPeriodEndOverride: null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(blockReason);
        Assert.Contains("trước", blockReason);
    }
}
