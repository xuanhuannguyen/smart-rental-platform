using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Extensions;

public static class RateLimitExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("AiChat", config =>
            {
                config.PermitLimit = 10;
                config.Window = TimeSpan.FromMinutes(1);
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 2;
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = "TOO_MANY_REQUESTS",
                        Message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 1 phút.",
                        Details = new { retryAfter = "1 minute" }
                    }, cancellationToken);
            };
        });

        return services;
    }
}
