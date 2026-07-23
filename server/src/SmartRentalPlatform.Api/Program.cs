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

if (args.Length > 0 && args[0] == "reset-public-schema")
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Infrastructure.Persistence.AppDbContext>();

    await dbContext.Database.ExecuteSqlRawAsync("""
        DROP SCHEMA IF EXISTS public CASCADE;
        CREATE SCHEMA public;
        """);

    return;
}

if (args.Length > 0 && args[0] == "seed-showcase-data")
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Infrastructure.Persistence.AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.IPasswordService>();
    var mediaStorageService = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.Media.IMediaStorageService>();
    var mediaObjectKeyFactory = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Application.Common.Interfaces.Media.IMediaObjectKeyFactory>();

    await dbContext.Database.MigrateAsync();
    await SmartRentalPlatform.Infrastructure.Persistence.Seed.DevelopmentDataSeed.SeedAdminAsync(dbContext, passwordService);
    await SmartRentalPlatform.Infrastructure.Persistence.Seed.DevelopmentDataSeed.SeedAsync(
        dbContext,
        passwordService,
        mediaStorageService,
        mediaObjectKeyFactory);

    return;
}

if (args.Length > 0 && args[0] == "verify-showcase-seed")
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartRentalPlatform.Infrastructure.Persistence.AppDbContext>();
    var connection = dbContext.Database.GetDbConnection();

    await connection.OpenAsync();

    static async Task<object?> ScalarAsync(System.Data.Common.DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    var summary = new[]
    {
        ("users", "SELECT COUNT(*) FROM users"),
        ("rooming_houses", "SELECT COUNT(*) FROM rooming_houses WHERE deleted_at IS NULL"),
        ("property_images_with_media", "SELECT COUNT(*) FROM property_images WHERE media_asset_id IS NOT NULL"),
        ("active_showcase_contract", "SELECT COUNT(*) FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' AND status = 'Active'"),
        ("expired_review_contract", "SELECT COUNT(*) FROM contracts WHERE contract_number = 'HD-XH-A01-20250901' AND status = 'Expired' AND end_date = DATE '2026-02-28'"),
        ("showcase_contract_pdf_files", "SELECT COUNT(*) FROM contract_files WHERE contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1) AND content_type = 'application/pdf'"),
        ("showcase_invoices", "SELECT COUNT(*) FROM invoices WHERE contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1)"),
        ("showcase_meter_readings", "SELECT COUNT(*) FROM meter_readings WHERE contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1)"),
        ("showcase_signatures", "SELECT COUNT(*) FROM contract_signatures WHERE contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1)"),
        ("landlord_reserved_balance", "SELECT reserved_balance FROM wallet_accounts WHERE user_id = '10000000-0000-0000-0000-000000000002' LIMIT 1"),
        ("no_demo_flow_contracts", "SELECT COUNT(*) FROM contracts WHERE contract_number ILIKE '%DEMO-FLOW%' OR contract_number ILIKE '%DEMO-REVIEW%'"),
        ("no_final_b201_invoice", "SELECT COUNT(*) FROM invoices WHERE invoice_no = 'HD-B201-FINAL-202607'"),
        ("no_80000_draft_invoice", "SELECT COUNT(*) FROM invoices i JOIN invoice_items ii ON ii.invoice_id = i.id WHERE i.status = 'Draft' AND ii.unit_price = 80000"),
        ("nguyen_landlord_name", "SELECT display_name FROM users WHERE email = 'nguyenxuanhuan21102005@gmail.com' LIMIT 1"),
        ("nguyen_landlord_kyc_name", "SELECT ocr_full_name FROM kyc_verifications WHERE user_id = '10000000-0000-0000-0000-000000000002' AND status = 'Approved' ORDER BY created_at DESC LIMIT 1"),
        ("xunhuns_landlord_name", "SELECT display_name FROM users WHERE email = 'xunhuns21@gmail.com' LIMIT 1"),
        ("xunhuns_landlord_kyc_name", "SELECT ocr_full_name FROM kyc_verifications WHERE user_id = '10000000-0000-0000-0000-000000000004' AND status = 'Approved' ORDER BY created_at DESC LIMIT 1"),
        ("b201_july_real_meter_images", "SELECT COUNT(*) FROM meter_readings mr JOIN media_assets ma ON ma.id = mr.proof_media_asset_id WHERE mr.contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1) AND mr.billing_period_start = DATE '2026-07-01' AND ma.content_type IN ('image/png','image/jpeg','image/webp')"),
        ("xunhuns_rooming_houses", "SELECT COUNT(*) FROM rooming_houses WHERE landlord_user_id = '10000000-0000-0000-0000-000000000004' AND deleted_at IS NULL"),
        ("xunhuns_occupied_rooms", "SELECT COUNT(*) FROM rooms r JOIN rooming_houses h ON h.id = r.rooming_house_id WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004' AND r.status = 'Occupied' AND r.deleted_at IS NULL"),
        ("xunhuns_active_contracts", "SELECT COUNT(*) FROM contracts c JOIN rooms r ON r.id = c.room_id JOIN rooming_houses h ON h.id = r.rooming_house_id WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004' AND c.status = 'Active'"),
        ("xunhuns_invoices_apr_to_may", "SELECT COUNT(*) FROM invoices i JOIN contracts c ON c.id = i.contract_id JOIN rooms r ON r.id = c.room_id JOIN rooming_houses h ON h.id = r.rooming_house_id WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004' AND i.billing_period_start >= DATE '2026-04-01' AND i.billing_period_start <= DATE '2026-05-01'"),
        ("xunhuns_june_invoices", "SELECT COUNT(*) FROM invoices i JOIN contracts c ON c.id = i.contract_id JOIN rooms r ON r.id = c.room_id JOIN rooming_houses h ON h.id = r.rooming_house_id WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004' AND i.billing_period_start = DATE '2026-06-01'"),
        ("xunhuns_july_invoices", "SELECT COUNT(*) FROM invoices i JOIN contracts c ON c.id = i.contract_id JOIN rooms r ON r.id = c.room_id JOIN rooming_houses h ON h.id = r.rooming_house_id WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004' AND i.billing_period_start = DATE '2026-07-01'"),
        ("demo_service_prices", "SELECT COUNT(*) FROM rooming_house_service_prices WHERE rooming_house_id IN ('20000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000004') AND is_active = TRUE"),
        ("demo_house_rules", "SELECT COUNT(*) FROM rooming_house_rules WHERE rooming_house_id IN ('20000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000004') AND general_rules IS NOT NULL AND quiet_hours IS NOT NULL AND security_policy IS NOT NULL AND cleaning_policy IS NOT NULL AND guest_policy IS NOT NULL AND parking_policy IS NOT NULL AND utility_policy IS NOT NULL AND damage_compensation_policy IS NOT NULL"),
        ("bulk_invoice_tenant", "SELECT display_name FROM users WHERE email = 'huanjrfc@gmail.com' LIMIT 1"),
        ("xunhuns_withdrawal_requests", "SELECT COUNT(*) FROM withdrawal_requests WHERE wallet_account_id = '70000000-0000-0000-0000-000000000006'"),
        ("reviews_with_required_reply", "SELECT COUNT(*) FROM rooming_house_reviews r JOIN contracts c ON c.id = r.rental_contract_id WHERE c.status IN ('Expired','Cancelled') AND r.landlord_reply IS NOT NULL AND r.landlord_reply_created_at > r.created_at"),
        ("demo_media_svg_count", "SELECT COUNT(*) FROM media_assets WHERE content_type = 'image/svg+xml' AND deleted_at IS NULL")
    };

    foreach (var (label, sql) in summary)
    {
        Console.WriteLine($"{label}: {await ScalarAsync(connection, sql)}");
    }

    await using var pdfCommand = connection.CreateCommand();
    pdfCommand.CommandText = """
        SELECT purpose, content_type, is_legally_signed, LEFT(sha256_hash, 12) AS sha256_prefix
        FROM contract_files
        WHERE contract_id = (SELECT id FROM contracts WHERE contract_number = 'HD-XH-B201-20260601' LIMIT 1)
        ORDER BY purpose;
        """;

    await using var reader = await pdfCommand.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"showcase_contract_file: purpose={reader.GetString(0)}, content_type={reader.GetString(1)}, signed={reader.GetBoolean(2)}, sha256={reader.GetString(3)}...");
    }

    return;
}

if (args.Length > 0 && (args[0] == "seed-display-data" || args[0] == "validate-display-seed" || args[0] == "redistribute-display-coordinates"))
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
    else if (args[0] == "redistribute-display-coordinates")
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
            await runner.RunRedistributeCoordinatesAsync(version);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Coordinate redistribution failed: {ex}");
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
