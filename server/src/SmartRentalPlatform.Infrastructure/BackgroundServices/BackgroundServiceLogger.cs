using Microsoft.Extensions.Logging;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

internal static class BackgroundServiceLogger
{
    public static void SafeLogError<T>(this ILogger<T> logger, Exception exception, string message, params object?[] args)
    {
        try
        {
            logger.LogError(exception, message, args);
        }
        catch
        {
            // Logging providers can be disposed during shutdown; never let logging kill a worker.
        }
    }

    public static void SafeLogInformation<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        try
        {
            logger.LogInformation(message, args);
        }
        catch
        {
            // Ignore logging provider failures.
        }
    }

    public static void SafeLogWarning<T>(this ILogger<T> logger, string message, params object?[] args)
    {
        try
        {
            logger.LogWarning(message, args);
        }
        catch
        {
            // Ignore logging provider failures.
        }
    }
}
