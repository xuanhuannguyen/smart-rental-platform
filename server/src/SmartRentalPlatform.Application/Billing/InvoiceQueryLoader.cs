using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public sealed class InvoiceQueryLoader
{
    private readonly IAppDbContext context;

    public InvoiceQueryLoader(IAppDbContext context)
    {
        this.context = context;
    }

    internal IQueryable<Invoice> Query()
    {
        return context.Invoices
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.Tenant)
            .Include(x => x.Items)
                .ThenInclude(x => x.ServiceType);
    }

    internal async Task<InvoiceResponse> GetInvoiceResponseAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await Query()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        return BillingResponseMapper.ToInvoiceResponse(invoice);
    }

    internal async Task<bool> CanTenantViewInvoiceAsync(
        Invoice invoice,
        Guid tenantUserId,
        CancellationToken cancellationToken)
    {
        if (invoice.TenantUserId == tenantUserId)
        {
            return true;
        }

        return await context.ContractOccupants.AnyAsync(
            x => x.RentalContractId == invoice.ContractId &&
                 x.UserId == tenantUserId &&
                 x.Status != ContractOccupantStatus.Voided &&
                 x.MoveInDate <= invoice.BillingPeriodEnd &&
                 (x.MoveOutDate == null || x.MoveOutDate >= invoice.BillingPeriodStart),
            cancellationToken);
    }

    internal async Task MarkOverdueInvoicesAsync(
        Guid? landlordUserId,
        Guid? tenantUserId,
        Guid? invoiceId,
        Guid? contractId,
        CancellationToken cancellationToken)
    {
        var today = BillingPeriodResolver.GetBusinessToday();
        var query = context.Invoices
            .Where(x => x.DueDate < today &&
                        x.Status == InvoiceStatus.Issued);

        if (landlordUserId.HasValue)
        {
            query = query.Where(x => x.LandlordUserId == landlordUserId.Value);
        }

        if (tenantUserId.HasValue)
        {
            query = query.Where(x => x.TenantUserId == tenantUserId.Value);
        }

        if (invoiceId.HasValue)
        {
            query = query.Where(x => x.Id == invoiceId.Value);
        }

        if (contractId.HasValue)
        {
            query = query.Where(x => x.ContractId == contractId.Value);
        }

        var overdueInvoices = await query.ToListAsync(cancellationToken);
        if (overdueInvoices.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var invoice in overdueInvoices)
        {
            invoice.Status = InvoiceStatus.Overdue;
            invoice.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
