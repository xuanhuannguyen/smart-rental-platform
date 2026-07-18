using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Domain.Entities.Billing;

namespace SmartRentalPlatform.Application.Billing;

internal static class BillingResponseMapper
{
    public static BillingServiceTypeResponse ToBillingServiceTypeResponse(BillingServiceType serviceType)
    {
        return new BillingServiceTypeResponse(
            serviceType.Id,
            serviceType.Name,
            serviceType.SupportsMeterReading,
            serviceType.MeterUnitName,
            serviceType.IsActive);
    }

    public static InvoiceResponse ToInvoiceResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.ContractId,
            invoice.RoomId,
            invoice.Room.RoomNumber,
            invoice.Room.RoomingHouseId,
            invoice.Room.RoomingHouse.Name,
            invoice.TenantUserId,
            invoice.Tenant.DisplayName,
            invoice.Tenant.Email,
            invoice.LandlordUserId,
            invoice.InvoiceNo,
            invoice.BillingPeriodStart,
            invoice.BillingPeriodEnd,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.RentAmount,
            invoice.UtilityAmount,
            invoice.ServiceAmount,
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.Status.ToString(),
            invoice.Note,
            invoice.SentAt,
            invoice.PaidAt,
            invoice.Items.OrderBy(x => x.CreatedAt).Select(ToInvoiceItemResponse).ToList(),
            invoice.WalletTransferGroupId);
    }

    private static InvoiceItemResponse ToInvoiceItemResponse(InvoiceItem item)
    {
        return new InvoiceItemResponse(
            item.Id,
            item.ServiceTypeId,
            item.ServiceType?.Name,
            item.MeterReadingId,
            item.MeterReading?.ProofMediaAssetId,
            BuildPrivateProofImageUrl(item.MeterReading?.ProofMediaAssetId),
            item.ItemType.ToString(),
            item.Description,
            item.Quantity,
            item.UnitPrice,
            item.Amount);
    }

    private static string? BuildPrivateProofImageUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : null;
    }
}
