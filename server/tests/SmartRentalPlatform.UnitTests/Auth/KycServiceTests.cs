using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Contracts.Kyc;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Auth;

public class KycServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakeVnptEkycClient _vnptEkycClient;
    private readonly FakeHashService _hashService;
    private readonly FakeMediaAccessService _mediaAccessService;
    private readonly FakeSensitiveDataProtector _sensitiveDataProtector;

    public KycServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _vnptEkycClient = new FakeVnptEkycClient();
        _hashService = new FakeHashService();
        _mediaAccessService = new FakeMediaAccessService();
        _sensitiveDataProtector = new FakeSensitiveDataProtector();
    }

    [Fact]
    public async Task SubmitAsync_ShouldThrowKycBusinessException_WhenRequiredFilesAreMissing()
    {
        // Arrange
        var context = _fixture.Context;
        var kycService = CreateService(context);
        var userId = Guid.NewGuid();
        var request = new SubmitKycRequest
        {
            DocumentType = "NationalId",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = Guid.Empty, // Missing
            BackMediaAssetId = Guid.NewGuid(),
            SelfieMediaAssetId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<KycBusinessException>(() => kycService.SubmitAsync(userId, request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_ShouldCreatePendingRequest_WhenInputIsValid()
    {
        // Arrange
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/10/front-file.jpg", OriginalFileName = "front.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/10/back-file.jpg", OriginalFileName = "back.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/10/selfie-file.jpg", OriginalFileName = "selfie.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = "CCCD",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id
        };

        var ekycResult = new VnptEkycClientResult
        {
            SessionId = "session_123",
            EkycResult = EkycResult.Passed.ToString(),
            OcrFullName = "NGUYEN VAN A",
            OcrCitizenId = "123456789012",
            OcrDateOfBirth = new DateTime(1995, 1, 1),
            OcrGender = "Male",
            OcrAddress = "Ha Noi, Viet Nam",
            OcrConfidence = 0.95m,
            DocumentCheckResult = DocumentCheckResult.Valid.ToString(),
            FaceMatchScore = 0.92m,
            FaceMatchResult = FaceMatchResult.Matched.ToString(),
            LivenessResult = LivenessResult.Passed.ToString()
        };

        _vnptEkycClient.VerifyAsyncFunc = (inp) => Task.FromResult(ekycResult);

        _hashService.HashSha256HexFunc = (val) => "sha256_hash_123";
        _sensitiveDataProtector.EncryptFunc = (plain) => "encrypted_citizen_id";

        // Act
        var result = await kycService.SubmitAsync(user.Id, request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(KycVerificationStatus.PendingAdminReview.ToString(), result.Status);
        Assert.Equal("NGUYEN VAN A", result.OcrFullName);

        var kycInDb = await context.KycVerifications.FirstOrDefaultAsync(k => k.UserId == user.Id);
        Assert.NotNull(kycInDb);
        Assert.Equal("sha256_hash_123", kycInDb!.CitizenIdHash);
        Assert.NotNull(kycInDb.FrontMediaAssetId);
        Assert.NotNull(kycInDb.BackMediaAssetId);
        Assert.NotNull(kycInDb.SelfieMediaAssetId);

        var mediaAssets = await context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(KycVerification) && x.LinkedEntityId == kycInDb.Id)
            .ToListAsync();
        Assert.Equal(3, mediaAssets.Count);
        Assert.Equal(3, mediaAssets.Count);
        Assert.All(mediaAssets, x =>
        {
            Assert.Equal(MediaScope.KycDocument, x.Scope);
            Assert.Equal(MediaVisibility.Private, x.Visibility);
            Assert.Equal(MediaStatus.Linked, x.Status);
            Assert.Equal(user.Id, x.OwnerUserId);
        });

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetMyStatusAsync_ShouldReturnHasNoSubmission_WhenNoKycExists()
    {
        var context = _fixture.Context;
        var kycService = CreateService(context);

        var result = await kycService.GetMyStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.HasSubmission);
        Assert.Null(result.KycId);
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_ShouldUpdateOnboardingAndReturnPendingReviewMessage()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "kyc-happy-regression@example.com", displayName: "Kyc Happy Regression");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/14/front-happy.jpg", OriginalFileName = "front-happy.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/14/back-happy.jpg", OriginalFileName = "back-happy.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/2026/07/14/selfie-happy.jpg", OriginalFileName = "selfie-happy.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        _vnptEkycClient.VerifyAsyncFunc = _ => Task.FromResult(new VnptEkycClientResult
        {
            SessionId = "session-happy-regression",
            EkycResult = EkycResult.Passed.ToString(),
            OcrFullName = "TRAN VAN B",
            OcrCitizenId = "123456789012",
            OcrDateOfBirth = new DateTime(1998, 2, 3),
            OcrGender = "Male",
            OcrAddress = "Ho Chi Minh City, Viet Nam",
            OcrConfidence = 0.98m,
            DocumentCheckResult = DocumentCheckResult.Valid.ToString(),
            FaceMatchScore = 0.96m,
            FaceMatchResult = FaceMatchResult.Matched.ToString(),
            LivenessResult = LivenessResult.Passed.ToString()
        });
        _hashService.HashSha256HexFunc = _ => "happy_hash";
        _sensitiveDataProtector.EncryptFunc = value => $"encrypted::{value}";

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = KycDocumentType.CCCD.ToString(),
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id
        };

        var result = await kycService.SubmitAsync(user.Id, request, CancellationToken.None);

        Assert.Equal(KycVerificationStatus.PendingAdminReview.ToString(), result.Status);
        Assert.Equal(EkycResult.Passed.ToString(), result.EkycResult);
        Assert.Equal(KycRiskLevel.Low.ToString(), result.RiskLevel);
        Assert.False(result.SubmittedWithManualFallback);
        Assert.Equal("Submission received. Your profile is pending admin review.", result.Message);
        Assert.Equal("1234xxxx9012", result.OcrCitizenIdMasked);

        context.ChangeTracker.Clear();

        var savedUser = await context.Users.SingleAsync(x => x.Id == user.Id);
        var savedKyc = await context.KycVerifications.SingleAsync(x => x.UserId == user.Id);
        var linkedAssets = await context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(KycVerification) && x.LinkedEntityId == savedKyc.Id)
            .ToListAsync();

        Assert.Equal(OnboardingStatus.KycPending, savedUser.OnboardingStatus);
        Assert.Equal(frontAsset.Id, savedKyc.FrontMediaAssetId);
        Assert.Equal(backAsset.Id, savedKyc.BackMediaAssetId);
        Assert.Equal(selfieAsset.Id, savedKyc.SelfieMediaAssetId);
        Assert.Equal("happy_hash", savedKyc.CitizenIdHash);
        Assert.Equal(3, linkedAssets.Count);
    }

    [Fact]
    public async Task SubmitAsync_ShouldCreatePendingReview_WhenEkycProviderFails()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "kyc-provider-failure@example.com", displayName: "Kyc Provider Failure");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/front-provider-failure.jpg", OriginalFileName = "front-provider-failure.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/back-provider-failure.jpg", OriginalFileName = "back-provider-failure.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/selfie-provider-failure.jpg", OriginalFileName = "selfie-provider-failure.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        _vnptEkycClient.VerifyAsyncFunc = _ => Task.FromResult(new VnptEkycClientResult
        {
            SessionId = "session-provider-failure",
            EkycResult = EkycResult.ProviderError.ToString(),
            IsProviderFailure = true,
            ErrorCode = "VNPT_OCR_HTTP",
            ErrorMessage = "VNPT HTTP 401 Unauthorized."
        });

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = KycDocumentType.CCCD.ToString(),
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id,
            ManualCitizenId = "012345678901",
            ManualFullName = "Nguyen Van Provider",
            ManualDateOfBirth = new DateTime(1999, 1, 2),
            ManualGender = "Nam",
            ManualAddress = "Da Nang"
        };

        var result = await kycService.SubmitAsync(user.Id, request, CancellationToken.None);

        Assert.Equal(KycVerificationStatus.PendingAdminReview.ToString(), result.Status);
        Assert.Equal(EkycResult.ProviderError.ToString(), result.EkycResult);
        Assert.Equal(KycRiskLevel.High.ToString(), result.RiskLevel);
        Assert.True(result.SubmittedWithManualFallback);
        Assert.Equal("VNPT không đọc được hồ sơ tự động. Thông tin bạn điền thủ công đã được gửi cho admin duyệt.", result.Message);

        context.ChangeTracker.Clear();

        var savedUser = await context.Users.SingleAsync(x => x.Id == user.Id);
        var savedKyc = await context.KycVerifications.SingleAsync(x => x.UserId == user.Id);
        var linkedAssets = await context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(KycVerification) && x.LinkedEntityId == savedKyc.Id)
            .ToListAsync();

        Assert.Equal(OnboardingStatus.KycPending, savedUser.OnboardingStatus);
        Assert.Equal(KycVerificationStatus.PendingAdminReview, savedKyc.Status);
        Assert.Equal(EkycResult.ProviderError, savedKyc.EkycResult);
        Assert.Equal("VNPT_OCR_HTTP", savedKyc.EkycErrorCode);
        Assert.Equal("Nguyen Van Provider", savedKyc.OcrFullName);
        Assert.Equal("0123xxxx8901", savedKyc.OcrCitizenIdMasked);
        Assert.Equal(3, linkedAssets.Count);
    }

    [Fact]
    public async Task SubmitAsync_ShouldThrowKycBusinessException_WhenDocumentTypeIsInvalid()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "invalid-doc@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "front.jpg", OriginalFileName = "front.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "back.jpg", OriginalFileName = "back.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "selfie.jpg", OriginalFileName = "selfie.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = "NationalId",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id
        };

        await Assert.ThrowsAsync<KycBusinessException>(() => kycService.SubmitAsync(user.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_ShouldThrowKycBusinessException_WhenCitizenIdAlreadyApprovedForAnotherUser()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "new-kyc@example.com");
        var approvedUser = TestDataBuilder.BuildUser(email: "approved-kyc@example.com");
        var approved = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = approvedUser.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            CitizenIdHash = "duplicate_hash",
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = KycVerificationStatus.Approved,
            SubmittedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.AddRange(user, approvedUser);
        context.KycVerifications.Add(approved);
        await context.SaveChangesAsync();

        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "front.jpg", OriginalFileName = "front.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "back.jpg", OriginalFileName = "back.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "selfie.jpg", OriginalFileName = "selfie.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Uploaded };
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = KycDocumentType.CCCD.ToString(),
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id
        };
        _hashService.HashSha256HexFunc = _ => "duplicate_hash";
        _sensitiveDataProtector.EncryptFunc = _ => "encrypted";
        _vnptEkycClient.VerifyAsyncFunc = _ => Task.FromResult(new VnptEkycClientResult
        {
            SessionId = "session-dup",
            EkycResult = EkycResult.Passed.ToString(),
            OcrCitizenId = "123456789012",
            DocumentCheckResult = DocumentCheckResult.Valid.ToString(),
            FaceMatchResult = FaceMatchResult.Matched.ToString(),
            LivenessResult = LivenessResult.Passed.ToString()
        });

        await Assert.ThrowsAsync<KycBusinessException>(() => kycService.SubmitAsync(user.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task GetMyStatusAsync_ShouldReturnLatestSubmission_WhenKycExists()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "status-kyc@example.com");
        var frontAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/status-front.jpg", OriginalFileName = "front.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Linked };
        var backAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/status-back.jpg", OriginalFileName = "back.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Linked };
        var selfieAsset = new MediaAsset { Id = Guid.NewGuid(), ObjectKey = "private/kyc-documents/status-selfie.jpg", OriginalFileName = "selfie.jpg", OwnerUserId = user.Id, Scope = MediaScope.KycDocument, Visibility = MediaVisibility.Private, Status = MediaStatus.Linked };
        var kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            CitizenIdHash = "hash",
            OcrFullName = "NGUYEN VAN B",
            EkycResult = EkycResult.NeedReview,
            RiskLevel = KycRiskLevel.Medium,
            Status = KycVerificationStatus.PendingAdminReview,
            FrontMediaAssetId = frontAsset.Id,
            BackMediaAssetId = backAsset.Id,
            SelfieMediaAssetId = selfieAsset.Id,
            SubmittedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        context.MediaAssets.AddRange(frontAsset, backAsset, selfieAsset);
        context.KycVerifications.Add(kyc);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);

        var result = await kycService.GetMyStatusAsync(user.Id, CancellationToken.None);

        Assert.True(result.HasSubmission);
        Assert.Equal(kyc.Id, result.KycId);
        Assert.Equal("NGUYEN VAN B", result.OcrFullName);
        Assert.Equal(frontAsset.Id, result.FrontMediaAssetId);
        Assert.Equal(backAsset.Id, result.BackMediaAssetId);
        Assert.Equal(selfieAsset.Id, result.SelfieMediaAssetId);
    }

    [Fact]
    public async Task GetMyHistoryAsync_ShouldReturnSubmissionsOrderedBySubmittedAtDescending()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "history-kyc@example.com");
        var older = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            CitizenIdHash = "hash1",
            EkycResult = EkycResult.Failed,
            RiskLevel = KycRiskLevel.High,
            Status = KycVerificationStatus.EkycFailed,
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var newer = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            CitizenIdHash = "hash2",
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = KycVerificationStatus.PendingAdminReview,
            SubmittedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        context.KycVerifications.AddRange(older, newer);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);

        var result = await kycService.GetMyHistoryAsync(user.Id, CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(newer.Id, result.Items[0].KycId);
        Assert.Equal(older.Id, result.Items[1].KycId);
    }

    private KycService CreateService(AppDbContext context)
    {
        return new KycService(
            context,
            _vnptEkycClient,
            _hashService,
            _mediaAccessService,
            _sensitiveDataProtector);
    }
}

#region Fakes for KycService
public class FakeVnptEkycClient : IVnptEkycClient
{
    public Func<VnptEkycVerifyInput, Task<VnptEkycClientResult>> VerifyAsyncFunc { get; set; } = _ => Task.FromResult(new VnptEkycClientResult());

    public Task<VnptEkycClientResult> VerifyAsync(VnptEkycVerifyInput input, CancellationToken cancellationToken = default)
        => VerifyAsyncFunc(input);
}

public sealed class FakeMediaAccessService : IMediaAccessService
{
    public Task<MediaAccessResult> OpenReadAsync(
        Guid mediaAssetId,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null)
    {
        return Task.FromResult(new MediaAccessResult
        {
            MediaAsset = new MediaAsset
            {
                Id = mediaAssetId,
                OriginalFileName = $"{mediaAssetId:N}.jpg",
                ContentType = "image/jpeg"
            },
            Stream = new MemoryStream(new byte[] { 1, 2, 3 }),
            ContentType = "image/jpeg",
            DownloadFileName = $"{mediaAssetId:N}.jpg"
        });
    }

    public Task<string> GetDownloadUrlAsync(
        Guid mediaAssetId,
        TimeSpan ttl,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default,
        MediaAuditContext? auditContext = null)
    {
        return Task.FromResult($"/api/media/private/{mediaAssetId:D}");
    }
}

public class FakeHashService : IHashService
{
    public Func<string, string> HashSha256HexFunc { get; set; } = val => val;

    public string HashSha256Hex(string value) => HashSha256HexFunc(value);
}

public class FakeSensitiveDataProtector : ISensitiveDataProtector
{
    public Func<string, string> EncryptFunc { get; set; } = val => val;
    public Func<string, string> DecryptFunc { get; set; } = val => val;

    public string Encrypt(string plainText) => EncryptFunc(plainText);
    public string Decrypt(string encryptedText) => DecryptFunc(encryptedText);
}
#endregion
