using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Kyc;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.AdminApproval;

public class AdminKycApprovalServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingAdminReviewItemsOrderedBySubmittedAt()
    {
        var user = TestDataBuilder.BuildUser(email: "tenant@unit.test", displayName: "Tenant");
        var older = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddHours(-1), fullName: "Newer");
        var approved = BuildKyc(user.Id, status: KycVerificationStatus.Approved);
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.AddRange(older, newer, approved);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.GetPendingAsync(pageNumber: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([newer.Id, older.Id], result.Items.Select(x => x.Id));
        Assert.Equal("tenant@unit.test", result.Items[0].UserEmail);
        Assert.Equal(KycRiskLevel.Low.ToString(), result.Items[0].RiskLevel);
    }

    [Fact]
    public async Task GetDetailAsync_WhenKycMissing_ReturnsNull()
    {
        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.GetDetailAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailAsync_WhenKycExists_ReturnsMappedDetailWithPrivateMediaUrls()
    {
        var user = TestDataBuilder.BuildUser(email: "kyc@unit.test", displayName: "KYC User");
        var kyc = BuildKyc(user.Id, frontKey: "front/key 1", backKey: "back/key 1", selfieKey: "selfie/key 1");
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(kyc);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.GetDetailAsync(kyc.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.UserEmail);
        Assert.Equal(KycDocumentType.CCCD.ToString(), result.DocumentType);
        Assert.Equal(kyc.FrontMediaAssetId, result.FrontMediaAssetId);
        Assert.Equal(kyc.BackMediaAssetId, result.BackMediaAssetId);
        Assert.Equal(kyc.SelfieMediaAssetId, result.SelfieMediaAssetId);
        Assert.Equal($"/api/admin/media/private/{kyc.FrontMediaAssetId:D}", result.FrontImageUrl);
        Assert.Equal(DocumentCheckResult.Valid.ToString(), result.DocumentCheckResult);
    }

    [Fact]
    public async Task ApproveAsync_WhenKycPending_ApprovesKycCompletesUserAndCreatesProfile()
    {
        var adminId = Guid.NewGuid();
        var user = TestDataBuilder.BuildUser(status: UserStatus.Active);
        user.OnboardingStatus = OnboardingStatus.KycPending;
        var kyc = BuildKyc(user.Id, fullName: "Approved Name");
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(kyc);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.ApproveAsync(kyc.Id, adminId);

        Assert.True(result);
        Assert.Equal(KycVerificationStatus.Approved, kyc.Status);
        Assert.Equal(adminId, kyc.ReviewedByAdminId);
        Assert.Null(kyc.RejectedReason);
        Assert.Equal(OnboardingStatus.Completed, user.OnboardingStatus);
        var profile = Assert.Single(_fixture.Context.UserProfiles.Where(x => x.UserId == user.Id));
        Assert.Equal("Approved Name", profile.FullName);
        Assert.Equal("012******789", profile.VerifiedCitizenIdMasked);
    }

    [Fact]
    public async Task ApproveAsync_WhenKycNotPending_ReturnsFalse()
    {
        var user = TestDataBuilder.BuildUser();
        var kyc = BuildKyc(user.Id, status: KycVerificationStatus.Approved);
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(kyc);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.ApproveAsync(kyc.Id, Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task RejectAsync_WhenReasonBlank_ReturnsFalse()
    {
        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), "   ");

        Assert.False(result);
    }

    [Fact]
    public async Task RejectAsync_WhenKycPending_RejectsAndTrimsReason()
    {
        var adminId = Guid.NewGuid();
        var user = TestDataBuilder.BuildUser();
        user.OnboardingStatus = OnboardingStatus.KycPending;
        var kyc = BuildKyc(user.Id);
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.Add(kyc);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.RejectAsync(kyc.Id, adminId, "  blurry images  ");

        Assert.True(result);
        Assert.Equal(KycVerificationStatus.Rejected, kyc.Status);
        Assert.Equal("blurry images", kyc.RejectedReason);
        Assert.Equal(adminId, kyc.ReviewedByAdminId);
        Assert.Equal(OnboardingStatus.NeedProfileUpdate, user.OnboardingStatus);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsUserKycHistoryOrderedBySubmittedAt()
    {
        var user = TestDataBuilder.BuildUser();
        var older = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddDays(-2), fullName: "Older");
        var newer = BuildKyc(user.Id, submittedAt: DateTimeOffset.UtcNow.AddDays(-1), fullName: "Newer");
        var otherUser = BuildKyc(Guid.NewGuid(), submittedAt: DateTimeOffset.UtcNow);
        _fixture.Context.Users.Add(user);
        _fixture.Context.KycVerifications.AddRange(older, newer, otherUser);
        await _fixture.Context.SaveChangesAsync();

        var service = new AdminKycApprovalService(_fixture.Context);

        var result = await service.GetHistoryAsync(user.Id);

        Assert.Equal([newer.Id, older.Id], result.Select(x => x.Id));
        Assert.Equal("Newer", result[0].OcrFullName);
        Assert.Equal(newer.FrontMediaAssetId, result[0].FrontMediaAssetId);
    }

    private static KycVerification BuildKyc(
        Guid userId,
        KycVerificationStatus status = KycVerificationStatus.PendingAdminReview,
        DateTimeOffset? submittedAt = null,
        string fullName = "Unit Test User",
        string frontKey = "front",
        string backKey = "back",
        string selfieKey = "selfie")
    {
        var now = DateTimeOffset.UtcNow;

        return new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            EkycSessionId = Guid.NewGuid().ToString("N"),
            FrontMediaAssetId = Guid.NewGuid(),
            BackMediaAssetId = Guid.NewGuid(),
            SelfieMediaAssetId = Guid.NewGuid(),
            OcrFullName = fullName,
            OcrCitizenIdMasked = "012******789",
            CitizenIdHash = Guid.NewGuid().ToString("N"),
            OcrDateOfBirth = new DateOnly(1999, 1, 2),
            OcrGender = "Nam",
            OcrAddress = "Unit address",
            OcrConfidence = 0.98m,
            DocumentCheckResult = DocumentCheckResult.Valid,
            FaceMatchScore = 0.99m,
            FaceMatchResult = FaceMatchResult.Matched,
            LivenessResult = LivenessResult.Passed,
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = status,
            SubmittedAt = submittedAt ?? now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
