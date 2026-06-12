using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.BackgroundServices;
using SmartRentalPlatform.Infrastructure.ExternalServices.Ekyc;
using SmartRentalPlatform.Infrastructure.ExternalServices.Email;
using SmartRentalPlatform.Infrastructure.ExternalServices.Google;
using SmartRentalPlatform.Infrastructure.Identity;
using SmartRentalPlatform.Infrastructure.Options;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Security;
using SmartRentalPlatform.Infrastructure.Storage;

namespace SmartRentalPlatform.Infrastructure;

public static class DependencyInjection
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

        services.AddScoped<IAppDbContext>(provider =>
              provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.Configure<VnptEkycOptions>(configuration.GetSection(VnptEkycOptions.SectionName));
        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddScoped<IPrivateStorageService, LocalPrivateStorageService>();
        services.AddScoped<IHashService, Sha256HashService>();
        services.AddScoped<ISensitiveDataProtector, DataProtectionSensitiveDataProtector>();
        services.AddHostedService<RoomDepositExpirationWorker>();
        services.AddHostedService<RentalContractExpirationWorker>();
        services.AddHttpClient(RealVnptEkycClient.HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<VnptEkycOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        var useMock = configuration
            .GetSection(VnptEkycOptions.SectionName)
            .GetValue(nameof(VnptEkycOptions.UseMock), true);

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
