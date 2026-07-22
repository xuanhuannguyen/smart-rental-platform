using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Application.Common.Models.ESign;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractESignService
{
    Task<StartESignEnvelopeResponse?> StartContractEnvelopeAsync(
        Guid userId,
        Guid contractId,
        string? returnUrl,
        CancellationToken cancellationToken = default);

    Task<RequestESignOtpResponse> RequestSignatureOtpAsync(
        Guid userId,
        Guid contractId,
        Guid? appendixId,
        ESignOtpMethod method,
        CancellationToken cancellationToken = default);

    Task SubmitSignatureOtpAsync(
        Guid userId,
        Guid contractId,
        Guid? appendixId,
        string otpCode,
        string signatureImageBase64,
        CancellationToken cancellationToken = default);

    Task<StartESignEnvelopeResponse?> StartAppendixEnvelopeAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        string? returnUrl,
        CancellationToken cancellationToken = default);

    Task<ESignEnvelopeResponse?> GetEnvelopeAsync(
        Guid userId,
        Guid envelopeId,
        CancellationToken cancellationToken = default);

    Task ProcessProviderWebhookAsync(
        ESignProvider provider,
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken = default);
}
