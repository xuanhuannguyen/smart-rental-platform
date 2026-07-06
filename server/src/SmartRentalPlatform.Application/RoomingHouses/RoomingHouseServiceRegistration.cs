using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.RoomingHouses.Search;

namespace SmartRentalPlatform.Application.RoomingHouses;

internal static class RoomingHouseServiceRegistration
{
    public static IServiceCollection AddRoomingHouseServices(this IServiceCollection services)
    {
        services.AddScoped<IRoomingHouseQueryService, RoomingHouseQueryService>();
        services.AddScoped<IRoomingHouseAiChatService, RoomingHouseAiChatService>();
        services.AddScoped<IRoomingHouseSearchParser, RoomingHouseSearchParser>();
        services.AddScoped<IRoomingHouseSearchIntentEnricher, GeminiRoomingHouseSearchIntentEnricher>();
        services.AddScoped<IRoomingHouseRecommendationScorer, RuleBasedRoomingHouseRecommendationScorer>();
        services.AddScoped<IRoomingHouseRecommendationReranker, GeminiRoomingHouseRecommendationReranker>();
        services.AddScoped<IRoomingHouseRuleService, RoomingHouseRuleService>();
        services.AddScoped<IRoomingHouseRentalPolicyService, RoomingHouseRentalPolicyService>();
        services.AddScoped<IRoomingHouseServicePriceService, RoomingHouseServicePriceService>();
        services.AddScoped<IRoomingHouseDraftService, RoomingHouseDraftService>();
        services.AddScoped<IRoomingHouseMediaService, RoomingHouseMediaService>();
        services.AddScoped<IRoomingHouseSubmissionService, RoomingHouseSubmissionService>();

        return services;
    }
}
