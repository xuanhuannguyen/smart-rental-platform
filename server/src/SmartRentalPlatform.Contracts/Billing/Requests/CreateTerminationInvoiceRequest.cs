namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record CreateTerminationInvoiceRequest(
    decimal DiscountAmount,
    string? Note,
    List<MeterReadingInput> MeterReadings);
