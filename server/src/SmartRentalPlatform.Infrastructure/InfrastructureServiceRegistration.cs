using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Abstractions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Services.Kyc;
using SmartRentalPlatform.Infrastructure.Ekyc;
using SmartRentalPlatform.Infrastructure.Options;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Security;
using SmartRentalPlatform.Infrastructure.Services;
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

        // ===== Develop services =====
        services.AddScoped<IAppDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddHttpContextAccessor();
        services.AddScoped<SmartRentalPlatform.Application.Common.Interfaces.ICurrentUserService, CurrentUserService>();

        // ===== KYC services =====
        services.Configure<VnptEkycOptions>(
            configuration.GetSection(VnptEkycOptions.SectionName));

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
