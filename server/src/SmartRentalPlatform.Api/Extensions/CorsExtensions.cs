namespace SmartRentalPlatform.Api.Extensions;

/// <summary>
/// Extension method cấu hình CORS cho React client.
/// </summary>
public static class CorsExtensions
{
    public const string ClientAppPolicyName = "ClientApp";

    public static IServiceCollection AddClientCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(ClientAppPolicyName, policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:5173",
                        "http://127.0.0.1:5500")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
