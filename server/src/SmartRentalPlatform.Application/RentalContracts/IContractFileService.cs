using SmartRentalPlatform.Contracts.RentalContracts.Responses;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractFileService
{
    Task<ContractFileResponse?> GenerateSignedContractFileAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContractFileResponse>> GetFilesAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, string FileName)?> OpenFileAsync(
        Guid userId,
        Guid contractId,
        Guid fileId,
        CancellationToken cancellationToken = default);
}
