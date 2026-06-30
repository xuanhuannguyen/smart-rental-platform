using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Contracts.Kyc;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.UnitTests.Common;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Auth;

public class KycServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly FakePrivateStorageService _storage;
    private readonly FakeVnptEkycClient _vnptEkycClient;
    private readonly FakeHashService _hashService;
    private readonly FakeSensitiveDataProtector _sensitiveDataProtector;

    public KycServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _storage = new FakePrivateStorageService();
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
        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);
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

        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);
        var request = new SubmitKycRequest
        {
            DocumentType = "CCCD",
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontImage = CreateMockFile(),
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
        };

        _storage.UploadAsyncFunc = (s, ct, ok) => Task.FromResult("storage_key_123");

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

        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetMyStatusAsync_ShouldReturnHasNoSubmission_WhenNoKycExists()
    {
        var context = _fixture.Context;
        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);

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

        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);
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

        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);
        var request = new SubmitKycRequest
        {
            DocumentType = KycDocumentType.CCCD.ToString(),
            SelfieCaptureMethod = SelfieCaptureMethod.Upload.ToString(),
            FrontImage = CreateMockFile(),
            BackImage = CreateMockFile(),
            SelfieImage = CreateMockFile()
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

        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);

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

        var kycService = new KycService(context, _storage, _vnptEkycClient, _hashService, _sensitiveDataProtector);

        var result = await kycService.GetMyHistoryAsync(user.Id, CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        Assert.Equal(newer.Id, result.Items[0].KycId);
        Assert.Equal(older.Id, result.Items[1].KycId);
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

public class FakePrivateStorageService : IPrivateStorageService
{
    public Func<Stream, string, string, Task<string>> UploadAsyncFunc { get; set; } = (_, _, _) => Task.FromResult("key");
    public Func<string, Task<Stream>> OpenReadAsyncFunc { get; set; } = _ => Task.FromResult<Stream>(new MemoryStream());

    public Task<string> UploadAsync(Stream content, string contentType, string objectKey, CancellationToken cancellationToken = default)
        => UploadAsyncFunc(content, contentType, objectKey);

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        => OpenReadAsyncFunc(objectKey);
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
