using SmartRentalPlatform.Contracts.LandlordDashboard.Responses;

namespace SmartRentalPlatform.Application.LandlordDashboard;

public interface ILandlordDashboardService
{
    Task<LandlordDashboardResponse> GetDashboardAsync(
        Guid landlordUserId,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default);
}
