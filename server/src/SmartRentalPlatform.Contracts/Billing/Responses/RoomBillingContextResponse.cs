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
    string ContractStatus);
