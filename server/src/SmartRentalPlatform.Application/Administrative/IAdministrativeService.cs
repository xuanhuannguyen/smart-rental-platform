using SmartRentalPlatform.Contracts.Administrative;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Application.Administrative
{
    public interface IAdministrativeService
    {
        Task<List<ProvinceResponse>> GetActiveProvincesAsync(CancellationToken cancellationToken = default);
        Task<List<WardResponse>> GetWardsByProvinceAsync(string provinceCode, CancellationToken cancellationToken = default);

    }
}
