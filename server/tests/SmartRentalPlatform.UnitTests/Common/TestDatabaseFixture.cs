using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartRentalPlatform.Infrastructure.Persistence;

namespace SmartRentalPlatform.UnitTests.Common;

public class TestDatabaseFixture : IDisposable
{
    public AppDbContext Context { get; }

    public TestDatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Context = new TestAppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Reset()
    {
        Context.ChangeTracker.Clear();
        Context.Database.EnsureDeleted();
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
    }
}

internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options)
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
