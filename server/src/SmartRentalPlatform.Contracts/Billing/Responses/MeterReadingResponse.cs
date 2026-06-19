namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record MeterReadingResponse(
    Guid Id,
    Guid RoomId,
    Guid ContractId,
    Guid ServiceTypeId,
    string ServiceCode,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Consumption,
    string? ProofImageObjectKey,
    string Status,
    Guid RecordedByLandlordUserId,
    DateTimeOffset ReadingAt);
