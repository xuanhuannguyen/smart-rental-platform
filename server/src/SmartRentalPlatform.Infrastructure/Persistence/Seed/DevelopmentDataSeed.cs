using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;

using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Payments;
using WalletAccountStatus = SmartRentalPlatform.Domain.Enums.Payments.WalletAccountStatus;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class DevelopmentDataSeed
{
    public const string AdminEmail = "admin.demo@example.com";
    public const string TenantEmail = "tenant.demo@example.com"; // "tenant.demo@example.com";
    public const string LandlordEmail = "landlord.demo@example.com"; // "landlord.demo@example.com";
    public const string CoTenantEmail = "cotenant.demo@example.com";
    public const string DemoPassword = "Demo@123456";

    private static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000099");
    private static readonly Guid TenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CoTenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid DummyLandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000009999");
    private static readonly Guid TenantApprovedKycId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ApprovedHouseId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ApprovedHouseRuleId = Guid.Parse("21000000-0000-0000-0000-000000000001");
    private static readonly Guid DraftHouseId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid SunriseHouseId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid GreenViewHouseId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid PendingHouseId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    private static readonly Guid RejectedHouseId = Guid.Parse("20000000-0000-0000-0000-000000000006");
    private static readonly Guid Room101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    private static readonly Guid Room102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    private static readonly Guid Room201Id = Guid.Parse("30000000-0000-0000-0000-000000000201");
    private static readonly Guid Room202Id = Guid.Parse("30000000-0000-0000-0000-000000000202");
    private static readonly Guid Room301Id = Guid.Parse("30000000-0000-0000-0000-000000000301");
    private static readonly Guid SunriseRoomA1Id = Guid.Parse("30000000-0000-0000-0000-000000000401");
    private static readonly Guid SunriseRoomA2Id = Guid.Parse("30000000-0000-0000-0000-000000000402");
    private static readonly Guid SunriseRoomB1Id = Guid.Parse("30000000-0000-0000-0000-000000000403");
    private static readonly Guid GreenViewRoom101Id = Guid.Parse("30000000-0000-0000-0000-000000000501");
    private static readonly Guid GreenViewRoom102Id = Guid.Parse("30000000-0000-0000-0000-000000000502");
    private static readonly Guid PendingRoomG1Id = Guid.Parse("30000000-0000-0000-0000-000000000601");
    private static readonly Guid ActiveContractId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid LinhContractId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid MinhEndedContractId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid TenantWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid TenantLinhWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly Guid TenantMinhWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000004");
    private static readonly Guid LandlordMaiWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000005");
    private static readonly Guid ElectricServiceTypeId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid WaterServiceTypeId = Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid InternetServiceTypeId = Guid.Parse("80000000-0000-0000-0000-000000000003");
    private static readonly Guid TrashServiceTypeId = Guid.Parse("80000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PlaceholderImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5Wv7sAAAAASUVORK5CYII=");

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordService passwordService,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken = default)
    {
        var location = await GetSeedLocationAsync(context, cancellationToken);
        if (location is null)
        {
            return;
        }

        await SeedUsersAsync(context, passwordService, cancellationToken);
        await SeedApprovedKycAsync(context, cancellationToken);
        await SeedBillingServiceTypesAsync(context, cancellationToken);
        await SeedRoomingHousesAsync(
            context,
            location,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        await SeedRoomsAsync(context, cancellationToken);
        await LargeScaleRoomingHouseSeeder.SeedAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        await BackfillPublicSeedMediaAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        // await SeedAdditionalRoomsAsync(context, cancellationToken);
        // await SeedBillingAsync(context, cancellationToken);
    }

    public static async Task SeedAdminAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken = default)
    {
        var admin = await EnsureSeedUserAsync(
            context,
            passwordService,
            AdminUserId,
            AdminEmail,
            "Admin Demo",
            RoleSeed.AdminRoleId,
            cancellationToken);

        if (!await context.UserProfiles.AnyAsync(x => x.UserId == admin.Id, cancellationToken))
        {
            context.UserProfiles.Add(CreateProfile(admin.Id, "Admin Demo"));
        }

        if (!admin.EmailConfirmed || admin.OnboardingStatus != OnboardingStatus.Completed)
        {
            admin.EmailConfirmed = true;
            admin.OnboardingStatus = OnboardingStatus.Completed;
            admin.Status = UserStatus.Active;
            admin.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await SeedApprovedTenantAsync(context, passwordService, admin.Id, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedApprovedTenantAsync(
        AppDbContext context,
        IPasswordService passwordService,
        Guid reviewedByAdminId,
        CancellationToken cancellationToken)
    {
        var tenant = await EnsureSeedUserAsync(
            context,
            passwordService,
            TenantUserId,
            TenantEmail,
            "Nguyen Tenant Demo",
            RoleSeed.TenantRoleId,
            cancellationToken);

        tenant.Status = UserStatus.Active;
        tenant.OnboardingStatus = OnboardingStatus.Completed;
        tenant.EmailConfirmed = true;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        var profile = await context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == tenant.Id, cancellationToken);

        if (profile is null)
        {
            profile = CreateProfile(tenant.Id, "Nguyen Tenant Demo");
            context.UserProfiles.Add(profile);
        }

        profile.FullName = "Nguyen Tenant Demo";
        profile.DateOfBirth = new DateOnly(1998, 1, 1);
        profile.Gender = "Male";
        profile.AddressLine = "123 Test Street, Ho Chi Minh City";
        profile.VerifiedCitizenIdMasked = "079********001";
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        if (!await context.KycVerifications.AnyAsync(
            x => x.UserId == tenant.Id && x.Status == KycVerificationStatus.Approved,
            cancellationToken))
        {
            context.KycVerifications.Add(new KycVerification
            {
                Id = TenantApprovedKycId,
                UserId = tenant.Id,
                DocumentType = KycDocumentType.CCCD,
                EkycProvider = EkycProvider.VNPT,
                EkycSessionId = "dev-approved-tenant-session",
                SelfieCaptureMethod = SelfieCaptureMethod.Upload,
                OcrFullName = "Nguyen Tenant Demo",
                OcrCitizenIdMasked = "079********001",
                CitizenIdHash = "dev-approved-tenant-citizen-id-hash",
                OcrDateOfBirth = new DateOnly(1998, 1, 1),
                OcrGender = "Male",
                OcrAddress = "123 Test Street, Ho Chi Minh City",
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
    }

    private static async Task SeedUsersAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(
            x => x.Id == CoTenantUserId || x.NormalizedEmail == CoTenantEmail.ToUpperInvariant(),
            cancellationToken))
        {
            context.Users.Add(CreateUser(CoTenantUserId, CoTenantEmail, "Lê CoTenant Demo", passwordService));
            context.UserProfiles.Add(CreateProfile(CoTenantUserId, "Lê CoTenant Demo"));
            context.UserRoles.Add(new UserRole
            {
                UserId = CoTenantUserId,
                RoleId = RoleSeed.TenantRoleId,
                CreatedAt = SeededAt
            });
        }

        if (!await context.Users.AnyAsync(
            x => x.Id == TenantUserId || x.NormalizedEmail == TenantEmail.ToUpperInvariant(),
            cancellationToken))
        {
            context.Users.Add(CreateUser(TenantUserId, TenantEmail, "Nguyễn Tenant Demo", passwordService));
            context.UserProfiles.Add(CreateProfile(TenantUserId, "Nguyễn Tenant Demo"));
            context.UserRoles.Add(new UserRole
            {
                UserId = TenantUserId,
                RoleId = RoleSeed.TenantRoleId,
                CreatedAt = SeededAt
            });
        }

        if (!await context.Users.AnyAsync(
            x => x.Id == LandlordUserId || x.NormalizedEmail == LandlordEmail.ToUpperInvariant(),
            cancellationToken))
        {
            context.Users.Add(CreateUser(LandlordUserId, LandlordEmail, "Trần Landlord Demo", passwordService));
            context.UserProfiles.Add(CreateProfile(LandlordUserId, "Trần Landlord Demo"));
            context.UserRoles.Add(new UserRole
            {
                UserId = LandlordUserId,
                RoleId = RoleSeed.LandlordRoleId,
                CreatedAt = SeededAt
            });
        }

        if (!await context.Users.AnyAsync(x => x.Id == DummyLandlordUserId, cancellationToken))
        {
            context.Users.Add(CreateUser(DummyLandlordUserId, "landlord.mock@example.com", "Chủ trọ Mock", passwordService));
            context.UserProfiles.Add(CreateProfile(DummyLandlordUserId, "Chủ trọ Mock"));
            context.UserRoles.Add(new UserRole
            {
                UserId = DummyLandlordUserId,
                RoleId = RoleSeed.LandlordRoleId,
                CreatedAt = SeededAt
            });
        }

        // await SeedDemoUserAsync(
        //     context,
        //     passwordService,
        //     TenantLinhUserId,
        //     "linh.tenant.demo@example.com",
        //     "Pham Linh",
        //     RoleSeed.TenantRoleId,
        //     cancellationToken);

        // await SeedDemoUserAsync(
        //     context,
        //     passwordService,
        //     TenantMinhUserId,
        //     "minh.tenant.demo@example.com",
        //     "Le Minh",
        //     RoleSeed.TenantRoleId,
        //     cancellationToken);

        // await SeedDemoUserAsync(
        //     context,
        //     passwordService,
        //     LandlordMaiUserId,
        //     "mai.landlord.demo@example.com",
        //     "Hoang Mai",
        //     RoleSeed.LandlordRoleId,
        //     cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBillingServiceTypesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var serviceTypes = new[]
        {
            new BillingServiceType
            {
                Id = ElectricServiceTypeId,
                Name = "Điện",
                SupportsMeterReading = true,
                MeterUnitName = "kWh",
                IsActive = true,
                CreatedAt = now
            },
            new BillingServiceType
            {
                Id = WaterServiceTypeId,
                Name = "Nước",
                SupportsMeterReading = true,
                MeterUnitName = "m3",
                IsActive = true,
                CreatedAt = now
            },
            new BillingServiceType
            {
                Id = InternetServiceTypeId,
                Name = "Internet",
                SupportsMeterReading = false,
                MeterUnitName = null,
                IsActive = true,
                CreatedAt = now
            },
            new BillingServiceType
            {
                Id = TrashServiceTypeId,
                Name = "Rác",
                SupportsMeterReading = false,
                MeterUnitName = null,
                IsActive = true,
                CreatedAt = now
            }
        };

        var legacyInternetServiceName = "Wifi";
        var serviceTypeNames = serviceTypes
            .Select(x => x.Name)
            .Append(legacyInternetServiceName)
            .ToArray();
        var existingServiceTypes = await context.BillingServiceTypes
            .Where(x => serviceTypeNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        foreach (var serviceType in serviceTypes)
        {
            var existingServiceType = existingServiceTypes
                .FirstOrDefault(x => x.Name == serviceType.Name);

            if (serviceType.Id == InternetServiceTypeId && existingServiceType is null)
            {
                existingServiceType = existingServiceTypes
                    .FirstOrDefault(x => x.Name == legacyInternetServiceName);
            }

            if (existingServiceType is null)
            {
                context.BillingServiceTypes.Add(serviceType);
                continue;
            }

            existingServiceType.SupportsMeterReading = serviceType.SupportsMeterReading;
            existingServiceType.MeterUnitName = serviceType.MeterUnitName;
            existingServiceType.IsActive = serviceType.IsActive;

            if (existingServiceType.Name == legacyInternetServiceName)
            {
                existingServiceType.Name = serviceType.Name;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedApprovedKycAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.KycVerifications.AnyAsync(
                x => x.UserId == TenantUserId && x.Status == KycVerificationStatus.Approved,
                cancellationToken))
        {
            context.KycVerifications.Add(CreateApprovedKyc(
                Guid.Parse("60000000-0000-0000-0000-000000000001"),
                TenantUserId,
                "Nguyễn Tenant Demo",
                "********901",
                "demo-tenant-citizen-id-hash",
                new DateOnly(2000, 1, 1)));
        }

        if (!await context.KycVerifications.AnyAsync(
                x => x.UserId == CoTenantUserId && x.Status == KycVerificationStatus.Approved,
                cancellationToken))
        {
            context.KycVerifications.Add(CreateApprovedKyc(
                Guid.Parse("60000000-0000-0000-0000-000000000002"),
                CoTenantUserId,
                "Lê CoTenant Demo",
                "********902",
                "demo-cotenant-citizen-id-hash",
                new DateOnly(2001, 2, 2)));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static KycVerification CreateApprovedKyc(
        Guid id,
        Guid userId,
        string fullName,
        string citizenIdMasked,
        string citizenIdHash,
        DateOnly dateOfBirth)
    {
        return new KycVerification
        {
            Id = id,
            UserId = userId,
            DocumentType = KycDocumentType.CCCD,
            EkycProvider = EkycProvider.VNPT,
            OcrFullName = fullName,
            OcrCitizenIdMasked = citizenIdMasked,
            CitizenIdHash = citizenIdHash,
            OcrDateOfBirth = dateOfBirth,
            OcrGender = "Male",
            OcrAddress = "TP. Hồ Chí Minh",
            EkycResult = EkycResult.Passed,
            RiskLevel = KycRiskLevel.Low,
            Status = KycVerificationStatus.Approved,
            SubmittedAt = SeededAt,
            ReviewedAt = SeededAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static async Task SeedRoomingHousesAsync(
        AppDbContext context,
        SeedLocation location,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        if (!await context.RoomingHouses.AnyAsync(x => x.Id == ApprovedHouseId, cancellationToken))
        {
            context.RoomingHouses.Add(new RoomingHouse
            {
                Id = ApprovedHouseId,
                LandlordUserId = LandlordUserId,
                Name = "Nhà trọ Hoa Sen",
                Description = "Khu trọ sạch sẽ, gần trường đại học và trạm xe buýt.",
                AddressLine = "123 Nguyễn Văn Cừ",
                ProvinceCode = location.ProvinceCode,
                WardCode = location.WardCode,
                AddressDisplay = $"123 Nguyễn Văn Cừ, {location.WardName}, {location.ProvinceName}",
                Latitude = 15.975400m,
                Longitude = 108.263800m,
                ApprovalStatus = RoomingHouseApprovalStatus.Approved,
                VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
                ReviewedAt = SeededAt,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });

            context.RoomingHouseAmenities.AddRange(
                CreateHouseAmenity(ApprovedHouseId, AmenitySeed.WifiId),
                CreateHouseAmenity(ApprovedHouseId, AmenitySeed.SecurityCameraId),
                CreateHouseAmenity(ApprovedHouseId, AmenitySeed.ParkingId),
                CreateHouseAmenity(ApprovedHouseId, AmenitySeed.WashingMachineId));

            context.PropertyImages.Add(new PropertyImage
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                RoomingHouseId = ApprovedHouseId,
                ImageUrl = string.Empty,
                Caption = "Mặt tiền nhà trọ Hoa Sen",
                IsCover = true,
                SortOrder = 1,
                CreatedAt = SeededAt
            });

            context.RoomingHouseLegalDocuments.Add(new RoomingHouseLegalDocument
            {
                RoomingHouseId = ApprovedHouseId,
                DocumentType = LegalDocumentType.LAND_USE_CERTIFICATE,
                DocumentNumberMasked = "*****6789",
                DocumentNumberHash = "demo-hoa-sen-legal-document-hash",
                UploadedAt = SeededAt,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
        } 

        if (!await context.RentalPolicies.AnyAsync(x => x.RoomingHouseId == ApprovedHouseId, cancellationToken))
        {
            context.RentalPolicies.Add(new RentalPolicy
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
                RoomingHouseId = ApprovedHouseId,
                MinRentalMonths = 3,
                MaxRentalMonths = 12,
                AllowShortTermRenewal = true,
                RenewalNoticeDays = 30,
                DepositMonths = 1m,
                DefaultPaymentDay = 5,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
        }

        await EnsureApprovedHouseRuleSeedAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);

        if (!await context.RoomingHouses.AnyAsync(x => x.Id == DraftHouseId, cancellationToken))
        {
            context.RoomingHouses.Add(new RoomingHouse
            {
                Id = DraftHouseId,
                LandlordUserId = DummyLandlordUserId,
                Name = "Nhà trọ Minh Anh",
                Description = "Bản nháp dùng để test chỉnh sửa khu trọ.",
                AddressLine = "45 Lê Lợi",
                ProvinceCode = location.ProvinceCode,
                WardCode = location.WardCode,
                AddressDisplay = $"45 Lê Lợi, {location.WardName}, {location.ProvinceName}",
                ApprovalStatus = RoomingHouseApprovalStatus.Draft,
                VisibilityStatus = RoomingHouseVisibilityStatus.Hidden,
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt
            });
        }

        await SeedRoomingHouseAsync(
            context,
            location,
            SunriseHouseId,
            LandlordUserId,
            "Nhà trọ Sunrise",
            "Nhà trọ mini mới, gần khu công nghệ và siêu thị, phù hợp sinh viên và nhân viên văn phòng.",
            "88 Duong So 7",
            15.980000m,
            108.265000m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "Mặt tiền Nhà trọ Sunrise",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.ParkingId,
            AmenitySeed.SecurityCameraId,
            AmenitySeed.AirConditionerId);

        await SeedRoomingHouseAsync(
            context,
            location,
            GreenViewHouseId,
            LandlordUserId,
            "Nhà trọ Green View",
            "Khu trọ yên tĩnh, có ban công, máy giặt chung và khu để xe riêng.",
            "12 Pham Van Dong",
            15.972000m,
            108.260000m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "Không gian chung Nhà trọ Green View",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.WashingMachineId,
            AmenitySeed.BalconyId,
            AmenitySeed.ParkingId);

        await SeedRoomingHouseAsync(
            context,
            location,
            PendingHouseId,
            DummyLandlordUserId,
            "Nhà trọ Garden Pending",
            "Hồ sơ nhà trọ đang chờ admin duyệt, dùng để test luồng kiểm duyệt.",
            "36 Nguyen Huu Tho",
            15.982000m,
            108.268000m,
            RoomingHouseApprovalStatus.Pending,
            RoomingHouseVisibilityStatus.Hidden,
            "Ảnh tổng quan Nhà trọ Garden Pending",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.PrivateBathroomId);

        await SeedRoomingHouseAsync(
            context,
            location,
            RejectedHouseId,
            DummyLandlordUserId,
            "Nhà trọ Old Town",
            "Hồ sơ bị từ chối để test màn hình lý do và gửi lại hồ sơ.",
            "7 Tran Hung Dao",
            15.970000m,
            108.258000m,
            RoomingHouseApprovalStatus.Rejected,
            RoomingHouseVisibilityStatus.Hidden,
            "Ảnh hiện trạng Nhà trọ Old Town",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.ParkingId);

        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureApprovedHouseRuleSeedAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken = default)
    {
        var approvedHouse = context.RoomingHouses.Local.FirstOrDefault(x => x.Id == ApprovedHouseId)
            ?? await context.RoomingHouses.FirstOrDefaultAsync(x => x.Id == ApprovedHouseId, cancellationToken);

        if (approvedHouse is null)
        {
            return;
        }

        var request = BuildApprovedHouseRuleRequest();
        var now = DateTimeOffset.UtcNow;
        var houseRule = context.RoomingHouseRules.Local.FirstOrDefault(x => x.RoomingHouseId == ApprovedHouseId)
            ?? await context.RoomingHouseRules.FirstOrDefaultAsync(x => x.RoomingHouseId == ApprovedHouseId, cancellationToken);

        if (houseRule is null)
        {
            houseRule = new RoomingHouseRule
            {
                Id = ApprovedHouseRuleId,
                RoomingHouseId = ApprovedHouseId,
                CreatedAt = SeededAt
            };
            context.RoomingHouseRules.Add(houseRule);
        }

        var mediaAsset = houseRule.MediaAssetId.HasValue
            ? context.MediaAssets.Local.FirstOrDefault(x => x.Id == houseRule.MediaAssetId.Value)
                ?? await context.MediaAssets.FirstOrDefaultAsync(x => x.Id == houseRule.MediaAssetId.Value, cancellationToken)
            : null;

        mediaAsset = await EnsureApprovedHouseRuleMediaAssetAsync(
            context,
            approvedHouse,
            mediaAsset,
            mediaStorageService,
            mediaObjectKeyFactory,
            request,
            cancellationToken);

        houseRule.SourceType = RoomingHouseRuleSourceType.FormGenerated;
        houseRule.MediaAssetId = mediaAsset.Id;
        houseRule.GeneralRules = request.GeneralRules;
        houseRule.QuietHours = request.QuietHours;
        houseRule.SecurityPolicy = request.SecurityPolicy;
        houseRule.CleaningPolicy = request.CleaningPolicy;
        houseRule.GuestPolicy = request.GuestPolicy;
        houseRule.ParkingPolicy = request.ParkingPolicy;
        houseRule.UtilityPolicy = request.UtilityPolicy;
        houseRule.DamageCompensationPolicy = request.DamageCompensationPolicy;
        houseRule.AdditionalNotes = request.AdditionalNotes;
        houseRule.UpdatedAt = now;
    }

    private static async Task<MediaAsset> EnsureApprovedHouseRuleMediaAssetAsync(
        AppDbContext context,
        RoomingHouse approvedHouse,
        MediaAsset? existingAsset,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        UpsertRoomingHouseRuleRequest request,
        CancellationToken cancellationToken)
    {
        const string fileName = "house-rule-approved-demo.pdf";
        await using var pdfStream = RoomingHouseRulePdfGenerator.Generate(approvedHouse, request);
        using var buffer = new MemoryStream();
        await pdfStream.CopyToAsync(buffer, cancellationToken);
        var pdfBytes = buffer.ToArray();

        if (existingAsset is not null)
        {
            existingAsset.OwnerUserId = LandlordUserId;
            existingAsset.Scope = MediaScope.RoomingHouseRulePdf;
            existingAsset.Visibility = MediaVisibility.Public;
            existingAsset.Status = MediaStatus.Linked;
            existingAsset.LinkedEntityType = nameof(RoomingHouseRule);
            existingAsset.LinkedEntityId = ApprovedHouseId;
            existingAsset.DeletedAt = null;
            existingAsset.ContentType = "application/pdf";
            existingAsset.FileSize = pdfBytes.Length;
            existingAsset.OriginalFileName = fileName;
            existingAsset.UpdatedAt = DateTimeOffset.UtcNow;

            if (string.IsNullOrWhiteSpace(existingAsset.ObjectKey))
            {
                var objectKey = mediaObjectKeyFactory.Create(
                    MediaScope.RoomingHouseRulePdf,
                    MediaVisibility.Public,
                    fileName);
                existingAsset.ObjectKey = objectKey.ObjectKey;
                existingAsset.StoredFileName = objectKey.StoredFileName;
            }
            else if (string.IsNullOrWhiteSpace(existingAsset.StoredFileName))
            {
                existingAsset.StoredFileName = Path.GetFileName(existingAsset.ObjectKey);
            }

            if (string.IsNullOrWhiteSpace(existingAsset.BucketName))
            {
                existingAsset.BucketName = mediaStorageService.GetBucketName();
            }

            if (!await mediaStorageService.ExistsAsync(existingAsset.ObjectKey, cancellationToken))
            {
                await UploadApprovedHouseRulePdfAsync(
                    mediaStorageService,
                    existingAsset.ObjectKey,
                    pdfBytes,
                    fileName,
                    cancellationToken);
            }

            return existingAsset;
        }

        var createdObjectKey = mediaObjectKeyFactory.Create(
            MediaScope.RoomingHouseRulePdf,
            MediaVisibility.Public,
            fileName);

        await UploadApprovedHouseRulePdfAsync(
            mediaStorageService,
            createdObjectKey.ObjectKey,
            pdfBytes,
            fileName,
            cancellationToken);

        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = LandlordUserId,
            BucketName = mediaStorageService.GetBucketName(),
            ObjectKey = createdObjectKey.ObjectKey,
            OriginalFileName = fileName,
            StoredFileName = createdObjectKey.StoredFileName,
            ContentType = "application/pdf",
            FileSize = pdfBytes.Length,
            Scope = MediaScope.RoomingHouseRulePdf,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(RoomingHouseRule),
            LinkedEntityId = ApprovedHouseId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.MediaAssets.Add(mediaAsset);
        return mediaAsset;
    }

    private static async Task UploadApprovedHouseRulePdfAsync(
        IMediaStorageService mediaStorageService,
        string objectKey,
        byte[] pdfBytes,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(pdfBytes);
        await mediaStorageService.UploadAsync(
            new MediaUploadRequest
            {
                Content = content,
                OriginalFileName = fileName,
                ContentType = "application/pdf",
                FileSize = pdfBytes.Length,
                ObjectKey = objectKey,
                Visibility = MediaVisibility.Public
            },
            cancellationToken);
    }

    private static UpsertRoomingHouseRuleRequest BuildApprovedHouseRuleRequest()
    {
        return new UpsertRoomingHouseRuleRequest
        {
            SourceType = RoomingHouseRuleSourceType.FormGenerated.ToString(),
            GeneralRules = "Giu gin trat tu chung, khong gay on ao, khong tu y cai tao phong.",
            QuietHours = "Sau 22:30 vui long giam tieng on va khong tu tap dong nguoi.",
            SecurityPolicy = "Khoa cua, tat dien khi ra khoi phong va bao ngay cho chu tro neu co su co.",
            CleaningPolicy = "Do rac dung gio, giu ve sinh hanh lang, khu bep va nha tam chung.",
            GuestPolicy = "Khach o lai qua dem can thong bao truoc cho chu tro.",
            ParkingPolicy = "De xe dung vi tri quy dinh, khong chan loi di chung.",
            UtilityPolicy = "Su dung dien, nuoc tiet kiem va khong dau noi thiet bi cong suat lon trai phep.",
            DamageCompensationPolicy = "Nguoi gay hu hong tai san co trach nhiem boi thuong theo muc do thiet hai.",
            AdditionalNotes = "Lien he chu tro qua so hotline noi bo neu can ho tro khan cap."
        };
    }

    private static async Task SeedRoomingHouseAsync(
        AppDbContext context,
        SeedLocation location,
        Guid roomingHouseId,
        Guid landlordUserId,
        string name,
        string description,
        string addressLine,
        decimal latitude,
        decimal longitude,
        RoomingHouseApprovalStatus approvalStatus,
        RoomingHouseVisibilityStatus visibilityStatus,
        string coverCaption,
        CancellationToken cancellationToken,
        params int[] amenityIds)
    {
        var existing = await context.RoomingHouses.FirstOrDefaultAsync(x => x.Id == roomingHouseId, cancellationToken);
        if (existing is not null)
        {
            existing.Name = name;
            existing.Description = description;

            var coverImage = await context.PropertyImages
                .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId && x.IsCover, cancellationToken);
            if (coverImage is not null)
            {
                coverImage.Caption = coverCaption;
            }
            return;
        }

        var reviewedAt = approvalStatus is RoomingHouseApprovalStatus.Approved or RoomingHouseApprovalStatus.Rejected
            ? SeededAt
            : (DateTimeOffset?)null;

        context.RoomingHouses.Add(new RoomingHouse
        {
            Id = roomingHouseId,
            LandlordUserId = landlordUserId,
            Name = name,
            Description = description,
            AddressLine = addressLine,
            ProvinceCode = location.ProvinceCode,
            WardCode = location.WardCode,
            AddressDisplay = $"{addressLine}, {location.WardName}, {location.ProvinceName}",
            Latitude = latitude,
            Longitude = longitude,
            ApprovalStatus = approvalStatus,
            VisibilityStatus = visibilityStatus,
            RejectedReason = approvalStatus == RoomingHouseApprovalStatus.Rejected
                ? "Anh giay to chua ro va dia chi chua khop voi ho so."
                : null,
            ReviewedByAdminId = reviewedAt.HasValue ? AdminUserId : null,
            ReviewedAt = reviewedAt,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });

        context.RoomingHouseAmenities.AddRange(
            amenityIds.Distinct().Select(amenityId => CreateHouseAmenity(roomingHouseId, amenityId)));

        context.PropertyImages.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = roomingHouseId,
            ImageUrl = string.Empty,
            Caption = coverCaption,
            IsCover = true,
            SortOrder = 1,
            CreatedAt = SeededAt
        });
    }

    private static async Task SeedRoomsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Rooms.AnyAsync(x => x.RoomingHouseId == ApprovedHouseId, cancellationToken))
        {
            await EnsureSeedRoomPricingAsync(context, cancellationToken);
            return;
        }

        var rooms = new[]
        {
            CreateRoom(Room101Id, "101", 1, 18m, 3, RoomStatus.Available),
            CreateRoom(Room102Id, "102", 1, 20m, 2, RoomStatus.Occupied),
            CreateRoom(Room201Id, "201", 2, 24m, 3, RoomStatus.Hidden)
        };

        context.Rooms.AddRange(rooms);

        context.RoomAmenities.AddRange(
            CreateRoomAmenity(Room101Id, AmenitySeed.WifiId),
            CreateRoomAmenity(Room101Id, AmenitySeed.AirConditionerId),
            CreateRoomAmenity(Room102Id, AmenitySeed.WifiId),
            CreateRoomAmenity(Room102Id, AmenitySeed.PrivateBathroomId),
            CreateRoomAmenity(Room201Id, AmenitySeed.WifiId),
            CreateRoomAmenity(Room201Id, AmenitySeed.MezzanineId),
            CreateRoomAmenity(Room201Id, AmenitySeed.BalconyId));

        context.RoomPriceTiers.AddRange(
            CreatePriceTier(Room101Id, 1, 2500000m),
            CreatePriceTier(Room101Id, 2, 3000000m),
            CreatePriceTier(Room101Id, 3, 3500000m),
            CreatePriceTier(Room102Id, 1, 2800000m),
            CreatePriceTier(Room102Id, 2, 3300000m),
            CreatePriceTier(Room201Id, 1, 3500000m),
            CreatePriceTier(Room201Id, 2, 4000000m),
            CreatePriceTier(Room201Id, 3, 4500000m));

        context.PropertyImages.AddRange(
            CreateRoomImage(Room101Id, "Phòng 101"),
            CreateRoomImage(Room102Id, "Phòng 102"),
            CreateRoomImage(Room201Id, "Phòng 201"));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSeedRoomPricingAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var seededRooms = await context.Rooms
            .Where(x => x.Id == Room101Id || x.Id == Room102Id || x.Id == Room201Id)
            .ToListAsync(cancellationToken);

        foreach (var room in seededRooms)
        {
            room.IsTieredPricing = true;
            room.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var room101 = seededRooms.FirstOrDefault(x => x.Id == Room101Id);
        if (room101 is not null && room101.MaxOccupants < 3)
        {
            room101.MaxOccupants = 3;
        }

        if (seededRooms.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task BackfillPublicSeedMediaAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        await BackfillRoomingHouseImagesAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        await BackfillRoomImagesAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task BackfillRoomingHouseImagesAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var roomingHouses = await context.RoomingHouses
            .Where(x => x.DeletedAt == null)
            .Where(x => !context.PropertyImages.Any(pi => pi.RoomingHouseId == x.Id && pi.MediaAssetId.HasValue))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.LandlordUserId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (roomingHouses.Count == 0)
        {
            return;
        }

        var roomingHouseIds = roomingHouses.Select(x => x.Id).ToList();
        var existingImages = await context.PropertyImages
            .Where(x => x.RoomingHouseId.HasValue && roomingHouseIds.Contains(x.RoomingHouseId.Value))
            .OrderByDescending(x => x.IsCover)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var roomingHouse in roomingHouses)
        {
            var propertyImage = existingImages.FirstOrDefault(x => x.RoomingHouseId == roomingHouse.Id)
                ?? new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    RoomingHouseId = roomingHouse.Id,
                    Caption = $"Ảnh tổng quan {roomingHouse.Name}",
                    IsCover = true,
                    SortOrder = 1,
                    CreatedAt = roomingHouse.CreatedAt
                };

            await EnsurePublicPropertyImageMediaAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                roomingHouse.LandlordUserId,
                propertyImage,
                MediaScope.RoomingHouseImage,
                $"seed-rooming-house-{roomingHouse.Id:N}-cover.png",
                roomingHouse.CreatedAt,
                cancellationToken);
        }
    }

    private static async Task BackfillRoomImagesAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var rooms = await context.Rooms
            .Where(x => x.DeletedAt == null)
            .Where(x => !context.PropertyImages.Any(pi => pi.RoomId == x.Id && pi.MediaAssetId.HasValue))
            .Select(x => new
            {
                x.Id,
                x.RoomNumber,
                x.CreatedAt,
                x.RoomingHouseId,
                LandlordUserId = x.RoomingHouse.LandlordUserId
            })
            .ToListAsync(cancellationToken);

        if (rooms.Count == 0)
        {
            return;
        }

        var roomIds = rooms.Select(x => x.Id).ToList();
        var existingImages = await context.PropertyImages
            .Where(x => x.RoomId.HasValue && roomIds.Contains(x.RoomId.Value))
            .OrderByDescending(x => x.IsCover)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var room in rooms)
        {
            var propertyImage = existingImages.FirstOrDefault(x => x.RoomId == room.Id)
                ?? new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    RoomId = room.Id,
                    Caption = $"Phòng {room.RoomNumber}",
                    IsCover = true,
                    SortOrder = 1,
                    CreatedAt = room.CreatedAt
                };

            await EnsurePublicPropertyImageMediaAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                room.LandlordUserId,
                propertyImage,
                MediaScope.RoomImage,
                $"seed-room-{room.Id:N}-cover.png",
                room.CreatedAt,
                cancellationToken);
        }
    }

    private static async Task EnsurePublicPropertyImageMediaAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid ownerUserId,
        PropertyImage propertyImage,
        MediaScope scope,
        string fileName,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (propertyImage.MediaAssetId.HasValue)
        {
            propertyImage.ImageUrl = PublicMediaPathBuilder.Build(propertyImage.MediaAssetId.Value);
            return;
        }

        var objectKey = mediaObjectKeyFactory.Create(scope, MediaVisibility.Public, fileName);
        var storedObject = await UploadPlaceholderImageAsync(
            mediaStorageService,
            objectKey.ObjectKey,
            fileName,
            cancellationToken);
        var mediaAssetId = Guid.NewGuid();

        context.MediaAssets.Add(new MediaAsset
        {
            Id = mediaAssetId,
            OwnerUserId = ownerUserId,
            BucketName = storedObject.BucketName,
            ObjectKey = storedObject.ObjectKey,
            OriginalFileName = fileName,
            StoredFileName = storedObject.StoredFileName,
            ContentType = "image/png",
            FileSize = PlaceholderImageBytes.Length,
            Scope = scope,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Linked,
            LinkedEntityType = nameof(PropertyImage),
            LinkedEntityId = propertyImage.Id,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });

        if (context.Entry(propertyImage).State == EntityState.Detached)
        {
            context.PropertyImages.Add(propertyImage);
        }

        propertyImage.MediaAssetId = mediaAssetId;
        propertyImage.ImageUrl = PublicMediaPathBuilder.Build(mediaAssetId);
        propertyImage.IsCover = true;
        propertyImage.SortOrder = propertyImage.SortOrder <= 0 ? 1 : propertyImage.SortOrder;
    }

    private static async Task<MediaStoredObjectResult> UploadPlaceholderImageAsync(
        IMediaStorageService mediaStorageService,
        string objectKey,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(PlaceholderImageBytes, writable: false);
        return await mediaStorageService.UploadAsync(
            new MediaUploadRequest
            {
                Content = content,
                OriginalFileName = fileName,
                ContentType = "image/png",
                FileSize = PlaceholderImageBytes.Length,
                ObjectKey = objectKey,
                Visibility = MediaVisibility.Public
            },
            cancellationToken);
    }

    private static async Task<User> EnsureSeedUserAsync(
        AppDbContext context,
        IPasswordService passwordService,
        Guid seedUserId,
        string email,
        string displayName,
        int roleId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var matchingUsers = await context.Users
            .Include(x => x.UserRoles)
            .Where(x => x.Id == seedUserId || x.NormalizedEmail == normalizedEmail)
            .ToListAsync(cancellationToken);

        var user = matchingUsers.FirstOrDefault(x => x.Id == seedUserId)
            ?? matchingUsers.FirstOrDefault(x => x.NormalizedEmail == normalizedEmail);

        if (user is null)
        {
            user = CreateUser(seedUserId, email, displayName, passwordService);
            context.Users.Add(user);
        }
        else
        {
            var hasEmailConflict = matchingUsers.Any(x => x.Id != user.Id && x.NormalizedEmail == normalizedEmail);
            if (!hasEmailConflict)
            {
                user.Email = email;
                user.NormalizedEmail = normalizedEmail;
            }

            user.DisplayName = displayName;
            user.Status = UserStatus.Active;
            user.OnboardingStatus = OnboardingStatus.Completed;
            user.EmailConfirmed = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = passwordService.HashPassword(DemoPassword);
            }
        }

        if (user.UserRoles.All(x => x.RoleId != roleId))
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                CreatedAt = SeededAt
            });
        }

        return user;
    }

    private static User CreateUser(
        Guid id,
        string email,
        string displayName,
        IPasswordService passwordService)
    {
        return new User
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = passwordService.HashPassword(DemoPassword),
            DisplayName = displayName,
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.Completed,
            EmailConfirmed = true,
            PhoneConfirmed = false,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static UserProfile CreateProfile(Guid userId, string fullName)
    {
        return new UserProfile
        {
            UserId = userId,
            FullName = fullName,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static Room CreateRoom(
        Guid id,
        string roomNumber,
        int floor,
        decimal areaM2,
        int maxOccupants,
        RoomStatus status)
    {
        return new Room
        {
            Id = id,
            RoomingHouseId = ApprovedHouseId,
            RoomNumber = roomNumber,
            Floor = floor,
            AreaM2 = areaM2,
            MaxOccupants = maxOccupants,
            IsTieredPricing = true,
            Status = status,
            Description = $"Phòng {roomNumber} dùng để test dashboard chủ trọ.",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static Room CreateRoom(
        Guid roomingHouseId,
        Guid id,
        string roomNumber,
        int floor,
        decimal areaM2,
        int maxOccupants,
        RoomStatus status)
    {
        var room = CreateRoom(id, roomNumber, floor, areaM2, maxOccupants, status);
        room.RoomingHouseId = roomingHouseId;
        room.Description = $"Phòng {roomNumber} mock data cho nhà trọ demo.";
        room.IsTieredPricing = maxOccupants > 1;
        return room;
    }

    private static void AddRoomMockDetails(AppDbContext context, Room room)
    {
        var amenityIds = room.Id switch
        {
            var id when id == Room202Id => new[] { AmenitySeed.WifiId, AmenitySeed.PrivateBathroomId },
            var id when id == Room301Id => new[] { AmenitySeed.WifiId, AmenitySeed.AirConditionerId, AmenitySeed.BalconyId },
            var id when id == SunriseRoomA1Id => new[] { AmenitySeed.WifiId, AmenitySeed.PrivateBathroomId },
            var id when id == SunriseRoomA2Id => new[] { AmenitySeed.WifiId, AmenitySeed.AirConditionerId },
            var id when id == SunriseRoomB1Id => new[] { AmenitySeed.WifiId, AmenitySeed.MezzanineId, AmenitySeed.BalconyId },
            var id when id == GreenViewRoom101Id => new[] { AmenitySeed.WifiId, AmenitySeed.WashingMachineId },
            var id when id == GreenViewRoom102Id => new[] { AmenitySeed.WifiId, AmenitySeed.PrivateBathroomId, AmenitySeed.BalconyId },
            _ => new[] { AmenitySeed.WifiId }
        };

        context.RoomAmenities.AddRange(
            amenityIds.Distinct().Select(amenityId => CreateRoomAmenity(room.Id, amenityId)));

        for (var occupantCount = 1; occupantCount <= room.MaxOccupants; occupantCount++)
        {
            var baseRent = room.RoomingHouseId == GreenViewHouseId ? 3200000m : 2600000m;
            var areaPremium = (room.AreaM2 ?? 18m) * 35000m;
            context.RoomPriceTiers.Add(CreatePriceTier(
                room.Id,
                occupantCount,
                baseRent + areaPremium + ((occupantCount - 1) * 450000m)));
        }

        context.PropertyImages.Add(CreateRoomImage(
            room.Id,
            $"Phòng {room.RoomNumber}"));
    }

    private static RoomPriceTier CreatePriceTier(Guid roomId, int occupantCount, decimal monthlyRent)
    {
        return new RoomPriceTier
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            OccupantCount = occupantCount,
            MonthlyRent = monthlyRent,
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
    }

    private static PropertyImage CreateRoomImage(Guid roomId, string caption)
    {
        return new PropertyImage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ImageUrl = string.Empty,
            Caption = caption,
            IsCover = true,
            SortOrder = 1,
            CreatedAt = SeededAt
        };
    }

    private static RoomingHouseAmenity CreateHouseAmenity(Guid roomingHouseId, int amenityId)
    {
        return new RoomingHouseAmenity
        {
            RoomingHouseId = roomingHouseId,
            AmenityId = amenityId
        };
    }

    private static RoomAmenity CreateRoomAmenity(Guid roomId, int amenityId)
    {
        return new RoomAmenity
        {
            RoomId = roomId,
            AmenityId = amenityId
        };
    }

    private static async Task<SeedLocation?> GetSeedLocationAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var ward = await context.AdministrativeWards
            .AsNoTracking()
            .Where(x => x.IsActive && x.Code == "20285")
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.ProvinceCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ward is null)
        {
            return null;
        }

        var province = await context.AdministrativeProvinces
            .AsNoTracking()
            .Where(x => x.Code == ward.ProvinceCode)
            .Select(x => new
            {
                x.Code,
                x.Name
            })
            .FirstAsync(cancellationToken);

        return new SeedLocation(
            province.Code,
            province.Name,
            ward.Code,
            ward.Name);
    }

    private sealed record SeedLocation(
        string ProvinceCode,
        string ProvinceName,
        string WardCode,
        string WardName);
}
