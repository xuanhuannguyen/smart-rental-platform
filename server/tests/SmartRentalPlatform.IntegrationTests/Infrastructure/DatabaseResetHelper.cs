using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Infrastructure.Persistence;
using System.Threading.Tasks;

namespace SmartRentalPlatform.IntegrationTests.Infrastructure;

public static class DatabaseResetHelper
{
    public static async Task ResetDatabaseAsync(AppDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
