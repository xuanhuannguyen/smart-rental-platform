namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record InvoiceItemResponse(
    Guid Id,
    Guid? ServiceTypeId,
    string? ServiceName,
    Guid? MeterReadingId,
    string ItemType,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);
