using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Api.Middlewares;
using SmartRentalPlatform.Application;
using SmartRentalPlatform.Infrastructure;
using QuestPDF.Infrastructure;
using SmartRentalPlatform.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

QuestPDF.Settings.License = LicenseType.Community;

// Đăng ký controller để dùng mô hình API Controller.
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Đăng ký Swagger để test API trên trình duyệt.
builder.Services.AddSwaggerDocumentation();

// Đăng ký JWT authentication và authorization.
builder.Services.AddJwtAuthentication(builder.Configuration);

// Cho phép frontend React gọi backend.
builder.Services.AddClientCors(builder.Configuration);

builder.Services.AddApiRateLimiting();

// Đăng ký các layer tự viết.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (args.Length > 0 && (args[0] == "seed-display-data" || args[0] == "validate-display-seed"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Infrastructure.Persistence.AppDbContext>();
    var mediaStorageService = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.Media.IMediaStorageService>();
    var mediaObjectKeyFactory = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.Media.IMediaObjectKeyFactory>();
    var passwordService = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.IPasswordService>();
    
    var runner = new SmartRentalPlatform.Infrastructure.Persistence.Seed.DisplayCatalogSeedRunner(
        dbContext,
        mediaStorageService,
        mediaObjectKeyFactory,
        passwordService);
        
    if (args[0] == "seed-display-data")
    {
        int count = 500;
        int assetCount = 180;
        bool uploadMedia = true;
        string version = "display-catalog-v1";
        string? mediaSource = null;
        
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--count" && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out count);
            }
            else if (args[i] == "--asset-count" && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out assetCount);
            }
            else if (args[i] == "--upload-media" && i + 1 < args.Length)
            {
                bool.TryParse(args[i + 1], out uploadMedia);
            }
            else if (args[i] == "--version" && i + 1 < args.Length)
            {
                version = args[i + 1];
            }
            else if (args[i] == "--media-source" && i + 1 < args.Length)
            {
                mediaSource = args[i + 1];
            }
        }
        
        try
        {
            await runner.RunSeedAsync(count, assetCount, uploadMedia, version, mediaSource);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Seeding failed: {ex}");
            Environment.Exit(1);
        }
    }
    else if (args[0] == "validate-display-seed")
    {
        string version = "display-catalog-v1";
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--version" && i + 1 < args.Length)
            {
                version = args[i + 1];
            }
        }
        
        try
        {
            var report = await runner.RunValidateAsync(version);
            Environment.Exit(report.IsValid ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex}");
            Environment.Exit(1);
        }
    }
}

await app.InitializeDevelopmentDatabaseAsync();
await app.SeedConfiguredDemoDataAsync();

// Chỉ bật Swagger ở môi trường Development.
app.UseSwaggerDocumentation();

// app.UseHttpsRedirection();

// Middleware bắt exception phải đặt sớm nhất để bắt mọi lỗi.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// CORS phải đặt trước Authorization.
app.UseCors(CorsExtensions.ClientAppPolicyName);

// Rate limiting middleware — phải sau CORS, trước controllers.
app.UseRateLimiter();

app.UsePublicStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapDevelopmentEndpoints();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program;
