using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractPdfRenderer
{
    byte[] RenderSignedRentalContract(RentalContract contract, ContractRenderOptions options);

    byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options);
}

