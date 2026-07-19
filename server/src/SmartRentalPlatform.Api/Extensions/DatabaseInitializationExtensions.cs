using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using SmartRentalPlatform.Infrastructure.Persistence.Seeders;

namespace SmartRentalPlatform.Api.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDevelopmentDatabaseAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public static async Task SeedConfiguredDemoDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        if (app.Configuration.GetValue("SeedData:Development:Enabled", false))
        {
            await SeedDevelopmentDataAsync(app);
        }

        if (app.Configuration.GetValue("SeedData:WalletQa:Enabled", false))
        {
            await SeedWalletQaDataAsync(app);
        }
    }

    private static async Task SeedDevelopmentDataAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var mediaStorageService = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();
        var mediaObjectKeyFactory = scope.ServiceProvider.GetRequiredService<IMediaObjectKeyFactory>();

        await DevelopmentDataSeed.SeedAdminAsync(dbContext, passwordService);
        await DevelopmentDataSeed.SeedAsync(
            dbContext,
            passwordService,
            mediaStorageService,
            mediaObjectKeyFactory);
    }

    private static async Task SeedWalletQaDataAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        await WalletQaDataSeeder.SeedAsync(dbContext, passwordService);
    }
}
