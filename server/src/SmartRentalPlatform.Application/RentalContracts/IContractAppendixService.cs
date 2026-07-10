using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractAppendixService
{
    Task<ContractAppendixResponse?> CreateAsync(
        Guid userId,
        Guid contractId,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContractAppendixResponse>> GetByContractAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<ContractAppendixResponse?> GetByIdAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default);

    Task<ContractAppendixResponse?> UpdateAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CreateContractAppendixRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractPreviewPdfResult?> GetPreviewPdfAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default);



    Task<ContractAppendixResponse?> RejectAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        RejectContractRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractAppendixResponse?> RequestRevisionAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        RequestContractRevisionRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        CancellationToken cancellationToken = default);

    Task<int> ApplyDueAppendicesAsync(CancellationToken cancellationToken = default);
}
