using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.Rooms;

internal static class RoomServiceRegistration
{
    public static IServiceCollection AddRoomServices(this IServiceCollection services)
    {
        services.AddScoped<RoomAccessService>();
        services.AddScoped<IRoomQueryService, RoomQueryService>();
        services.AddScoped<IRoomCommandService, RoomCommandService>();
        services.AddScoped<IRoomMediaService, RoomMediaService>();
        services.AddScoped<IRoomPriceTierService, RoomPriceTierService>();
        services.AddScoped<IRoomStatusService, RoomStatusService>();

        return services;
    }
}
