using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.Users;

namespace SmartRentalPlatform.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
