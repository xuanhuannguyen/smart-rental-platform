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

namespace SmartRentalPlatform.UnitTests.Media;

public class DefaultMediaPermissionServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture fixture;

    public DefaultMediaPermissionServiceTests(TestDatabaseFixture fixture)
    {
        this.fixture = fixture;
        this.fixture.Reset();
    }

    [Fact]
    public async Task CanViewAsync_ForRawContractFile_ShouldAllowMainTenantAndDenyOccupant()
    {
        var graph = await SeedContractFileGraphAsync(ContractFileVariant.Raw);
        var service = new DefaultMediaPermissionService(fixture.Context);

        var mainTenantAllowed = await service.CanViewAsync(graph.MainTenantId, graph.MediaAsset);
        var occupantAllowed = await service.CanViewAsync(graph.OccupantId, graph.MediaAsset);

        Assert.True(mainTenantAllowed);
        Assert.False(occupantAllowed);
    }

    [Fact]
    public async Task CanViewAsync_ForMaskedContractFile_ShouldAllowOccupant()
    {
        var graph = await SeedContractFileGraphAsync(ContractFileVariant.Masked);
        var service = new DefaultMediaPermissionService(fixture.Context);

        var occupantAllowed = await service.CanViewAsync(graph.OccupantId, graph.MediaAsset);
        var outsiderAllowed = await service.CanViewAsync(Guid.NewGuid(), graph.MediaAsset);

        Assert.True(occupantAllowed);
        Assert.False(outsiderAllowed);
    }

    [Fact]
    public async Task CanViewAsync_ForRawAppendixFile_ShouldAllowPreviousMainTenantAndDenyOccupant()
    {
        var graph = await SeedAppendixFileGraphAsync(ContractFileVariant.Raw);
        var service = new DefaultMediaPermissionService(fixture.Context);

        var previousMainTenantAllowed = await service.CanViewAsync(graph.PreviousMainTenantId, graph.MediaAsset);
        var occupantAllowed = await service.CanViewAsync(graph.OccupantId, graph.MediaAsset);

        Assert.True(previousMainTenantAllowed);
        Assert.False(occupantAllowed);
    }

    [Fact]
    public async Task CanViewAsync_ForMaskedAppendixFile_ShouldAllowOccupantAndDenyOutsider()
    {
        var graph = await SeedAppendixFileGraphAsync(ContractFileVariant.Masked);
        var service = new DefaultMediaPermissionService(fixture.Context);

        var occupantAllowed = await service.CanViewAsync(graph.OccupantId, graph.MediaAsset);
        var outsiderAllowed = await service.CanViewAsync(Guid.NewGuid(), graph.MediaAsset);

        Assert.True(occupantAllowed);
        Assert.False(outsiderAllowed);
    }

    private async Task<(Guid MainTenantId, Guid OccupantId, MediaAsset MediaAsset)> SeedContractFileGraphAsync(ContractFileVariant variant)
    {
        var landlordId = Guid.NewGuid();
        var mainTenantId = Guid.NewGuid();
        var occupantId = Guid.NewGuid();
        var roomingHouseId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var mediaAssetId = Guid.NewGuid();

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
        var mediaAsset = new MediaAsset
        {
            Id = mediaAssetId,
            BucketName = "local-media",
            ObjectKey = $"private/contract-pdfs/{mediaAssetId:N}.pdf",
            OriginalFileName = "contract.pdf",
            StoredFileName = "contract.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            Scope = MediaScope.ContractPdf,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(ContractFile),
            LinkedEntityId = fileId,
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
                    FullName = "Occupant",
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
            ],
            Files =
            [
                new ContractFile
                {
                    Id = fileId,
                    RentalContractId = contractId,
                    MediaAssetId = mediaAssetId,
                    MediaAsset = mediaAsset,
                    StorageObjectKey = mediaAsset.ObjectKey,
                    FileVariant = variant,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        fixture.Context.Users.AddRange(landlord, mainTenant, occupant);
        fixture.Context.RoomingHouses.Add(roomingHouse);
        fixture.Context.Rooms.Add(room);
        fixture.Context.MediaAssets.Add(mediaAsset);
        fixture.Context.RentalContracts.Add(contract);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        return (mainTenantId, occupantId, mediaAsset);
    }

    private async Task<(Guid PreviousMainTenantId, Guid CurrentMainTenantId, Guid OccupantId, MediaAsset MediaAsset)> SeedAppendixFileGraphAsync(ContractFileVariant variant)
    {
        var landlordId = Guid.NewGuid();
        var previousMainTenantId = Guid.NewGuid();
        var currentMainTenantId = Guid.NewGuid();
        var occupantId = Guid.NewGuid();
        var roomingHouseId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var appendixId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var mediaAssetId = Guid.NewGuid();

        var landlord = new User
        {
            Id = landlordId,
            Email = "landlord2@test.com",
            NormalizedEmail = "LANDLORD2@TEST.COM",
            DisplayName = "Landlord",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };
        var previousMainTenant = new User
        {
            Id = previousMainTenantId,
            Email = "prev-tenant@test.com",
            NormalizedEmail = "PREV-TENANT@TEST.COM",
            DisplayName = "Previous Tenant",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };
        var currentMainTenant = new User
        {
            Id = currentMainTenantId,
            Email = "current-tenant@test.com",
            NormalizedEmail = "CURRENT-TENANT@TEST.COM",
            DisplayName = "Current Tenant",
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed
        };
        var occupant = new User
        {
            Id = occupantId,
            Email = "occupant2@test.com",
            NormalizedEmail = "OCCUPANT2@TEST.COM",
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
            RoomNumber = "201",
            Status = RoomStatus.Occupied,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var mediaAsset = new MediaAsset
        {
            Id = mediaAssetId,
            BucketName = "local-media",
            ObjectKey = $"private/contract-appendix-pdfs/{mediaAssetId:N}.pdf",
            OriginalFileName = "appendix.pdf",
            StoredFileName = "appendix.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            Scope = MediaScope.ContractAppendixPdf,
            Visibility = MediaVisibility.Private,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(ContractFile),
            LinkedEntityId = fileId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var contract = new RentalContract
        {
            Id = contractId,
            RentalRequestId = Guid.NewGuid(),
            RoomDepositId = Guid.NewGuid(),
            RoomId = roomId,
            MainTenantUserId = currentMainTenantId,
            MainTenantUser = currentMainTenant,
            Room = room,
            ContractNumber = "C-002",
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
                    FullName = "Occupant",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    MoveInDate = new DateOnly(2026, 7, 1),
                    Status = ContractOccupantStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var appendix = new ContractAppendix
        {
            Id = appendixId,
            RentalContractId = contractId,
            RentalContract = contract,
            AppendixNumber = "PL-002",
            EffectiveDate = new DateOnly(2026, 8, 1),
            Status = ContractAppendixStatus.Active,
            CreatedByUserId = landlordId,
            AppliedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Changes =
            [
                new ContractAppendixChange
                {
                    Id = Guid.NewGuid(),
                    RentalContractAppendixId = appendixId,
                    ChangeType = ContractAppendixChangeType.Update,
                    TargetType = ContractAppendixTargetType.Contract,
                    FieldName = "mainTenantUserId",
                    OldValue = previousMainTenantId.ToString(),
                    NewValue = currentMainTenantId.ToString(),
                    SortOrder = 1,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            Signatures =
            [
                new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    RentalContractAppendixId = appendixId,
                    SignerUserId = previousMainTenantId,
                    SignerRole = ContractSignerRole.Tenant,
                    SignatureMethod = ContractSignatureMethod.EmailOtp,
                    SignedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            Files =
            [
                new ContractFile
                {
                    Id = fileId,
                    RentalContractId = contractId,
                    RentalContractAppendixId = appendixId,
                    MediaAssetId = mediaAssetId,
                    MediaAsset = mediaAsset,
                    StorageObjectKey = mediaAsset.ObjectKey,
                    FileVariant = variant,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        contract.Appendices.Add(appendix);

        fixture.Context.Users.AddRange(landlord, previousMainTenant, currentMainTenant, occupant);
        fixture.Context.RoomingHouses.Add(roomingHouse);
        fixture.Context.Rooms.Add(room);
        fixture.Context.MediaAssets.Add(mediaAsset);
        fixture.Context.RentalContracts.Add(contract);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        return (previousMainTenantId, currentMainTenantId, occupantId, mediaAsset);
    }
}
