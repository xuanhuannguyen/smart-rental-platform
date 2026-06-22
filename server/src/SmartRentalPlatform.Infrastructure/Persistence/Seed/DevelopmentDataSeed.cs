using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;

using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Billing;
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
    private static readonly Guid TenantApprovedKycId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ApprovedHouseId = Guid.Parse("20000000-0000-0000-0000-000000000001");
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

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordService passwordService,
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
        await SeedRoomingHousesAsync(context, location, cancellationToken);
        await SeedRoomsAsync(context, cancellationToken);
        await LargeScaleRoomingHouseSeeder.SeedAsync(context, cancellationToken);
        // await SeedAdditionalRoomsAsync(context, cancellationToken);
        // await SeedBillingAsync(context, cancellationToken);
    }

    public static async Task SeedAdminAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AdminEmail.ToUpperInvariant();
        var admin = await context.Users
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (admin is null)
        {
            admin = CreateUser(AdminUserId, AdminEmail, "Admin Demo", passwordService);
            context.Users.Add(admin);
        }

        if (!await context.UserProfiles.AnyAsync(x => x.UserId == admin.Id, cancellationToken))
        {
            context.UserProfiles.Add(CreateProfile(admin.Id, "Admin Demo"));
        }

        if (admin.UserRoles.All(x => x.RoleId != RoleSeed.AdminRoleId))
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = admin.Id,
                RoleId = RoleSeed.AdminRoleId,
                CreatedAt = SeededAt
            });
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
        var normalizedEmail = TenantEmail.ToUpperInvariant();
        var tenant = await context.Users
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (tenant is null)
        {
            tenant = CreateUser(TenantUserId, TenantEmail, "Nguyen Tenant Demo", passwordService);
            context.Users.Add(tenant);
        }

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

        if (tenant.UserRoles.All(x => x.RoleId != RoleSeed.TenantRoleId))
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = tenant.Id,
                RoleId = RoleSeed.TenantRoleId,
                CreatedAt = SeededAt
            });
        }

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
                FrontImageObjectKey = "demo/kyc/tenant/front.jpg",
                BackImageObjectKey = "demo/kyc/tenant/back.jpg",
                SelfieImageObjectKey = "demo/kyc/tenant/selfie.jpg",
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
        if (!await context.Users.AnyAsync(x => x.NormalizedEmail == CoTenantEmail.ToUpperInvariant(), cancellationToken))
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

        if (!await context.Users.AnyAsync(x => x.NormalizedEmail == TenantEmail.ToUpperInvariant(), cancellationToken))
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

        if (!await context.Users.AnyAsync(x => x.NormalizedEmail == LandlordEmail.ToUpperInvariant(), cancellationToken))
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
            FrontImageObjectKey = $"demo/kyc/{userId}/front.jpg",
            BackImageObjectKey = $"demo/kyc/{userId}/back.jpg",
            SelfieImageObjectKey = $"demo/kyc/{userId}/selfie.jpg",
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
                Latitude = 10.762622m,
                Longitude = 106.660172m,
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
                ObjectKey = "demo/houses/hoa-sen/cover.jpg",
                ImageUrl = "/uploads/demo/houses/hoa-sen/cover.jpg",
                Caption = "Mặt tiền nhà trọ Hoa Sen",
                IsCover = true,
                SortOrder = 1,
                CreatedAt = SeededAt
            });

            context.RoomingHouseLegalDocuments.Add(new RoomingHouseLegalDocument
            {
                RoomingHouseId = ApprovedHouseId,
                DocumentType = LegalDocumentType.LAND_USE_CERTIFICATE,
                FrontImageObjectKey = "demo/legal/hoa-sen/front.jpg",
                BackImageObjectKey = "demo/legal/hoa-sen/back.jpg",
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

        if (!await context.RoomingHouses.AnyAsync(x => x.Id == DraftHouseId, cancellationToken))
        {
            context.RoomingHouses.Add(new RoomingHouse
            {
                Id = DraftHouseId,
                LandlordUserId = LandlordUserId,
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
            10.841021m,
            106.809847m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "demo/houses/sunrise/cover.jpg",
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
            10.821903m,
            106.682179m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "demo/houses/green-view/cover.jpg",
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
            LandlordUserId,
            "Nhà trọ Garden Pending",
            "Hồ sơ nhà trọ đang chờ admin duyệt, dùng để test luồng kiểm duyệt.",
            "36 Nguyen Huu Tho",
            10.732681m,
            106.702184m,
            RoomingHouseApprovalStatus.Pending,
            RoomingHouseVisibilityStatus.Hidden,
            "demo/houses/garden-pending/cover.jpg",
            "Ảnh tổng quan Nhà trọ Garden Pending",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.PrivateBathroomId);

        await SeedRoomingHouseAsync(
            context,
            location,
            RejectedHouseId,
            LandlordUserId,
            "Nhà trọ Old Town",
            "Hồ sơ bị từ chối để test màn hình lý do và gửi lại hồ sơ.",
            "7 Tran Hung Dao",
            10.776901m,
            106.700914m,
            RoomingHouseApprovalStatus.Rejected,
            RoomingHouseVisibilityStatus.Hidden,
            "demo/houses/old-town/cover.jpg",
            "Ảnh hiện trạng Nhà trọ Old Town",
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.ParkingId);

        await context.SaveChangesAsync(cancellationToken);
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
        string coverObjectKey,
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
            ObjectKey = coverObjectKey,
            ImageUrl = $"/uploads/{coverObjectKey}",
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
            CreateRoomImage(Room101Id, "demo/rooms/101/cover.jpg", "Phòng 101"),
            CreateRoomImage(Room102Id, "demo/rooms/102/cover.jpg", "Phòng 102"),
            CreateRoomImage(Room201Id, "demo/rooms/201/cover.jpg", "Phòng 201"));

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

        var imageSlug = room.RoomNumber.ToLowerInvariant().Replace("-", string.Empty);
        context.PropertyImages.Add(CreateRoomImage(
            room.Id,
            $"demo/rooms/{imageSlug}/cover.jpg",
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

    private static PropertyImage CreateRoomImage(Guid roomId, string objectKey, string caption)
    {
        return new PropertyImage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            ObjectKey = objectKey,
            ImageUrl = $"/uploads/{objectKey}",
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
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
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
