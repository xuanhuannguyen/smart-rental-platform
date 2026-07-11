using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Application.Billing;

public interface IBillingService
{
    Task<List<BillingServiceTypeResponse>> GetBillingServiceTypesAsync(CancellationToken cancellationToken = default);

    // Admin CRUD cho BillingServiceType
    Task<PagedResult<AdminBillingServiceTypeResponse>> GetBillingServiceTypesAdminAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AdminBillingServiceTypeResponse> GetBillingServiceTypeAdminAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminBillingServiceTypeResponse> CreateBillingServiceTypeAsync(CreateBillingServiceTypeRequest request, CancellationToken cancellationToken = default);
    Task<AdminBillingServiceTypeResponse> UpdateBillingServiceTypeAsync(Guid id, UpdateBillingServiceTypeRequest request, CancellationToken cancellationToken = default);
    Task ToggleBillingServiceTypeActiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoomBillingContextResponse> GetRoomBillingContextAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default);

    Task<RoomInvoicePreviewResponse> GetRoomInvoicePreviewAsync(
        Guid landlordUserId,
        Guid roomId,
        DateOnly billingPeriodStart,
        DateOnly? billingPeriodEnd = null,
        CancellationToken cancellationToken = default);

    Task<RoomInvoicePreviewResponse> GetTerminationInvoicePreviewAsync(
        Guid landlordUserId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<List<InvoiceResponse>> GetLandlordInvoicesAsync(Guid landlordUserId, string? status = null, string? search = null, Guid? contractId = null, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GetLandlordInvoiceAsync(Guid landlordUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> IssueInvoiceAsync(Guid landlordUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CancelInvoiceAsync(Guid landlordUserId, Guid invoiceId, string? reason, CancellationToken cancellationToken = default);

    Task<List<InvoiceResponse>> GetMyInvoicesAsync(Guid tenantUserId, CancellationToken cancellationToken = default);

    Task<List<InvoiceResponse>> GetMyContractInvoicesAsync(Guid tenantUserId, Guid contractId, string? status = null, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GetMyInvoiceAsync(Guid tenantUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    Task<InvoiceResponse> PayInvoiceAsync(Guid tenantUserId, Guid invoiceId, CancellationToken cancellationToken = default);

    // --- Endpoint mới: tạo hóa đơn kết hợp nhập chỉ số đồng hồ trong 1 bước ---
    Task<InvoiceResponse> GenerateInvoiceWithReadingsAsync(
        Guid landlordUserId,
        GenerateInvoiceWithReadingsRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CreateFinalInvoiceForTerminationAsync(
        Guid landlordUserId,
        Guid contractId,
        DateOnly terminationDate,
        decimal discountAmount,
        string? note,
        IReadOnlyCollection<MeterReadingInput> meterReadings,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CreateNextTerminationInvoiceAsync(
        Guid landlordUserId,
        Guid contractId,
        CreateTerminationInvoiceRequest request,
        CancellationToken cancellationToken = default);
}
