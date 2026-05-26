using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

/// <summary>
/// Optional dev-only seeder. Call from Program.cs when ASPNETCORE_ENVIRONMENT=Development.
/// Mirrors qa/kyc/seed-test-users.sql fixed GUIDs.
/// </summary>
public static class KycQaDataSeeder
{
    public static readonly Guid HappyPathUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid BlockedUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid RejectedUiUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedUserAsync(db, HappyPathUserId, "kyc.happy@example.com", "0900000001", "KYC Happy Path User", cancellationToken);
        await SeedUserAsync(db, BlockedUserId, "kyc.blocked@example.com", "0900000002", "KYC Blocked User", cancellationToken);
        await SeedUserAsync(db, RejectedUiUserId, "kyc.rejected.ui@example.com", "0900000003", "KYC Rejected UI User", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUserAsync(
        AppDbContext db,
        Guid id,
        string email,
        string phone,
        string displayName,
        CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == id, cancellationToken);
        if (exists)
            return;

        var now = DateTimeOffset.UtcNow;
        db.Users.Add(new User
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PhoneNumber = phone,
            PasswordHash = "QA_NO_PASSWORD",
            DisplayName = displayName,
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.NeedKyc,
            EmailConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
