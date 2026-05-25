using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Administrative;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Application.Administrative
{
    public class AdministrativeService : IAdministrativeService
    {
        private readonly IAppDbContext context;

        public AdministrativeService(IAppDbContext context)
        {
            this.context = context;
        }
        public async Task<List<ProvinceResponse>> GetActiveProvincesAsync(CancellationToken cancellationToken = default)
        {
            return await context.AdministrativeProvinces
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ProvinceResponse
                {
                    Code = x.Code,
                    Name = x.Name,
                    Type = x.Type.ToString()
                }).ToListAsync(cancellationToken);
        }

        public async Task<List<WardResponse>> GetWardsByProvinceAsync(string provinceCode, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(provinceCode))
            {
                return new List<WardResponse>();
            }

            return await context.AdministrativeWards
                .AsNoTracking()
                .Where(x => x.IsActive && x.ProvinceCode == provinceCode)
                .OrderBy(x => x.Name)
                .Select(x => new WardResponse
                {
                    Code = x.Code,
                    ProvinceCode = x.ProvinceCode,
                    Name = x.Name,
                    Type = x.Type.ToString()
                }).ToListAsync(cancellationToken);
        }
    }
}
