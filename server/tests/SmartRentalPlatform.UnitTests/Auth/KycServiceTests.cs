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
using SmartRentalPlatform.Infrastructure.Media;
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
    private readonly QueueMediaObjectKeyFactory _mediaObjectKeyFactory;
    private readonly RecordingMediaStorageService _mediaStorageService;
    private readonly MediaAssetService _mediaAssetService;
    private readonly FakeVnptEkycClient _vnptEkycClient;
    private readonly FakeHashService _hashService;
    private readonly FakeSensitiveDataProtector _sensitiveDataProtector;

    public KycServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _mediaObjectKeyFactory = new QueueMediaObjectKeyFactory();
        _mediaStorageService = new RecordingMediaStorageService();
        _mediaAssetService = new MediaAssetService(_fixture.Context);
        _vnptEkycClient = new FakeVnptEkycClient();
        _hashService = new FakeHashService();
        _sensitiveDataProtector = new FakeSensitiveDataProtector();
    }

    private static IFormFile CreateMockFile()
    {
        return new FakeFormFile("dummy image data");
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
            FrontImage = null!, // Missing
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
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

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = "CCCD",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontImage = CreateMockFile(),
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
        };

        _mediaObjectKeyFactory.Enqueue(
            "private/kyc-documents/2026/07/10/front-file.jpg",
            "private/kyc-documents/2026/07/10/back-file.jpg",
            "private/kyc-documents/2026/07/10/selfie-file.jpg");

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
        Assert.Equal(3, _mediaStorageService.UploadRequests.Count);
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
    public async Task SubmitAsync_ShouldThrowKycBusinessException_WhenDocumentTypeIsInvalid()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "invalid-doc@example.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = "NationalId",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontImage = CreateMockFile(),
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
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
            FrontImageObjectKey = "front",
            BackImageObjectKey = "back",
            SelfieImageObjectKey = "selfie",
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

        var kycService = CreateService(context);
        var request = new SubmitKycRequest
        {
            DocumentType = KycDocumentType.CCCD.ToString(),
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontImage = CreateMockFile(),
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
        };

        _mediaObjectKeyFactory.Enqueue(
            "private/kyc-documents/2026/07/10/front-dup.jpg",
            "private/kyc-documents/2026/07/10/back-dup.jpg",
            "private/kyc-documents/2026/07/10/selfie-dup.jpg");
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
        Assert.Equal(3, _mediaStorageService.DeletedObjectKeys.Count);
    }

    [Fact]
    public async Task GetMyStatusAsync_ShouldReturnLatestSubmission_WhenKycExists()
    {
        var context = _fixture.Context;
        var user = TestDataBuilder.BuildUser(email: "status-kyc@example.com");
        var kyc = new KycVerification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            FrontImageObjectKey = "front",
            BackImageObjectKey = "back",
            SelfieImageObjectKey = "selfie",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload,
            CitizenIdHash = "hash",
            OcrFullName = "NGUYEN VAN B",
            EkycResult = EkycResult.NeedReview,
            RiskLevel = KycRiskLevel.Medium,
            Status = KycVerificationStatus.PendingAdminReview,
            SubmittedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        context.KycVerifications.Add(kyc);
        await context.SaveChangesAsync();

        var kycService = CreateService(context);

        var result = await kycService.GetMyStatusAsync(user.Id, CancellationToken.None);

        Assert.True(result.HasSubmission);
        Assert.Equal(kyc.Id, result.KycId);
        Assert.Equal("NGUYEN VAN B", result.OcrFullName);
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
            FrontImageObjectKey = "front1",
            BackImageObjectKey = "back1",
            SelfieImageObjectKey = "selfie1",
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
            FrontImageObjectKey = "front2",
            BackImageObjectKey = "back2",
            SelfieImageObjectKey = "selfie2",
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
            _mediaObjectKeyFactory,
            _mediaStorageService,
            _mediaAssetService,
            _vnptEkycClient,
            _hashService,
            _sensitiveDataProtector);
    }
}

#region Fakes for KycService
public class FakeFormFile : IFormFile
{
    private readonly MemoryStream _stream;

    public FakeFormFile(string content, string fileName = "test.jpg", string contentType = "image/jpeg")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        _stream = new MemoryStream(bytes);
        Length = bytes.Length;
        FileName = fileName;
        ContentType = contentType;
    }

    public string ContentType { get; }
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length { get; }
    public string Name => "file";
    public string FileName { get; }

    public Stream OpenReadStream()
    {
        return new MemoryStream(_stream.ToArray());
    }

    public void CopyTo(Stream target)
    {
        _stream.Position = 0;
        _stream.CopyTo(target);
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        _stream.Position = 0;
        return _stream.CopyToAsync(target, cancellationToken);
    }
}

public class QueueMediaObjectKeyFactory : IMediaObjectKeyFactory
{
    private readonly Queue<string> _objectKeys = new();

    public void Enqueue(params string[] objectKeys)
    {
        foreach (var objectKey in objectKeys)
        {
            _objectKeys.Enqueue(objectKey);
        }
    }

    public MediaObjectKeyResult Create(MediaScope scope, MediaVisibility visibility, string originalFileName)
    {
        return new MediaObjectKeyResult
        {
            ObjectKey = _objectKeys.Count > 0
                ? _objectKeys.Dequeue()
                : $"private/kyc-documents/{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}",
            StoredFileName = originalFileName
        };
    }
}

public class RecordingMediaStorageService : IMediaStorageService
{
    public List<MediaUploadRequest> UploadRequests { get; } = new();
    public List<string> DeletedObjectKeys { get; } = new();

    public async Task<MediaStoredObjectResult> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
    {
        UploadRequests.Add(request);

        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);

        return new MediaStoredObjectResult
        {
            BucketName = "test-media-bucket",
            ObjectKey = request.ObjectKey,
            PublicUrl = null,
            StoredFileName = Path.GetFileName(request.ObjectKey)
        };
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        DeletedObjectKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.FromResult($"/media/{objectKey}");
}

public class FakeVnptEkycClient : IVnptEkycClient
{
    public Func<VnptEkycVerifyInput, Task<VnptEkycClientResult>> VerifyAsyncFunc { get; set; } = _ => Task.FromResult(new VnptEkycClientResult());

    public Task<VnptEkycClientResult> VerifyAsync(VnptEkycVerifyInput input, CancellationToken cancellationToken = default)
        => VerifyAsyncFunc(input);
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
