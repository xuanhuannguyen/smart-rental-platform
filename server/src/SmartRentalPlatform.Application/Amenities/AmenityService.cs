using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Amenities;

public class AmenityService : IAmenityService
{
    private readonly IAppDbContext context;

    public AmenityService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<List<AmenityResponse>> GetActiveAmenitiesAsync(
        AmenityScope? scope,
        CancellationToken cancellationToken = default)
    {
        var query = context.Amenities
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (scope is AmenityScope.House or AmenityScope.Room)
        {
            query = query.Where(x => x.Scope == scope || x.Scope == AmenityScope.Both);
        }
        else if (scope is AmenityScope.Both)
        {
            query = query.Where(x => x.Scope == AmenityScope.Both);
        }

        return await query
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Name)
            .Select(x => new AmenityResponse
            {
                Id = x.Id,
                Name = x.Name,
                Scope = x.Scope.ToString(),
                IconCode = x.IconCode
            })
            .ToListAsync(cancellationToken);
    }
}
