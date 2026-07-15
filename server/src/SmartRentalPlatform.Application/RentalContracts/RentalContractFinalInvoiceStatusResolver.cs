using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class RentalContractFinalInvoiceStatusResolver
{
    private readonly IAppDbContext context;

    public RentalContractFinalInvoiceStatusResolver(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<bool> IsAwaitingFinalInvoiceAsync(
        RentalContract contract,
        CancellationToken cancellationToken)
    {
        if (!TryResolveRequiredFinalInvoicePeriod(contract, out DateOnly periodStart, out DateOnly periodEnd))
        {
            return false;
        }

        return !await context.Invoices.AsNoTracking().AnyAsync(
            x => x.ContractId == contract.Id &&
                x.BillingPeriodStart == periodStart &&
                x.BillingPeriodEnd == periodEnd &&
                x.Status != InvoiceStatus.Cancelled,
            cancellationToken);
    }

    public async Task<HashSet<Guid>> GetAwaitingFinalInvoiceContractIdsAsync(
        IReadOnlyCollection<RentalContract> contracts,
        CancellationToken cancellationToken)
    {
        var candidates = contracts
            .Select(contract => TryResolveRequiredFinalInvoicePeriod(contract, out DateOnly start, out DateOnly end)
                ? new FinalInvoicePeriodCandidate(contract.Id, start, end)
                : null)
            .Where(x => x is not null)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        Guid[] contractIds = candidates.Select(x => x!.ContractId).ToArray();
        var invoices = await context.Invoices.AsNoTracking()
            .Where(x => contractIds.Contains(x.ContractId) && x.Status != InvoiceStatus.Cancelled)
            .Select(x => new { x.ContractId, x.BillingPeriodStart, x.BillingPeriodEnd })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(candidate => !invoices.Any(invoice =>
                invoice.ContractId == candidate!.ContractId &&
                invoice.BillingPeriodStart == candidate.PeriodStart &&
                invoice.BillingPeriodEnd == candidate.PeriodEnd))
            .Select(candidate => candidate!.ContractId)
            .ToHashSet();
    }

    private static bool TryResolveRequiredFinalInvoicePeriod(
        RentalContract contract,
        out DateOnly periodStart,
        out DateOnly periodEnd)
    {
        periodStart = default;
        periodEnd = default;
        if (contract.Status != RentalContractStatus.Cancelled ||
            contract.TerminationType != ContractTerminationType.TenantUnilateral ||
            !contract.TerminationDate.HasValue ||
            contract.TerminationDate.Value < contract.StartDate)
        {
            return false;
        }

        periodEnd = contract.TerminationDate.Value;
        DateOnly monthStart = new(periodEnd.Year, periodEnd.Month, 1);
        periodStart = contract.StartDate > monthStart ? contract.StartDate : monthStart;
        return true;
    }

    private sealed record FinalInvoicePeriodCandidate(Guid ContractId, DateOnly PeriodStart, DateOnly PeriodEnd);
}
