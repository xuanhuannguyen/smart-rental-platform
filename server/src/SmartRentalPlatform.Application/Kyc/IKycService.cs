using SmartRentalPlatform.Contracts.Kyc;

namespace SmartRentalPlatform.Application.Kyc;

public interface IKycService
{
    Task<KycSubmissionResponse> SubmitAsync(
        Guid userId,
        SubmitKycRequest request,
        CancellationToken cancellationToken = default);

    Task<KycStatusResponse> GetMyStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<KycHistoryResponse> GetMyHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
