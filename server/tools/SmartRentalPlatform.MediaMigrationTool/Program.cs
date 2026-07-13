using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Infrastructure.MediaMigration;
using SmartRentalPlatform.Infrastructure.Options;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Storage;

var command = args.FirstOrDefault() ?? "phase5b";
if (!string.Equals(command, "phase5b", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase))
{
    PrintUsage();
    return 1;
}

var parsedArgs = ParseArgs(args.Skip(1));
var mode = parsedArgs.TryGetValue("mode", out var configuredMode)
    ? configuredMode
    : string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase) ? "cleanup" : "report";
if (string.Equals(command, "phase5b", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(mode, "report", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(mode, "backfill", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Unsupported mode. Use --mode report or --mode backfill.");
    return 1;
}

if (string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(mode, "cleanup", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Unsupported mode. Use --mode cleanup for phase5e.");
    return 1;
}

var dryRun = GetBool(parsedArgs, "dry-run", defaultValue: true);
if (!dryRun &&
    string.Equals(command, "phase5b", StringComparison.OrdinalIgnoreCase) &&
    string.Equals(mode, "report", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Report mode is read-only. Use --mode backfill when passing --dry-run false.");
    return 2;
}

var checkStorage = string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase) ||
                   GetBool(parsedArgs, "check-storage", defaultValue: false);
var requireStoragePresent = GetBool(parsedArgs, "require-storage-present", defaultValue: false);
var sampleLimit = GetInt(parsedArgs, "sample-limit", defaultValue: 10);
var outputPath = parsedArgs.TryGetValue("output", out var configuredOutput)
    ? configuredOutput
    : Path.Combine("server", "data", "media-migration",
        string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase)
            ? "phase5e-cleanup-report.json"
            : string.Equals(mode, "backfill", StringComparison.OrdinalIgnoreCase)
                ? "phase5b-backfill-report.json"
                : "phase5b-readiness-report.json");

var configuration = BuildConfiguration();
var configuredConnectionString =
    (parsedArgs.TryGetValue("connection-string", out var explicitConnectionString)
        ? explicitConnectionString
        : null) ??
    configuration.GetConnectionString("DefaultConnection") ??
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (!string.IsNullOrWhiteSpace(configuredConnectionString))
{
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", configuredConnectionString);
}

await using var dbContext = new AppDbContextFactory().CreateDbContext(args);
try
{
    if (!await dbContext.Database.CanConnectAsync())
    {
        Console.Error.WriteLine("Could not connect to the configured database.");
        Console.Error.WriteLine($"Connection string: {MaskConnectionString(configuredConnectionString)}");
        Console.Error.WriteLine("Start the database or pass --connection-string \"...\" to target another database.");
        return 3;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine("Could not connect to the configured database.");
    Console.Error.WriteLine($"Connection string: {MaskConnectionString(configuredConnectionString)}");
    Console.Error.WriteLine(ex.GetBaseException().Message);
    Console.Error.WriteLine("Start the database or pass --connection-string \"...\" to target another database.");
    return 3;
}

S3StorageService? mediaStorageService = null;

if (checkStorage)
{
    var s3Options = configuration.GetSection(S3StorageOptions.SectionPath).Get<S3StorageOptions>() ?? new S3StorageOptions();
    S3StorageService.ValidateOptions(s3Options);
    mediaStorageService = new S3StorageService(
        S3StorageService.CreateClient(s3Options),
        Options.Create(s3Options));
}

var service = new LegacyMediaMigrationReadinessService(dbContext, mediaStorageService);

object report;
if (string.Equals(command, "phase5e", StringComparison.OrdinalIgnoreCase))
{
    report = await service.CleanupMissingStorageAsync(
        new LegacyMediaCleanupOptions
        {
            DryRun = dryRun,
            SampleLimitPerModule = sampleLimit
        });
}
else if (string.Equals(mode, "backfill", StringComparison.OrdinalIgnoreCase))
{
    report = await service.BackfillAsync(
        new LegacyMediaBackfillOptions
        {
            DryRun = dryRun,
            CheckStorage = checkStorage,
            RequireStoragePresent = requireStoragePresent,
            SampleLimitPerModule = sampleLimit
        });
}
else
{
    report = await service.BuildReportAsync(
        new LegacyMediaMigrationReadinessOptions
        {
            CheckStorage = checkStorage,
            SampleLimitPerModule = sampleLimit
        });
}

var fullOutputPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

await File.WriteAllTextAsync(
    fullOutputPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

if (report is LegacyMediaBackfillReport backfillReport)
{
    Console.WriteLine($"Phase 5B backfill {(backfillReport.DryRun ? "dry-run" : "apply")} report generated.");
    Console.WriteLine($"Output: {fullOutputPath}");
    Console.WriteLine($"Candidates: {backfillReport.Totals.Candidates}");
    Console.WriteLine($"Planned creates: {backfillReport.Totals.PlannedCreates}");
    Console.WriteLine($"Planned links: {backfillReport.Totals.PlannedLinks}");
    Console.WriteLine($"Created MediaAssets: {backfillReport.Totals.CreatedMediaAssets}");
    Console.WriteLine($"Linked legacy rows: {backfillReport.Totals.LinkedLegacyRows}");
    Console.WriteLine($"Skipped schema not ready: {backfillReport.Totals.SkippedSchemaNotReady}");
    Console.WriteLine($"Skipped storage missing: {backfillReport.Totals.SkippedStorageMissing}");
    return 0;
}

if (report is LegacyMediaCleanupReport cleanupReport)
{
    Console.WriteLine($"Phase 5E cleanup {(cleanupReport.DryRun ? "dry-run" : "apply")} report generated.");
    Console.WriteLine($"Output: {fullOutputPath}");
    Console.WriteLine($"Candidates: {cleanupReport.Totals.Candidates}");
    Console.WriteLine($"Planned deletes: {cleanupReport.Totals.PlannedDeletes}");
    Console.WriteLine($"Planned clears: {cleanupReport.Totals.PlannedClears}");
    Console.WriteLine($"Applied deletes: {cleanupReport.Totals.AppliedDeletes}");
    Console.WriteLine($"Applied clears: {cleanupReport.Totals.AppliedClears}");
    Console.WriteLine($"Skipped storage present: {cleanupReport.Totals.SkippedStoragePresent}");
    Console.WriteLine($"Skipped storage errors: {cleanupReport.Totals.SkippedStorageError}");
    Console.WriteLine($"Skipped no cleanup target: {cleanupReport.Totals.SkippedNoCleanupTarget}");
    return 0;
}

var readinessReport = (LegacyMediaMigrationReadinessReport)report;
Console.WriteLine("Phase 5B dry-run report generated.");
Console.WriteLine($"Output: {fullOutputPath}");
Console.WriteLine($"Legacy references: {readinessReport.Totals.LegacyReferences}");
Console.WriteLine($"Missing MediaAsset links: {readinessReport.Totals.MissingMediaAssetLinks}");
Console.WriteLine($"Missing MediaAssets by object key: {readinessReport.Totals.MissingMediaAssetsByObjectKey}");
Console.WriteLine($"Storage check: {(readinessReport.StorageCheckRequested ? "requested" : "skipped")}");

if (readinessReport.StorageCheckRequested)
{
    Console.WriteLine($"Storage present: {readinessReport.Totals.StoragePresent}");
    Console.WriteLine($"Storage missing: {readinessReport.Totals.StorageMissing}");
    Console.WriteLine($"Storage errors: {readinessReport.Totals.StorageErrors}");
}

return 0;

static IConfiguration BuildConfiguration()
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
    var apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "server", "src", "SmartRentalPlatform.Api"));
    if (!Directory.Exists(apiPath))
    {
        apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "src", "SmartRentalPlatform.Api"));
    }

    return new ConfigurationBuilder()
        .SetBasePath(Directory.Exists(apiPath) ? apiPath : Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddJsonFile("appsettings.Local.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
}

static Dictionary<string, string> ParseArgs(IEnumerable<string> argsToParse)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using var enumerator = argsToParse.GetEnumerator();
    while (enumerator.MoveNext())
    {
        var current = enumerator.Current;
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = current[2..];
        if (!enumerator.MoveNext())
        {
            result[key] = "true";
            break;
        }

        result[key] = enumerator.Current;
    }

    return result;
}

static bool GetBool(IReadOnlyDictionary<string, string> argsByName, string name, bool defaultValue)
{
    return argsByName.TryGetValue(name, out var value) && bool.TryParse(value, out var parsed)
        ? parsed
        : defaultValue;
}

static int GetInt(IReadOnlyDictionary<string, string> argsByName, string name, int defaultValue)
{
    return argsByName.TryGetValue(name, out var value) && int.TryParse(value, out var parsed)
        ? parsed
        : defaultValue;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --no-restore --project server/tools/SmartRentalPlatform.MediaMigrationTool -- phase5b --mode report --dry-run true --check-storage false --sample-limit 10 --output server/data/media-migration/phase5b-readiness-report.json");
    Console.WriteLine("  dotnet run --no-restore --project server/tools/SmartRentalPlatform.MediaMigrationTool -- phase5b --mode backfill --dry-run true --sample-limit 10 --output server/data/media-migration/phase5b-backfill-report.json");
    Console.WriteLine("  dotnet run --no-restore --project server/tools/SmartRentalPlatform.MediaMigrationTool -- phase5e --mode cleanup --dry-run true --sample-limit 10 --output server/data/media-migration/phase5e-cleanup-report.json");
    Console.WriteLine("  dotnet run --no-restore --project server/tools/SmartRentalPlatform.MediaMigrationTool -- phase5b --connection-string \"Host=localhost;Port=5433;Database=smart_rental_platform;Username=postgres;Password=postgres\"");
}

static string MaskConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "<not configured>";
    }

    return string.Join(
        ';',
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : part));
}
