using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ExceptionHandlingMiddleware> logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException exception)
        {
            await WriteErrorResponseAsync(
                context,
                exception.StatusCode,
                exception.ErrorCode,
                exception.Message,
                exception.Details);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Unauthorized,
                exception.Message,
                null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception occurred.");

            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError,
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                null);
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string message,
        object? details)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("Phản hồi đã bắt đầu được gửi.");
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            Details = details
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
