using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Chat;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;

using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Chat;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Payments;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using WalletAccountStatus = SmartRentalPlatform.Domain.Enums.Payments.WalletAccountStatus;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed;

public static class DevelopmentDataSeed
{
    public const string AdminEmail = "admin.demo@example.com";
    public const string TenantEmail = "nguyenxuanhuan.dev@gmail.com";
    public const string LandlordEmail = "nguyenxuanhuan21102005@gmail.com";
    public const string SecondaryLandlordEmail = "xunhuns21@gmail.com";
    public const string CoTenantEmail = "hoctienganh4english@gmail.com";
    public const string BulkInvoiceTenantEmail = "huanjrfc@gmail.com";
    public const string GuestTenantEmail = "pham.ngoc.mai@example.com";
    public const string NewOccupantEmail = "vo.thao.vy@example.com";
    public const string DemoPassword = "Demo@123456";

    private static readonly Guid AdminUserId = Guid.Parse("10000000-0000-0000-0000-000000000099");
    private static readonly Guid TenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CoTenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid SecondaryLandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid GuestTenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid NewOccupantUserId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid BulkInvoiceTenantUserId = Guid.Parse("10000000-0000-0000-0000-000000000007");
    private static readonly Guid DummyLandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000009999");
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
    private static readonly Guid AnPhuRoomA103Id = Guid.Parse("30000000-0000-0000-0000-000000000403");
    private static readonly Guid AnPhuRoomA104Id = Guid.Parse("30000000-0000-0000-0000-000000000404");
    private static readonly Guid AnPhuRoomA105Id = Guid.Parse("30000000-0000-0000-0000-000000000405");
    private static readonly Guid MinhKhangRoomB203Id = Guid.Parse("30000000-0000-0000-0000-000000000503");
    private static readonly Guid MinhKhangRoomB204Id = Guid.Parse("30000000-0000-0000-0000-000000000504");
    private static readonly Guid MinhKhangRoomB205Id = Guid.Parse("30000000-0000-0000-0000-000000000505");
    private static readonly Guid PendingRoomG1Id = Guid.Parse("30000000-0000-0000-0000-000000000601");
    private static readonly Guid ActiveContractId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid LinhContractId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid MinhEndedContractId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid LinhRentalRequestId = Guid.Parse("51000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhRoomDepositId = Guid.Parse("52000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhContractOccupantId = Guid.Parse("56000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhCurrentInvoiceId = Guid.Parse("81000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhPaidInvoiceId = Guid.Parse("81000000-0000-0000-0000-000000000101");
    private static readonly Guid LinhFinalInvoiceId = Guid.Parse("81000000-0000-0000-0000-000000000199");
    private static readonly Guid LinhElectricReadingId = Guid.Parse("82000000-0000-0000-0000-000000000101");
    private static readonly Guid LinhWaterReadingId = Guid.Parse("82000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhPreviewContractFileId = Guid.Parse("57000000-0000-0000-0000-000000000101");
    private static readonly Guid LinhSignedContractFileId = Guid.Parse("57000000-0000-0000-0000-000000000102");
    private static readonly Guid LinhMaskedContractFileId = Guid.Parse("57000000-0000-0000-0000-000000000103");
    private static readonly Guid LinhLandlordSignatureId = Guid.Parse("58000000-0000-0000-0000-000000000101");
    private static readonly Guid LinhTenantSignatureId = Guid.Parse("58000000-0000-0000-0000-000000000102");
    private const string LinhShowcaseContractNumber = "HD-XH-B201-20260601";
    private const string ReviewShowcaseContractNumber = "HD-XH-A01-20250901";
    private const string SunriseReviewContractNumber = "HD-SR-A1-20250901";
    private static readonly Guid TenantWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid LandlordWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid TenantLinhWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly Guid TenantMinhWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000004");
    private static readonly Guid LandlordMaiWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000005");
    private static readonly Guid SecondaryLandlordWalletAccountId = Guid.Parse("70000000-0000-0000-0000-000000000006");
    private static readonly Guid ElectricServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid WaterServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly Guid InternetServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    private static readonly Guid TrashServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string DemoImageDirectory = @"C:\Users\Admin\Downloads\Demo";
    private const string DemoElectricMeterImagePath = @"C:\Users\Admin\Downloads\Demo\1341.png";
    private const string DemoWaterMeterImagePath = @"C:\Users\Admin\Downloads\Demo\96.png";

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

        Console.WriteLine("[seed] users");
        await SeedUsersAsync(context, passwordService, cancellationToken);
        Console.WriteLine("[seed] reset tenant ekyc requirement");
        await ResetTenantEkycRequirementAsync(context, cancellationToken);
        Console.WriteLine("[seed] approved kyc");
        await SeedApprovedKycAsync(context, cancellationToken);
        Console.WriteLine("[seed] billing service types");
        await SeedBillingServiceTypesAsync(context, cancellationToken);
        Console.WriteLine("[seed] base rooming houses");
        await SeedRoomingHousesAsync(
            context,
            location,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        Console.WriteLine("[seed] demo service prices");
        await SeedDemoServicePricesAsync(context, cancellationToken);
        Console.WriteLine("[seed] base rooms");
        await SeedRoomsAsync(context, cancellationToken);
        Console.WriteLine("[seed] backfill public seed media");
        await BackfillPublicSeedMediaAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        Console.WriteLine("[seed] demo reviews");
        await SeedDemoReviewsAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        Console.WriteLine("[seed] showcase contract flow");
        await SeedShowcaseContractFlowAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        Console.WriteLine("[seed] secondary landlord operations");
        await SeedSecondaryLandlordOperationsDemoAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);

        if (IsLargeScaleSeedEnabled())
        {
            Console.WriteLine("[seed] large scale catalog");
            await LargeScaleRoomingHouseSeeder.SeedAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                cancellationToken);
        }
        Console.WriteLine("[seed] done");
        // await SeedAdditionalRoomsAsync(context, cancellationToken);
        // await SeedBillingAsync(context, cancellationToken);
    }

    private static bool IsLargeScaleSeedEnabled()
    {
        var value = Environment.GetEnvironmentVariable("SeedData__LargeScale__Enabled");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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
            "Quan tri Smart Rental",
            RoleSeed.AdminRoleId,
            cancellationToken);

        if (!await context.UserProfiles.AnyAsync(x => x.UserId == admin.Id, cancellationToken))
        {
            context.UserProfiles.Add(CreateProfile(admin.Id, "Quan tri Smart Rental"));
        }

        if (!admin.EmailConfirmed || admin.OnboardingStatus != OnboardingStatus.Completed)
        {
            admin.EmailConfirmed = true;
            admin.OnboardingStatus = OnboardingStatus.Completed;
            admin.Status = UserStatus.Active;
            admin.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await SeedEkycRequiredTenantAsync(context, passwordService, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedEkycRequiredTenantAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken)
    {
        var tenant = await EnsureSeedUserAsync(
            context,
            passwordService,
            TenantUserId,
            TenantEmail,
            "Nguyen Xuan Huan",
            RoleSeed.TenantRoleId,
            cancellationToken);

        tenant.Status = UserStatus.Active;
        tenant.OnboardingStatus = OnboardingStatus.NeedProfileUpdate;
        tenant.EmailConfirmed = true;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        var profile = await context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == tenant.Id, cancellationToken);

        if (profile is null)
        {
            profile = CreateProfile(tenant.Id, "Nguyen Xuan Huan");
            context.UserProfiles.Add(profile);
        }

        profile.FullName = "Nguyen Xuan Huan";
        profile.DateOfBirth = new DateOnly(2000, 10, 21);
        profile.Gender = "Male";
        profile.AddressLine = "Da Nang";
        profile.VerifiedCitizenIdMasked = null;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await ResetTenantEkycRequirementAsync(context, cancellationToken);
    }

    private static async Task SeedUsersAsync(
        AppDbContext context,
        IPasswordService passwordService,
        CancellationToken cancellationToken)
    {
        await EnsureDemoUserAsync(context, passwordService, TenantUserId, TenantEmail, "Nguyen Xuan Huan", RoleSeed.TenantRoleId, cancellationToken, OnboardingStatus.NeedProfileUpdate);
        await EnsureDemoUserAsync(context, passwordService, LandlordUserId, LandlordEmail, "Nguyễn Xuân Huấn", RoleSeed.LandlordRoleId, cancellationToken);
        await EnsureDemoUserAsync(context, passwordService, CoTenantUserId, CoTenantEmail, "Le Quang Linh", RoleSeed.TenantRoleId, cancellationToken);
        await EnsureDemoUserAsync(context, passwordService, SecondaryLandlordUserId, SecondaryLandlordEmail, "Xuân Huấn", RoleSeed.LandlordRoleId, cancellationToken);
        await EnsureDemoUserAsync(context, passwordService, GuestTenantUserId, GuestTenantEmail, "Pham Ngoc Mai", RoleSeed.TenantRoleId, cancellationToken);
        await EnsureDemoUserAsync(context, passwordService, NewOccupantUserId, NewOccupantEmail, "Vo Thao Vy", RoleSeed.TenantRoleId, cancellationToken);
        await EnsureDemoUserAsync(context, passwordService, DummyLandlordUserId, "pham.minh.landlord@example.com", "Pham Minh", RoleSeed.LandlordRoleId, cancellationToken);

        foreach (var tenant in SecondaryLandlordTenantSeeds)
        {
            await EnsureDemoUserAsync(context, passwordService, tenant.UserId, tenant.Email, tenant.FullName, RoleSeed.TenantRoleId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static SecondaryLandlordRoomSeed[] GetSecondaryLandlordRoomSeeds() =>
    [
        new(SunriseHouseId, SunriseRoomA1Id, "Khu trọ An Phú", "A101", 1, 18m, 2, 3200000m),
        new(SunriseHouseId, SunriseRoomA2Id, "Khu trọ An Phú", "A102", 1, 21m, 2, 3400000m),
        new(SunriseHouseId, SunriseRoomB1Id, "Khu trọ An Phú", "A103", 2, 25m, 3, 3800000m),
        new(SunriseHouseId, AnPhuRoomA104Id, "Khu trọ An Phú", "A104", 2, 22m, 2, 3500000m),
        new(SunriseHouseId, AnPhuRoomA105Id, "Khu trọ An Phú", "A105", 3, 24m, 3, 3900000m),
        new(GreenViewHouseId, GreenViewRoom101Id, "Nhà trọ Minh Khang", "B201", 1, 20m, 2, 3300000m),
        new(GreenViewHouseId, GreenViewRoom102Id, "Nhà trọ Minh Khang", "B202", 1, 24m, 3, 3700000m),
        new(GreenViewHouseId, MinhKhangRoomB203Id, "Nhà trọ Minh Khang", "B203", 2, 23m, 2, 3600000m),
        new(GreenViewHouseId, MinhKhangRoomB204Id, "Nhà trọ Minh Khang", "B204", 2, 26m, 3, 4100000m),
        new(GreenViewHouseId, MinhKhangRoomB205Id, "Nhà trọ Minh Khang", "B205", 3, 28m, 3, 4300000m)
    ];

    private static readonly SecondaryLandlordTenantSeed[] SecondaryLandlordTenantSeeds =
    [
        new(BulkInvoiceTenantUserId, BulkInvoiceTenantEmail, "Hoàng Minh", "0902000001", new DateOnly(1999, 9, 9)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000108"), "tran.minh.thu.demo@example.com", "Trần Minh Thư", "0902000002", new DateOnly(2000, 2, 12)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000109"), "le.gia.huy.demo@example.com", "Lê Gia Huy", "0902000003", new DateOnly(1998, 8, 20)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000110"), "pham.thao.nguyen.demo@example.com", "Phạm Thảo Nguyên", "0902000004", new DateOnly(2001, 3, 15)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000111"), "nguyen.huu.dat.demo@example.com", "Nguyễn Hữu Đạt", "0902000005", new DateOnly(1997, 11, 4)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000112"), "doan.ngoc.anh.demo@example.com", "Đoàn Ngọc Anh", "0902000006", new DateOnly(2002, 1, 27)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000113"), "bui.quang.khai.demo@example.com", "Bùi Quang Khải", "0902000007", new DateOnly(1999, 6, 18)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000114"), "vo.lan.chi.demo@example.com", "Võ Lan Chi", "0902000008", new DateOnly(2000, 12, 8)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000115"), "dang.minh.khoa.demo@example.com", "Đặng Minh Khoa", "0902000009", new DateOnly(1998, 5, 25)),
        new(Guid.Parse("10000000-0000-0000-0000-000000000116"), "mai.phuong.linh.demo@example.com", "Mai Phương Linh", "0902000010", new DateOnly(2001, 7, 2))
    ];

    private static async Task EnsureOperationalRoomAsync(
        AppDbContext context,
        SecondaryLandlordRoomSeed seed,
        CancellationToken cancellationToken)
    {
        var room = await context.Rooms.FirstOrDefaultAsync(x => x.Id == seed.RoomId, cancellationToken);
        if (room is null)
        {
            room = new Room
            {
                Id = seed.RoomId,
                CreatedAt = SeededAt
            };
            context.Rooms.Add(room);
        }

        room.RoomingHouseId = seed.RoomingHouseId;
        room.RoomNumber = seed.RoomNumber;
        room.Floor = seed.Floor;
        room.AreaM2 = seed.AreaM2;
        room.MaxOccupants = seed.MaxOccupants;
        room.IsTieredPricing = seed.MaxOccupants > 1;
        room.Status = RoomStatus.Occupied;
        room.Description = $"Phòng {seed.RoomNumber} đang thuê, dùng cho luồng dashboard và hóa đơn chủ trọ.";
        room.UpdatedAt = DateTimeOffset.UtcNow;

        if (!await context.RoomAmenities.AnyAsync(x => x.RoomId == seed.RoomId, cancellationToken))
        {
            context.RoomAmenities.AddRange(
                CreateRoomAmenity(seed.RoomId, AmenitySeed.WifiId),
                CreateRoomAmenity(seed.RoomId, AmenitySeed.PrivateBathroomId),
                CreateRoomAmenity(seed.RoomId, AmenitySeed.AirConditionerId));
        }

        await EnsureRoomPriceTierAsync(context, seed.RoomId, 1, seed.MonthlyRent, cancellationToken);
        if (seed.MaxOccupants >= 2)
        {
            await EnsureRoomPriceTierAsync(context, seed.RoomId, 2, seed.MonthlyRent + 450000m, cancellationToken);
        }
        if (seed.MaxOccupants >= 3)
        {
            await EnsureRoomPriceTierAsync(context, seed.RoomId, 3, seed.MonthlyRent + 850000m, cancellationToken);
        }
    }

    private static async Task EnsureOperationalViewingAppointmentAsync(
        AppDbContext context,
        Guid appointmentId,
        Guid roomId,
        Guid tenantUserId,
        int index,
        CancellationToken cancellationToken)
    {
        var appointment = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);
        if (appointment is null)
        {
            appointment = new ViewingAppointment { Id = appointmentId };
            context.ViewingAppointments.Add(appointment);
        }

        var scheduledAt = SeedVietnamTime(2026, 3, 4 + index, 9);
        appointment.RoomId = roomId;
        appointment.TenantUserId = tenantUserId;
        appointment.CreatedByUserId = tenantUserId;
        appointment.ScheduledAt = scheduledAt;
        appointment.DurationMinutes = 45;
        appointment.Status = ViewingAppointmentStatus.Completed;
        appointment.TenantNote = "Muốn xem phòng trước khi gửi yêu cầu thuê.";
        appointment.LandlordNote = "Khách đã xem phòng, đồng ý gửi yêu cầu thuê.";
        appointment.RespondedAt = scheduledAt.AddHours(-6);
        appointment.CancelReason = null;
        appointment.CreatedAt = scheduledAt.AddDays(-1);
        appointment.UpdatedAt = scheduledAt.AddHours(1);
    }

    private static async Task EnsureOperationalRentalRequestAsync(
        AppDbContext context,
        Guid requestId,
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        int index,
        CancellationToken cancellationToken)
    {
        var request = await context.RentalRequests.FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null)
        {
            request = new RentalRequest { Id = requestId, CreatedAt = SeedVietnamTime(2026, 3, 6 + index, 10) };
            context.RentalRequests.Add(request);
        }

        request.RoomId = roomSeed.RoomId;
        request.TenantUserId = tenantSeed.UserId;
        request.ApprovedByLandlordId = SecondaryLandlordUserId;
        request.DesiredStartDate = new DateOnly(2026, 4, 1);
        request.ExpectedEndDate = new DateOnly(2027, 3, 31);
        request.ExpectedOccupantCount = 1;
        request.MonthlyRentSnapshot = roomSeed.MonthlyRent;
        request.DepositAmountSnapshot = roomSeed.MonthlyRent;
        request.TenantNote = $"Đã xem phòng {roomSeed.RoomNumber}, mong muốn thuê từ tháng 04/2026.";
        request.Status = RentalRequestStatus.Accepted;
        request.RespondedAt = request.CreatedAt.AddHours(8);
        request.RejectedReason = null;
        request.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureOperationalDepositAsync(
        AppDbContext context,
        Guid depositId,
        Guid requestId,
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        CancellationToken cancellationToken)
    {
        var deposit = await context.RoomDeposits.FirstOrDefaultAsync(x => x.Id == depositId, cancellationToken);
        if (deposit is null)
        {
            deposit = new RoomDeposit { Id = depositId, CreatedAt = SeedVietnamTime(2026, 3, 8, 9) };
            context.RoomDeposits.Add(deposit);
        }

        deposit.RentalRequestId = requestId;
        deposit.RoomId = roomSeed.RoomId;
        deposit.TenantUserId = tenantSeed.UserId;
        deposit.LandlordUserId = SecondaryLandlordUserId;
        deposit.DepositAmount = roomSeed.MonthlyRent;
        deposit.Currency = "VND";
        deposit.Status = RoomDepositStatus.Paid;
        deposit.PaymentDeadlineAt = deposit.CreatedAt.AddDays(2);
        deposit.PaidAt = deposit.CreatedAt.AddHours(5);
        deposit.RefundedAt = null;
        deposit.ForfeitedAt = null;
        deposit.RefundAmount = null;
        deposit.ForfeitedAmount = null;
        deposit.Note = $"Tiền cọc phòng {roomSeed.RoomNumber} đã thanh toán trước khi ký hợp đồng.";
        deposit.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureOperationalContractAsync(
        AppDbContext context,
        Guid contractId,
        Guid requestId,
        Guid depositId,
        Guid occupantId,
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        string contractNumber,
        CancellationToken cancellationToken)
    {
        var contract = await context.RentalContracts.FirstOrDefaultAsync(x => x.Id == contractId || x.ContractNumber == contractNumber, cancellationToken);
        if (contract is null)
        {
            contract = new RentalContract { Id = contractId, CreatedAt = SeedVietnamTime(2026, 3, 10, 9) };
            context.RentalContracts.Add(contract);
        }

        contract.RentalRequestId = requestId;
        contract.RoomDepositId = depositId;
        contract.RoomId = roomSeed.RoomId;
        contract.MainTenantUserId = tenantSeed.UserId;
        contract.ContractNumber = contractNumber;
        contract.StartDate = new DateOnly(2026, 4, 1);
        contract.EndDate = new DateOnly(2027, 3, 31);
        contract.MonthlyRent = roomSeed.MonthlyRent;
        contract.DepositAmount = roomSeed.MonthlyRent;
        contract.PaymentDay = 5;
        contract.Status = RentalContractStatus.Active;
        contract.RoomSnapshot = $$"""{"RoomNumber":"{{roomSeed.RoomNumber}}","RoomingHouseName":"{{roomSeed.HouseName}}","MaxOccupants":{{roomSeed.MaxOccupants}},"OccupantCount":1}""";
        contract.SignatureDeadlineAt = contract.CreatedAt.AddDays(3);
        contract.ActivatedAt = contract.CreatedAt.AddDays(1);
        contract.TerminationDate = null;
        contract.TerminationType = null;
        contract.StatusReason = "Hợp đồng active dùng cho dashboard và hóa đơn hàng loạt.";
        contract.DeletedAt = null;
        contract.UpdatedAt = DateTimeOffset.UtcNow;

        var occupant = await context.ContractOccupants.FirstOrDefaultAsync(x => x.Id == occupantId, cancellationToken);
        if (occupant is null)
        {
            occupant = new ContractOccupant { Id = occupantId, CreatedAt = contract.CreatedAt };
            context.ContractOccupants.Add(occupant);
        }

        occupant.RentalContractId = contract.Id;
        occupant.UserId = tenantSeed.UserId;
        occupant.FullName = tenantSeed.FullName;
        occupant.PhoneNumber = tenantSeed.PhoneNumber;
        occupant.DateOfBirth = tenantSeed.DateOfBirth;
        occupant.RelationshipToMainTenant = "Self";
        occupant.MoveInDate = contract.StartDate;
        occupant.MoveOutDate = null;
        occupant.Status = ContractOccupantStatus.Active;
        occupant.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureOperationalContractDocumentsAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid contractId,
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        string contractNumber,
        CancellationToken cancellationToken)
    {
        var signedBytes = BuildOperationalContractPdf(roomSeed, tenantSeed, contractNumber);
        await EnsureContractPdfFileForContractAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            CreateSeedGuid($"xunhuns:contract-file:signed:{contractNumber}"),
            contractId,
            SecondaryLandlordUserId,
            ContractFilePurpose.SignedLegalDocument,
            $"{contractNumber.ToLowerInvariant()}-signed.pdf",
            signedBytes,
            isLegallySigned: true,
            cancellationToken);

        await EnsureContractSignatureForContractAsync(
            context,
            CreateSeedGuid($"xunhuns:signature:landlord:{contractNumber}"),
            contractId,
            SecondaryLandlordUserId,
            ContractSignerRole.Landlord,
            1,
            $"VNPT-XHUNS-LANDLORD-{roomSeed.RoomNumber}",
            "CN=Nguyễn Xuân Huấn, O=VNPT SmartCA, C=VN",
            cancellationToken);

        await EnsureContractSignatureForContractAsync(
            context,
            CreateSeedGuid($"xunhuns:signature:tenant:{contractNumber}"),
            contractId,
            tenantSeed.UserId,
            ContractSignerRole.Tenant,
            2,
            $"VNPT-XHUNS-TENANT-{roomSeed.RoomNumber}",
            $"CN={tenantSeed.FullName}, O=VNPT SmartCA, C=VN",
            cancellationToken);
    }

    private static byte[] BuildOperationalContractPdf(
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        string contractNumber)
    {
        var model = new ContractDocumentModel
        {
            PreparedAt = SeedVietnamTime(2026, 3, 10, 9),
            ContractNumber = contractNumber,
            Landlord = new ContractDocumentParty
            {
                UserId = SecondaryLandlordUserId,
                FullName = "Nguyễn Xuân Huấn",
                DateOfBirth = new DateOnly(1995, 8, 8),
                DocumentNumber = "079********103",
                Address = "Đà Nẵng",
                PhoneNumber = "0901000021",
                Email = SecondaryLandlordEmail
            },
            Tenant = new ContractDocumentParty
            {
                UserId = tenantSeed.UserId,
                FullName = tenantSeed.FullName,
                DateOfBirth = tenantSeed.DateOfBirth,
                DocumentNumber = "079********200",
                Address = "Đà Nẵng",
                PhoneNumber = tenantSeed.PhoneNumber,
                Email = tenantSeed.Email
            },
            Property = new ContractDocumentProperty
            {
                RoomId = roomSeed.RoomId,
                RoomNumber = roomSeed.RoomNumber,
                RoomingHouseName = roomSeed.HouseName,
                Address = roomSeed.HouseName == "Khu trọ An Phú"
                    ? "88 Đường An Phú, Phường Ngũ Hành Sơn, Thành phố Đà Nẵng"
                    : "12 Phạm Văn Đồng, Phường Ngũ Hành Sơn, Thành phố Đà Nẵng",
                Floor = roomSeed.Floor,
                AreaM2 = roomSeed.AreaM2,
                MaxOccupants = roomSeed.MaxOccupants,
                Description = $"Phòng {roomSeed.RoomNumber} đang thuê tại {roomSeed.HouseName}."
            },
            FinancialTerms = new ContractDocumentFinancialTerms
            {
                StartDate = new DateOnly(2026, 4, 1),
                EndDate = new DateOnly(2027, 3, 31),
                MonthlyRent = roomSeed.MonthlyRent,
                DepositAmount = roomSeed.MonthlyRent,
                PaymentDay = 5,
                DepositPaidAt = SeedVietnamTime(2026, 3, 8, 14)
            },
            ServicePrices = new[]
            {
                new ContractDocumentServicePrice { ServiceName = "Điện", PricingUnit = "kWh", UnitPrice = 4000m, EffectiveFrom = new DateOnly(2026, 4, 1) },
                new ContractDocumentServicePrice { ServiceName = "Nước", PricingUnit = "m3", UnitPrice = 18000m, EffectiveFrom = new DateOnly(2026, 4, 1) },
                new ContractDocumentServicePrice { ServiceName = "Internet", PricingUnit = "tháng", UnitPrice = 120000m, EffectiveFrom = new DateOnly(2026, 4, 1) }
            },
            Occupants = new[]
            {
                new ContractDocumentOccupant
                {
                    OccupantId = CreateSeedGuid($"xunhuns:doc-occupant:{contractNumber}"),
                    UserId = tenantSeed.UserId,
                    FullName = tenantSeed.FullName,
                    DateOfBirth = tenantSeed.DateOfBirth,
                    DocumentNumber = "079********200",
                    Relationship = "Self",
                    MoveInDate = new DateOnly(2026, 4, 1)
                }
            },
            HouseRules = new[]
            {
                "Giữ vệ sinh chung, không gây ồn sau 22:30.",
                "Tiền điện nước được tính theo chỉ số thực tế mỗi tháng.",
                "Người thuê thanh toán hóa đơn trước ngày 05 hằng tháng."
            }
        };

        var renderer = new ContractPdfRenderer();
        return renderer.RenderSignedRentalContract(
            model,
            new ContractRenderOptions
            {
                ViewerMode = ContractFilePurpose.SignedLegalDocument.ToString(),
                ShowFullDocumentNumbers = false
            });
    }

    private static async Task<MeterReading> EnsureOperationalMeterReadingAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid id,
        Guid roomId,
        Guid contractId,
        Guid serviceTypeId,
        string fileName,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal previousReading,
        decimal currentReading,
        string aiRawText,
        CancellationToken cancellationToken)
    {
        var reusableProofMediaAssetId = await context.MeterReadings
            .Where(x => x.Id == (fileName == "1341.png" ? LinhElectricReadingId : LinhWaterReadingId))
            .Select(x => x.ProofMediaAssetId)
            .FirstOrDefaultAsync(cancellationToken);

        if (reusableProofMediaAssetId is null)
        {
            var bytes = LoadRequiredDemoImageBytes(fileName);
            var mediaAsset = await EnsureBinaryMediaAssetAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                CreateSeedGuid($"xunhuns:meter-media:{id}"),
                SecondaryLandlordUserId,
                MediaScope.MeterReadingImage,
                MediaVisibility.Private,
                $"{Path.GetFileNameWithoutExtension(fileName)}-{id:N}.png",
                "image/png",
                bytes,
                nameof(MeterReading),
                id,
                cancellationToken);
            reusableProofMediaAssetId = mediaAsset.Id;
        }

        var reading = await context.MeterReadings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reading is null)
        {
            reading = new MeterReading { Id = id, CreatedAt = SeededAt.AddMonths(periodStart.Month) };
            context.MeterReadings.Add(reading);
        }

        reading.RoomId = roomId;
        reading.ContractId = contractId;
        reading.ServiceTypeId = serviceTypeId;
        reading.BillingPeriodStart = periodStart;
        reading.BillingPeriodEnd = periodEnd;
        reading.PreviousReading = previousReading;
        reading.CurrentReading = currentReading;
        reading.Consumption = currentReading - previousReading;
        reading.ProofMediaAssetId = reusableProofMediaAssetId;
        reading.AiReading = currentReading;
        reading.AiRawText = aiRawText;
        reading.WasManuallyCorrected = false;
        reading.RecordedByLandlordUserId = SecondaryLandlordUserId;
        reading.ReadingAt = SeedVietnamTime(periodEnd.Year, periodEnd.Month, periodEnd.Day, 8);
        reading.UpdatedAt = DateTimeOffset.UtcNow;
        return reading;
    }

    private static async Task<decimal> EnsureOperationalInvoiceAsync(
        AppDbContext context,
        Guid invoiceId,
        Guid contractId,
        SecondaryLandlordRoomSeed roomSeed,
        SecondaryLandlordTenantSeed tenantSeed,
        DateOnly periodStart,
        DateOnly periodEnd,
        InvoiceStatus status,
        MeterReading electricReading,
        MeterReading waterReading,
        CancellationToken cancellationToken)
    {
        var electricAmount = electricReading.Consumption * 4000m;
        var waterAmount = waterReading.Consumption * 18000m;
        const decimal internetAmount = 120000m;
        const decimal trashAmount = 30000m;
        var serviceAmount = internetAmount + trashAmount;
        var utilityAmount = electricAmount + waterAmount;
        var totalAmount = roomSeed.MonthlyRent + utilityAmount + serviceAmount;
        var invoiceNo = $"HD-XHUNS-{roomSeed.RoomNumber}-{periodStart:yyyyMM}";

        var invoice = await context.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId || x.InvoiceNo == invoiceNo, cancellationToken);
        if (invoice is null)
        {
            invoice = new Invoice { Id = invoiceId, CreatedAt = SeededAt.AddMonths(periodStart.Month) };
            context.Invoices.Add(invoice);
        }

        invoice.ContractId = contractId;
        invoice.RoomId = roomSeed.RoomId;
        invoice.TenantUserId = tenantSeed.UserId;
        invoice.LandlordUserId = SecondaryLandlordUserId;
        invoice.InvoiceNo = invoiceNo;
        invoice.BillingPeriodStart = periodStart;
        invoice.BillingPeriodEnd = periodEnd;
        invoice.IssueDate = status == InvoiceStatus.Draft ? null : periodEnd;
        invoice.DueDate = periodEnd.AddDays(5);
        invoice.RentAmount = roomSeed.MonthlyRent;
        invoice.UtilityAmount = utilityAmount;
        invoice.ServiceAmount = serviceAmount;
        invoice.DiscountAmount = 0m;
        invoice.TotalAmount = totalAmount;
        invoice.Status = status;
        invoice.Note = status == InvoiceStatus.Draft
            ? "Hóa đơn tháng hiện tại chờ phát hành hàng loạt."
            : "Hóa đơn đã thanh toán, dùng cho dashboard doanh thu chủ trọ.";
        invoice.SentAt = status == InvoiceStatus.Draft ? null : SeededAt.AddMonths(periodStart.Month).AddDays(2);
        invoice.PaidAt = status == InvoiceStatus.Paid ? SeededAt.AddMonths(periodStart.Month).AddDays(3) : null;
        invoice.CancelledAt = null;
        invoice.CancelReason = null;
        invoice.WalletTransferGroupId = status == InvoiceStatus.Paid
            ? CreateSeedGuid($"xunhuns:invoice-transfer:{roomSeed.RoomNumber}:{periodStart:yyyyMM}")
            : null;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await ReplaceInvoiceItemsAsync(
            context,
            invoice.Id,
            new[]
            {
                CreateInvoiceItem(CreateSeedGuid($"xunhuns:item:rent:{invoiceNo}"), invoice.Id, null, null, InvoiceItemType.Rent, $"Tiền phòng {roomSeed.RoomNumber} tháng {periodStart:MM/yyyy}", 1, roomSeed.MonthlyRent),
                CreateInvoiceItem(CreateSeedGuid($"xunhuns:item:electric:{invoiceNo}"), invoice.Id, ElectricServiceTypeId, electricReading.Id, InvoiceItemType.Service, $"Điện tháng {periodStart:MM/yyyy}: {electricReading.CurrentReading} - {electricReading.PreviousReading} = {electricReading.Consumption} kWh", electricReading.Consumption, 4000m),
                CreateInvoiceItem(CreateSeedGuid($"xunhuns:item:water:{invoiceNo}"), invoice.Id, WaterServiceTypeId, waterReading.Id, InvoiceItemType.Service, $"Nước tháng {periodStart:MM/yyyy}: {waterReading.CurrentReading} - {waterReading.PreviousReading} = {waterReading.Consumption} m3", waterReading.Consumption, 18000m),
                CreateInvoiceItem(CreateSeedGuid($"xunhuns:item:internet:{invoiceNo}"), invoice.Id, InternetServiceTypeId, null, InvoiceItemType.Service, $"Internet tháng {periodStart:MM/yyyy}", 1, internetAmount),
                CreateInvoiceItem(CreateSeedGuid($"xunhuns:item:trash:{invoiceNo}"), invoice.Id, TrashServiceTypeId, null, InvoiceItemType.Service, $"Rác và vệ sinh chung tháng {periodStart:MM/yyyy}", 1, trashAmount)
            },
            cancellationToken);

        return totalAmount;
    }

    private static async Task EnsureSecondaryLandlordWithdrawalAsync(
        AppDbContext context,
        decimal landlordBalance,
        CancellationToken cancellationToken)
    {
        var withdrawalId = CreateSeedGuid("xunhuns:withdrawal:completed:202607");
        var withdrawal = await context.WithdrawalRequests.FirstOrDefaultAsync(x => x.Id == withdrawalId, cancellationToken);
        if (withdrawal is null)
        {
            withdrawal = new WithdrawalRequest { Id = withdrawalId };
            context.WithdrawalRequests.Add(withdrawal);
        }

        withdrawal.WalletAccountId = SecondaryLandlordWalletAccountId;
        withdrawal.Amount = 5000000m;
        withdrawal.Fee = 0m;
        withdrawal.Status = WithdrawalStatus.Succeeded;
        withdrawal.ProviderOrderCode = "WD-XHUNS-202607-0001";
        withdrawal.ProviderTransactionCode = "PAYOS-WD-XHUNS-202607-0001";
        withdrawal.BankBin = "970422";
        withdrawal.AccountName = "NGUYEN XUAN HUAN";
        withdrawal.AccountNumber = "0123456789";
        withdrawal.Description = "Rút tiền demo sau khi nhận doanh thu hóa đơn.";
        withdrawal.IdempotencyKey = "xunhuns-withdrawal-demo-202607";
        withdrawal.CreatedAt = SeedVietnamTime(2026, 7, 18, 9);
        withdrawal.UpdatedAt = withdrawal.CreatedAt.AddHours(2);

        await EnsureWalletTransactionAsync(
            context,
            CreateSeedGuid("xunhuns:withdrawal:succeeded:202607"),
            SecondaryLandlordWalletAccountId,
            SecondaryLandlordUserId,
            WalletTransactionType.WalletWithdrawalSucceeded,
            WalletTransactionDirection.Debit,
            withdrawal.Amount,
            landlordBalance,
            landlordBalance - withdrawal.Amount,
            0m,
            0m,
            nameof(WithdrawalRequest),
            withdrawal.Id,
            "Rút tiền demo từ ví chủ trọ Xuân Huấn.",
            withdrawal.UpdatedAt,
            cancellationToken);
    }

    private static async Task EnsureSecondaryLandlordChatAsync(
        AppDbContext context,
        IReadOnlyList<SecondaryLandlordRoomSeed> roomSeeds,
        IReadOnlyList<SecondaryLandlordTenantSeed> tenantSeeds,
        CancellationToken cancellationToken)
    {
        var conversationId = CreateSeedGuid("xunhuns:chat:cu-dan-an-phu");
        var conversation = await context.Conversations
            .Include(x => x.Participants)
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = conversationId,
                CreatedAt = SeedVietnamTime(2026, 7, 1, 8)
            };
            context.Conversations.Add(conversation);
        }

        conversation.Type = ConversationType.Group;
        conversation.Title = "Cư dân Khu trọ An Phú";
        conversation.RoomingHouseId = SunriseHouseId;
        conversation.RoomId = null;
        conversation.DirectUserAId = null;
        conversation.DirectUserBId = null;
        conversation.CreatedByUserId = SecondaryLandlordUserId;
        conversation.RequiresJoinApproval = true;
        conversation.IsClosed = false;
        conversation.LastMessageAt = SeedVietnamTime(2026, 7, 5, 20, 10);
        conversation.LastMessagePreview = "Tối nay mọi người để xe gọn trong sân giúp anh nhé.";
        conversation.UpdatedAt = conversation.LastMessageAt.Value;

        await EnsureConversationParticipantAsync(context, conversation.Id, SecondaryLandlordUserId, ConversationParticipantRole.Owner, SecondaryLandlordUserId, cancellationToken);
        for (var i = 0; i < 5; i++)
        {
            await EnsureConversationParticipantAsync(context, conversation.Id, tenantSeeds[i].UserId, i == 0 ? ConversationParticipantRole.Admin : ConversationParticipantRole.Member, SecondaryLandlordUserId, cancellationToken);
        }

        await EnsureChatMessageAsync(
            context,
            CreateSeedGuid("xunhuns:chat:msg:1"),
            conversation.Id,
            SecondaryLandlordUserId,
            ChatMessageType.System,
            "Xuân Huấn đã tạo nhóm cư dân Khu trọ An Phú.",
            SeedVietnamTime(2026, 7, 1, 8),
            cancellationToken);
        await EnsureChatMessageAsync(
            context,
            CreateSeedGuid("xunhuns:chat:msg:2"),
            conversation.Id,
            BulkInvoiceTenantUserId,
            ChatMessageType.Text,
            "Em đã nhận phòng A101, cảm ơn anh đã hỗ trợ hợp đồng nhanh.",
            SeedVietnamTime(2026, 7, 1, 19, 30),
            cancellationToken);
        await EnsureChatMessageAsync(
            context,
            CreateSeedGuid("xunhuns:chat:msg:3"),
            conversation.Id,
            SecondaryLandlordUserId,
            ChatMessageType.Text,
            "Tối nay mọi người để xe gọn trong sân giúp anh nhé.",
            SeedVietnamTime(2026, 7, 5, 20, 10),
            cancellationToken);
    }

    private static async Task EnsureSecondaryLandlordReviewsAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new SecondaryReviewSeed(
                CreateSeedGuid("xunhuns:review:an-phu"),
                CreateSeedGuid("xunhuns:review-contract:an-phu"),
                CreateSeedGuid("xunhuns:review-request:an-phu"),
                CreateSeedGuid("xunhuns:review-deposit:an-phu"),
                SunriseHouseId,
                SunriseRoomA2Id,
                SecondaryLandlordTenantSeeds[1],
                5,
                "Phòng sạch, khu để xe rộng và chủ trọ phản hồi rất nhanh khi cần hỗ trợ."),
            new SecondaryReviewSeed(
                CreateSeedGuid("xunhuns:review:minh-khang"),
                CreateSeedGuid("xunhuns:review-contract:minh-khang"),
                CreateSeedGuid("xunhuns:review-request:minh-khang"),
                CreateSeedGuid("xunhuns:review-deposit:minh-khang"),
                GreenViewHouseId,
                GreenViewRoom102Id,
                SecondaryLandlordTenantSeeds[7],
                4,
                "Khu trọ yên tĩnh, hóa đơn điện nước rõ ràng, phù hợp ở lâu dài.")
        };

        foreach (var seed in seeds)
        {
            await EnsureEndedReviewContractAsync(context, seed, cancellationToken);
            var review = await context.RoomingHouseReviews.FirstOrDefaultAsync(x => x.Id == seed.ReviewId, cancellationToken);
            if (review is null)
            {
                review = new RoomingHouseReview { Id = seed.ReviewId };
                context.RoomingHouseReviews.Add(review);
            }

            review.RoomingHouseId = seed.RoomingHouseId;
            review.TenantUserId = seed.Tenant.UserId;
            review.RentalContractId = seed.ContractId;
            review.Rating = seed.Rating;
            review.Comment = seed.Comment;
            review.LandlordReply = "Cảm ơn bạn đã thuê phòng và góp ý. Anh sẽ tiếp tục giữ khu trọ sạch sẽ, hỗ trợ cư dân nhanh nhất có thể.";
            review.LandlordReplyCreatedAt = SeedVietnamTime(2026, 3, 4, 10);
            review.IsHidden = false;
            review.ModerationStatus = RoomingHouseReviewModerationStatus.Approved;
            review.ModerationReason = "Seed review approved.";
            review.AiModerationProvider = "seed";
            review.AiModerationRiskLevel = "Low";
            review.AiModerationCategories = "[]";
            review.AiModerationJson = "{\"contentComment\":\"Seed review approved\",\"imageComment\":\"Real image approved\"}";
            review.AiReviewedAt = SeedVietnamTime(2026, 3, 3, 9);
            review.ReviewedByAdminId = await ResolveSeedAdminUserIdAsync(context, cancellationToken);
            review.AdminReviewedAt = review.AiReviewedAt;
            review.AdminNote = "Seed review with valid rental timeline.";
            review.CreatedAt = SeedVietnamTime(2026, 3, 3, 8);
            review.UpdatedAt = DateTimeOffset.UtcNow;

            await EnsureReviewImageAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                review.Id,
                seed.Tenant.UserId,
                $"Ảnh đánh giá {seed.RoomingHouseId:N}",
                cancellationToken);

            await RoomingHouseRatingHelper.UpdateRatingAsync(context, seed.RoomingHouseId, cancellationToken);
        }
    }

    private static async Task EnsureEndedReviewContractAsync(
        AppDbContext context,
        SecondaryReviewSeed seed,
        CancellationToken cancellationToken)
    {
        var request = await context.RentalRequests.FirstOrDefaultAsync(x => x.Id == seed.RentalRequestId, cancellationToken);
        if (request is null)
        {
            request = new RentalRequest { Id = seed.RentalRequestId, CreatedAt = SeedVietnamTime(2025, 8, 20, 9) };
            context.RentalRequests.Add(request);
        }

        request.RoomId = seed.RoomId;
        request.TenantUserId = seed.Tenant.UserId;
        request.ApprovedByLandlordId = SecondaryLandlordUserId;
        request.DesiredStartDate = new DateOnly(2025, 9, 1);
        request.ExpectedEndDate = new DateOnly(2026, 2, 28);
        request.ExpectedOccupantCount = 1;
        request.MonthlyRentSnapshot = 3000000m;
        request.DepositAmountSnapshot = 3000000m;
        request.TenantNote = "Đã xem phòng và thuê đủ kỳ, dùng làm dữ liệu đánh giá sau khi kết thúc.";
        request.Status = RentalRequestStatus.Accepted;
        request.RespondedAt = request.CreatedAt.AddHours(6);
        request.RejectedReason = null;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        var deposit = await context.RoomDeposits.FirstOrDefaultAsync(x => x.Id == seed.RoomDepositId, cancellationToken);
        if (deposit is null)
        {
            deposit = new RoomDeposit { Id = seed.RoomDepositId, CreatedAt = request.CreatedAt.AddDays(1) };
            context.RoomDeposits.Add(deposit);
        }

        deposit.RentalRequestId = request.Id;
        deposit.RoomId = seed.RoomId;
        deposit.TenantUserId = seed.Tenant.UserId;
        deposit.LandlordUserId = SecondaryLandlordUserId;
        deposit.DepositAmount = 3000000m;
        deposit.Currency = "VND";
        deposit.Status = RoomDepositStatus.Paid;
        deposit.PaymentDeadlineAt = deposit.CreatedAt.AddDays(2);
        deposit.PaidAt = deposit.CreatedAt.AddHours(4);
        deposit.RefundedAt = SeedVietnamTime(2026, 2, 28, 16);
        deposit.ForfeitedAt = null;
        deposit.RefundAmount = 3000000m;
        deposit.ForfeitedAmount = null;
        deposit.Note = "Cọc đã hoàn khi hợp đồng kết thúc đúng hạn.";
        deposit.UpdatedAt = DateTimeOffset.UtcNow;

        var contract = await context.RentalContracts.FirstOrDefaultAsync(x => x.Id == seed.ContractId, cancellationToken);
        if (contract is null)
        {
            contract = new RentalContract { Id = seed.ContractId, CreatedAt = request.CreatedAt.AddDays(2) };
            context.RentalContracts.Add(contract);
        }

        contract.RentalRequestId = request.Id;
        contract.RoomDepositId = deposit.Id;
        contract.RoomId = seed.RoomId;
        contract.MainTenantUserId = seed.Tenant.UserId;
        contract.ContractNumber = $"HD-XHUNS-REVIEW-{seed.ContractId.ToString("N")[^8..].ToUpperInvariant()}";
        contract.StartDate = new DateOnly(2025, 9, 1);
        contract.EndDate = new DateOnly(2026, 2, 28);
        contract.MonthlyRent = 3000000m;
        contract.DepositAmount = 3000000m;
        contract.PaymentDay = 5;
        contract.Status = RentalContractStatus.Expired;
        contract.RoomSnapshot = "{}";
        contract.SignatureDeadlineAt = contract.CreatedAt.AddDays(3);
        contract.ActivatedAt = contract.CreatedAt.AddDays(1);
        contract.TerminationDate = new DateOnly(2026, 2, 28);
        contract.TerminationType = ContractTerminationType.NormalExpiration;
        contract.StatusReason = "Hợp đồng đã kết thúc đúng hạn, đủ điều kiện đánh giá.";
        contract.DeletedAt = null;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureConversationParticipantAsync(
        AppDbContext context,
        Guid conversationId,
        Guid userId,
        ConversationParticipantRole role,
        Guid addedByUserId,
        CancellationToken cancellationToken)
    {
        var participant = await context.ConversationParticipants
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.UserId == userId, cancellationToken);
        if (participant is null)
        {
            participant = new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = userId,
                JoinedAt = SeedVietnamTime(2026, 7, 1, 8)
            };
            context.ConversationParticipants.Add(participant);
        }

        participant.Role = role;
        participant.Source = ConversationParticipantSource.Manual;
        participant.AddedByUserId = addedByUserId;
        participant.LeftAt = null;
        participant.LastReadAt = SeedVietnamTime(2026, 7, 5, 20, 30);
        participant.UnreadCount = 0;
        participant.IsMuted = false;
        participant.InboxStatus = ConversationParticipantInboxStatus.Main;
        participant.InboxStatusUpdatedAt = participant.JoinedAt;
        participant.InboxStatusUpdatedByUserId = addedByUserId;
    }

    private static async Task EnsureChatMessageAsync(
        AppDbContext context,
        Guid messageId,
        Guid conversationId,
        Guid senderId,
        ChatMessageType messageType,
        string content,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var message = await context.ChatMessages.FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null)
        {
            message = new ChatMessage { Id = messageId };
            context.ChatMessages.Add(message);
        }

        message.ConversationId = conversationId;
        message.SenderId = senderId;
        message.MessageType = messageType;
        message.Content = content;
        message.MediaAssetId = null;
        message.ImageUrl = null;
        message.FileUrl = null;
        message.FileName = null;
        message.FileContentType = null;
        message.FileSize = null;
        message.CreatedAt = createdAt;
        message.DeletedAt = null;
    }

    private static async Task EnsureRoomingHouseImageSetAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid roomingHouseId,
        Guid ownerUserId,
        string slug,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 3; i++)
        {
            await EnsurePropertyImageAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                CreateSeedGuid($"xunhuns:house-image:{roomingHouseId}:{i}"),
                CreateSeedGuid($"xunhuns:house-media:{roomingHouseId}:{i}"),
                ownerUserId,
                roomingHouseId,
                roomId: null,
                reviewId: null,
                caption: i == 0 ? $"Ảnh bìa {slug}" : $"Không gian {slug} {i + 1}",
                isCover: i == 0,
                sortOrder: i,
                imageSelector: $"{slug}:house:{i}",
                cancellationToken);
        }
    }

    private static async Task EnsureRoomImageSetAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        SecondaryLandlordRoomSeed seed,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 3; i++)
        {
            await EnsurePropertyImageAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                CreateSeedGuid($"xunhuns:room-image:{seed.RoomId}:{i}"),
                CreateSeedGuid($"xunhuns:room-media:{seed.RoomId}:{i}"),
                SecondaryLandlordUserId,
                roomingHouseId: null,
                roomId: seed.RoomId,
                reviewId: null,
                caption: i == 0 ? $"Ảnh bìa phòng {seed.RoomNumber}" : $"Góc phòng {seed.RoomNumber} {i + 1}",
                isCover: i == 0,
                sortOrder: i,
                imageSelector: $"{seed.RoomNumber}:room:{i}",
                cancellationToken);
        }
    }

    private static async Task EnsureReviewImageAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid reviewId,
        Guid ownerUserId,
        string caption,
        CancellationToken cancellationToken)
    {
        await EnsurePropertyImageAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            CreateSeedGuid($"xunhuns:review-image:{reviewId}"),
            CreateSeedGuid($"xunhuns:review-media:{reviewId}"),
            ownerUserId,
            roomingHouseId: null,
            roomId: null,
            reviewId: reviewId,
            caption: caption,
            isCover: false,
            sortOrder: 0,
            imageSelector: $"review:{reviewId:N}",
            cancellationToken);
    }

    private static async Task EnsurePropertyImageAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid propertyImageId,
        Guid mediaAssetId,
        Guid ownerUserId,
        Guid? roomingHouseId,
        Guid? roomId,
        Guid? reviewId,
        string caption,
        bool isCover,
        int sortOrder,
        string imageSelector,
        CancellationToken cancellationToken)
    {
        var imageFile = PickDemoPropertyImage(imageSelector);
        var bytes = File.ReadAllBytes(imageFile);
        var contentType = ResolveImageContentType(imageFile);
        var mediaAsset = await EnsureBinaryMediaAssetAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            mediaAssetId,
            ownerUserId,
            MediaScope.RoomingHouseImage,
            MediaVisibility.Public,
            Path.GetFileName(imageFile),
            contentType,
            bytes,
            nameof(PropertyImage),
            propertyImageId,
            cancellationToken);

        var propertyImage = await context.PropertyImages.FirstOrDefaultAsync(x => x.Id == propertyImageId, cancellationToken);
        if (propertyImage is null)
        {
            propertyImage = new PropertyImage
            {
                Id = propertyImageId,
                CreatedAt = SeededAt
            };
            context.PropertyImages.Add(propertyImage);
        }

        propertyImage.RoomingHouseId = roomingHouseId;
        propertyImage.RoomId = roomId;
        propertyImage.RoomingHouseReviewId = reviewId;
        propertyImage.MediaAssetId = mediaAsset.Id;
        propertyImage.ImageUrl = PublicMediaPathBuilder.Build(mediaAsset.Id);
        propertyImage.Caption = caption;
        propertyImage.IsCover = isCover;
        propertyImage.SortOrder = sortOrder;
    }





    private static async Task EnsureContractPdfFileForContractAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid fileId,
        Guid contractId,
        Guid ownerUserId,
        ContractFilePurpose purpose,
        string fileName,
        byte[] pdfBytes,
        bool isLegallySigned,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await EnsureBinaryMediaAssetAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            CreateSeedGuid($"contract-file-media:{fileId}"),
            ownerUserId,
            MediaScope.ContractPdf,
            MediaVisibility.Private,
            fileName,
            "application/pdf",
            pdfBytes,
            nameof(ContractFile),
            fileId,
            cancellationToken);

        var contractFile = await context.ContractFiles.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);
        if (contractFile is null)
        {
            contractFile = new ContractFile { Id = fileId };
            context.ContractFiles.Add(contractFile);
        }

        contractFile.RentalContractId = contractId;
        contractFile.RentalContractAppendixId = null;
        contractFile.MediaAssetId = mediaAsset.Id;
        contractFile.Purpose = purpose;
        contractFile.ContentType = "application/pdf";
        contractFile.FileUrl = PublicMediaPathBuilder.Build(mediaAsset.Id);
        contractFile.Sha256Hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();
        contractFile.IsLegallySigned = isLegallySigned;
        contractFile.CreatedAt = SeededAt.AddMonths(3).AddDays(10);
    }

    private static async Task EnsureContractSignatureForContractAsync(
        AppDbContext context,
        Guid id,
        Guid contractId,
        Guid signerUserId,
        ContractSignerRole signerRole,
        int signingOrder,
        string providerParticipantId,
        string certificateSubject,
        CancellationToken cancellationToken)
    {
        var signature = await context.ContractSignatures.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (signature is null)
        {
            signature = new ContractSignature { Id = id };
            context.ContractSignatures.Add(signature);
        }

        signature.RentalContractId = contractId;
        signature.RentalContractAppendixId = null;
        signature.SignerUserId = signerUserId;
        signature.SignerRole = signerRole;
        signature.SignatureMethod = ContractSignatureMethod.VnptSmsOtp;
        signature.Status = ContractSignatureStatus.Signed;
        signature.SigningOrder = signingOrder;
        signature.Provider = ESignProvider.Vnpt;
        signature.ProviderEnvelopeId = $"VNPT-ECONTRACT-XHUNS-{contractId:N}";
        signature.ProviderParticipantId = providerParticipantId;
        signature.SigningUrl = null;
        signature.CertificateSerialNumber = $"VNPT-CA-XHUNS-{signingOrder:D2}-{contractId.ToString("N")[..8]}";
        signature.CertificateSubject = certificateSubject;
        signature.CertificateIssuer = "VNPT Certification Authority";
        signature.SignedFileSha256Hash = null;
        signature.ProviderEvidenceJson = $$"""{"provider":"VNPT eContract","auth_method":"SMS_OTP","participant":"{{providerParticipantId}}"}""";
        signature.NotifiedAt = SeededAt.AddMonths(3).AddDays(10).AddHours(signingOrder);
        signature.SignedAt = SeededAt.AddMonths(3).AddDays(10).AddHours(signingOrder + 1);
        signature.IpAddress = "127.0.0.1";
        signature.UserAgent = "Operational seed VNPT eContract";
        signature.CreatedAt = SeededAt.AddMonths(3).AddDays(10);
    }

    private static async Task RemoveInvoiceIfExistsAsync(
        AppDbContext context,
        Guid invoiceId,
        string invoiceNo,
        CancellationToken cancellationToken)
    {
        var invoices = await context.Invoices
            .Where(x => x.Id == invoiceId || x.InvoiceNo == invoiceNo)
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0)
        {
            return;
        }

        var invoiceIds = invoices.Select(x => x.Id).ToList();
        var items = await context.InvoiceItems
            .Where(x => invoiceIds.Contains(x.InvoiceId))
            .ToListAsync(cancellationToken);
        context.InvoiceItems.RemoveRange(items);
        context.Invoices.RemoveRange(invoices);
    }

    private static void EnsureDemoImageSourceReady()
    {
        if (!File.Exists(DemoElectricMeterImagePath))
        {
            throw new FileNotFoundException("Missing real electric meter image for demo seed.", DemoElectricMeterImagePath);
        }

        if (!File.Exists(DemoWaterMeterImagePath))
        {
            throw new FileNotFoundException("Missing real water meter image for demo seed.", DemoWaterMeterImagePath);
        }

        _ = PickDemoPropertyImage("seed-check");
    }

    private static byte[] LoadRequiredDemoImageBytes(string fileName)
    {
        var path = fileName.Contains("96", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("water", StringComparison.OrdinalIgnoreCase)
            ? DemoWaterMeterImagePath
            : DemoElectricMeterImagePath;

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Missing real meter image for demo seed.", path);
        }

        return File.ReadAllBytes(path);
    }

    private static string PickDemoPropertyImage(string selector)
    {
        if (!Directory.Exists(DemoImageDirectory))
        {
            throw new DirectoryNotFoundException($"Missing real image directory for demo seed: {DemoImageDirectory}");
        }

        var files = Directory
            .EnumerateFiles(DemoImageDirectory, "*.*", SearchOption.AllDirectories)
            .Where(x =>
            {
                var extension = Path.GetExtension(x).ToLowerInvariant();
                var fileName = Path.GetFileName(x);
                return extension is ".jpg" or ".jpeg" or ".png" or ".webp" &&
                       !fileName.Equals("1341.png", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Equals("96.png", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException($"No real rooming-house images found in {DemoImageDirectory}.");
        }

        var hash = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(selector));
        var index = BitConverter.ToUInt32(hash, 0) % files.Length;
        return files[index];
    }

    private static string ResolveImageContentType(string imagePath)
    {
        return Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException($"Unsupported demo image type: {imagePath}")
        };
    }

    private static Guid CreateSeedGuid(string seed)
    {
        var bytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes);
    }

    private static DateTimeOffset SeedVietnamTime(
        int year,
        int month,
        int day,
        int hour,
        int minute = 0,
        int second = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromHours(7)).ToUniversalTime();

    private sealed record SecondaryLandlordRoomSeed(
        Guid RoomingHouseId,
        Guid RoomId,
        string HouseName,
        string RoomNumber,
        int Floor,
        decimal AreaM2,
        int MaxOccupants,
        decimal MonthlyRent);

    private sealed record SecondaryLandlordTenantSeed(
        Guid UserId,
        string Email,
        string FullName,
        string PhoneNumber,
        DateOnly DateOfBirth);

    private sealed record SecondaryReviewSeed(
        Guid ReviewId,
        Guid ContractId,
        Guid RentalRequestId,
        Guid RoomDepositId,
        Guid RoomingHouseId,
        Guid RoomId,
        SecondaryLandlordTenantSeed Tenant,
        int Rating,
        string Comment);

    private sealed record DemoServicePriceSeed(
        Guid RoomingHouseId,
        Guid ServiceTypeId,
        PricingUnit PricingUnit,
        decimal UnitPrice,
        DateOnly EffectiveFrom,
        string Note);

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
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000002"), CoTenantUserId, "Le Quang Linh", "079********104", "demo-le-quang-linh-citizen-id-hash", new DateOnly(1998, 4, 12), cancellationToken);
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000003"), LandlordUserId, "Nguyễn Xuân Huấn", "079********102", "demo-landlord-nguyen-xuan-huan-citizen-id-hash", new DateOnly(1995, 10, 21), cancellationToken);
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000004"), SecondaryLandlordUserId, "Nguyễn Xuân Huấn", "079********103", "demo-secondary-landlord-nguyen-xuan-huan-citizen-id-hash", new DateOnly(1995, 8, 8), cancellationToken);
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000007"), BulkInvoiceTenantUserId, "Hoàng Minh", "079********107", "demo-bulk-invoice-tenant-hoang-minh-citizen-id-hash", new DateOnly(1999, 9, 9), cancellationToken);
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000005"), GuestTenantUserId, "Pham Ngoc Mai", "079********105", "demo-guest-tenant-pham-ngoc-mai-citizen-id-hash", new DateOnly(1999, 5, 5), cancellationToken);
        await EnsureApprovedKycAsync(context, Guid.Parse("60000000-0000-0000-0000-000000000006"), NewOccupantUserId, "Vo Thao Vy", "079********106", "demo-new-occupant-vo-thao-vy-citizen-id-hash", new DateOnly(2001, 6, 15), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoServicePricesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var effectiveFrom = new DateOnly(2026, 4, 1);
        var prices = new[]
        {
            new DemoServicePriceSeed(ApprovedHouseId, ElectricServiceTypeId, PricingUnit.MeterReading, 4000m, effectiveFrom, "Đơn giá điện cho Khu trọ Xuân Huấn."),
            new DemoServicePriceSeed(ApprovedHouseId, WaterServiceTypeId, PricingUnit.MeterReading, 18000m, effectiveFrom, "Đơn giá nước cho Khu trọ Xuân Huấn."),
            new DemoServicePriceSeed(ApprovedHouseId, InternetServiceTypeId, PricingUnit.PerMonth, 120000m, effectiveFrom, "Internet cố định hằng tháng cho Khu trọ Xuân Huấn."),
            new DemoServicePriceSeed(ApprovedHouseId, TrashServiceTypeId, PricingUnit.PerMonth, 40000m, effectiveFrom, "Phí rác, vệ sinh chung hằng tháng cho Khu trọ Xuân Huấn."),

            new DemoServicePriceSeed(SunriseHouseId, ElectricServiceTypeId, PricingUnit.MeterReading, 4000m, effectiveFrom, "Đơn giá điện cho Khu trọ An Phú."),
            new DemoServicePriceSeed(SunriseHouseId, WaterServiceTypeId, PricingUnit.MeterReading, 18000m, effectiveFrom, "Đơn giá nước cho Khu trọ An Phú."),
            new DemoServicePriceSeed(SunriseHouseId, InternetServiceTypeId, PricingUnit.PerMonth, 120000m, effectiveFrom, "Internet cố định hằng tháng cho Khu trọ An Phú."),
            new DemoServicePriceSeed(SunriseHouseId, TrashServiceTypeId, PricingUnit.PerMonth, 30000m, effectiveFrom, "Phí rác, vệ sinh chung hằng tháng cho Khu trọ An Phú."),

            new DemoServicePriceSeed(GreenViewHouseId, ElectricServiceTypeId, PricingUnit.MeterReading, 4000m, effectiveFrom, "Đơn giá điện cho Nhà trọ Minh Khang."),
            new DemoServicePriceSeed(GreenViewHouseId, WaterServiceTypeId, PricingUnit.MeterReading, 18000m, effectiveFrom, "Đơn giá nước cho Nhà trọ Minh Khang."),
            new DemoServicePriceSeed(GreenViewHouseId, InternetServiceTypeId, PricingUnit.PerMonth, 120000m, effectiveFrom, "Internet cố định hằng tháng cho Nhà trọ Minh Khang."),
            new DemoServicePriceSeed(GreenViewHouseId, TrashServiceTypeId, PricingUnit.PerMonth, 30000m, effectiveFrom, "Phí rác, vệ sinh chung hằng tháng cho Nhà trọ Minh Khang.")
        };

        foreach (var seed in prices)
        {
            var price = await context.RoomingHouseServicePrices
                .FirstOrDefaultAsync(x =>
                    x.RoomingHouseId == seed.RoomingHouseId &&
                    x.ServiceTypeId == seed.ServiceTypeId &&
                    x.EffectiveFrom == seed.EffectiveFrom,
                    cancellationToken);

            if (price is null)
            {
                price = new RoomingHouseServicePrice
                {
                    Id = CreateSeedGuid($"service-price:{seed.RoomingHouseId}:{seed.ServiceTypeId}:{seed.EffectiveFrom:yyyyMMdd}"),
                    RoomingHouseId = seed.RoomingHouseId,
                    ServiceTypeId = seed.ServiceTypeId,
                    EffectiveFrom = seed.EffectiveFrom,
                    CreatedAt = SeededAt
                };
                context.RoomingHouseServicePrices.Add(price);
            }

            price.PricingUnit = seed.PricingUnit;
            price.UnitPrice = seed.UnitPrice;
            price.EffectiveTo = null;
            price.IsActive = true;
            price.Note = seed.Note;
            price.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureApprovedKycAsync(
        AppDbContext context,
        Guid id,
        Guid userId,
        string fullName,
        string citizenIdMasked,
        string citizenIdHash,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        var kyc = await context.KycVerifications
            .FirstOrDefaultAsync(x => x.Id == id || (x.UserId == userId && x.Status == KycVerificationStatus.Approved), cancellationToken);

        if (kyc is null)
        {
            context.KycVerifications.Add(CreateApprovedKyc(id, userId, fullName, citizenIdMasked, citizenIdHash, dateOfBirth));
            return;
        }

        kyc.UserId = userId;
        kyc.DocumentType = KycDocumentType.CCCD;
        kyc.EkycProvider = EkycProvider.VNPT;
        kyc.OcrFullName = fullName;
        kyc.OcrCitizenIdMasked = citizenIdMasked;
        kyc.CitizenIdHash = citizenIdHash;
        kyc.OcrDateOfBirth = dateOfBirth;
        kyc.OcrGender = "Male";
        kyc.OcrAddress = "TP. Hồ Chí Minh";
        kyc.EkycResult = EkycResult.Passed;
        kyc.RiskLevel = KycRiskLevel.Low;
        kyc.Status = KycVerificationStatus.Approved;
        kyc.EkycErrorCode = null;
        kyc.EkycErrorMessage = null;
        kyc.RejectedReason = null;
        kyc.SubmittedAt = SeededAt;
        kyc.ReviewedAt = SeededAt;
        kyc.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task ResetTenantEkycRequirementAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var tenant = await context.Users
            .FirstOrDefaultAsync(x => x.Id == TenantUserId || x.NormalizedEmail == TenantEmail.ToUpperInvariant(), cancellationToken);

        if (tenant is null)
        {
            return;
        }

        tenant.OnboardingStatus = OnboardingStatus.NeedProfileUpdate;
        tenant.EmailConfirmed = true;
        tenant.Status = UserStatus.Active;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        var profile = await context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == tenant.Id, cancellationToken);

        if (profile is not null)
        {
            profile.VerifiedCitizenIdMasked = null;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var blockingKycs = await context.KycVerifications
            .Where(x => x.UserId == tenant.Id &&
                (x.Status == KycVerificationStatus.Pending ||
                 x.Status == KycVerificationStatus.PendingEkyc ||
                 x.Status == KycVerificationStatus.PendingAdminReview ||
                 x.Status == KycVerificationStatus.Approved))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var kyc in blockingKycs)
        {
            kyc.Status = KycVerificationStatus.Rejected;
            kyc.RejectedReason = "Reset for demo eKYC flow.";
            kyc.ReviewedByAdminId = null;
            kyc.ReviewedAt = now;
            kyc.UpdatedAt = now;
        }
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
        var reviewedByAdminId = await ResolveSeedAdminUserIdAsync(context, cancellationToken);

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
                ReviewedByAdminId = reviewedByAdminId,
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
            SecondaryLandlordUserId,
            "Khu trọ An Phú",
            "Khu trọ sạch sẽ, đầy đủ tiện ích, phù hợp sinh viên và người đi làm cần không gian yên tĩnh.",
            "88 Đường An Phú",
            15.980000m,
            108.265000m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "Mặt tiền Khu trọ An Phú",
            reviewedByAdminId,
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.ParkingId,
            AmenitySeed.SecurityCameraId,
            AmenitySeed.AirConditionerId);

        await SeedRoomingHouseAsync(
            context,
            location,
            GreenViewHouseId,
            SecondaryLandlordUserId,
            "Nhà trọ Minh Khang",
            "Nhà trọ vận hành ổn định, các phòng đang thuê đầy đủ hợp đồng và hóa đơn điện nước.",
            "12 Phạm Văn Đồng",
            15.972000m,
            108.260000m,
            RoomingHouseApprovalStatus.Approved,
            RoomingHouseVisibilityStatus.Visible,
            "Không gian chung Nhà trọ Minh Khang",
            reviewedByAdminId,
            cancellationToken,
            AmenitySeed.WifiId,
            AmenitySeed.WashingMachineId,
            AmenitySeed.BalconyId,
            AmenitySeed.ParkingId);

        await EnsureOperationalHouseRulesAsync(context, cancellationToken);

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
            reviewedByAdminId,
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
            reviewedByAdminId,
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

    private static async Task EnsureOperationalHouseRulesAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new
            {
                Id = CreateSeedGuid("rooming-house-rule:an-phu"),
                HouseId = SunriseHouseId,
                Request = BuildOperationalHouseRuleRequest(
                    "Khu trọ An Phú",
                    "Cổng chính mở từ 5:30 đến 23:00. Người thuê giữ trật tự chung, không tự ý sửa chữa kết cấu phòng, không cho thuê lại hoặc chuyển nhượng phòng khi chưa có xác nhận của chủ trọ.",
                    "Từ 22:30 đến 6:00 cần giảm âm lượng, không hát karaoke, không tụ tập đông người tại hành lang hoặc sân để xe.",
                    "Khu trọ có camera khu vực chung. Người thuê tự bảo quản tài sản cá nhân, khóa cửa khi ra ngoài và báo ngay cho chủ trọ khi phát hiện người lạ hoặc sự cố an ninh.",
                    "Rác sinh hoạt bỏ vào thùng chung trước 21:00 mỗi ngày. Hành lang, cầu thang và khu giặt phơi cần được giữ thông thoáng, không để đồ cá nhân chắn lối đi.",
                    "Khách thăm cần gửi xe đúng vị trí và rời khu trọ trước 22:00. Nếu ở lại qua đêm phải báo trước với chủ trọ và cung cấp thông tin liên hệ khi cần.",
                    "Xe máy để theo ô được phân công, không nổ máy lâu trong sân, không chặn lối thoát hiểm. Mỗi phòng tối đa hai xe máy nếu chưa đăng ký thêm.",
                    "Điện nước chốt vào cuối tháng theo chỉ số đồng hồ. Người thuê kiểm tra chỉ số trước khi xác nhận hóa đơn và báo trong vòng 24 giờ nếu có sai lệch.",
                    "Hư hỏng do sử dụng sai quy định hoặc tự ý lắp đặt thiết bị công suất lớn sẽ bồi thường theo chi phí sửa chữa thực tế.",
                    "Ưu tiên trao đổi qua nhóm cư dân để chủ trọ phản hồi nhanh các vấn đề về hóa đơn, lịch sửa chữa và an ninh.")
            },
            new
            {
                Id = CreateSeedGuid("rooming-house-rule:minh-khang"),
                HouseId = GreenViewHouseId,
                Request = BuildOperationalHouseRuleRequest(
                    "Nhà trọ Minh Khang",
                    "Người thuê sử dụng phòng đúng mục đích lưu trú, không kinh doanh trong phòng, không lưu trữ vật dễ cháy nổ và không tự ý thay khóa.",
                    "Sau 22:00 hạn chế tiếng ồn, đóng cửa nhẹ tay và không sử dụng loa công suất lớn trong phòng.",
                    "Cửa cổng và khu để xe được kiểm soát bằng camera. Khi có khách giao hàng hoặc người thân đến thăm, người thuê chủ động nhận tại khu vực cổng.",
                    "Mỗi phòng tự vệ sinh khu vực trước cửa phòng. Khu vực rác chung được thu gom vào buổi tối, không đặt rác qua đêm ở hành lang.",
                    "Khách ở lại qua đêm tối đa hai đêm liên tiếp và cần thông báo trước. Người thuê chịu trách nhiệm về hành vi của khách trong thời gian lưu trú.",
                    "Khu để xe ưu tiên xe đã đăng ký với chủ trọ. Không sạc pin xe điện trong phòng nếu chưa có ổ cắm chuyên dụng được kiểm tra an toàn.",
                    "Đơn giá điện, nước, internet và rác được công khai theo bảng giá của khu trọ. Hóa đơn phát hành sau khi chủ trọ chốt chỉ số và người thuê có thể xem ảnh đồng hồ.",
                    "Tài sản chung như camera, máy bơm, đèn hành lang nếu bị làm hỏng do lỗi cá nhân sẽ bồi thường theo báo giá sửa chữa.",
                    "Khi cần sửa chữa phòng, người thuê đặt lịch trước để chủ trọ sắp xếp thợ và thông báo thời gian vào phòng.")
            }
        };

        foreach (var seed in seeds)
        {
            var rule = await context.RoomingHouseRules
                .FirstOrDefaultAsync(x => x.RoomingHouseId == seed.HouseId, cancellationToken);

            if (rule is null)
            {
                rule = new RoomingHouseRule
                {
                    Id = seed.Id,
                    RoomingHouseId = seed.HouseId,
                    CreatedAt = SeededAt
                };
                context.RoomingHouseRules.Add(rule);
            }

            rule.SourceType = RoomingHouseRuleSourceType.FormGenerated;
            rule.GeneralRules = seed.Request.GeneralRules;
            rule.QuietHours = seed.Request.QuietHours;
            rule.SecurityPolicy = seed.Request.SecurityPolicy;
            rule.CleaningPolicy = seed.Request.CleaningPolicy;
            rule.GuestPolicy = seed.Request.GuestPolicy;
            rule.ParkingPolicy = seed.Request.ParkingPolicy;
            rule.UtilityPolicy = seed.Request.UtilityPolicy;
            rule.DamageCompensationPolicy = seed.Request.DamageCompensationPolicy;
            rule.AdditionalNotes = seed.Request.AdditionalNotes;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static UpsertRoomingHouseRuleRequest BuildOperationalHouseRuleRequest(
        string houseName,
        string generalRules,
        string quietHours,
        string securityPolicy,
        string cleaningPolicy,
        string guestPolicy,
        string parkingPolicy,
        string utilityPolicy,
        string damageCompensationPolicy,
        string additionalNotes)
    {
        return new UpsertRoomingHouseRuleRequest
        {
            SourceType = RoomingHouseRuleSourceType.FormGenerated.ToString(),
            GeneralRules = generalRules,
            QuietHours = quietHours,
            SecurityPolicy = securityPolicy,
            CleaningPolicy = cleaningPolicy,
            GuestPolicy = guestPolicy,
            ParkingPolicy = parkingPolicy,
            UtilityPolicy = utilityPolicy,
            DamageCompensationPolicy = damageCompensationPolicy,
            AdditionalNotes = $"{additionalNotes} Áp dụng riêng cho {houseName}."
        };
    }

    private static UpsertRoomingHouseRuleRequest BuildApprovedHouseRuleRequest()
    {
        return new UpsertRoomingHouseRuleRequest
        {
            SourceType = RoomingHouseRuleSourceType.FormGenerated.ToString(),
            GeneralRules = "Người thuê giữ gìn trật tự chung, không gây ồn ào, không tự ý cải tạo phòng, không cho thuê lại phòng hoặc chuyển người ở khi chưa được chủ trọ xác nhận.",
            QuietHours = "Từ 22:30 đến 6:00 cần hạn chế tiếng ồn, không tụ tập đông người tại hành lang, sân để xe hoặc khu vực sinh hoạt chung.",
            SecurityPolicy = "Luôn khóa cửa phòng khi ra ngoài, tắt thiết bị điện không sử dụng và báo ngay cho chủ trọ khi phát hiện sự cố an ninh, cháy nổ hoặc người lạ ra vào bất thường.",
            CleaningPolicy = "Đổ rác đúng giờ, giữ vệ sinh hành lang, cầu thang, khu phơi đồ và khu vực sử dụng chung. Không để vật dụng cá nhân chắn lối thoát hiểm.",
            GuestPolicy = "Khách thăm cần rời khu trọ trước 22:00. Trường hợp ở lại qua đêm phải báo trước với chủ trọ và người thuê chịu trách nhiệm về khách của mình.",
            ParkingPolicy = "Để xe đúng vị trí đã đăng ký, không chắn lối đi chung, không sửa xe hoặc nổ máy lâu trong khu vực sân để xe.",
            UtilityPolicy = "Điện nước được chốt theo chỉ số đồng hồ. Người thuê kiểm tra chỉ số và phản hồi trong vòng 24 giờ kể từ khi hóa đơn được gửi.",
            DamageCompensationPolicy = "Tài sản chung hoặc thiết bị trong phòng bị hư hỏng do lỗi sử dụng sẽ được bồi thường theo chi phí sửa chữa hoặc thay thế thực tế.",
            AdditionalNotes = "Liên hệ chủ trọ qua hotline hoặc nhóm cư dân khi cần hỗ trợ khẩn cấp, sửa chữa phòng, xác nhận hóa đơn hoặc đăng ký khách ở lại."
        };
    }

    private static async Task<Guid?> ResolveSeedAdminUserIdAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var seedAdmin = context.Users.Local.FirstOrDefault(x => x.Id == AdminUserId)
            ?? await context.Users.FirstOrDefaultAsync(x => x.Id == AdminUserId, cancellationToken);

        if (seedAdmin is not null)
        {
            return seedAdmin.Id;
        }

        var localAdminRole = context.UserRoles.Local.FirstOrDefault(x => x.RoleId == RoleSeed.AdminRoleId);
        if (localAdminRole is not null)
        {
            return localAdminRole.UserId;
        }

        return await context.UserRoles
            .Where(x => x.RoleId == RoleSeed.AdminRoleId)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken);
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
        Guid? reviewedByAdminId,
        CancellationToken cancellationToken,
        params int[] amenityIds)
    {
        var existing = await context.RoomingHouses.FirstOrDefaultAsync(x => x.Id == roomingHouseId, cancellationToken);
        if (existing is not null)
        {
            existing.LandlordUserId = landlordUserId;
            existing.Name = name;
            existing.Description = description;
            existing.AddressLine = addressLine;
            existing.AddressDisplay = $"{addressLine}, {location.WardName}, {location.ProvinceName}";
            existing.Latitude = latitude;
            existing.Longitude = longitude;
            existing.ApprovalStatus = approvalStatus;
            existing.VisibilityStatus = visibilityStatus;
            existing.ReviewedByAdminId = approvalStatus is RoomingHouseApprovalStatus.Approved or RoomingHouseApprovalStatus.Rejected
                ? reviewedByAdminId
                : null;
            existing.ReviewedAt = approvalStatus is RoomingHouseApprovalStatus.Approved or RoomingHouseApprovalStatus.Rejected
                ? SeededAt
                : null;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

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
            ReviewedByAdminId = reviewedAt.HasValue ? reviewedByAdminId : null,
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
            await EnsureSecondaryDemoRoomsAsync(context, cancellationToken);
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
        await EnsureSecondaryDemoRoomsAsync(context, cancellationToken);
    }

    private static async Task EnsureSecondaryDemoRoomsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var roomsToSeed = new[]
        {
            CreateRoom(SunriseHouseId, SunriseRoomA1Id, "A101", 1, 18m, 2, RoomStatus.Occupied),
            CreateRoom(SunriseHouseId, SunriseRoomA2Id, "A102", 1, 21m, 2, RoomStatus.Occupied),
            CreateRoom(SunriseHouseId, SunriseRoomB1Id, "A103", 2, 25m, 3, RoomStatus.Occupied),
            CreateRoom(SunriseHouseId, AnPhuRoomA104Id, "A104", 2, 22m, 2, RoomStatus.Occupied),
            CreateRoom(SunriseHouseId, AnPhuRoomA105Id, "A105", 3, 24m, 3, RoomStatus.Occupied),
            CreateRoom(GreenViewHouseId, GreenViewRoom101Id, "B201", 1, 20m, 2, RoomStatus.Occupied),
            CreateRoom(GreenViewHouseId, GreenViewRoom102Id, "B202", 1, 24m, 3, RoomStatus.Occupied),
            CreateRoom(GreenViewHouseId, MinhKhangRoomB203Id, "B203", 2, 23m, 2, RoomStatus.Occupied),
            CreateRoom(GreenViewHouseId, MinhKhangRoomB204Id, "B204", 2, 26m, 3, RoomStatus.Occupied),
            CreateRoom(GreenViewHouseId, MinhKhangRoomB205Id, "B205", 3, 28m, 3, RoomStatus.Occupied)
        };

        foreach (var room in roomsToSeed)
        {
            var existingRoom = await context.Rooms.FirstOrDefaultAsync(x => x.Id == room.Id, cancellationToken);
            if (existingRoom is not null)
            {
                existingRoom.RoomingHouseId = room.RoomingHouseId;
                existingRoom.RoomNumber = room.RoomNumber;
                existingRoom.Floor = room.Floor;
                existingRoom.AreaM2 = room.AreaM2;
                existingRoom.MaxOccupants = room.MaxOccupants;
                existingRoom.IsTieredPricing = room.IsTieredPricing;
                existingRoom.Status = RoomStatus.Occupied;
                existingRoom.Description = room.Description;
                existingRoom.UpdatedAt = DateTimeOffset.UtcNow;
                continue;
            }

            context.Rooms.Add(room);
            AddRoomMockDetails(context, room);
        }

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

        var imageFile = PickDemoPropertyImage($"{propertyImage.Id}:{fileName}");
        var bytes = await File.ReadAllBytesAsync(imageFile, cancellationToken);
        var actualFileName = Path.GetFileName(imageFile);
        var contentType = ResolveImageContentType(imageFile);
        var objectKey = mediaObjectKeyFactory.Create(scope, MediaVisibility.Public, actualFileName);
        var storedObject = await UploadSeedImageAsync(
            mediaStorageService,
            objectKey.ObjectKey,
            actualFileName,
            contentType,
            bytes,
            cancellationToken);
        var mediaAssetId = Guid.NewGuid();

        context.MediaAssets.Add(new MediaAsset
        {
            Id = mediaAssetId,
            OwnerUserId = ownerUserId,
            BucketName = storedObject.BucketName,
            ObjectKey = storedObject.ObjectKey,
            OriginalFileName = actualFileName,
            StoredFileName = storedObject.StoredFileName,
            ContentType = contentType,
            FileSize = bytes.Length,
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

    private static async Task<MediaStoredObjectResult> UploadSeedImageAsync(
        IMediaStorageService mediaStorageService,
        string objectKey,
        string fileName,
        string contentType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(bytes, writable: false);
        return await mediaStorageService.UploadAsync(
            new MediaUploadRequest
            {
                Content = content,
                OriginalFileName = fileName,
                ContentType = contentType,
                FileSize = bytes.Length,
                ObjectKey = objectKey,
                Visibility = MediaVisibility.Public
            },
            cancellationToken);
    }

    private static async Task SeedDemoReviewsAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var reviewedByAdminId = await ResolveSeedAdminUserIdAsync(context, cancellationToken);
        var demoReviews = new[]
        {
            new DemoReviewSeed(
                Guid.Parse("53000000-0000-0000-0000-000000000001"),
                ActiveContractId,
                Guid.Parse("51000000-0000-0000-0000-000000000001"),
                Guid.Parse("52000000-0000-0000-0000-000000000001"),
                Guid.Parse("54000000-0000-0000-0000-000000000001"),
                Guid.Parse("55000000-0000-0000-0000-000000000001"),
                ApprovedHouseId,
                Room101Id,
                TenantUserId,
                5,
                "Khu trọ sạch, chủ trọ hỗ trợ nhanh, phòng đúng như ảnh thực tế khi xem.",
                "Cảm ơn bạn đã tin tưởng Hoa Sen.",
                "review-hoa-sen.png",
                "Review Hoa Sen",
                "Phong sach se, day du tien nghi"),
            new DemoReviewSeed(
                Guid.Parse("53000000-0000-0000-0000-000000000002"),
                Guid.Parse("50000000-0000-0000-0000-000000000004"),
                Guid.Parse("51000000-0000-0000-0000-000000000002"),
                Guid.Parse("52000000-0000-0000-0000-000000000002"),
                Guid.Parse("54000000-0000-0000-0000-000000000002"),
                Guid.Parse("55000000-0000-0000-0000-000000000002"),
                SunriseHouseId,
                SunriseRoomA1Id,
                CoTenantUserId,
                4,
                "Vị trí thuận tiện, phòng thoáng và khu vực chung được giữ gìn tốt.",
                "Cảm ơn bạn đã góp ý, bên mình sẽ tiếp tục giữ khu sinh hoạt chung sạch và yên tĩnh.",
                "review-sunrise.png",
                "Review Sunrise",
                "Anh demo phong thoang")
        };

        foreach (var seed in demoReviews)
        {
            var room = await context.Rooms
                .Include(x => x.RoomingHouse)
                .FirstOrDefaultAsync(x => x.Id == seed.RoomId, cancellationToken);

            if (room is null || room.RoomingHouseId != seed.RoomingHouseId)
            {
                continue;
            }

            await EnsureDemoReviewContractAsync(context, seed, room, cancellationToken);

            var review = await context.RoomingHouseReviews
                .Include(x => x.Images)
                .FirstOrDefaultAsync(
                    x => x.RentalContractId == seed.ContractId && x.TenantUserId == seed.TenantUserId,
                    cancellationToken);

            if (review is null)
            {
                review = new RoomingHouseReview
                {
                    Id = seed.ReviewId,
                    RoomingHouseId = seed.RoomingHouseId,
                    TenantUserId = seed.TenantUserId,
                    RentalContractId = seed.ContractId,
                    Rating = seed.Rating,
                    Comment = seed.Comment,
                    LandlordReply = seed.LandlordReply,
                    LandlordReplyCreatedAt = seed.LandlordReply is null ? null : SeededAt.AddDays(10),
                    IsHidden = false,
                    ModerationStatus = RoomingHouseReviewModerationStatus.Approved,
                    ModerationReason = "Demo seed review approved.",
                    AiModerationProvider = "seed",
                    AiModerationRiskLevel = "Low",
                    AiModerationCategories = "[]",
                    AiModerationJson = "{\"contentComment\":\"Seed review approved\",\"imageComment\":\"Seed image approved\"}",
                    AiReviewedAt = SeededAt.AddDays(9),
                    ReviewedByAdminId = reviewedByAdminId,
                    AdminReviewedAt = SeededAt.AddDays(9),
                    AdminNote = "Demo review seed.",
                    CreatedAt = SeededAt.AddDays(8),
                    UpdatedAt = SeededAt.AddDays(8)
                };
                context.RoomingHouseReviews.Add(review);
            }
            else
            {
                review.Rating = seed.Rating;
                review.Comment = seed.Comment;
                review.LandlordReply = seed.LandlordReply;
                review.LandlordReplyCreatedAt = seed.LandlordReply is null ? null : SeededAt.AddDays(10);
                review.IsHidden = false;
                review.ModerationStatus = RoomingHouseReviewModerationStatus.Approved;
                review.ModerationReason = "Demo seed review approved.";
                review.ReviewedByAdminId = reviewedByAdminId;
                review.AdminReviewedAt = SeededAt.AddDays(9);
                review.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await EnsureDemoReviewImageAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                seed,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await RoomingHouseRatingHelper.UpdateRatingAsync(context, seed.RoomingHouseId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoReviewContractAsync(
        AppDbContext context,
        DemoReviewSeed seed,
        Room room,
        CancellationToken cancellationToken)
    {
        if (!await context.RentalRequests.AnyAsync(x => x.Id == seed.RentalRequestId, cancellationToken))
        {
            context.RentalRequests.Add(new RentalRequest
            {
                Id = seed.RentalRequestId,
                RoomId = seed.RoomId,
                TenantUserId = seed.TenantUserId,
                ApprovedByLandlordId = room.RoomingHouse.LandlordUserId,
                DesiredStartDate = new DateOnly(2025, 9, 1),
                ExpectedEndDate = new DateOnly(2026, 2, 28),
                ExpectedOccupantCount = 1,
                MonthlyRentSnapshot = room.PriceTiers.FirstOrDefault()?.MonthlyRent ?? 2500000m,
                DepositAmountSnapshot = 2500000m,
                TenantNote = "Yeu cau thue phong A01 da hoan tat, dung lam du lieu danh gia sau khi ket thuc hop dong.",
                Status = RentalRequestStatus.Accepted,
                RespondedAt = SeededAt.AddDays(1),
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt.AddDays(1)
            });
        }

        if (!await context.RoomDeposits.AnyAsync(x => x.Id == seed.RoomDepositId, cancellationToken))
        {
            context.RoomDeposits.Add(new RoomDeposit
            {
                Id = seed.RoomDepositId,
                RentalRequestId = seed.RentalRequestId,
                RoomId = seed.RoomId,
                TenantUserId = seed.TenantUserId,
                LandlordUserId = room.RoomingHouse.LandlordUserId,
                DepositAmount = 2500000m,
                Currency = "VND",
                Status = RoomDepositStatus.Paid,
                PaymentDeadlineAt = SeededAt.AddDays(3),
                PaidAt = SeededAt.AddDays(2),
                Note = "Tien coc phong A01 da tat toan khi hop dong ket thuc.",
                CreatedAt = SeededAt,
                UpdatedAt = SeededAt.AddDays(2)
            });
        }

        var reviewContract = await context.RentalContracts.FirstOrDefaultAsync(x => x.Id == seed.ContractId, cancellationToken);
        if (reviewContract is null)
        {
            reviewContract = new RentalContract
            {
                Id = seed.ContractId,
                CreatedAt = SeededAt
            };
            context.RentalContracts.Add(reviewContract);
        }

        reviewContract.RentalRequestId = seed.RentalRequestId;
        reviewContract.RoomDepositId = seed.RoomDepositId;
        reviewContract.RoomId = seed.RoomId;
        reviewContract.MainTenantUserId = seed.TenantUserId;
        reviewContract.ContractNumber = seed.ContractId == ActiveContractId
            ? ReviewShowcaseContractNumber
            : SunriseReviewContractNumber;
        reviewContract.StartDate = new DateOnly(2025, 9, 1);
        reviewContract.EndDate = new DateOnly(2026, 2, 28);
        reviewContract.MonthlyRent = room.PriceTiers.FirstOrDefault()?.MonthlyRent ?? 2500000m;
        reviewContract.DepositAmount = 2500000m;
        reviewContract.PaymentDay = 5;
        reviewContract.Status = RentalContractStatus.Expired;
        reviewContract.ActivatedAt = SeededAt.AddDays(4);
        reviewContract.TerminationDate = new DateOnly(2026, 2, 28);
        reviewContract.TerminationType = ContractTerminationType.NormalExpiration;
        reviewContract.StatusReason = "Hop dong da ket thuc dung han ngay 28/02/2026.";
        reviewContract.RoomSnapshot = "{}";
        reviewContract.DeletedAt = null;
        reviewContract.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureDemoReviewImageAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        DemoReviewSeed seed,
        CancellationToken cancellationToken)
    {
        await EnsurePropertyImageAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            seed.PropertyImageId,
            seed.MediaAssetId,
            seed.TenantUserId,
            roomingHouseId: null,
            roomId: null,
            reviewId: seed.ReviewId,
            caption: seed.ImageTitle,
            isCover: false,
            sortOrder: 0,
            imageSelector: seed.ReviewId.ToString("N"),
            cancellationToken);
    }

    private sealed record DemoReviewSeed(
        Guid ReviewId,
        Guid ContractId,
        Guid RentalRequestId,
        Guid RoomDepositId,
        Guid PropertyImageId,
        Guid MediaAssetId,
        Guid RoomingHouseId,
        Guid RoomId,
        Guid TenantUserId,
        int Rating,
        string Comment,
        string? LandlordReply,
        string FileName,
        string ImageTitle,
        string ImageSubtitle);

    private static async Task<User> EnsureDemoUserAsync(
        AppDbContext context,
        IPasswordService passwordService,
        Guid seedUserId,
        string email,
        string displayName,
        int roleId,
        CancellationToken cancellationToken,
        OnboardingStatus onboardingStatus = OnboardingStatus.Completed)
    {
        var user = await EnsureSeedUserAsync(
            context,
            passwordService,
            seedUserId,
            email,
            displayName,
            roleId,
            cancellationToken);

        user.OnboardingStatus = onboardingStatus;
        user.EmailConfirmed = true;
        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var profile = await context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            context.UserProfiles.Add(CreateProfile(user.Id, displayName));
        }
        else
        {
            profile.FullName = displayName;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return user;
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

    private static async Task SeedShowcaseContractFlowAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var reviewedByAdminId = await ResolveSeedAdminUserIdAsync(context, cancellationToken);

        await EnsureShowcaseUsersAndHouseAsync(context, reviewedByAdminId, cancellationToken);
        await EnsureShowcaseWalletsAsync(context, cancellationToken);
        await EnsureShowcaseRentalRequestAndContractAsync(context, cancellationToken);
        await EnsureShowcaseInvoicesAndReadingsAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);
        await EnsureShowcaseContractFilesAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSecondaryLandlordOperationsDemoAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        EnsureDemoImageSourceReady();

        var roomSeeds = GetSecondaryLandlordRoomSeeds();
        var tenantSeeds = SecondaryLandlordTenantSeeds;
        var landlordBalance = 0m;

        await EnsureWalletAsync(context, SecondaryLandlordWalletAccountId, SecondaryLandlordUserId, 95000000m, 0m, cancellationToken);
        await EnsureRoomingHouseImageSetAsync(context, mediaStorageService, mediaObjectKeyFactory, SunriseHouseId, SecondaryLandlordUserId, "an-phu", cancellationToken);
        await EnsureRoomingHouseImageSetAsync(context, mediaStorageService, mediaObjectKeyFactory, GreenViewHouseId, SecondaryLandlordUserId, "minh-khang", cancellationToken);

        for (var i = 0; i < roomSeeds.Length; i++)
        {
            var roomSeed = roomSeeds[i];
            var tenantSeed = tenantSeeds[i];

            await EnsureOperationalRoomAsync(context, roomSeed, cancellationToken);
            await EnsureRoomImageSetAsync(context, mediaStorageService, mediaObjectKeyFactory, roomSeed, cancellationToken);

            var tenantWalletId = CreateSeedGuid($"wallet:{tenantSeed.Email}");
            await EnsureWalletAsync(context, tenantWalletId, tenantSeed.UserId, 25000000m, 0m, cancellationToken);

            var appointmentId = CreateSeedGuid($"xunhuns:appointment:{roomSeed.RoomNumber}");
            await EnsureOperationalViewingAppointmentAsync(context, appointmentId, roomSeed.RoomId, tenantSeed.UserId, i, cancellationToken);

            var requestId = CreateSeedGuid($"xunhuns:rental-request:{roomSeed.RoomNumber}");
            var depositId = CreateSeedGuid($"xunhuns:deposit:{roomSeed.RoomNumber}");
            var contractId = CreateSeedGuid($"xunhuns:contract:{roomSeed.RoomNumber}");
            var occupantId = CreateSeedGuid($"xunhuns:occupant:{roomSeed.RoomNumber}");
            var contractNumber = $"HD-XHUNS-{roomSeed.RoomNumber}-20260401";

            await EnsureOperationalRentalRequestAsync(context, requestId, roomSeed, tenantSeed, i, cancellationToken);
            await EnsureOperationalDepositAsync(context, depositId, requestId, roomSeed, tenantSeed, cancellationToken);
            await EnsureOperationalContractAsync(context, contractId, requestId, depositId, occupantId, roomSeed, tenantSeed, contractNumber, cancellationToken);
            await EnsureOperationalContractDocumentsAsync(
                context,
                mediaStorageService,
                mediaObjectKeyFactory,
                contractId,
                roomSeed,
                tenantSeed,
                contractNumber,
                cancellationToken);

            for (var month = 4; month <= 5; month++)
            {
                var status = InvoiceStatus.Paid;
                var invoiceId = CreateSeedGuid($"xunhuns:invoice:{roomSeed.RoomNumber}:2026-{month:D2}");
                var electricReadingId = CreateSeedGuid($"xunhuns:meter:electric:{roomSeed.RoomNumber}:2026-{month:D2}");
                var waterReadingId = CreateSeedGuid($"xunhuns:meter:water:{roomSeed.RoomNumber}:2026-{month:D2}");
                var periodStart = new DateOnly(2026, month, 1);
                var periodEnd = new DateOnly(2026, month, DateTime.DaysInMonth(2026, month));

                var electricPrevious = 980m + (i * 43m) + ((month - 4) * 76m);
                var electricCurrent = electricPrevious + 68m + (i % 4 * 6m);
                var waterPrevious = 42m + (i * 3m) + ((month - 4) * 7m);
                var waterCurrent = waterPrevious + 5m + (i % 3);

                var electricReading = await EnsureOperationalMeterReadingAsync(
                    context,
                    mediaStorageService,
                    mediaObjectKeyFactory,
                    electricReadingId,
                    roomSeed.RoomId,
                    contractId,
                    ElectricServiceTypeId,
                    "1341.png",
                    periodStart,
                    periodEnd,
                    electricPrevious,
                    electricCurrent,
                    $"AI OCR: dien phong {roomSeed.RoomNumber} thang {month:D2}/2026",
                    cancellationToken);

                var waterReading = await EnsureOperationalMeterReadingAsync(
                    context,
                    mediaStorageService,
                    mediaObjectKeyFactory,
                    waterReadingId,
                    roomSeed.RoomId,
                    contractId,
                    WaterServiceTypeId,
                    "96.png",
                    periodStart,
                    periodEnd,
                    waterPrevious,
                    waterCurrent,
                    $"AI OCR: nuoc phong {roomSeed.RoomNumber} thang {month:D2}/2026",
                    cancellationToken);

                var invoiceTotal = await EnsureOperationalInvoiceAsync(
                    context,
                    invoiceId,
                    contractId,
                    roomSeed,
                    tenantSeed,
                    periodStart,
                    periodEnd,
                    status,
                    electricReading,
                    waterReading,
                    cancellationToken);

                if (status == InvoiceStatus.Paid)
                {
                    var transferGroupId = CreateSeedGuid($"xunhuns:invoice-transfer:{roomSeed.RoomNumber}:2026-{month:D2}");
                    await EnsureWalletTransactionAsync(
                        context,
                        CreateSeedGuid($"xunhuns:tenant-payment:{roomSeed.RoomNumber}:2026-{month:D2}"),
                        tenantWalletId,
                        tenantSeed.UserId,
                        WalletTransactionType.InvoicePayment,
                        WalletTransactionDirection.Debit,
                        invoiceTotal,
                        25000000m,
                        25000000m - invoiceTotal,
                        0m,
                        0m,
                        nameof(Invoice),
                        invoiceId,
                        $"Thanh toán hóa đơn phòng {roomSeed.RoomNumber} tháng {month:D2}/2026.",
                        SeededAt.AddMonths(month).AddDays(3),
                        cancellationToken);

                    landlordBalance += invoiceTotal;
                    await EnsureWalletTransactionAsync(
                        context,
                        CreateSeedGuid($"xunhuns:landlord-receive:{roomSeed.RoomNumber}:2026-{month:D2}"),
                        SecondaryLandlordWalletAccountId,
                        SecondaryLandlordUserId,
                        WalletTransactionType.InvoiceReceive,
                        WalletTransactionDirection.Credit,
                        invoiceTotal,
                        landlordBalance - invoiceTotal,
                        landlordBalance,
                        0m,
                        0m,
                        nameof(Invoice),
                        invoiceId,
                        $"Nhận thanh toán hóa đơn phòng {roomSeed.RoomNumber} tháng {month:D2}/2026.",
                        SeededAt.AddMonths(month).AddDays(3),
                        cancellationToken);
                }
            }
        }

        await RemoveSecondaryLandlordOpenPeriodInvoicesAsync(context, cancellationToken);
        await EnsureSecondaryLandlordWithdrawalAsync(context, landlordBalance, cancellationToken);
        await EnsureSecondaryLandlordChatAsync(context, roomSeeds, tenantSeeds, cancellationToken);
        await EnsureSecondaryLandlordReviewsAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task RemoveSecondaryLandlordOpenPeriodInvoicesAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("""
            DELETE FROM wallet_transactions
            WHERE related_entity_type = 'Invoice'
              AND related_entity_id IN (
                  SELECT i.id
                  FROM invoices i
                  JOIN contracts c ON c.id = i.contract_id
                  JOIN rooms r ON r.id = c.room_id
                  JOIN rooming_houses h ON h.id = r.rooming_house_id
                  WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004'
                    AND i.billing_period_start >= DATE '2026-06-01'
              );

            DELETE FROM invoice_items
            WHERE invoice_id IN (
                SELECT i.id
                FROM invoices i
                JOIN contracts c ON c.id = i.contract_id
                JOIN rooms r ON r.id = c.room_id
                JOIN rooming_houses h ON h.id = r.rooming_house_id
                WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004'
                  AND i.billing_period_start >= DATE '2026-06-01'
            );

            DELETE FROM invoices
            WHERE id IN (
                SELECT i.id
                FROM invoices i
                JOIN contracts c ON c.id = i.contract_id
                JOIN rooms r ON r.id = c.room_id
                JOIN rooming_houses h ON h.id = r.rooming_house_id
                WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004'
                  AND i.billing_period_start >= DATE '2026-06-01'
            );

            DELETE FROM meter_readings
            WHERE contract_id IN (
                SELECT c.id
                FROM contracts c
                JOIN rooms r ON r.id = c.room_id
                JOIN rooming_houses h ON h.id = r.rooming_house_id
                WHERE h.landlord_user_id = '10000000-0000-0000-0000-000000000004'
            )
              AND billing_period_start >= DATE '2026-06-01';
            """, cancellationToken);
    }

    private static async Task EnsureShowcaseUsersAndHouseAsync(
        AppDbContext context,
        Guid? reviewedByAdminId,
        CancellationToken cancellationToken)
    {
        var landlord = await context.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == LandlordUserId, cancellationToken);
        var linh = await context.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == CoTenantUserId, cancellationToken);
        var phamNgocMai = await context.Users
            .Include(x => x.UserRoles)
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == GuestTenantUserId, cancellationToken);

        if (landlord is not null)
        {
            landlord.DisplayName = "Nguyễn Xuân Huấn";
            landlord.PhoneNumber = string.IsNullOrWhiteSpace(landlord.PhoneNumber) ? "0901000002" : landlord.PhoneNumber;
            landlord.UpdatedAt = DateTimeOffset.UtcNow;
            if (landlord.UserProfile is not null)
            {
                landlord.UserProfile.FullName = "Nguyễn Xuân Huấn";
                landlord.UserProfile.AddressLine = "Da Nang";
                landlord.UserProfile.VerifiedCitizenIdMasked = "079********102";
                landlord.UserProfile.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (linh is not null)
        {
            linh.DisplayName = "Le Quang Linh";
            linh.PhoneNumber = string.IsNullOrWhiteSpace(linh.PhoneNumber) ? "0901000004" : linh.PhoneNumber;
            linh.UpdatedAt = DateTimeOffset.UtcNow;
            if (linh.UserProfile is not null)
            {
                linh.UserProfile.FullName = "Le Quang Linh";
                linh.UserProfile.DateOfBirth = new DateOnly(1998, 4, 12);
                linh.UserProfile.AddressLine = "Da Nang";
                linh.UserProfile.VerifiedCitizenIdMasked = "079********104";
                linh.UserProfile.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (phamNgocMai is not null)
        {
            phamNgocMai.DisplayName = "Pham Ngoc Mai";
            phamNgocMai.PhoneNumber = string.IsNullOrWhiteSpace(phamNgocMai.PhoneNumber) ? "0901000005" : phamNgocMai.PhoneNumber;
            phamNgocMai.EmailConfirmed = true;
            phamNgocMai.OnboardingStatus = OnboardingStatus.Completed;
            phamNgocMai.UpdatedAt = DateTimeOffset.UtcNow;

            if (phamNgocMai.UserRoles.All(x => x.RoleId != RoleSeed.LandlordRoleId))
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = GuestTenantUserId,
                    RoleId = RoleSeed.LandlordRoleId,
                    CreatedAt = SeededAt
                });
            }

            if (phamNgocMai.UserProfile is not null)
            {
                phamNgocMai.UserProfile.FullName = "Pham Ngoc Mai";
                phamNgocMai.UserProfile.DateOfBirth = new DateOnly(1999, 5, 5);
                phamNgocMai.UserProfile.AddressLine = "Da Nang";
                phamNgocMai.UserProfile.VerifiedCitizenIdMasked = "079********105";
                phamNgocMai.UserProfile.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var mainHouse = await context.RoomingHouses
            .FirstOrDefaultAsync(x => x.Id == ApprovedHouseId, cancellationToken);
        if (mainHouse is not null)
        {
            mainHouse.LandlordUserId = LandlordUserId;
            mainHouse.Name = "Khu tro Xuan Huan";
            mainHouse.Description = "Khu tro trung tam cho demo: phong A01 con trong de khach dat lich, phong B201 dang co hop dong active cua Le Quang Linh.";
            mainHouse.AddressLine = "144 Tran Dai Nghia";
            mainHouse.AddressDisplay = "144 Tran Dai Nghia, Phuong Ngu Hanh Son, Thanh pho Da Nang";
            mainHouse.ApprovalStatus = RoomingHouseApprovalStatus.Approved;
            mainHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Visible;
            mainHouse.ReviewedByAdminId = reviewedByAdminId;
            mainHouse.ReviewedAt ??= SeededAt;
            mainHouse.DeletedAt = null;
            mainHouse.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var pendingHouse = await context.RoomingHouses
            .FirstOrDefaultAsync(x => x.Id == PendingHouseId, cancellationToken);
        if (pendingHouse is not null)
        {
            pendingHouse.LandlordUserId = GuestTenantUserId;
            pendingHouse.Name = "Khu tro An Nhien";
            pendingHouse.Description = "Ho so khu tro cua Pham Ngoc Mai dang cho admin duyet sau khi dang ky lam chu tro.";
            pendingHouse.AddressLine = "36 Nguyen Huu Tho";
            pendingHouse.AddressDisplay = "36 Nguyen Huu Tho, Phuong Ngu Hanh Son, Thanh pho Da Nang";
            pendingHouse.ApprovalStatus = RoomingHouseApprovalStatus.Pending;
            pendingHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
            pendingHouse.ReviewedByAdminId = null;
            pendingHouse.ReviewedAt = null;
            pendingHouse.DeletedAt = null;
            pendingHouse.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var roomA01 = await context.Rooms.FirstOrDefaultAsync(x => x.Id == Room101Id, cancellationToken);
        if (roomA01 is not null)
        {
            roomA01.RoomingHouseId = ApprovedHouseId;
            roomA01.RoomNumber = "A01";
            roomA01.Floor = 1;
            roomA01.AreaM2 = 20m;
            roomA01.MaxOccupants = 2;
            roomA01.IsTieredPricing = true;
            roomA01.Status = RoomStatus.Available;
            roomA01.Description = "Phong A01 con trong de guest tim kiem, dat lich xem phong va gui yeu cau thue.";
            roomA01.DeletedAt = null;
            roomA01.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var roomB201 = await context.Rooms.FirstOrDefaultAsync(x => x.Id == Room102Id, cancellationToken);
        if (roomB201 is not null)
        {
            roomB201.RoomingHouseId = ApprovedHouseId;
            roomB201.RoomNumber = "B201";
            roomB201.Floor = 2;
            roomB201.AreaM2 = 24m;
            roomB201.MaxOccupants = 2;
            roomB201.IsTieredPricing = true;
            roomB201.Status = RoomStatus.Occupied;
            roomB201.Description = "Phong B201 dang co hop dong active cua Le Quang Linh, dung de demo hoa don, huy hop dong va coc bi chuyen cho chu tro.";
            roomB201.DeletedAt = null;
            roomB201.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await EnsureRoomPriceTierAsync(context, Room101Id, 1, 3500000m, cancellationToken);
        await EnsureRoomPriceTierAsync(context, Room101Id, 2, 3900000m, cancellationToken);
        await EnsureRoomPriceTierAsync(context, Room102Id, 1, 3600000m, cancellationToken);
        await EnsureRoomPriceTierAsync(context, Room102Id, 2, 3950000m, cancellationToken);
    }

    private static async Task EnsureShowcaseWalletsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        await EnsureWalletAsync(context, TenantLinhWalletAccountId, CoTenantUserId, 50000000m, 0m, cancellationToken);
        await EnsureWalletAsync(context, LandlordWalletAccountId, LandlordUserId, 12500000m, 3600000m, cancellationToken);

        await EnsureWalletTransactionAsync(
            context,
            Guid.Parse("71000000-0000-0000-0000-000000000101"),
            TenantLinhWalletAccountId,
            CoTenantUserId,
            WalletTransactionType.WalletTopUp,
            WalletTransactionDirection.Credit,
            54280000m,
            0m,
            54280000m,
            0m,
            0m,
            "PaymentTransaction",
            null,
            "Seed vi Le Quang Linh du tien sau lich su hoa don thang 06.",
            SeededAt.AddMonths(5),
            cancellationToken);

        await EnsureWalletTransactionAsync(
            context,
            Guid.Parse("71000000-0000-0000-0000-000000000102"),
            LandlordWalletAccountId,
            LandlordUserId,
            WalletTransactionType.WalletTopUp,
            WalletTransactionDirection.Credit,
            12500000m,
            0m,
            12500000m,
            0m,
            3600000m,
            "PaymentTransaction",
            null,
            "Nap vi ban dau cho chu tro, gom 3.600.000 tien coc dang giu.",
            SeededAt.AddMonths(5),
            cancellationToken);
    }

    private static async Task EnsureShowcaseRentalRequestAndContractAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var request = await context.RentalRequests
            .FirstOrDefaultAsync(x => x.Id == LinhRentalRequestId, cancellationToken);
        if (request is null)
        {
            request = new RentalRequest { Id = LinhRentalRequestId, CreatedAt = SeededAt.AddMonths(5) };
            context.RentalRequests.Add(request);
        }

        request.RoomId = Room102Id;
        request.TenantUserId = CoTenantUserId;
        request.ApprovedByLandlordId = LandlordUserId;
        request.DesiredStartDate = new DateOnly(2026, 6, 1);
        request.ExpectedEndDate = new DateOnly(2027, 5, 31);
        request.ExpectedOccupantCount = 1;
        request.MonthlyRentSnapshot = 3600000m;
        request.DepositAmountSnapshot = 3600000m;
        request.TenantNote = "Showcase: request accepted de tao hop dong active phong B201.";
        request.Status = RentalRequestStatus.Accepted;
        request.RespondedAt = SeededAt.AddMonths(5).AddDays(1);
        request.RejectedReason = null;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        var deposit = await context.RoomDeposits
            .FirstOrDefaultAsync(x => x.Id == LinhRoomDepositId, cancellationToken);
        if (deposit is null)
        {
            deposit = new RoomDeposit { Id = LinhRoomDepositId, CreatedAt = SeededAt.AddMonths(5) };
            context.RoomDeposits.Add(deposit);
        }

        deposit.RentalRequestId = LinhRentalRequestId;
        deposit.RoomId = Room102Id;
        deposit.TenantUserId = CoTenantUserId;
        deposit.LandlordUserId = LandlordUserId;
        deposit.DepositAmount = 3600000m;
        deposit.Currency = "VND";
        deposit.Status = RoomDepositStatus.Paid;
        deposit.PaymentDeadlineAt = SeededAt.AddMonths(5).AddDays(2);
        deposit.PaidAt = SeededAt.AddMonths(5).AddDays(1).AddHours(2);
        deposit.RefundedAt = null;
        deposit.ForfeitedAt = null;
        deposit.RefundAmount = null;
        deposit.ForfeitedAmount = null;
        deposit.Note = "Showcase: coc 3.600.000 dang giu; neu huy truoc han thi chuyen vao vi chu tro.";
        deposit.UpdatedAt = DateTimeOffset.UtcNow;

        var contract = await context.RentalContracts
            .FirstOrDefaultAsync(x => x.Id == LinhContractId, cancellationToken);
        if (contract is null)
        {
            contract = new RentalContract { Id = LinhContractId, CreatedAt = SeededAt.AddMonths(5) };
            context.RentalContracts.Add(contract);
        }

        contract.RentalRequestId = LinhRentalRequestId;
        contract.RoomDepositId = LinhRoomDepositId;
        contract.RoomId = Room102Id;
        contract.MainTenantUserId = CoTenantUserId;
        contract.ContractNumber = LinhShowcaseContractNumber;
        contract.StartDate = new DateOnly(2026, 6, 1);
        contract.EndDate = new DateOnly(2027, 5, 31);
        contract.MonthlyRent = 3600000m;
        contract.DepositAmount = 3600000m;
        contract.PaymentDay = 5;
        contract.Status = RentalContractStatus.Active;
        contract.RoomSnapshot = "{\"RoomNumber\":\"B201\",\"RoomingHouseName\":\"Khu tro Xuan Huan\",\"MaxOccupants\":2,\"OccupantCount\":1}";
        contract.SignatureDeadlineAt = SeededAt.AddMonths(5).AddDays(3);
        contract.ActivatedAt = SeededAt.AddMonths(5).AddDays(2);
        contract.TerminationDate = null;
        contract.TerminationType = null;
        contract.StatusReason = "Showcase active contract for billing and termination demo.";
        contract.DeletedAt = null;
        contract.UpdatedAt = DateTimeOffset.UtcNow;

        var occupant = await context.ContractOccupants
            .FirstOrDefaultAsync(x => x.Id == LinhContractOccupantId, cancellationToken);
        if (occupant is null)
        {
            occupant = new ContractOccupant { Id = LinhContractOccupantId, CreatedAt = SeededAt.AddMonths(5) };
            context.ContractOccupants.Add(occupant);
        }

        occupant.RentalContractId = LinhContractId;
        occupant.UserId = CoTenantUserId;
        occupant.FullName = "Le Quang Linh";
        occupant.PhoneNumber = "0901000004";
        occupant.DateOfBirth = new DateOnly(1998, 4, 12);
        occupant.RelationshipToMainTenant = "Self";
        occupant.MoveInDate = new DateOnly(2026, 6, 1);
        occupant.MoveOutDate = null;
        occupant.Status = ContractOccupantStatus.Active;
        occupant.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureShowcaseInvoicesAndReadingsAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var electricReading = await EnsureMeterReadingAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            LinhElectricReadingId,
            ElectricServiceTypeId,
            "1341.png",
            1250m,
            1341m,
            "AI OCR: dien hien tai 1341 kWh",
            cancellationToken);
        var waterReading = await EnsureMeterReadingAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            LinhWaterReadingId,
            WaterServiceTypeId,
            "96.png",
            88m,
            96m,
            "AI OCR: nuoc hien tai 96 m3",
            cancellationToken);

        await EnsureInvoiceAsync(
            context,
            LinhPaidInvoiceId,
            "HD-B201-202606",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            InvoiceStatus.Paid,
            3600000m,
            520000m,
            160000m,
            4280000m,
            "Hoa don thang 06/2026 da thanh toan, dung lam lich su giao dich.",
            SeededAt.AddMonths(6).AddDays(1),
            cancellationToken);
        await ReplaceInvoiceItemsAsync(
            context,
            LinhPaidInvoiceId,
            new[]
            {
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000101"), LinhPaidInvoiceId, null, null, InvoiceItemType.Rent, "Tien phong B201 thang 06/2026", 1, 3600000m),
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000102"), LinhPaidInvoiceId, InternetServiceTypeId, null, InvoiceItemType.Service, "Internet + rac thang 06/2026", 1, 160000m),
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000103"), LinhPaidInvoiceId, ElectricServiceTypeId, null, InvoiceItemType.Service, "Dien nuoc thang 06/2026", 1, 520000m)
            },
            cancellationToken);

        await EnsureInvoiceAsync(
            context,
            LinhCurrentInvoiceId,
            "HD-B201-202607",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            InvoiceStatus.Issued,
            3600000m,
            508000m,
            120000m,
            4228000m,
            "Hoa don hien tai co anh cong to dien nuoc AI OCR; can thanh toan truoc khi huy hop dong.",
            null,
            cancellationToken);
        await ReplaceInvoiceItemsAsync(
            context,
            LinhCurrentInvoiceId,
            new[]
            {
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000201"), LinhCurrentInvoiceId, null, null, InvoiceItemType.Rent, "Tien phong B201 thang 07/2026", 1, 3600000m),
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000202"), LinhCurrentInvoiceId, ElectricServiceTypeId, electricReading.Id, InvoiceItemType.Service, "Dien thang 07/2026: 1341 - 1250 = 91 kWh", 91, 4000m),
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000203"), LinhCurrentInvoiceId, WaterServiceTypeId, waterReading.Id, InvoiceItemType.Service, "Nuoc thang 07/2026: 96 - 88 = 8 m3", 8, 18000m),
                CreateInvoiceItem(Guid.Parse("83000000-0000-0000-0000-000000000204"), LinhCurrentInvoiceId, InternetServiceTypeId, null, InvoiceItemType.Service, "Internet + rac thang 07/2026", 1, 120000m)
            },
            cancellationToken);

        await RemoveInvoiceIfExistsAsync(context, LinhFinalInvoiceId, "HD-B201-FINAL-202607", cancellationToken);

        await EnsureWalletTransactionAsync(
            context,
            Guid.Parse("71000000-0000-0000-0000-000000000201"),
            TenantLinhWalletAccountId,
            CoTenantUserId,
            WalletTransactionType.InvoicePayment,
            WalletTransactionDirection.Debit,
            4280000m,
            54280000m,
            50000000m,
            0m,
            0m,
            nameof(Invoice),
            LinhPaidInvoiceId,
            "Thanh toan hoa don B201 thang 06/2026.",
            SeededAt.AddMonths(6).AddDays(1),
            cancellationToken);

        await EnsureWalletTransactionAsync(
            context,
            Guid.Parse("71000000-0000-0000-0000-000000000202"),
            LandlordWalletAccountId,
            LandlordUserId,
            WalletTransactionType.InvoiceReceive,
            WalletTransactionDirection.Credit,
            4280000m,
            8220000m,
            12500000m,
            3600000m,
            3600000m,
            nameof(Invoice),
            LinhPaidInvoiceId,
            "Nhan thanh toan hoa don B201 thang 06/2026.",
            SeededAt.AddMonths(6).AddDays(1),
            cancellationToken);
    }

    private static async Task EnsureShowcaseContractFilesAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        CancellationToken cancellationToken)
    {
        var previewBytes = BuildShowcaseContractPdf(isSigned: false);
        var signedBytes = BuildShowcaseContractPdf(isSigned: true);

        await EnsureContractPdfFileAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            LinhPreviewContractFileId,
            ContractFilePurpose.Preview,
            "active-b201-preview.pdf",
            previewBytes,
            isLegallySigned: false,
            cancellationToken);
        await EnsureContractPdfFileAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            LinhSignedContractFileId,
            ContractFilePurpose.SignedLegalDocument,
            "active-b201-signed-vnpt.pdf",
            signedBytes,
            isLegallySigned: true,
            cancellationToken);
        await EnsureContractPdfFileAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            LinhMaskedContractFileId,
            ContractFilePurpose.MaskedReference,
            "active-b201-masked.pdf",
            signedBytes,
            isLegallySigned: true,
            cancellationToken);

        await EnsureContractSignatureAsync(
            context,
            LinhLandlordSignatureId,
            LandlordUserId,
            ContractSignerRole.Landlord,
            1,
            "VNPT-PART-LANDLORD-0901000002",
            "CN=Nguyen Xuan Huan, O=VNPT SmartCA, C=VN",
            cancellationToken);
        await EnsureContractSignatureAsync(
            context,
            LinhTenantSignatureId,
            CoTenantUserId,
            ContractSignerRole.Tenant,
            2,
            "VNPT-PART-TENANT-0901000004",
            "CN=Le Quang Linh, O=VNPT SmartCA, C=VN",
            cancellationToken);
    }

    private static byte[] BuildShowcaseContractPdf(bool isSigned)
    {
        var model = new ContractDocumentModel
        {
            PreparedAt = SeedVietnamTime(2026, 6, 1, 9),
            ContractNumber = LinhShowcaseContractNumber,
            Landlord = new ContractDocumentParty
            {
                UserId = LandlordUserId,
                FullName = "Nguyen Xuan Huan",
                DateOfBirth = new DateOnly(1995, 10, 21),
                DocumentNumber = "079********102",
                Address = "Da Nang",
                PhoneNumber = "0901000002",
                Email = LandlordEmail
            },
            Tenant = new ContractDocumentParty
            {
                UserId = CoTenantUserId,
                FullName = "Le Quang Linh",
                DateOfBirth = new DateOnly(1998, 4, 12),
                DocumentNumber = "079********104",
                Address = "Da Nang",
                PhoneNumber = "0901000004",
                Email = CoTenantEmail
            },
            Property = new ContractDocumentProperty
            {
                RoomId = Room102Id,
                RoomNumber = "B201",
                RoomingHouseName = "Khu tro Xuan Huan",
                Address = "144 Tran Dai Nghia, Phuong Ngu Hanh Son, Thanh pho Da Nang",
                Floor = 2,
                AreaM2 = 24m,
                MaxOccupants = 2,
                Description = "Phong B201 dang co hop dong active cua Le Quang Linh."
            },
            FinancialTerms = new ContractDocumentFinancialTerms
            {
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2027, 5, 31),
                MonthlyRent = 3600000m,
                DepositAmount = 3600000m,
                PaymentDay = 5,
                DepositPaidAt = SeedVietnamTime(2026, 5, 25, 10)
            },
            ServicePrices = new[]
            {
                new ContractDocumentServicePrice { ServiceName = "Dien", PricingUnit = "kWh", UnitPrice = 4000m, EffectiveFrom = new DateOnly(2026, 1, 1) },
                new ContractDocumentServicePrice { ServiceName = "Nuoc", PricingUnit = "m3", UnitPrice = 18000m, EffectiveFrom = new DateOnly(2026, 1, 1) },
                new ContractDocumentServicePrice { ServiceName = "Internet", PricingUnit = "thang", UnitPrice = 120000m, EffectiveFrom = new DateOnly(2026, 1, 1) }
            },
            Occupants = new[]
            {
                new ContractDocumentOccupant
                {
                    OccupantId = LinhContractOccupantId,
                    UserId = CoTenantUserId,
                    FullName = "Le Quang Linh",
                    DateOfBirth = new DateOnly(1998, 4, 12),
                    DocumentNumber = "079********104",
                    Relationship = "Self",
                    MoveInDate = new DateOnly(2026, 6, 1)
                }
            },
            HouseRules = new[]
            {
                "Giu ve sinh chung, khong gay on ao sau 22:30.",
                "Tien dien nuoc tinh theo chi so thuc te hang thang.",
                "Huy hop dong truoc han/vi pham dieu khoan co the bi mat coc theo noi dung hop dong."
            }
        };

        var renderer = new ContractPdfRenderer();
        var options = new ContractRenderOptions
        {
            ViewerMode = isSigned ? ContractFilePurpose.SignedLegalDocument.ToString() : ContractFilePurpose.Preview.ToString(),
            ShowFullDocumentNumbers = false
        };

        return isSigned
            ? renderer.RenderSignedRentalContract(model, options)
            : renderer.RenderRentalContractPreview(model, options);
    }

    private static async Task EnsureRoomPriceTierAsync(
        AppDbContext context,
        Guid roomId,
        int occupantCount,
        decimal monthlyRent,
        CancellationToken cancellationToken)
    {
        var tier = await context.RoomPriceTiers
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.OccupantCount == occupantCount, cancellationToken);

        if (tier is null)
        {
            context.RoomPriceTiers.Add(new RoomPriceTier
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                OccupantCount = occupantCount,
                MonthlyRent = monthlyRent,
                IsActive = true,
                CreatedAt = SeededAt,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        tier.MonthlyRent = monthlyRent;
        tier.IsActive = true;
        tier.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureWalletAsync(
        AppDbContext context,
        Guid walletId,
        Guid userId,
        decimal balance,
        decimal reservedBalance,
        CancellationToken cancellationToken)
    {
        var wallet = await context.WalletAccounts
            .FirstOrDefaultAsync(x => x.Id == walletId || x.UserId == userId, cancellationToken);

        if (wallet is null)
        {
            context.WalletAccounts.Add(new WalletAccount
            {
                Id = walletId,
                UserId = userId,
                Balance = balance,
                ReservedBalance = reservedBalance,
                Currency = "VND",
                Status = WalletAccountStatus.Active,
                CreatedAt = SeededAt,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        wallet.Balance = balance;
        wallet.ReservedBalance = reservedBalance;
        wallet.Currency = "VND";
        wallet.Status = WalletAccountStatus.Active;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task EnsureWalletTransactionAsync(
        AppDbContext context,
        Guid id,
        Guid walletAccountId,
        Guid userId,
        WalletTransactionType type,
        WalletTransactionDirection direction,
        decimal amount,
        decimal balanceBefore,
        decimal balanceAfter,
        decimal reservedBefore,
        decimal reservedAfter,
        string? relatedEntityType,
        Guid? relatedEntityId,
        string description,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var transaction = await context.WalletTransactions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transaction is null)
        {
            transaction = new WalletTransaction { Id = id };
            context.WalletTransactions.Add(transaction);
        }

        transaction.WalletAccountId = walletAccountId;
        transaction.UserId = userId;
        transaction.TransactionType = type;
        transaction.Direction = direction;
        transaction.Amount = amount;
        transaction.BalanceBefore = balanceBefore;
        transaction.BalanceAfter = balanceAfter;
        transaction.ReservedBalanceBefore = reservedBefore;
        transaction.ReservedBalanceAfter = reservedAfter;
        transaction.RelatedEntityType = relatedEntityType;
        transaction.RelatedEntityId = relatedEntityId;
        transaction.Description = description;
        transaction.Status = WalletTransactionStatus.Succeeded;
        transaction.CreatedAt = createdAt;
    }

    private static async Task<MeterReading> EnsureMeterReadingAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid id,
        Guid serviceTypeId,
        string fileName,
        decimal previousReading,
        decimal currentReading,
        string aiRawText,
        CancellationToken cancellationToken)
    {
        var imageBytes = LoadRequiredDemoImageBytes(fileName);
        var mediaAsset = await EnsureBinaryMediaAssetAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            Guid.Parse(id.ToString()),
            LandlordUserId,
            MediaScope.MeterReadingImage,
            MediaVisibility.Private,
            fileName,
            "image/png",
            imageBytes,
            nameof(MeterReading),
            id,
            cancellationToken);

        var reading = await context.MeterReadings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (reading is null)
        {
            reading = new MeterReading { Id = id, CreatedAt = SeededAt.AddMonths(6) };
            context.MeterReadings.Add(reading);
        }

        reading.RoomId = Room102Id;
        reading.ContractId = LinhContractId;
        reading.ServiceTypeId = serviceTypeId;
        reading.BillingPeriodStart = new DateOnly(2026, 7, 1);
        reading.BillingPeriodEnd = new DateOnly(2026, 7, 31);
        reading.PreviousReading = previousReading;
        reading.CurrentReading = currentReading;
        reading.Consumption = currentReading - previousReading;
        reading.ProofMediaAssetId = mediaAsset.Id;
        reading.AiReading = currentReading;
        reading.AiRawText = aiRawText;
        reading.WasManuallyCorrected = false;
        reading.RecordedByLandlordUserId = LandlordUserId;
        reading.ReadingAt = SeededAt.AddMonths(6).AddDays(30).AddHours(8);
        reading.UpdatedAt = DateTimeOffset.UtcNow;
        return reading;
    }

    private static async Task EnsureInvoiceAsync(
        AppDbContext context,
        Guid id,
        string invoiceNo,
        DateOnly periodStart,
        DateOnly periodEnd,
        InvoiceStatus status,
        decimal rentAmount,
        decimal utilityAmount,
        decimal serviceAmount,
        decimal totalAmount,
        string note,
        DateTimeOffset? paidAt,
        CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices
            .FirstOrDefaultAsync(x => x.Id == id || x.InvoiceNo == invoiceNo, cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice { Id = id, CreatedAt = SeededAt.AddMonths(6) };
            context.Invoices.Add(invoice);
        }

        invoice.ContractId = LinhContractId;
        invoice.RoomId = Room102Id;
        invoice.TenantUserId = CoTenantUserId;
        invoice.LandlordUserId = LandlordUserId;
        invoice.InvoiceNo = invoiceNo;
        invoice.BillingPeriodStart = periodStart;
        invoice.BillingPeriodEnd = periodEnd;
        invoice.IssueDate = status == InvoiceStatus.Draft ? null : periodEnd;
        invoice.DueDate = periodEnd.AddDays(5);
        invoice.RentAmount = rentAmount;
        invoice.UtilityAmount = utilityAmount;
        invoice.ServiceAmount = serviceAmount;
        invoice.DiscountAmount = 0m;
        invoice.TotalAmount = totalAmount;
        invoice.Status = status;
        invoice.Note = note;
        invoice.SentAt = status == InvoiceStatus.Draft ? null : SeededAt.AddMonths(6).AddDays(15);
        invoice.PaidAt = paidAt;
        invoice.CancelledAt = null;
        invoice.CancelReason = null;
        invoice.WalletTransferGroupId = status == InvoiceStatus.Paid
            ? Guid.Parse("84000000-0000-0000-0000-000000000101")
            : null;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static async Task ReplaceInvoiceItemsAsync(
        AppDbContext context,
        Guid invoiceId,
        IReadOnlyCollection<InvoiceItem> items,
        CancellationToken cancellationToken)
    {
        var existingItems = await context.InvoiceItems
            .Where(x => x.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
        context.InvoiceItems.RemoveRange(existingItems);
        context.InvoiceItems.AddRange(items);
    }

    private static InvoiceItem CreateInvoiceItem(
        Guid id,
        Guid invoiceId,
        Guid? serviceTypeId,
        Guid? meterReadingId,
        InvoiceItemType itemType,
        string description,
        decimal quantity,
        decimal unitPrice)
    {
        return new InvoiceItem
        {
            Id = id,
            InvoiceId = invoiceId,
            ServiceTypeId = serviceTypeId,
            MeterReadingId = meterReadingId,
            ItemType = itemType,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = quantity * unitPrice,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task EnsureContractPdfFileAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid fileId,
        ContractFilePurpose purpose,
        string fileName,
        byte[] pdfBytes,
        bool isLegallySigned,
        CancellationToken cancellationToken)
    {
        var mediaAssetId = Guid.Parse(fileId.ToString());
        var mediaAsset = await EnsureBinaryMediaAssetAsync(
            context,
            mediaStorageService,
            mediaObjectKeyFactory,
            mediaAssetId,
            LandlordUserId,
            MediaScope.ContractPdf,
            MediaVisibility.Private,
            fileName,
            "application/pdf",
            pdfBytes,
            nameof(ContractFile),
            fileId,
            cancellationToken);

        var contractFile = await context.ContractFiles
            .FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

        if (contractFile is null)
        {
            contractFile = new ContractFile { Id = fileId };
            context.ContractFiles.Add(contractFile);
        }

        contractFile.RentalContractId = LinhContractId;
        contractFile.RentalContractAppendixId = null;
        contractFile.MediaAssetId = mediaAsset.Id;
        contractFile.Purpose = purpose;
        contractFile.ContentType = "application/pdf";
        contractFile.FileUrl = PublicMediaPathBuilder.Build(mediaAsset.Id);
        contractFile.Sha256Hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();
        contractFile.IsLegallySigned = isLegallySigned;
        contractFile.CreatedAt = SeededAt.AddMonths(5).AddDays(2);
    }

    private static async Task<MediaAsset> EnsureBinaryMediaAssetAsync(
        AppDbContext context,
        IMediaStorageService mediaStorageService,
        IMediaObjectKeyFactory mediaObjectKeyFactory,
        Guid mediaAssetId,
        Guid ownerUserId,
        MediaScope scope,
        MediaVisibility visibility,
        string fileName,
        string contentType,
        byte[] bytes,
        string linkedEntityType,
        Guid linkedEntityId,
        CancellationToken cancellationToken)
    {
        var incomingHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var mediaAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken);

        if (mediaAsset is null)
        {
            var objectKey = mediaObjectKeyFactory.Create(scope, visibility, fileName);
            await using var content = new MemoryStream(bytes, writable: false);
            var stored = await mediaStorageService.UploadAsync(
                new MediaUploadRequest
                {
                    Content = content,
                    OriginalFileName = fileName,
                    ContentType = contentType,
                    FileSize = bytes.Length,
                    ObjectKey = objectKey.ObjectKey,
                    Visibility = visibility
                },
                cancellationToken);

            mediaAsset = new MediaAsset
            {
                Id = mediaAssetId,
                OwnerUserId = ownerUserId,
                BucketName = stored.BucketName,
                ObjectKey = stored.ObjectKey,
                OriginalFileName = fileName,
                StoredFileName = stored.StoredFileName,
                ContentType = contentType,
                FileSize = bytes.Length,
                FileHash = incomingHash,
                Scope = scope,
                Visibility = visibility,
                Status = MediaStatus.Linked,
                LinkedEntityType = linkedEntityType,
                LinkedEntityId = linkedEntityId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.MediaAssets.Add(mediaAsset);
            return mediaAsset;
        }

        var shouldUploadFreshContent = mediaAsset.ContentType != contentType ||
                                       mediaAsset.FileHash != incomingHash ||
                                       mediaAsset.FileSize != bytes.Length;

        mediaAsset.OwnerUserId = ownerUserId;
        mediaAsset.OriginalFileName = fileName;
        mediaAsset.ContentType = contentType;
        mediaAsset.FileSize = bytes.Length;
        mediaAsset.FileHash = incomingHash;
        mediaAsset.Scope = scope;
        mediaAsset.Visibility = visibility;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = linkedEntityType;
        mediaAsset.LinkedEntityId = linkedEntityId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(mediaAsset.ObjectKey))
        {
            var objectKey = mediaObjectKeyFactory.Create(scope, visibility, fileName);
            mediaAsset.ObjectKey = objectKey.ObjectKey;
            mediaAsset.StoredFileName = objectKey.StoredFileName;
        }

        if (string.IsNullOrWhiteSpace(mediaAsset.BucketName))
        {
            mediaAsset.BucketName = mediaStorageService.GetBucketName();
        }

        if (shouldUploadFreshContent || !await mediaStorageService.ExistsAsync(mediaAsset.ObjectKey, cancellationToken))
        {
            await using var content = new MemoryStream(bytes, writable: false);
            await mediaStorageService.UploadAsync(
                new MediaUploadRequest
                {
                    Content = content,
                    OriginalFileName = fileName,
                    ContentType = contentType,
                    FileSize = bytes.Length,
                    ObjectKey = mediaAsset.ObjectKey,
                    Visibility = visibility
                },
                cancellationToken);
        }

        return mediaAsset;
    }

    private static async Task EnsureContractSignatureAsync(
        AppDbContext context,
        Guid id,
        Guid signerUserId,
        ContractSignerRole signerRole,
        int signingOrder,
        string providerParticipantId,
        string certificateSubject,
        CancellationToken cancellationToken)
    {
        var signature = await context.ContractSignatures
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (signature is null)
        {
            signature = new ContractSignature { Id = id };
            context.ContractSignatures.Add(signature);
        }

        signature.RentalContractId = LinhContractId;
        signature.RentalContractAppendixId = null;
        signature.SignerUserId = signerUserId;
        signature.SignerRole = signerRole;
        signature.SignatureMethod = ContractSignatureMethod.VnptSmsOtp;
        signature.Status = ContractSignatureStatus.Signed;
        signature.SigningOrder = signingOrder;
        signature.Provider = ESignProvider.Vnpt;
        signature.ProviderEnvelopeId = "VNPT-ECONTRACT-B201-20260601";
        signature.ProviderParticipantId = providerParticipantId;
        signature.SigningUrl = null;
        signature.CertificateSerialNumber = $"VNPT-CA-2026-B201-{signingOrder:D2}";
        signature.CertificateSubject = certificateSubject;
        signature.CertificateIssuer = "VNPT Certification Authority";
        signature.SignedFileSha256Hash = null;
        signature.ProviderEvidenceJson = $$"""
        {"provider":"VNPT eContract","auth_method":"SMS_OTP","document_number":"HD-202606010900-B201-XUANHUAN","participant":"{{providerParticipantId}}"}
        """;
        signature.NotifiedAt = SeededAt.AddMonths(5).AddDays(2).AddHours(signingOrder);
        signature.SignedAt = SeededAt.AddMonths(5).AddDays(2).AddHours(signingOrder + 1);
        signature.IpAddress = "127.0.0.1";
        signature.UserAgent = "Showcase seed VNPT eContract";
        signature.CreatedAt = SeededAt.AddMonths(5).AddDays(2);
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
