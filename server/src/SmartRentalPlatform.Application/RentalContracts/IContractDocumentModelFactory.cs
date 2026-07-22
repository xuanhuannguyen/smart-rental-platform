using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractDocumentModelFactory
{
    Task<ContractDocumentModel> BuildAsync(
        RentalContract contract,
        ContractDocumentBuildMode mode,
        ContractSigningEnvelope? targetEnvelope = null,
        CancellationToken cancellationToken = default);
}
