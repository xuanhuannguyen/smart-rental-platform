using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Wallets;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Application.Billing;

public class InvoiceWalletPaymentService : IInvoiceWalletPaymentService
{
    private const string RelatedEntityType = "Invoice";

    private readonly IAppDbContext context;
    private readonly IWalletService walletService;
    private readonly IPaymentRowLockService rowLockService;

    public InvoiceWalletPaymentService(
        IAppDbContext context,
        IWalletService walletService,
        IPaymentRowLockService rowLockService)
    {
        this.context = context;
        this.walletService = walletService;
        this.rowLockService = rowLockService;
    }

    public async Task<InvoiceWalletPaymentResult> PayInvoiceAsync(
        Guid invoiceId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        var invoiceSnapshot = await context.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        EnsureTenantCanPay(invoiceSnapshot.TenantUserId, tenantUserId);

        if (invoiceSnapshot.Status == InvoiceStatus.Paid)
        {
            return new InvoiceWalletPaymentResult(true, invoiceSnapshot.WalletTransferGroupId, null);
        }

        EnsurePayableStatus(invoiceSnapshot.Status);

        var tenantWallet = await walletService.GetOrCreateWalletAsync(tenantUserId, cancellationToken);
        var landlordWallet = await walletService.GetOrCreateWalletAsync(invoiceSnapshot.LandlordUserId, cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var invoice = await rowLockService.LockInvoiceAsync(invoiceId, cancellationToken)
                ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

            EnsureTenantCanPay(invoice.TenantUserId, tenantUserId);

            if (invoice.Status == InvoiceStatus.Paid)
            {
                await transaction.CommitAsync(cancellationToken);
                return new InvoiceWalletPaymentResult(true, invoice.WalletTransferGroupId, null);
            }

            EnsurePayableStatus(invoice.Status);

            if (invoice.WalletTransferGroupId.HasValue)
            {
                throw new ConflictException(
                    ErrorCodes.WalletPaymentFailed,
                    "Hóa đơn đã có giao dịch thanh toán nhưng trạng thái chưa đồng bộ.",
                    new { invoice.Id, invoice.WalletTransferGroupId });
            }

            var transfer = await walletService.TransferWithinTransactionAsync(
                tenantWallet.Id,
                landlordWallet.Id,
                invoice.TotalAmount,
                WalletTransactionType.InvoicePayment,
                WalletTransactionType.InvoiceReceive,
                new WalletTransactionMetadata
                {
                    RelatedEntityType = RelatedEntityType,
                    RelatedEntityId = invoice.Id,
                    Description = $"Thanh toán hóa đơn {invoice.InvoiceNo}."
                },
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = now;
            invoice.UpdatedAt = now;
            invoice.WalletTransferGroupId = transfer.TransferGroupId;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new InvoiceWalletPaymentResult(true, transfer.TransferGroupId, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void EnsureTenantCanPay(Guid invoiceTenantUserId, Guid tenantUserId)
    {
        if (invoiceTenantUserId != tenantUserId)
        {
            throw new ForbiddenException(
                ErrorCodes.Forbidden,
                "Bạn không có quyền thanh toán hóa đơn này.");
        }
    }

    private static void EnsurePayableStatus(InvoiceStatus status)
    {
        if (status != InvoiceStatus.Issued && status != InvoiceStatus.Overdue)
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Chỉ có thể thanh toán hóa đơn đã phát hành hoặc quá hạn.",
                new { currentStatus = status.ToString() });
        }
    }
}
