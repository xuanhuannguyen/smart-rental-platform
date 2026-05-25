using Microsoft.Extensions.DependencyInjection;
<<<<<<< Updated upstream
=======
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Application.Amenities;
>>>>>>> Stashed changes
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Application.Users;

namespace SmartRentalPlatform.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
<<<<<<< Updated upstream
=======
        // Sau này đăng ký các service nghiệp vụ ở đây.
        // Ví dụ:
        // services.AddScoped<IAuthService, AuthService>();
        // services.AddScoped<IKycService, KycService>();
        services.AddScoped<IAdministrativeService, AdministrativeService>();
        services.AddScoped<IAmenityService, AmenityService>();
>>>>>>> Stashed changes
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoomingHouseService, RoomingHouseService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
