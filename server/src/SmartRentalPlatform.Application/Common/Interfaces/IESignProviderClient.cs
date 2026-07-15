using SmartRentalPlatform.Application.Common.Models.ESign;

namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IESignProviderClient
{
    Task<CreateEnvelopeResult> CreateEnvelopeAsync(
        CreateEnvelopeInput input,
        CancellationToken cancellationToken = default);

    Task<EnvelopeStatusResult> GetEnvelopeStatusAsync(
        string providerEnvelopeId,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadSignedPdfAsync(
        string providerEnvelopeId,
        CancellationToken cancellationToken = default);

    Task<Stream?> DownloadEvidenceAsync(
        string providerEnvelopeId,
        CancellationToken cancellationToken = default);

    Task<SendSignOtpResult> SendSignOtpAsync(
        string providerEnvelopeId,
        string providerDocumentDetailId,
        string signerContact,
        string providerAccessCode,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default);

    Task<SubmitSignOtpResult> SubmitSignOtpAsync(
        long otpId,
        long phienKyId,
        string otpCode,
        ESignSignatureImage signatureImage,
        string providerEvidenceJson,
        string signerContact,
        string providerAccessCode,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default);
}
