using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Application.Profiles;
using SmartRentalPlatform.Application.Roles;
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.Rooms;

namespace SmartRentalPlatform.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdministrativeService, AdministrativeService>();
        services.AddScoped<IAmenityService, AmenityService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoomingHouseService, RoomingHouseService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAdministrativeService, AdministrativeService>();
        services.AddScoped<IAmenityService, AmenityService>();
        services.AddScoped<IRoomingHouseService, RoomingHouseService>();
        services.AddScoped<IRoomService, RoomService>();
        return services;
    }
}