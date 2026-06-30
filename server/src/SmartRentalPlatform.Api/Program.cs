using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Api.Middlewares;
using SmartRentalPlatform.Application;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using SmartRentalPlatform.Infrastructure.Persistence.Seeders;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// Đăng ký controller để dùng mô hình API Controller.
builder.Services.AddControllers();

// Đăng ký Swagger để test API trên trình duyệt.
builder.Services.AddSwaggerDocumentation();

// Đăng ký JWT authentication và authorization.
builder.Services.AddJwtAuthentication(builder.Configuration);

// Cho phép frontend React gọi backend.
builder.Services.AddClientCors();

// Rate limiting: 10 requests / phút cho AI chatbot endpoint.
builder.Services.AddRateLimiter(options =>
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
            new SmartRentalPlatform.Contracts.Common.ApiErrorResponse
            {
                Success = false,
                ErrorCode = "TOO_MANY_REQUESTS",
                Message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 1 phút.",
                Details = new { retryAfter = "1 minute" }
            }, cancellationToken);
    };
});

// Đăng ký các layer tự viết.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (ShouldSeedDevelopmentData(app))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    await DevelopmentDataSeed.SeedAdminAsync(dbContext, passwordService);
    await DevelopmentDataSeed.SeedAsync(dbContext, passwordService);
}

if (ShouldSeedWalletQaData(app))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    await WalletQaDataSeeder.SeedAsync(dbContext, passwordService);
}

// Chỉ bật Swagger ở môi trường Development.
app.UseSwaggerDocumentation();

// app.UseHttpsRedirection();

// Middleware bắt exception phải đặt sớm nhất để bắt mọi lỗi.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS phải đặt trước Authorization.
app.UseCors(CorsExtensions.ClientAppPolicyName);

// Rate limiting middleware — phải sau CORS, trước controllers.
app.UseRateLimiter();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    }
});

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/email/test-otp", async (
        TestEmailOtpRequest request,
        IEmailSender emailSender,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new
            {
                success = false,
                message = "Email is required."
            });
        }

        var otp = Random.Shared.Next(0, 1_000_000).ToString("D6");
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Tester"
            : request.DisplayName.Trim();

        await emailSender.SendEmailVerificationOtpAsync(
            request.Email.Trim(),
            displayName,
            otp,
            cancellationToken);

        return Results.Ok(new
        {
            success = true,
            email = request.Email.Trim(),
            otp,
            message = "Test OTP email sent in Development environment."
        });
    })
    .AllowAnonymous()
    .WithTags("Dev");
}

app.MapControllers();

app.Run();

static bool ShouldSeedDevelopmentData(WebApplication app)
{
    return app.Environment.IsDevelopment() &&
        app.Configuration.GetValue("SeedData:Development:Enabled", false);
}

static bool ShouldSeedWalletQaData(WebApplication app)
{
    return app.Environment.IsDevelopment() &&
        app.Configuration.GetValue("SeedData:WalletQa:Enabled", false);
}

public sealed record TestEmailOtpRequest(string Email, string? DisplayName);

public partial class Program;
