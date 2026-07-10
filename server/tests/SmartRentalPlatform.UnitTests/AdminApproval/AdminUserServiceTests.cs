using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Users;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.AdminApproval;

public class AdminUserServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task GetUsersAsync_ExcludesAdminUsersAndMapsRolesOrderedByCreatedAt()
    {
        var tenant = TestDataBuilder.BuildUser(email: "tenant-list@unit.test", displayName: "Tenant List");
        tenant.CreatedAt = DateTimeOffset.UtcNow.AddDays(2);
        tenant.PhoneNumber = "0900000001";
        var landlord = TestDataBuilder.BuildUser(email: "landlord-list@unit.test", displayName: "Landlord List");
        landlord.CreatedAt = DateTimeOffset.UtcNow.AddDays(1);
        var admin = TestDataBuilder.BuildUser(email: "admin-list@unit.test", displayName: "Admin List");
        admin.CreatedAt = DateTimeOffset.UtcNow.AddDays(3);

        _fixture.Context.Users.AddRange(tenant, landlord, admin);
        _fixture.Context.UserRoles.AddRange(
            new UserRole { UserId = tenant.Id, RoleId = (int)RoleName.Tenant },
            new UserRole { UserId = landlord.Id, RoleId = (int)RoleName.Landlord },
            new UserRole { UserId = admin.Id, RoleId = (int)RoleName.Admin });
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminUserService(_fixture.Context);

        var result = await service.GetUsersAsync(pageNumber: 1, pageSize: 10);

        Assert.DoesNotContain(result.Items, x => x.Id == admin.Id);
        Assert.Contains(result.Items, x => x.Id == tenant.Id && x.Roles.Contains(RoleName.Tenant.ToString()));
        Assert.Contains(result.Items, x => x.Id == landlord.Id && x.Roles.Contains(RoleName.Landlord.ToString()));
        Assert.True(result.TotalCount >= 2);
    }

    [Fact]
    public async Task GetUserDetailAsync_WhenUserMissing_ReturnsNull()
    {
        var service = new AdminUserService(_fixture.Context);

        var result = await service.GetUserDetailAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserDetailAsync_ReturnsProfileRolesAndLatestApprovedKycInfoForCompletedUser()
    {
        var user = TestDataBuilder.BuildUser(email: "detail@unit.test", displayName: "Detail User");
        user.OnboardingStatus = OnboardingStatus.Completed;
        _fixture.Context.Users.Add(user);
        _fixture.Context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = (int)RoleName.Tenant });
        _fixture.Context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            FullName = "Profile Name",
            DateOfBirth = new DateOnly(1998, 3, 4),
            Gender = "Nu",
            AddressLine = "Profile address",
            VerifiedCitizenIdMasked = "999******111",
            EmergencyContactName = "Emergency",
            EmergencyContactPhone = "0911111111",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var oldApproved = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddDays(-3), fullName: "Old KYC");
        var latestApproved = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddDays(-1), fullName: "Latest KYC", frontKey: "front latest");
        var pending = BuildKyc(user.Id, KycVerificationStatus.PendingAdminReview, DateTimeOffset.UtcNow, "Pending KYC");
        _fixture.Context.KycVerifications.AddRange(oldApproved, latestApproved, pending);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminUserService(_fixture.Context);

        var result = await service.GetUserDetailAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal("Profile Name", result.FullName);
        Assert.Contains(RoleName.Tenant.ToString(), result.Roles);
        Assert.NotNull(result.KycInfo);
        Assert.Equal(latestApproved.Id, result.KycInfo.KycId);
        Assert.Equal("Latest KYC", result.KycInfo.OcrFullName);
        Assert.Equal($"/api/admin/media/private/{latestApproved.FrontMediaAssetId:D}", result.KycInfo.FrontImageUrl);
    }

    private static KycVerification BuildKyc(
        Guid userId,
        KycVerificationStatus status = KycVerificationStatus.Approved,
        DateTimeOffset? submittedAt = null,
        string fullName = "Approved User",
        string frontKey = "front")
    {
        var now = DateTimeOffset.UtcNow;

        return new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = KycDocumentType.CCCD,
            FrontMediaAssetId = Guid.NewGuid(),
            BackMediaAssetId = Guid.NewGuid(),
            SelfieMediaAssetId = Guid.NewGuid(),
            FrontImageObjectKey = frontKey,
            BackImageObjectKey = "back",
            SelfieImageObjectKey = "selfie",
            OcrFullName = fullName,
            OcrCitizenIdMasked = "123******456",
            CitizenIdHash = Guid.NewGuid().ToString("N"),
            OcrDateOfBirth = new DateOnly(1998, 3, 4),
            OcrGender = "Nu",
            OcrAddress = "KYC address",
            FaceMatchScore = 0.97m,
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = status,
            SubmittedAt = submittedAt ?? now,
            ReviewedAt = status == KycVerificationStatus.Approved ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
