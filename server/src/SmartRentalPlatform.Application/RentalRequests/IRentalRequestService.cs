using SmartRentalPlatform.Contracts.RentalRequests.Requests;
using SmartRentalPlatform.Contracts.RentalRequests.Responses;

namespace SmartRentalPlatform.Application.RentalRequests;

public interface IRentalRequestService
{
    Task<RentalRequestResponse> CreateAsync(
        Guid tenantUserId,
        Guid roomId,
        CreateRentalRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<List<RentalRequestResponse>> GetMyRequestsAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default);

    Task<List<RentalRequestResponse>> GetIncomingRequestsAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default);

    Task<RentalRequestResponse?> GetByIdAsync(
        Guid userId,
        Guid rentalRequestId,
        CancellationToken cancellationToken = default);

    Task<RentalRequestResponse?> ApproveAsync(
        Guid landlordUserId,
        Guid rentalRequestId,
        ApproveRentalRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<RentalRequestResponse?> RejectAsync(
        Guid landlordUserId,
        Guid rentalRequestId,
        RejectRentalRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<RentalRequestResponse?> CancelAsync(
        Guid tenantUserId,
        Guid rentalRequestId,
        CancellationToken cancellationToken = default);
}
