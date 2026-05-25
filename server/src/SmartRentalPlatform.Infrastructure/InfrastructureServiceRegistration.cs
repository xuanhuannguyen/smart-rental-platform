using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Abstractions;
using SmartRentalPlatform.Application.Services.Kyc;
using SmartRentalPlatform.Infrastructure.Ekyc;
using SmartRentalPlatform.Infrastructure.Options;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Security;
using SmartRentalPlatform.Infrastructure.Services.Kyc;
using SmartRentalPlatform.Infrastructure.Storage;

namespace SmartRentalPlatform.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.Configure<VnptEkycOptions>(configuration.GetSection(VnptEkycOptions.SectionName));

        services.AddMemoryCache();

        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IPrivateStorageService, LocalPrivateStorageService>();
        services.AddScoped<IHashService, Sha256HashService>();

        services.AddHttpClient(RealVnptEkycClient.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VnptEkycOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        var useMock = configuration.GetSection(VnptEkycOptions.SectionName)
            .GetValue<bool>(nameof(VnptEkycOptions.UseMock), true);

        if (useMock)
        {
            services.AddScoped<IVnptEkycClient, MockVnptEkycClient>();
        }
        else
        {
            services.AddScoped<IVnptEkycClient, RealVnptEkycClient>();
        }

        return services;
    }
}
