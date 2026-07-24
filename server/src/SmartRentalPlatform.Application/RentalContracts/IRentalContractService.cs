using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IRentalContractService
{
    Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetMyHistoryAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContractHistoryItemResponse>> GetLandlordContractsAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> GetByIdAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> GetActiveContractByRoomIdAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContractOccupantResponse>?> GetActiveTenantsByRoomIdAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<ContractPreviewPdfResult?> GetPreviewPdfAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> SubmitOccupantsAsync(
        Guid tenantUserId,
        Guid contractId,
        SubmitContractOccupantsRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> UpdateTermsAsync(
        Guid landlordUserId,
        Guid contractId,
        UpdateContractTermsRequest request,
        CancellationToken cancellationToken = default);



    Task<ContractDetailResponse?> RequestRevisionAsync(
        Guid userId,
        Guid contractId,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> RejectAsync(
        Guid userId,
        Guid contractId,
        RejectContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractDetailResponse?> TerminateAsync(
        Guid userId,
        Guid contractId,
        TerminateContractRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ExpireOverdueLandlordSignaturesAsync(CancellationToken cancellationToken = default);

    Task<int> ExpireOverdueTenantSignaturesAsync(CancellationToken cancellationToken = default);

    Task<int> ActivatePendingMoveInsAsync(CancellationToken cancellationToken = default);
}
