namespace SmartRentalPlatform.Api.Extensions;

/// <summary>
/// Extension method cấu hình CORS cho React client.
/// </summary>
public static class CorsExtensions
{
    public const string ClientAppPolicyName = "ClientApp";

    public static IServiceCollection AddClientCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            allowedOrigins =
            [
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://[::1]:5173",
                "http://127.0.0.1:5500"
            ];
        }

        services.AddCors(options =>
        {
            options.AddPolicy(ClientAppPolicyName, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
