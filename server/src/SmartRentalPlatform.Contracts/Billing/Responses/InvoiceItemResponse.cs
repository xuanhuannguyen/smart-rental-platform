namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record InvoiceItemResponse(
    Guid Id,
    Guid? ServiceTypeId,
    Guid? MeterReadingId,
    string ItemType,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);
