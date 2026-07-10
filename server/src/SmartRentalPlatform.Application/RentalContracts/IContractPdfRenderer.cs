using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractPdfRenderer
{
    byte[] RenderRentalContractPreview(ContractDocumentModel document, ContractRenderOptions options);

    byte[] RenderContractAppendixPreview(ContractAppendix appendix, ContractRenderOptions options);

    byte[] RenderSignedRentalContract(ContractDocumentModel document, ContractRenderOptions options);

    byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options);

    /// <summary>
    /// Renders the contract PDF for the ESign workflow and captures the exact
    /// bounding boxes of each signature zone from the layout engine.
    /// Returns both the PDF bytes and a dictionary of <see cref="SignatureZone"/>
    /// keyed by signer role ("Landlord", "Tenant").
    /// </summary>
    PdfRenderResult RenderRentalContractForESign(ContractDocumentModel document, ContractRenderOptions options);

    /// <summary>
    /// Renders the contract appendix PDF for the ESign workflow and captures
    /// signature zone positions from the layout engine.
    /// </summary>
    PdfRenderResult RenderContractAppendixForESign(ContractAppendix appendix, ContractRenderOptions options);
}
