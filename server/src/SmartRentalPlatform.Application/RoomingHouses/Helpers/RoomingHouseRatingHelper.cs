using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Application.RoomingHouses.Helpers;

public static class RoomingHouseRatingHelper
{
    public static async Task UpdateRatingAsync(IAppDbContext context, Guid roomingHouseId, CancellationToken cancellationToken)
    {
        var stats = await context.RoomingHouseReviews
            .Where(x => x.RoomingHouseId == roomingHouseId && !x.IsHidden)
            .GroupBy(x => x.RoomingHouseId)
            .Select(g => new { Total = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .FirstOrDefaultAsync(cancellationToken);

        var house = await context.RoomingHouses.FindAsync(new object[] { roomingHouseId }, cancellationToken);
        if (house != null)
        {
            house.TotalReviews = stats?.Total ?? 0;
            house.AverageRating = stats != null ? Math.Round(stats.Average, 1) : 0;
            context.RoomingHouses.Update(house);
            // Note: Caller is responsible for calling context.SaveChangesAsync()
        }
    }
}
