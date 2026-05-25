using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Application.Profiles;
using SmartRentalPlatform.Application.Roles;

namespace SmartRentalPlatform.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IRoleService, RoleService>();
        return services;
    }
}
