using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public interface IBillingContractReadService
{
    Task<BillingContractSnapshot?> GetActiveContractAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<BillingContractSnapshot?> GetContractAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public sealed record BillingContractSnapshot(
    Guid Id,
    Guid RoomId,
    Guid RoomingHouseId,
    Guid TenantUserId,
    Guid LandlordUserId,
    decimal MonthlyRent,
    int PaymentDay,
    DateOnly StartDate,
    DateOnly EndDate,
    RentalContractStatus Status,
    DateOnly? TerminationDate,
    ContractTerminationType? TerminationType);
