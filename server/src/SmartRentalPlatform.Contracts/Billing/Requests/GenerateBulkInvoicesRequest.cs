namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record GenerateBulkInvoicesRequest(
    Guid RoomingHouseId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    List<BulkInvoiceRoomInput> Rooms);

public sealed record BulkInvoiceRoomInput(
    Guid ContractId,
    decimal DiscountAmount,
    string? Note,
    List<MeterReadingInput> MeterReadings);
