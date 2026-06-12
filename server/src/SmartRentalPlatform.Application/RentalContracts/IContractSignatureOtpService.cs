using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public interface IContractSignatureOtpService
{
    Task<RequestContractSignatureOtpResponse?> RequestOtpAsync(
        Guid userId,
        Guid contractId,
        ContractSignerRole signerRole,
        CancellationToken cancellationToken = default);

    Task VerifyAndConsumeOtpAsync(
        Guid userId,
        Guid contractId,
        ContractSignerRole signerRole,
        string? otp,
        CancellationToken cancellationToken = default);

    Task<RequestContractSignatureOtpResponse?> RequestAppendixOtpAsync(
        Guid userId,
        Guid contractId,
        Guid appendixId,
        ContractSignerRole signerRole,
        CancellationToken cancellationToken = default);

    Task VerifyAndConsumeAppendixOtpAsync(
        Guid userId,
        Guid appendixId,
        ContractSignerRole signerRole,
        string? otp,
        CancellationToken cancellationToken = default);
}
