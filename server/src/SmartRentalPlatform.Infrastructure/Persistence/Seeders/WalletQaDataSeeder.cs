using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seeders;

public static class WalletQaDataSeeder
{
    public const string TenantEmail = "tenant.demo@example.com";
    public const string LandlordEmail = "landlord.demo@example.com";
    public const string AdminEmail = "admin.demo@example.com";
    public const string DemoPassword = "Demo@123456";

    private static readonly Guid TenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000099");

    private static readonly Guid TenantKycId = Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordKycId = Guid.Parse("51000000-0000-0000-0000-000000000002");

    private static readonly Guid TenantWalletId = Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordWalletId = Guid.Parse("61000000-0000-0000-0000-000000000002");

    private static readonly Guid TenantTopUpTransactionId = Guid.Parse("62000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantAdjustmentTransactionId = Guid.Parse("62000000-0000-0000-0000-000000000002");
    private static readonly Guid LandlordTopUpTransactionId = Guid.Parse("62000000-0000-0000-0000-000000000003");
    private static readonly Guid LandlordAdjustmentTransactionId = Guid.Parse("62000000-0000-0000-0000-000000000004");

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync(context, cancellationToken);

        var admin = await EnsureUserAsync(
            context,
            passwordService,
            AdminUserId,
            AdminEmail,
            "Admin Demo",
            RoleSeed.AdminRoleId,
            cancellationToken);

        var tenant = await EnsureUserAsync(
            context,
            passwordService,
            TenantUserId,
            TenantEmail,
            "Nguyen Tenant Demo",
            RoleSeed.TenantRoleId,
            cancellationToken);

        var landlord = await EnsureUserAsync(
            context,
            passwordService,
            LandlordUserId,
            LandlordEmail,
            "Tran Landlord Demo",
            RoleSeed.LandlordRoleId,
            cancellationToken);

        await EnsureApprovedKycAsync(
            context,
            tenant,
            TenantKycId,
            admin.Id,
            "Nguyen Tenant Demo",
            "079********001",
            "wallet-qa-tenant-citizen-id-hash",
            new DateOnly(1998, 1, 1),
            "Male",
            "123 Test Street, Ho Chi Minh City",
            cancellationToken);

        await EnsureApprovedKycAsync(
            context,
            landlord,
            LandlordKycId,
            admin.Id,
            "Tran Landlord Demo",
            "079********002",
            "wallet-qa-landlord-citizen-id-hash",
            new DateOnly(1990, 6, 15),
            "Male",
            "456 Test Street, Ho Chi Minh City",
            cancellationToken);

        await EnsureWalletAsync(
            context,
            tenant.Id,
            TenantWalletId,
            500_000m,
            TenantTopUpTransactionId,
            TenantAdjustmentTransactionId,
            cancellationToken);

        await EnsureWalletAsync(
            context,
            landlord.Id,
            LandlordWalletId,
            100_000m,
            LandlordTopUpTransactionId,
            LandlordAdjustmentTransactionId,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRolesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        foreach (var role in RoleSeed.GetRoles())
        {
            if (!await context.Roles.AnyAsync(x => x.Id == role.Id, cancellationToken))
            {
                context.Roles.Add(role);
            }
        }
    }

    private static async Task<User> EnsureUserAsync(
        AppDbContext context,
        IPasswordService passwordService,
        Guid fallbackId,
        string email,
        string displayName,
        int roleId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await context.Users
            .FirstOrDefaultAsync(
                x => x.Id == fallbackId || x.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = fallbackId,
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = passwordService.HashPassword(DemoPassword),
                DisplayName = displayName,
                Status = UserStatus.Active,
                OnboardingStatus = OnboardingStatus.Completed,
                EmailConfirmed = true,
                PhoneConfirmed = false,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            };

            context.Users.Add(user);
        }

        if (!await context.UserRoles.AnyAsync(
            x => x.UserId == user.Id && x.RoleId == roleId,
            cancellationToken))
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                CreatedAt = SeededAt
            });
        }

        if (!await context.UserProfiles.AnyAsync(x => x.UserId == user.Id, cancellationToken))
        {
            context.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = displayName,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
        }

        return user;
    }

    private static async Task EnsureApprovedKycAsync(
        AppDbContext context,
        User user,
        Guid kycId,
        Guid reviewedByAdminId,
        string fullName,
        string citizenIdMasked,
        string citizenIdHash,
        DateOnly dateOfBirth,
        string gender,
        string address,
        CancellationToken cancellationToken)
    {
        if (await context.KycVerifications.AnyAsync(
            x => x.UserId == user.Id && x.Status == KycVerificationStatus.Approved,
            cancellationToken))
        {
            return;
        }

        context.KycVerifications.Add(new KycVerification
        {
            Id = kycId,
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            EkycSessionId = $"wallet-qa-approved-{user.Id:N}",
            FrontImageObjectKey = string.Empty,
            BackImageObjectKey = string.Empty,
            SelfieImageObjectKey = string.Empty,
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            OcrFullName = fullName,
            OcrCitizenIdMasked = citizenIdMasked,
            CitizenIdHash = citizenIdHash,
            OcrDateOfBirth = dateOfBirth,
            OcrGender = gender,
            OcrAddress = address,
            OcrConfidence = 0.9900m,
            DocumentCheckResult = DocumentCheckResult.Valid,
            FaceMatchScore = 0.9900m,
            FaceMatchResult = FaceMatchResult.Matched,
            LivenessResult = LivenessResult.Passed,
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = KycVerificationStatus.Approved,
            ReviewedByAdminId = reviewedByAdminId,
            SubmittedAt = SeededAt,
            ReviewedAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });
    }

    private static async Task EnsureWalletAsync(
        AppDbContext context,
        Guid userId,
        Guid walletId,
        decimal initialBalance,
        Guid topUpTransactionId,
        Guid adjustmentTransactionId,
        CancellationToken cancellationToken)
    {
        var wallet = await context.WalletAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (wallet is not null)
        {
            return;
        }

        wallet = new WalletAccount
        {
            Id = walletId,
            UserId = userId,
            Balance = initialBalance,
            ReservedBalance = 0m,
            Currency = "VND",
            Status = WalletAccountStatus.Active,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };

        context.WalletAccounts.Add(wallet);

        var topUpAmount = initialBalance * 0.8m;
        var adjustmentAmount = initialBalance - topUpAmount;

        AddWalletTransaction(
            context,
            topUpTransactionId,
            wallet.Id,
            userId,
            WalletTransactionType.WalletTopUp,
            topUpAmount,
            0m,
            topUpAmount,
            "QA seed wallet top-up");

        AddWalletTransaction(
            context,
            adjustmentTransactionId,
            wallet.Id,
            userId,
            WalletTransactionType.ManualAdjustment,
            adjustmentAmount,
            topUpAmount,
            initialBalance,
            "QA seed manual adjustment");
    }

    private static void AddWalletTransaction(
        AppDbContext context,
        Guid id,
        Guid walletAccountId,
        Guid userId,
        WalletTransactionType transactionType,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        string description)
    {
        context.WalletTransactions.Add(new WalletTransaction
        {
            Id = id,
            WalletAccountId = walletAccountId,
            UserId = userId,
            TransactionType = transactionType,
            Direction = WalletTransactionDirection.Credit,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReservedBalanceBefore = 0m,
            ReservedBalanceAfter = 0m,
            RelatedEntityType = "WalletQaSeed",
            Description = description,
            Status = WalletTransactionStatus.Succeeded,
            CreatedAt = SeededAt
        });
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }
}
