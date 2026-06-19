using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;

namespace SmartRentalPlatform.Application.Billing;

public interface IBillingService
{
    Task<List<ServicePriceResponse>> GetServicePricesAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default);

    Task<RoomBillingContextResponse> GetRoomBillingContextAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default);

    Task<ServicePriceResponse> CreateServicePriceAsync(Guid landlordUserId, Guid roomingHouseId, CreateServicePriceRequest request, CancellationToken cancellationToken = default);

    Task<MeterReadingResponse> CreateMeterReadingAsync(Guid landlordUserId, CreateMeterReadingRequest request, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GenerateDraftInvoiceAsync(Guid landlordUserId, GenerateInvoiceDraftRequest request, CancellationToken cancellationToken = default);

    Task<List<InvoiceResponse>> GetLandlordInvoicesAsync(Guid landlordUserId, string? status = null, string? search = null, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GetLandlordInvoiceAsync(Guid landlordUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> IssueInvoiceAsync(Guid landlordUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CancelInvoiceAsync(Guid landlordUserId, Guid invoiceId, string? reason, CancellationToken cancellationToken = default);

    Task<List<InvoiceResponse>> GetMyInvoicesAsync(Guid tenantUserId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GetMyInvoiceAsync(Guid tenantUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> PayInvoiceAsync(Guid tenantUserId, Guid invoiceId, CancellationToken cancellationToken = default);
}
