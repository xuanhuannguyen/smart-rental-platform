using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartRentalPlatform.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDockerConnectionString =
        "Host=localhost;Port=5444;Database=smart_rental_platform;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var currentDirectory = Directory.GetCurrentDirectory();
        var basePathCandidates = new[]
        {
            currentDirectory,
            Path.Combine(currentDirectory, "server", "src", "SmartRentalPlatform.Api"),
            Path.GetFullPath(Path.Combine(currentDirectory, "../SmartRentalPlatform.Api"))
        };
        var basePath = basePathCandidates.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, "appsettings.json"))) ?? currentDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            LocalDockerConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
