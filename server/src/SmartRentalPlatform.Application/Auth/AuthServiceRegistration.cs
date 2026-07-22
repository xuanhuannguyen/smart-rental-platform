using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.Auth;

internal static class AuthServiceRegistration
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IAuthPasswordService, AuthPasswordService>();
        services.AddScoped<IGoogleLoginService, GoogleLoginService>();

        return services;
    }
}
