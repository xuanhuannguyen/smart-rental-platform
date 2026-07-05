namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record MeterAiResponse(
    decimal Reading,
    string RawText,
    string ProofImageObjectKey,
    string ProofImageUrl);
