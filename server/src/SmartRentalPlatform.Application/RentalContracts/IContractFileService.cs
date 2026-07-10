using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractFileService
{
    Task<ContractFileResponse?> EnsureMaskedContractFileAsync(
        Guid userId,
        Guid contractId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the unsigned PDF for the ESign workflow and returns both the stored
    /// <see cref="ContractFile"/> and the signature zone coordinates captured by the
    /// renderer. Zones are keyed by signer role ("Landlord", "Tenant").
    /// </summary>
    Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> SignatureZones)?> CreateUnsignedContractPdfForESignAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the unsigned appendix PDF for the ESign workflow and returns both the
    /// stored <see cref="ContractFile"/> and the signature zone coordinates.
    /// </summary>
    Task<(ContractFile File, IReadOnlyDictionary<string, SignatureZone> SignatureZones)?> CreateUnsignedAppendixPdfForESignAsync(
        Guid envelopeId,
        CancellationToken cancellationToken = default);

    Task EnsureMaskedReferenceFileAsync(
        Guid contractId,
        Guid? appendixId,
        CancellationToken cancellationToken = default);

    Task<ContractFile?> StoreProviderSignedPdfAsync(
        Guid envelopeId,
        Stream pdfStream,
        CancellationToken cancellationToken = default);

    Task<ContractFile?> StoreProviderEvidenceAsync(
        Guid envelopeId,
        Stream evidenceStream,
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
