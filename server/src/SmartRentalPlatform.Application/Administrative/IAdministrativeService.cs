using SmartRentalPlatform.Contracts.Administrative;
using SmartRentalPlatform.Contracts.Administrative.Requests;
using SmartRentalPlatform.Contracts.Administrative.Responses;
using SmartRentalPlatform.Contracts.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Application.Administrative;

public interface IAdministrativeService
{
    Task<List<ProvinceResponse>> GetActiveProvincesAsync(CancellationToken cancellationToken = default);
    Task<List<WardResponse>> GetWardsByProvinceAsync(string provinceCode, CancellationToken cancellationToken = default);

    // Admin: Provinces
    Task<PagedResult<AdminProvinceResponse>> GetProvincesAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AdminProvinceResponse> GetProvinceAsync(string code, CancellationToken cancellationToken = default);
    Task<AdminProvinceResponse> CreateProvinceAsync(CreateProvinceRequest request, CancellationToken cancellationToken = default);
    Task<AdminProvinceResponse> UpdateProvinceAsync(string code, UpdateProvinceRequest request, CancellationToken cancellationToken = default);
    Task ToggleProvinceActiveAsync(string code, CancellationToken cancellationToken = default);

    // Admin: Wards
    Task<PagedResult<AdminWardResponse>> GetWardsAsync(string provinceCode, int page, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AdminWardResponse> GetWardAsync(string code, CancellationToken cancellationToken = default);
    Task<AdminWardResponse> CreateWardAsync(CreateWardRequest request, CancellationToken cancellationToken = default);
    Task<AdminWardResponse> UpdateWardAsync(string code, UpdateWardRequest request, CancellationToken cancellationToken = default);
    Task ToggleWardActiveAsync(string code, CancellationToken cancellationToken = default);
}
