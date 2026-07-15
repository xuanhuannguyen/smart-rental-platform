using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Infrastructure.Persistence;
using Xunit;

namespace SmartRentalPlatform.IntegrationTests.Infrastructure;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var httpContext = Context;

        if (string.Equals(
            httpContext.Request.Headers["X-Test-Anonymous"].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        
        string userId = httpContext.Request.Headers["X-Test-User-Id"].FirstOrDefault() ?? "e2cfbf61-3444-42b7-a365-515a4430e386";
        string email = httpContext.Request.Headers["X-Test-User-Email"].FirstOrDefault() ?? "test@example.com";
        string rolesList = httpContext.Request.Headers["X-Test-User-Roles"].FirstOrDefault() ?? "Tenant";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
        };

        foreach (var role in rolesList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "smart-rental-platform-test-secret-key-32bytes-minimum");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "SmartRentalPlatform.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "SmartRentalPlatform.Tests");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => Task.CompletedTask;

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    public HttpClient CreateAuthenticatedClient(
        string role,
        Guid? userId = null,
        string? email = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-User-Id",
            (userId ?? Guid.Parse("e2cfbf61-3444-42b7-a365-515a4430e386")).ToString());
        client.DefaultRequestHeaders.Add("X-Test-User-Email", email ?? "test@example.com");
        client.DefaultRequestHeaders.Add("X-Test-User-Roles", role);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(NullLoggerProvider.Instance);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var appDbContextConcreteDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));

            if (appDbContextConcreteDescriptor != null)
            {
                services.Remove(appDbContextConcreteDescriptor);
            }

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(System.Data.Common.DbConnection));

            if (dbConnectionDescriptor != null)
            {
                services.Remove(dbConnectionDescriptor);
            }

            var appDbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAppDbContext));

            if (appDbContextDescriptor != null)
            {
                services.Remove(appDbContextDescriptor);
            }

            // Register DbContext with EF Core In-Memory database
            var efServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddScoped<AppDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("smart_rental_integration_test")
                    .UseInternalServiceProvider(efServiceProvider)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options;

                return new TestIntegrationAppDbContext(options);
            });

            services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

            // Replace standard Authentication with our Test Authentication Scheme
            var authDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuthenticationService));
            if (authDescriptor != null)
            {
                // We let AddAuthentication override defaults
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Ensure schema is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

internal sealed class TestIntegrationAppDbContext : AppDbContext
{
    public TestIntegrationAppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            SetTimestampIfPresent(entry, "CreatedAt", now, onlyWhenMissing: true);
            SetTimestampIfPresent(entry, "UpdatedAt", now, onlyWhenMissing: entry.State == EntityState.Added);

            if (entry.State == EntityState.Modified)
            {
                SetTimestampIfPresent(entry, "UpdatedAt", now, onlyWhenMissing: false);
            }
        }
    }

    private static void SetTimestampIfPresent(EntityEntry entry, string propertyName, DateTimeOffset value, bool onlyWhenMissing)
    {
        var property = entry.Metadata.FindProperty(propertyName);
        if (property is null || property.ClrType != typeof(DateTimeOffset) && property.ClrType != typeof(DateTimeOffset?))
        {
            return;
        }

        var propertyEntry = entry.Property(propertyName);
        if (!onlyWhenMissing || propertyEntry.CurrentValue is null || propertyEntry.CurrentValue.Equals(default(DateTimeOffset)))
        {
            propertyEntry.CurrentValue = value;
        }
    }
}
