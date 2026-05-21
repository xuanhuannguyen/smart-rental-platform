using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Sau này đăng ký các service nghiệp vụ ở đây.
        // Ví dụ:
        // services.AddScoped<IAuthService, AuthService>();
        // services.AddScoped<IKycService, KycService>();

        return services;
    }
}