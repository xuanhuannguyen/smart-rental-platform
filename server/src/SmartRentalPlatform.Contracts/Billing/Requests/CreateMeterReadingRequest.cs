namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record CreateMeterReadingRequest(
    Guid RoomId,
    Guid ContractId,
    string ServiceCode,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    decimal PreviousReading,
    decimal CurrentReading,
    string? ProofImageObjectKey);
