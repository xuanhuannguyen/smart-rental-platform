using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Api.Middlewares;
using SmartRentalPlatform.Application;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký controller để dùng mô hình API Controller.
builder.Services.AddControllers();

// Đăng ký Swagger để test API trên trình duyệt.
builder.Services.AddSwaggerDocumentation();

// Đăng ký JWT authentication và authorization.
builder.Services.AddJwtAuthentication(builder.Configuration);

// Cho phép frontend React gọi backend.
builder.Services.AddClientCors();

// Đăng ký các layer tự viết.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    await DevelopmentDataSeed.SeedAdminAsync(dbContext, passwordService);
}

// Chỉ bật Swagger ở môi trường Development.
app.UseSwaggerDocumentation();

// app.UseHttpsRedirection();

// Middleware bắt exception phải đặt sớm nhất để bắt mọi lỗi.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS phải đặt trước Authorization.
app.UseCors(CorsExtensions.ClientAppPolicyName);

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

public sealed record TestEmailOtpRequest(string Email, string? DisplayName);
