using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.Billing;

public sealed class BillingPeriodResolver
{
    private readonly IAppDbContext context;

    public BillingPeriodResolver(IAppDbContext context)
    {
        this.context = context;
    }

    internal ResolvedBillingPeriod ResolveWithinContract(
        DateOnly contractStart,
        DateOnly contractEnd,
        DateOnly requestedMonth,
        DateOnly? billingPeriodEndOverride = null)
    {
        var monthStart = new DateOnly(requestedMonth.Year, requestedMonth.Month, 1);
        var monthEnd = new DateOnly(
            requestedMonth.Year,
            requestedMonth.Month,
            DateTime.DaysInMonth(requestedMonth.Year, requestedMonth.Month));
        var start = contractStart > monthStart ? contractStart : monthStart;
        var end = contractEnd < monthEnd ? contractEnd : monthEnd;
        if (billingPeriodEndOverride.HasValue && billingPeriodEndOverride.Value < end)
        {
            end = billingPeriodEndOverride.Value;
        }

        if (start > end)
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Tháng hóa đơn không nằm trong thời hạn hợp đồng.");
        }

        var billableDays = end.DayNumber - start.DayNumber + 1;
        var daysInMonth = monthEnd.DayNumber - monthStart.DayNumber + 1;
        var isFullMonth = start == monthStart && end == monthEnd;

        return new ResolvedBillingPeriod(
            start,
            end,
            monthStart,
            monthEnd,
            billableDays,
            daysInMonth,
            isFullMonth);
    }

    internal async Task<ResolvedInvoicePeriodContext> ResolveInvoicePeriodContextAsync(
        Guid contractId,
        DateOnly contractStart,
        DateOnly contractEnd,
        DateOnly requestedMonth,
        DateOnly? billingPeriodEndOverride,
        CancellationToken cancellationToken)
    {
        var billingPeriod = ResolveWithinContract(
            contractStart,
            contractEnd,
            requestedMonth,
            billingPeriodEndOverride);

        var blockReason = await GetInvoiceGenerationBlockReasonAsync(
            contractId,
            contractStart,
            contractEnd,
            billingPeriod,
            billingPeriodEndOverride,
            cancellationToken);

        if (IsFutureBillingPeriod(billingPeriod))
        {
            blockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        return new ResolvedInvoicePeriodContext(billingPeriod, blockReason);
    }

    internal async Task<bool> InvoicePeriodExistsAsync(
        Guid contractId,
        ResolvedBillingPeriod billingPeriod,
        CancellationToken cancellationToken)
    {
        return await context.Invoices.AnyAsync(
            x => x.ContractId == contractId &&
                 x.BillingPeriodStart == billingPeriod.Start &&
                 x.BillingPeriodEnd == billingPeriod.End &&
                 x.Status != InvoiceStatus.Cancelled,
            cancellationToken);
    }

    internal async Task<string?> GetInvoiceGenerationBlockReasonAsync(
        Guid contractId,
        DateOnly contractStart,
        DateOnly contractEnd,
        ResolvedBillingPeriod billingPeriod,
        DateOnly? billingPeriodEndOverride,
        CancellationToken cancellationToken)
    {
        var latestInvoice = await context.Invoices
            .AsNoTracking()
            .Where(x => x.ContractId == contractId &&
                        x.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ThenByDescending(x => x.BillingPeriodStart)
            .Select(x => new
            {
                x.BillingPeriodStart,
                x.BillingPeriodEnd
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestInvoice is null)
        {
            var expectedFirstPeriod = ResolveWithinContract(
                contractStart,
                contractEnd,
                contractStart,
                billingPeriodEndOverride);

            return IsSameBillingPeriod(expectedFirstPeriod, billingPeriod)
                ? null
                : BuildExpectedInvoicePeriodMessage(expectedFirstPeriod, billingPeriod);
        }

        if (latestInvoice.BillingPeriodEnd >= billingPeriod.Start)
        {
            return $"Đã có hóa đơn kỳ {FormatPeriod(latestInvoice.BillingPeriodStart, latestInvoice.BillingPeriodEnd)}. Vui lòng hủy hóa đơn kỳ này hoặc các kỳ sau trước khi tạo lại kỳ {FormatPeriod(billingPeriod.Start, billingPeriod.End)}.";
        }

        var expectedStart = latestInvoice.BillingPeriodEnd.AddDays(1);
        if (expectedStart > contractEnd)
        {
            return "Hợp đồng đã có hóa đơn đến hết thời hạn.";
        }

        var expectedPeriod = ResolveWithinContract(
            contractStart,
            contractEnd,
            expectedStart,
            billingPeriodEndOverride);

        return IsSameBillingPeriod(expectedPeriod, billingPeriod)
            ? null
            : BuildExpectedInvoicePeriodMessage(expectedPeriod, billingPeriod);
    }

    internal static bool IsFutureBillingPeriod(ResolvedBillingPeriod period)
    {
        var today = GetBusinessToday();
        return period.Start > today || period.End > today;
    }

    internal static DateOnly GetBusinessToday()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    }

    internal static decimal CalculatePeriodAmount(decimal monthlyAmount, ResolvedBillingPeriod period)
    {
        if (period.IsFullMonth)
        {
            return monthlyAmount;
        }

        return RoundMoney(monthlyAmount * period.BillableDays / period.DaysInMonth);
    }

    internal static decimal GetPeriodQuantity(ResolvedBillingPeriod period)
    {
        if (period.IsFullMonth)
        {
            return 1;
        }

        return Math.Round((decimal)period.BillableDays / period.DaysInMonth, 2, MidpointRounding.AwayFromZero);
    }

    internal static string BuildPeriodDescription(string description, ResolvedBillingPeriod period)
    {
        return period.IsFullMonth
            ? description
            : $"{description} ({period.BillableDays}/{period.DaysInMonth} ngay)";
    }

    internal static DateOnly BuildDueDate(DateOnly billingPeriodEnd, int paymentDay)
    {
        var normalizedDay = Math.Clamp(paymentDay, 1, 28);
        var nextMonth = billingPeriodEnd.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, normalizedDay);
    }

    internal static decimal RoundMoney(decimal amount)
    {
        return Math.Round(amount, 0, MidpointRounding.AwayFromZero);
    }

    private static bool IsSameBillingPeriod(ResolvedBillingPeriod left, ResolvedBillingPeriod right)
    {
        return left.Start == right.Start && left.End == right.End;
    }

    private static string BuildExpectedInvoicePeriodMessage(
        ResolvedBillingPeriod expectedPeriod,
        ResolvedBillingPeriod requestedPeriod)
    {
        return $"Vui lòng tạo hóa đơn kỳ {FormatPeriod(expectedPeriod.Start, expectedPeriod.End)} trước khi tạo kỳ {FormatPeriod(requestedPeriod.Start, requestedPeriod.End)}.";
    }

    private static string FormatPeriod(DateOnly start, DateOnly end)
    {
        return $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
    }
}

internal sealed record ResolvedBillingPeriod(
    DateOnly Start,
    DateOnly End,
    DateOnly MonthStart,
    DateOnly MonthEnd,
    int BillableDays,
    int DaysInMonth,
    bool IsFullMonth);

internal sealed record ResolvedInvoicePeriodContext(
    ResolvedBillingPeriod BillingPeriod,
    string? BlockReason);
