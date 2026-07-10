using System.Text;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.RentalContracts;
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

public class ContractFileServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture fixture;

    public ContractFileServiceTests(TestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        this.fixture.Reset();
    }

    [Fact]
    public async Task GenerateSignedContractFileAsync_ShouldCreateMediaAssetsAndKeepLegacyStorageObjectKey()
    {
        var graph = await SeedContractGraphAsync();
        var mediaStorage = new RecordingMediaStorageService();
        var service = new ContractFileService(
            fixture.Context,
            new FakeContractPdfRenderer(),
            new FakePrivateStorageService(),
            new QueueMediaObjectKeyFactory(
                "private/contract-pdfs/2026/07/10/raw.pdf",
                "private/contract-pdfs/2026/07/10/masked.pdf"),
            mediaStorage,
            new MediaAssetService(fixture.Context),
            new FakeMediaAccessService(),
            new FakeSensitiveDataProtector());

        var response = await service.GenerateSignedContractFileAsync(graph.LandlordId, graph.ContractId);

        Assert.NotNull(response);
        Assert.NotNull(response!.MediaAssetId);
        Assert.Equal("private/contract-pdfs/2026/07/10/raw.pdf", response.StorageObjectKey);

        var contractFiles = fixture.Context.ContractFiles
            .Where(x => x.RentalContractId == graph.ContractId && x.RentalContractAppendixId == null)
            .OrderBy(x => x.FileVariant)
            .ToList();

        Assert.Equal(2, contractFiles.Count);
        Assert.All(contractFiles, x => Assert.NotNull(x.MediaAssetId));
        Assert.Contains(contractFiles, x => x.FileVariant == ContractFileVariant.Raw);
        Assert.Contains(contractFiles, x => x.FileVariant == ContractFileVariant.Masked);
        Assert.Equal(2, fixture.Context.MediaAssets.Count());
        Assert.Equal(2, mediaStorage.UploadRequests.Count);
    }

    [Fact]
    public async Task OpenFileAsync_WhenMediaAssetIdExists_ShouldUseMediaAccessService()
    {
        var graph = await SeedContractGraphAsync();
        var mediaAssetId = Guid.NewGuid();
        fixture.Context.ContractFiles.Add(new ContractFile
        {
            Id = Guid.NewGuid(),
            RentalContractId = graph.ContractId,
            MediaAssetId = mediaAssetId,
            StorageObjectKey = "private/contract-pdfs/2026/07/10/raw.pdf",
            FileVariant = ContractFileVariant.Raw,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var privateStorage = new FakePrivateStorageService();
        var mediaAccess = new FakeMediaAccessService();
        var service = new ContractFileService(
            fixture.Context,
            new FakeContractPdfRenderer(),
            privateStorage,
            new QueueMediaObjectKeyFactory(),
            new RecordingMediaStorageService(),
            new MediaAssetService(fixture.Context),
            mediaAccess,
            new FakeSensitiveDataProtector());

        var result = await service.OpenFileAsync(
            graph.LandlordId,
            graph.ContractId,
            fixture.Context.ContractFiles.Single().Id);

        Assert.NotNull(result);
        Assert.Equal(mediaAssetId, mediaAccess.LastMediaAssetId);
        Assert.Equal(0, privateStorage.OpenReadCallCount);
    }

    [Fact]
    public async Task OpenFileAsync_WhenLegacyFileHasNoMediaAssetId_ShouldFallbackToPrivateStorage()
    {
        var graph = await SeedContractGraphAsync();
        var fileId = Guid.NewGuid();
        fixture.Context.ContractFiles.Add(new ContractFile
        {
            Id = fileId,
            RentalContractId = graph.ContractId,
            MediaAssetId = null,
            StorageObjectKey = "contracts/legacy-raw.pdf",
            FileVariant = ContractFileVariant.Raw,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var privateStorage = new FakePrivateStorageService();
        var service = new ContractFileService(
            fixture.Context,
            new FakeContractPdfRenderer(),
            privateStorage,
            new QueueMediaObjectKeyFactory(),
            new RecordingMediaStorageService(),
            new MediaAssetService(fixture.Context),
            new FakeMediaAccessService(),
            new FakeSensitiveDataProtector());

        var result = await service.OpenFileAsync(graph.LandlordId, graph.ContractId, fileId);

        Assert.NotNull(result);
        Assert.Equal(1, privateStorage.OpenReadCallCount);
        Assert.Equal("legacy content", await new StreamReader(result!.Value.Content).ReadToEndAsync());
    }

    private async Task<(Guid ContractId, Guid LandlordId, Guid MainTenantId, Guid OccupantId)> SeedContractGraphAsync()
    {
        var landlordId = Guid.NewGuid();
        var mainTenantId = Guid.NewGuid();
        var occupantId = Guid.NewGuid();
        var roomingHouseId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

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
            DisplayName = "Main Tenant",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };
        var occupant = new User
        {
            Id = occupantId,
            Email = "occupant@test.com",
            NormalizedEmail = "OCCUPANT@TEST.COM",
            DisplayName = "Occupant",
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
            UpdatedAt = DateTimeOffset.UtcNow,
            Occupants =
            [
                new ContractOccupant
                {
                    Id = Guid.NewGuid(),
                    RentalContractId = contractId,
                    UserId = occupantId,
                    User = occupant,
                    FullName = "Occupant User",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    MoveInDate = new DateOnly(2026, 7, 1),
                    Status = ContractOccupantStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ],
            Signatures =
            [
                new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    RentalContractId = contractId,
                    SignerUserId = landlordId,
                    SignerRole = ContractSignerRole.Landlord,
                    SignatureMethod = ContractSignatureMethod.EmailOtp,
                    SignedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    RentalContractId = contractId,
                    SignerUserId = mainTenantId,
                    SignerRole = ContractSignerRole.Tenant,
                    SignatureMethod = ContractSignatureMethod.EmailOtp,
                    SignedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        fixture.Context.Users.AddRange(landlord, mainTenant, occupant);
        fixture.Context.RoomingHouses.Add(roomingHouse);
        fixture.Context.Rooms.Add(room);
        fixture.Context.RentalContracts.Add(contract);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        return (contractId, landlordId, mainTenantId, occupantId);
    }

    private sealed class FakeContractPdfRenderer : IContractPdfRenderer
    {
        public byte[] RenderSignedRentalContract(RentalContract contract, ContractRenderOptions options)
        {
            return Encoding.UTF8.GetBytes($"pdf-{contract.Id}-{options.ViewerMode}");
        }

        public byte[] RenderSignedContractAppendix(ContractAppendix appendix, ContractRenderOptions options)
        {
            return Encoding.UTF8.GetBytes("appendix");
        }
    }

    private sealed class FakePrivateStorageService : IPrivateStorageService
    {
        public int OpenReadCallCount { get; private set; }

        public Task<string> UploadAsync(Stream content, string contentType, string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(objectKey);
        }

        public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            OpenReadCallCount++;
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("legacy content"));
            return Task.FromResult(stream);
        }
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
                : $"private/contract-pdfs/{Guid.NewGuid():N}.pdf";

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
        {
            return Task.FromResult(false);
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"/uploads/{objectKey}");
        }
    }

    private sealed class FakeMediaAccessService : IMediaAccessService
    {
        public Guid? LastMediaAssetId { get; private set; }

        public Task<MediaAccessResult> OpenReadAsync(Guid mediaAssetId, Guid? actorUserId = null, CancellationToken cancellationToken = default)
        {
            LastMediaAssetId = mediaAssetId;
            return Task.FromResult(new MediaAccessResult
            {
                MediaAsset = new MediaAsset
                {
                    Id = mediaAssetId,
                    OriginalFileName = "from-media.pdf",
                    ContentType = "application/pdf"
                },
                Stream = new MemoryStream(Encoding.UTF8.GetBytes("media content")),
                ContentType = "application/pdf",
                DownloadFileName = "from-media.pdf"
            });
        }

        public Task<string> GetDownloadUrlAsync(Guid mediaAssetId, TimeSpan ttl, Guid? actorUserId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"/download/{mediaAssetId}");
        }
    }

    private sealed class FakeSensitiveDataProtector : ISensitiveDataProtector
    {
        public string Encrypt(string plainText) => plainText;

        public string Decrypt(string encryptedText) => encryptedText;
    }
}
