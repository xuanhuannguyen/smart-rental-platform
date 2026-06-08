using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class DevelopmentDataSeed
{
    public const string AdminEmail = "admin.demo@example.com";
    public const string TenantEmail = "tenant.demo@example.com";
    public const string LandlordEmail = "landlord.demo@example.com";
    public const string DemoPassword = "Demo@123456";

    private static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000099");
    private static readonly Guid TenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid TenantApprovedKycId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ApprovedHouseId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid DraftHouseId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Room101Id = Guid.Parse("30000000-0000-0000-0000-000000000101");
    private static readonly Guid Room102Id = Guid.Parse("30000000-0000-0000-0000-000000000102");
    private static readonly Guid Room201Id = Guid.Parse("30000000-0000-0000-0000-000000000201");
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
        await SeedRoomingHousesAsync(context, location, cancellationToken);
        await SeedRoomsAsync(context, cancellationToken);
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

        await context.SaveChangesAsync(cancellationToken);
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

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRoomsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Rooms.AnyAsync(x => x.RoomingHouseId == ApprovedHouseId, cancellationToken))
        {
            return;
        }

        var rooms = new[]
        {
            CreateRoom(Room101Id, "101", 1, 18m, 2, RoomStatus.Available),
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
            Status = status,
            Description = $"Phòng {roomNumber} dùng để test dashboard chủ trọ.",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        };
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
