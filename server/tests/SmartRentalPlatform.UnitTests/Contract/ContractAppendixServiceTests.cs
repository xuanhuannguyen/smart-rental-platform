using System.Reflection;
using System.Text;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Infrastructure.Media;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Contract;

public class ContractAppendixServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture fixture;

    public ContractAppendixServiceTests(TestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        this.fixture.Reset();
    }

    [Fact]
    public async Task GenerateAppendixFileAsync_ShouldCreateMediaAssetsForRawAndMaskedVariants()
    {
        var appendix = await SeedAppendixGraphAsync();
        var mediaStorage = new RecordingMediaStorageService();
        var service = new ContractAppendixService(
            fixture.Context,
            new FakeContractSignatureOtpService(),
            new FakeContractPdfRenderer(),
            new QueueMediaObjectKeyFactory(
                "private/contract-appendix-pdfs/2026/07/10/raw.pdf",
                "private/contract-appendix-pdfs/2026/07/10/masked.pdf"),
            mediaStorage,
            new MediaAssetService(fixture.Context),
            new FakeHashService(),
            new FakeSensitiveDataProtector());

        await InvokeGenerateAppendixFileAsync(service, appendix, DateTimeOffset.UtcNow);
        await fixture.Context.SaveChangesAsync();

        var files = fixture.Context.ContractFiles
            .Where(x => x.RentalContractAppendixId == appendix.Id)
            .OrderBy(x => x.FileVariant)
            .ToList();

        Assert.Equal(2, files.Count);
        Assert.All(files, x => Assert.NotNull(x.MediaAssetId));
        Assert.Contains(files, x => x.FileVariant == ContractFileVariant.Raw);
        Assert.Contains(files, x => x.FileVariant == ContractFileVariant.Masked);
        Assert.Equal(2, fixture.Context.MediaAssets.Count());
        Assert.All(fixture.Context.MediaAssets, x => Assert.Equal(MediaScope.ContractAppendixPdf, x.Scope));
        Assert.Equal(2, mediaStorage.UploadRequests.Count);
    }

    private async Task<ContractAppendix> SeedAppendixGraphAsync()
    {
        var landlordId = Guid.NewGuid();
        var mainTenantId = Guid.NewGuid();
        var roomingHouseId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var appendixId = Guid.NewGuid();

        var landlord = new User
        {
            Id = landlordId,
            Email = "landlord@test.com",
            NormalizedEmail = "LANDLORD@TEST.COM",
            DisplayName = "Landlord",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };
        var mainTenant = new User
        {
            Id = mainTenantId,
            Email = "tenant@test.com",
            NormalizedEmail = "TENANT@TEST.COM",
            DisplayName = "Tenant",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };

        var roomingHouse = new RoomingHouse
        {
            Id = roomingHouseId,
            LandlordUserId = landlordId,
            Landlord = landlord,
            Name = "House",
            AddressLine = "Addr",
            WardCode = "001",
            ProvinceCode = "001",
            AddressDisplay = "Addr Display",
            ApprovalStatus = RoomingHouseApprovalStatus.Approved,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var room = new Room
        {
            Id = roomId,
            RoomingHouseId = roomingHouseId,
            RoomingHouse = roomingHouse,
            RoomNumber = "101",
            Status = RoomStatus.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var contract = new RentalContract
        {
            Id = contractId,
            RentalRequestId = Guid.NewGuid(),
            RoomDepositId = Guid.NewGuid(),
            RoomId = roomId,
            MainTenantUserId = mainTenantId,
            MainTenantUser = mainTenant,
            Room = room,
            ContractNumber = "C-001",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2027, 7, 1),
            MonthlyRent = 100,
            DepositAmount = 100,
            PaymentDay = 5,
            Status = RentalContractStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var appendix = new ContractAppendix
        {
            Id = appendixId,
            RentalContractId = contractId,
            RentalContract = contract,
            AppendixNumber = "PL-001",
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            Status = ContractAppendixStatus.PendingSignature,
            CreatedByUserId = landlordId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        contract.Appendices.Add(appendix);

        fixture.Context.Users.AddRange(landlord, mainTenant);
        fixture.Context.RoomingHouses.Add(roomingHouse);
        fixture.Context.Rooms.Add(room);
        fixture.Context.RentalContracts.Add(contract);
        fixture.Context.ContractAppendices.Add(appendix);
        await fixture.Context.SaveChangesAsync();

        return appendix;
    }

    private static async Task InvokeGenerateAppendixFileAsync(
        ContractAppendixService service,
        ContractAppendix appendix,
        DateTimeOffset now)
    {
        var method = typeof(ContractAppendixService).GetMethod(
            "GenerateAppendixFileAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task)method!.Invoke(service, [appendix, now, CancellationToken.None])!;
        await task;
    }

    private sealed class FakeContractSignatureOtpService : IContractSignatureOtpService
    {
        public Task<RequestContractSignatureOtpResponse?> RequestOtpAsync(Guid userId, Guid contractId, ContractSignerRole signerRole, CancellationToken cancellationToken = default)
            => Task.FromResult<RequestContractSignatureOtpResponse?>(null);

        public Task VerifyAndConsumeOtpAsync(Guid userId, Guid contractId, ContractSignerRole signerRole, string? otp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<RequestContractSignatureOtpResponse?> RequestAppendixOtpAsync(Guid userId, Guid contractId, Guid appendixId, ContractSignerRole signerRole, CancellationToken cancellationToken = default)
            => Task.FromResult<RequestContractSignatureOtpResponse?>(null);

        public Task VerifyAndConsumeAppendixOtpAsync(Guid userId, Guid appendixId, ContractSignerRole signerRole, string? otp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeContractPdfRenderer : IContractPdfRenderer
    {
        public byte[] RenderSignedRentalContract(RentalContract contract, ContractRenderOptions options)
            => Encoding.UTF8.GetBytes("contract");

        public byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options)
            => Encoding.UTF8.GetBytes($"appendix-{appendix.Id}-{options.ViewerMode}");
    }

    private sealed class QueueMediaObjectKeyFactory : IMediaObjectKeyFactory
    {
        private readonly Queue<string> objectKeys;

        public QueueMediaObjectKeyFactory(params string[] objectKeys)
        {
            this.objectKeys = new Queue<string>(objectKeys);
        }

        public MediaObjectKeyResult Create(MediaScope scope, MediaVisibility visibility, string originalFileName)
        {
            var objectKey = objectKeys.Count > 0
                ? objectKeys.Dequeue()
                : $"private/contract-appendix-pdfs/{Guid.NewGuid():N}.pdf";

            return new MediaObjectKeyResult
            {
                ObjectKey = objectKey,
                StoredFileName = Path.GetFileName(objectKey)
            };
        }
    }

    private sealed class RecordingMediaStorageService : IMediaStorageService
    {
        public List<MediaUploadRequest> UploadRequests { get; } = [];

        public string GetBucketName()
        {
            return "local-media";
        }

        public Task<MediaStoredObjectResult> UploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            UploadRequests.Add(request);
            return Task.FromResult(new MediaStoredObjectResult
            {
                BucketName = "local-media",
                ObjectKey = request.ObjectKey,
                StoredFileName = Path.GetFileName(request.ObjectKey)
            });
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("media-storage"));
            return Task.FromResult(stream);
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<MediaObjectMetadataResult> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new MediaObjectMetadataResult
            {
                ObjectKey = objectKey,
                ContentType = "application/pdf",
                FileSize = 100
            });

        public Task<MediaUploadUrlResult> GetUploadUrlAsync(
            string objectKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MediaUploadUrlResult
            {
                Url = $"https://example.test/upload/{objectKey}",
                HttpMethod = "PUT",
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl)
            });

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
            => Task.FromResult($"/download/{objectKey}");
    }

    private sealed class FakeHashService : IHashService
    {
        public string HashSha256Hex(string value) => value;
    }

    private sealed class FakeSensitiveDataProtector : ISensitiveDataProtector
    {
        public string Encrypt(string plainText) => plainText;

        public string Decrypt(string encryptedText) => encryptedText;
    }
}
