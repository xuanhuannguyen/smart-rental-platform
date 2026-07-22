namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record RoomBillingContextResponse(
    Guid RoomId,
    string RoomNumber,
    Guid RoomingHouseId,
    Guid ContractId,
    string ContractNumber,
    Guid TenantUserId,
    string TenantName,
    string TenantEmail,
    decimal MonthlyRent,
    int PaymentDay,
    DateOnly ContractStartDate,
    DateOnly ContractEndDate,
    string ContractStatus,
    IReadOnlyDictionary<Guid, LatestMeterReadingResponse> LatestReadingByServiceType);

public sealed record LatestMeterReadingResponse(
    Guid ServiceTypeId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Consumption,
    Guid? ProofMediaAssetId,
    string? ProofImageUrl);

public sealed record RoomInvoicePreviewResponse(
    Guid RoomId,
    string RoomNumber,
    Guid RoomingHouseId,
    Guid ContractId,
    string ContractNumber,
    Guid TenantUserId,
    string TenantName,
    string TenantEmail,
    decimal MonthlyRent,
    int PaymentDay,
    DateOnly ContractStartDate,
    DateOnly ContractEndDate,
    string ContractStatus,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    int BillableDays,
    int DaysInMonth,
    bool IsFullMonth,
    InvoiceLinePreviewResponse RentPreview,
    IReadOnlyCollection<FixedServicePreviewResponse> FixedServices,
    IReadOnlyCollection<MeteredServicePreviewResponse> MeteredServices,
    decimal RentAmount,
    decimal FixedServiceAmount,
    decimal UtilityAmount,
    decimal TotalAmount,
    bool CanGenerate,
    string? BlockReason);

public sealed record InvoiceLinePreviewResponse(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record FixedServicePreviewResponse(
    Guid ServiceTypeId,
    string ServiceName,
    string PricingUnit,
    string DisplayUnitName,
    decimal UnitPrice,
    decimal Quantity,
    int OccupantCount,
    decimal Amount);

public sealed record MeteredServicePreviewResponse(
    Guid ServiceTypeId,
    string ServiceName,
    string MeterUnitName,
    decimal UnitPrice,
    LatestMeterReadingResponse? LatestReading,
    bool RequiresPreviousReading);
