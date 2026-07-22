using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Middlewares;

/// <summary>
/// Middleware bắt exception từ Application layer và trả về ApiErrorResponse chuẩn.
/// AppException (và các lớp con: UnauthorizedException, BadRequestException,
/// ConflictException, ForbiddenException, TooManyRequestsException) sẽ được
/// map sang đúng HTTP status code thay vì trả 500.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            // Lỗi nghiệp vụ đã biết trước — log warning, trả đúng status code.
            _logger.LogWarning(
                "Business exception: {ErrorCode} - {Message} (HTTP {StatusCode})",
                ex.ErrorCode, ex.Message, ex.StatusCode);

            await WriteErrorResponse(context, ex.StatusCode, new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                Message = ex.Message,
                Details = ex.Details
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access exception: {Message}", ex.Message);

            await WriteErrorResponse(context, StatusCodes.Status401Unauthorized, new ApiErrorResponse
            {
                Success = false,
                ErrorCode = "UNAUTHORIZED",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            // Lỗi không mong đợi — log error đầy đủ, trả 500 chung.
            _logger.LogError(ex, "Unhandled exception occurred.");

            await WriteErrorResponse(context, StatusCodes.Status500InternalServerError, new ApiErrorResponse
            {
                Success = false,
                ErrorCode = "INTERNAL_SERVER_ERROR",
                Message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                Details = _environment.IsDevelopment() || _environment.IsEnvironment("Test")
                    ? new
                    {
                        exception = ex.GetType().FullName,
                        ex.Message,
                        innerException = ex.InnerException?.GetType().FullName,
                        innerMessage = ex.InnerException?.Message
                    }
                    : null
            });
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, ApiErrorResponse response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsJsonAsync(response, options);
    }
}
