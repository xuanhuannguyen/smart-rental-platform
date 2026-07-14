using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartRentalPlatform.Infrastructure.Persistence;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260623120000_DemoFullFlowDataset")]
    public partial class DemoFullFlowDataset : Migration
    {
        public const string DefaultPassword = "Demo@123456";

        public const string AdminEmail = "admin.hoasen@example.com";
        public const string LandlordEmail = "nguyenxuanhuan.dev@gmail.com";
        public const string MainTenantEmail = "hoctienganh4english@gmail.com";
        public const string CoTenantEmail = "phan.van.thanh@example.com";
        public const string NoKycTenantEmail = "hoang.phuc.nhat.quang@example.com";
        public const string DemoTenantEmail = "demoThueTro@gmail.com";

        private const string ProvinceCode = "48";
        private const string ProvinceName = "Thành phố Đà Nẵng";
        private const string WardCode = "20285";
        private const string WardName = "Phường Ngũ Hành Sơn";

        private static readonly Guid AdminUser = Id("KFC-SCENARIO-ADMIN");
        private static readonly Guid Landlord = Id("DEMO-FULL-FLOW-LANDLORD-NGUYEN-XUAN-HUAN");
        private static readonly Guid MainTenant = Id("DEMO-FULL-FLOW-TENANT-HOC-TIENG-ANH");
        private static readonly Guid CoTenant = Id("KFC-SCENARIO-COTENANT-PHAN-VAN-THANH");
        private static readonly Guid NoKycTenant = Id("KFC-SCENARIO-TENANT-HOANG-PHUC-NHAT-QUANG");
        private static readonly Guid DemoTenant = Id("DEMO-FULL-FLOW-TENANT-DEMO-THUE-TRO");
        private static readonly Guid LegacyKfcLandlord = Id("KFC-SCENARIO-LANDLORD-NGUYEN-XUAN-HUAN");
        private static readonly Guid LegacyKfcMainTenant = Id("KFC-SCENARIO-TENANT-LE-QUANG-LINH");

        private static readonly Guid HouseHoaSen = Id("KFC-SCENARIO-HOUSE");
        private static readonly Guid Room101 = Id("KFC-SCENARIO-ROOM-101");
        private static readonly Guid Room102 = Id("KFC-SCENARIO-ROOM-102");
        private static readonly Guid Room201 = Id("KFC-SCENARIO-ROOM-201");

        private static readonly Guid RentalRequest101 = Id("KFC-SCENARIO-REQUEST-101");
        private static readonly Guid Deposit101 = Id("KFC-SCENARIO-DEPOSIT-101");
        private static readonly Guid Contract101 = Id("KFC-SCENARIO-CONTRACT-101");
        private static readonly Guid OccupantMain = Id("KFC-SCENARIO-OCCUPANT-MAIN");
        private static readonly Guid OccupantCoTenant = Id("KFC-SCENARIO-OCCUPANT-COTENANT");
        private static readonly Guid OccupantNoAccount = Id("KFC-SCENARIO-OCCUPANT-NO-ACCOUNT");

        private static readonly Guid InvoiceApril = Id("KFC-SCENARIO-INVOICE-APRIL");
        private static readonly Guid InvoiceMay = Id("KFC-SCENARIO-INVOICE-MAY");

        private static readonly Guid WalletAdmin = Id("KFC-SCENARIO-WALLET-ADMIN");
        private static readonly Guid WalletLandlord = Id("KFC-SCENARIO-WALLET-LANDLORD");
        private static readonly Guid WalletMainTenant = Id("KFC-SCENARIO-WALLET-MAIN-TENANT");
        private static readonly Guid WalletCoTenant = Id("KFC-SCENARIO-WALLET-COTENANT");
        private static readonly Guid WalletNoKycTenant = Id("KFC-SCENARIO-WALLET-NO-KYC");
        private static readonly Guid WalletDemoTenant = Id("DEMO-FULL-FLOW-WALLET-DEMO-THUE-TRO");

        private static readonly Guid DepositTransferGroup = Id("KFC-SCENARIO-DEPOSIT-TRANSFER-GROUP");
        private static readonly Guid InvoiceAprilTransferGroup = Id("KFC-SCENARIO-INVOICE-APRIL-TRANSFER-GROUP");

        private static readonly Guid ElectricService = Guid.Parse("60000000-0000-0000-0000-000000000001");
        private static readonly Guid WaterService = Guid.Parse("60000000-0000-0000-0000-000000000002");
        private static readonly Guid InternetService = Guid.Parse("60000000-0000-0000-0000-000000000003");
        private static readonly Guid TrashService = Guid.Parse("60000000-0000-0000-0000-000000000004");

        private static readonly DateOnly LeaseStart = new(2026, 4, 20);
        private static readonly DateOnly LeaseEnd = new(2027, 4, 20);
        private const decimal RentOnePerson = 2_500_000m;
        private const decimal RentTwoPeople = 3_000_000m;
        private const decimal RentThreePeople = 3_500_000m;
        private const decimal DepositAmount = 3_500_000m;
        private const decimal InvoiceAprilAmount = 1_283_333m;
        private const decimal InvoiceMayAmount = 3_500_000m;
        private const decimal TenantFinalBalance = 50_000_000m;
        private const decimal DemoTenantBalance = 50_000_000m;
        private const decimal LandlordFinalBalance = 50_000_000m;
        private const decimal TenantTopUpAmount = TenantFinalBalance + DepositAmount + InvoiceAprilAmount;
        private const decimal LandlordTopUpAmount = LandlordFinalBalance - DepositAmount - InvoiceAprilAmount;

        private static DateTimeOffset Now => DateTimeOffset.UtcNow;
        private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0)
            => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ResetDemoData(migrationBuilder);
            SeedUsers(migrationBuilder);
            SeedKyc(migrationBuilder);
            SeedProperty(migrationBuilder);
            SeedRentalLifecycle(migrationBuilder);
            SeedBilling(migrationBuilder);
            SeedWalletsAndPayments(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM payment_webhook_logs WHERE provider_event_id LIKE 'KFC-SCENARIO-%' OR provider_event_id LIKE 'DEMO-FULL-FLOW-%' OR idempotency_key LIKE 'kfc-scenario:%' OR idempotency_key LIKE 'demo-full-flow:%';");
            migrationBuilder.Sql("DELETE FROM payment_transactions WHERE idempotency_key LIKE 'kfc-scenario:%' OR idempotency_key LIKE 'demo-full-flow:%' OR provider_order_code LIKE 'KFC-SCENARIO-%' OR provider_order_code LIKE 'DEMO-FULL-FLOW-%';");
            DeleteByIds(migrationBuilder, "wallet_transactions", "wallet_account_id", WalletAdmin, WalletLandlord, WalletMainTenant, WalletCoTenant, WalletNoKycTenant, WalletDemoTenant);
            DeleteByIds(migrationBuilder, "wallet_accounts", "id", WalletAdmin, WalletLandlord, WalletMainTenant, WalletCoTenant, WalletNoKycTenant, WalletDemoTenant);

            migrationBuilder.Sql("DELETE FROM invoice_items WHERE invoice_id IN (SELECT id FROM invoices WHERE invoice_no LIKE 'KFC-SCENARIO-%');");
            migrationBuilder.Sql("DELETE FROM invoices WHERE invoice_no LIKE 'KFC-SCENARIO-%';");

            migrationBuilder.Sql("DELETE FROM contract_files WHERE storage_object_key LIKE 'kfc-scenario/%';");
            migrationBuilder.Sql("DELETE FROM contract_signatures WHERE contract_id = " + Value(Contract101) + ";");
            migrationBuilder.Sql("DELETE FROM contract_occupant_documents WHERE contract_occupant_id IN (SELECT id FROM contract_occupants WHERE contract_id = " + Value(Contract101) + ");");
            migrationBuilder.Sql("DELETE FROM contract_occupants WHERE contract_id = " + Value(Contract101) + ";");
            DeleteByIds(migrationBuilder, "contracts", "id", Contract101);

            DeleteByIds(migrationBuilder, "room_deposits", "id", Deposit101);
            DeleteByIds(migrationBuilder, "rental_requests", "id", RentalRequest101);

            DeleteByIds(migrationBuilder, "room_amenities", "room_id", Room101, Room102, Room201);
            DeleteByIds(migrationBuilder, "room_price_tiers", "room_id", Room101, Room102, Room201);
            migrationBuilder.Sql("DELETE FROM property_images WHERE object_key LIKE 'kfc-scenario/%';");
            DeleteByIds(migrationBuilder, "rooms", "id", Room101, Room102, Room201);
            DeleteByIds(migrationBuilder, "rooming_house_service_prices", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_amenities", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_rules", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rental_policies", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_legal_documents", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_houses", "id", HouseHoaSen);

            DeleteByIds(migrationBuilder, "kyc_verifications", "user_id", Landlord, MainTenant, CoTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "user_roles", "user_id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "user_profiles", "user_id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "users", "id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
        }

        private static void ResetDemoData(MigrationBuilder migrationBuilder)
        {
            // Xóa dữ liệu giao dịch và ví của kịch bản demo
            migrationBuilder.Sql("DELETE FROM payment_webhook_logs WHERE provider_event_id LIKE 'KFC-SCENARIO-%' OR provider_event_id LIKE 'DEMO-FULL-FLOW-%' OR idempotency_key LIKE 'kfc-scenario:%' OR idempotency_key LIKE 'demo-full-flow:%';");
            migrationBuilder.Sql("DELETE FROM payment_transactions WHERE idempotency_key LIKE 'kfc-scenario:%' OR idempotency_key LIKE 'demo-full-flow:%' OR provider_order_code LIKE 'KFC-SCENARIO-%' OR provider_order_code LIKE 'DEMO-FULL-FLOW-%';");
            DeleteByIds(migrationBuilder, "wallet_transactions", "wallet_account_id", WalletAdmin, WalletLandlord, WalletMainTenant, WalletCoTenant, WalletNoKycTenant, WalletDemoTenant);
            DeleteByIds(migrationBuilder, "wallet_accounts", "id", WalletAdmin, WalletLandlord, WalletMainTenant, WalletCoTenant, WalletNoKycTenant, WalletDemoTenant);

            // Xóa hóa đơn của kịch bản demo
            migrationBuilder.Sql("DELETE FROM invoice_items WHERE invoice_id IN (SELECT id FROM invoices WHERE invoice_no LIKE 'KFC-SCENARIO-%');");
            migrationBuilder.Sql("DELETE FROM invoices WHERE invoice_no LIKE 'KFC-SCENARIO-%';");

            // Xóa hợp đồng và người ở của kịch bản demo
            migrationBuilder.Sql("DELETE FROM contract_files WHERE storage_object_key LIKE 'kfc-scenario/%';");
            migrationBuilder.Sql("DELETE FROM contract_signatures WHERE contract_id = " + Value(Contract101) + ";");
            migrationBuilder.Sql("DELETE FROM contract_occupant_documents WHERE contract_occupant_id IN (SELECT id FROM contract_occupants WHERE contract_id = " + Value(Contract101) + ");");
            migrationBuilder.Sql("DELETE FROM contract_occupants WHERE contract_id = " + Value(Contract101) + ";");
            DeleteByIds(migrationBuilder, "contracts", "id", Contract101);

            // Xóa yêu cầu thuê và đặt cọc của kịch bản demo
            DeleteByIds(migrationBuilder, "room_deposits", "id", Deposit101);
            DeleteByIds(migrationBuilder, "rental_requests", "id", RentalRequest101);

            // Xóa khu trọ/phòng của kịch bản demo trước khi xóa landlord legacy
            DeleteByIds(migrationBuilder, "room_amenities", "room_id", Room101, Room102, Room201);
            DeleteByIds(migrationBuilder, "room_price_tiers", "room_id", Room101, Room102, Room201);
            migrationBuilder.Sql("DELETE FROM property_images WHERE object_key LIKE 'kfc-scenario/%';");
            DeleteByIds(migrationBuilder, "rooms", "id", Room101, Room102, Room201);
            DeleteByIds(migrationBuilder, "rooming_house_service_prices", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_amenities", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_rules", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rental_policies", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_house_legal_documents", "rooming_house_id", HouseHoaSen);
            DeleteByIds(migrationBuilder, "rooming_houses", "id", HouseHoaSen);

            // Xóa các tài khoản user của kịch bản demo
            DeleteByIds(migrationBuilder, "kyc_verifications", "user_id", Landlord, MainTenant, CoTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "user_roles", "user_id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "user_profiles", "user_id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
            DeleteByIds(migrationBuilder, "users", "id", AdminUser, Landlord, MainTenant, CoTenant, NoKycTenant, DemoTenant, LegacyKfcLandlord, LegacyKfcMainTenant);
        }

        private static void SeedUsers(MigrationBuilder migrationBuilder)
        {
            InsertRows(migrationBuilder, "users",
                ["id", "email", "normalized_email", "phone_number", "password_hash", "display_name", "avatar_url", "status", "onboarding_status", "email_confirmed", "phone_confirmed", "access_failed_count", "lockout_end_at", "last_login_at", "created_at", "updated_at", "deleted_at"],
                User(AdminUser, AdminEmail, "Quản trị viên hệ thống", "Active", "Completed", "0900000000"),
                User(Landlord, LandlordEmail, "Nguyễn Xuân Huấn", "Active", "Completed", "0900000001"),
                User(MainTenant, MainTenantEmail, "Lê Quang Linh", "Active", "Completed", "0900000002"),
                User(CoTenant, CoTenantEmail, "Phan Văn Thành", "Active", "Completed", "0900000003"),
                User(NoKycTenant, NoKycTenantEmail, "Hoàng Phúc Nhật Quang", "Active", "NeedProfileUpdate", "0900000004"),
                User(DemoTenant, DemoTenantEmail, "Demo Thuê Trọ", "Active", "Completed", "0900000005"));

            InsertRows(migrationBuilder, "user_profiles",
                ["user_id", "full_name", "date_of_birth", "gender", "address_line", "verified_citizen_id_masked", "emergency_contact_name", "emergency_contact_phone", "created_at", "updated_at"],
                Profile(AdminUser, "Quản trị viên hệ thống", null),
                Profile(Landlord, "Nguyễn Xuân Huấn", "001********001"),
                Profile(MainTenant, "Lê Quang Linh", "079********001"),
                Profile(CoTenant, "Phan Văn Thành", "079********002"),
                Profile(NoKycTenant, "Hoàng Phúc Nhật Quang", null),
                Profile(DemoTenant, "Demo Thuê Trọ", "079********005"));

            InsertRows(migrationBuilder, "user_roles", ["user_id", "role_id", "created_at"],
                [AdminUser, 1, Utc(2026, 4, 1, 8)],
                [Landlord, 3, Utc(2026, 4, 1, 8)],
                [MainTenant, 2, Utc(2026, 4, 1, 8)],
                [CoTenant, 2, Utc(2026, 4, 1, 8)],
                [NoKycTenant, 2, Utc(2026, 4, 1, 8)],
                [DemoTenant, 2, Utc(2026, 4, 1, 8)]);
        }

        private static object?[] User(Guid id, string email, string displayName, string status, string onboardingStatus, string phone)
        {
            return [id, email, email.ToUpperInvariant(), phone, PasswordHash(), displayName, null, status, onboardingStatus, true, false, 0, null, Utc(2026, 4, 1, 8), Utc(2026, 4, 1, 8), Now, null];
        }

        private static object?[] Profile(Guid userId, string fullName, string? citizenIdMasked)
        {
            return [userId, fullName, new DateOnly(1995, 1, 1), "Male", $"15 Đường KFC Riverside, {WardName}, {ProvinceName}", citizenIdMasked, "Người thân liên hệ", "0999999999", Utc(2026, 4, 1, 8), Now];
        }

        private static void SeedKyc(MigrationBuilder migrationBuilder)
        {
            InsertRows(migrationBuilder, "kyc_verifications",
                ["id", "user_id", "document_type", "ekyc_provider", "ekyc_session_id", "front_image_object_key", "back_image_object_key", "selfie_image_object_key", "selfie_capture_method", "ocr_full_name", "ocr_citizen_id_masked", "citizen_id_hash", "document_number_encrypted", "ocr_date_of_birth", "ocr_gender", "ocr_address", "ocr_confidence", "document_check_result", "face_match_score", "face_match_result", "liveness_result", "ekyc_result", "ekyc_error_code", "ekyc_error_message", "risk_level", "status", "reviewed_by_admin_id", "rejected_reason", "submitted_at", "reviewed_at", "created_at", "updated_at"],
                Kyc("LANDLORD", Landlord, "Nguyễn Xuân Huấn", "001000000001"),
                Kyc("TENANT", MainTenant, "Lê Quang Linh", "079000000001"),
                Kyc("COTENANT", CoTenant, "Phan Văn Thành", "079000000002"),
                Kyc("DEMO-TENANT", DemoTenant, "Demo Thuê Trọ", "079000000005"));
        }

        private static object?[] Kyc(string key, Guid userId, string fullName, string citizenId)
        {
            return [Id($"KFC-SCENARIO-KYC-{key}"), userId, "CCCD", "Vnpt", $"kfc-scenario-kyc-{key.ToLowerInvariant()}", $"kfc-scenario/kyc/{key.ToLowerInvariant()}/front.jpg", $"kfc-scenario/kyc/{key.ToLowerInvariant()}/back.jpg", $"kfc-scenario/kyc/{key.ToLowerInvariant()}/selfie.jpg", "Upload", fullName, MaskCitizenId(citizenId), HashToken(citizenId), $"encrypted-{citizenId}", new DateOnly(1995, 1, 1), "Male", $"15 Đường KFC Riverside, {WardName}, {ProvinceName}", 0.9800m, "Valid", 0.9700m, "Matched", "Passed", "Passed", null, null, "Low", "Approved", AdminUser, null, Utc(2026, 4, 1, 9), Utc(2026, 4, 1, 10), Utc(2026, 4, 1, 9), Now];
        }

        private static void SeedProperty(MigrationBuilder migrationBuilder)
        {
            InsertRows(migrationBuilder, "rooming_houses",
                ["id", "landlord_user_id", "name", "description", "address_line", "ward_code", "province_code", "address_display", "latitude", "longitude", "google_map_url", "approval_status", "visibility_status", "rejected_reason", "reviewed_by_admin_id", "reviewed_at", "created_at", "updated_at", "deleted_at"],
                [HouseHoaSen, Landlord, "Khu trọ KFC Riverside", "Khu trọ KFC Riverside của Nguyễn Xuân Huấn dùng cho kịch bản thuê phòng, thanh toán cọc, hóa đơn và quản lý hợp đồng.", "15 Đường KFC Riverside", WardCode, ProvinceCode, $"15 Đường KFC Riverside, {WardName}, {ProvinceName}", 15.975400m, 108.263800m, "https://maps.example/da-nang/khu-tro-kfc-scenario", "Approved", "Visible", null, AdminUser, Utc(2026, 4, 1, 8), Utc(2026, 4, 1, 8), Now, null]);

            InsertRows(migrationBuilder, "rooming_house_legal_documents",
                ["rooming_house_id", "document_type", "front_image_object_key", "back_image_object_key", "extra_image_object_key", "document_number_masked", "document_number_hash", "uploaded_at", "created_at", "updated_at"],
                [HouseHoaSen, "LAND_USE_CERTIFICATE", "kfc-scenario/legal/front.jpg", "kfc-scenario/legal/back.jpg", null, "HS****2026", HashToken("kfc-scenario-legal-2026"), Utc(2026, 4, 1, 8), Utc(2026, 4, 1, 8), Now]);

            InsertRows(migrationBuilder, "rental_policies",
                ["id", "rooming_house_id", "min_rental_months", "max_rental_months", "allow_short_term_renewal", "renewal_notice_days", "deposit_months", "default_payment_day", "is_active", "created_at", "updated_at"],
                [Id("KFC-SCENARIO-POLICY"), HouseHoaSen, 6, 12, true, 30, 1.0m, 5, true, Utc(2026, 4, 1, 8), Now]);

            InsertRows(migrationBuilder, "rooming_house_rules",
                ["id", "rooming_house_id", "source_type", "pdf_object_key", "general_rules", "quiet_hours", "security_policy", "cleaning_policy", "guest_policy", "parking_policy", "utility_policy", "damage_compensation_policy", "additional_notes", "created_at", "updated_at"],
                [Id("KFC-SCENARIO-RULE"), HouseHoaSen, "FormGenerated", "kfc-scenario/rules/noi-quy.pdf", "Giữ vệ sinh chung, không gây mất trật tự và tuân thủ quy định phòng cháy chữa cháy.", "22:00-06:00", "Không tự ý giao chìa khóa cho người ngoài.", "Đổ rác đúng nơi quy định.", "Khách qua đêm phải báo trước với chủ trọ.", "Để xe đúng vị trí được phân công.", "Thanh toán tiền thuê và dịch vụ đúng hạn.", "Bồi thường theo thiệt hại thực tế nếu làm hư hỏng tài sản.", "Nội quy dùng cho kịch bản test Khu trọ KFC Riverside.", Utc(2026, 4, 1, 8), Now]);

            InsertRows(migrationBuilder, "property_images",
                ["id", "rooming_house_id", "room_id", "object_key", "image_url", "caption", "is_cover", "sort_order", "created_at"],
                Image("HOUSE-1", HouseHoaSen, null, true, "kfc-scenario/house/cover.jpg", "Mặt tiền Khu trọ KFC Riverside", 1),
                Image("HOUSE-2", HouseHoaSen, null, false, "kfc-scenario/house/corridor.jpg", "Hành lang Khu trọ KFC Riverside", 2),
                Image("HOUSE-3", HouseHoaSen, null, false, "kfc-scenario/house/parking.jpg", "Khu để xe Khu trọ KFC Riverside", 3));

            InsertRows(migrationBuilder, "rooming_house_amenities", ["rooming_house_id", "amenity_id"],
                [HouseHoaSen, 1],
                [HouseHoaSen, 2],
                [HouseHoaSen, 3]);

            InsertRows(migrationBuilder, "rooms",
                ["id", "rooming_house_id", "room_number", "floor", "area_m2", "max_occupants", "is_tiered_pricing", "status", "description", "created_at", "updated_at", "deleted_at"],
                Room(Room101, "KFC-101", 1, 28m, 3, "Occupied", "Phòng KFC-101 đang có hợp đồng thuê với Lê Quang Linh."),
                Room(Room102, "KFC-102", 1, 26m, 3, "Available", "Phòng KFC-102 đang trống để test luồng thuê mới."),
                Room(Room201, "KFC-201", 2, 24m, 2, "Hidden", "Phòng KFC-201 đang ở trạng thái draft/ẩn."));

            InsertRows(migrationBuilder, "room_price_tiers",
                ["id", "room_id", "occupant_count", "monthly_rent", "is_active", "created_at", "updated_at"],
                Tier(Room101, 1, RentOnePerson), Tier(Room101, 2, RentTwoPeople), Tier(Room101, 3, RentThreePeople),
                Tier(Room102, 1, RentOnePerson), Tier(Room102, 2, RentTwoPeople), Tier(Room102, 3, RentThreePeople),
                Tier(Room201, 1, RentOnePerson), Tier(Room201, 2, RentTwoPeople));

            InsertRows(migrationBuilder, "property_images",
                ["id", "rooming_house_id", "room_id", "object_key", "image_url", "caption", "is_cover", "sort_order", "created_at"],
                Image("ROOM-101-1", null, Room101, true, "kfc-scenario/rooms/101/cover.jpg", "Phòng KFC-101", 1),
                Image("ROOM-101-2", null, Room101, false, "kfc-scenario/rooms/101/window.jpg", "Cửa sổ phòng KFC-101", 2),
                Image("ROOM-101-3", null, Room101, false, "kfc-scenario/rooms/101/bathroom.jpg", "Nhà vệ sinh phòng KFC-101", 3),
                Image("ROOM-102-1", null, Room102, true, "kfc-scenario/rooms/102/cover.jpg", "Phòng KFC-102", 1),
                Image("ROOM-102-2", null, Room102, false, "kfc-scenario/rooms/102/window.jpg", "Cửa sổ phòng KFC-102", 2),
                Image("ROOM-102-3", null, Room102, false, "kfc-scenario/rooms/102/bathroom.jpg", "Nhà vệ sinh phòng KFC-102", 3));

            InsertRows(migrationBuilder, "room_amenities", ["room_id", "amenity_id"],
                [Room101, 1], [Room101, 5], [Room101, 7],
                [Room102, 1], [Room102, 5], [Room102, 7]);

            InsertRows(migrationBuilder, "rooming_house_service_prices",
                ["id", "rooming_house_id", "service_type_id", "pricing_unit", "unit_price", "effective_from", "effective_to", "is_active", "note", "created_at", "updated_at"],
                [Id("KFC-SCENARIO-SERVICE-ELECTRIC"), HouseHoaSen, ElectricService, "PerMonth", 0m, new DateOnly(2026, 4, 1), null, true, "Không thu riêng tiền điện trong kịch bản test.", Utc(2026, 4, 1, 8), Now],
                [Id("KFC-SCENARIO-SERVICE-WATER"), HouseHoaSen, WaterService, "PerMonth", 0m, new DateOnly(2026, 4, 1), null, true, "Không thu riêng tiền nước trong kịch bản test.", Utc(2026, 4, 1, 8), Now],
                [Id("KFC-SCENARIO-SERVICE-INTERNET"), HouseHoaSen, InternetService, "PerMonth", 0m, new DateOnly(2026, 4, 1), null, true, "Không thu riêng Internet trong kịch bản test.", Utc(2026, 4, 1, 8), Now],
                [Id("KFC-SCENARIO-SERVICE-TRASH"), HouseHoaSen, TrashService, "PerPersonPerMonth", 0m, new DateOnly(2026, 4, 1), null, true, "Không thu riêng phí vệ sinh trong kịch bản test.", Utc(2026, 4, 1, 8), Now]);
        }

        private static object?[] Room(Guid id, string number, int floor, decimal area, int maxOccupants, string status, string description)
        {
            return [id, HouseHoaSen, number, floor, area, maxOccupants, true, status, description, Utc(2026, 4, 1, 8), Now, null];
        }

        private static object?[] Tier(Guid roomId, int occupantCount, decimal rent)
        {
            return [Id($"KFC-SCENARIO-TIER-{roomId:N}-{occupantCount}"), roomId, occupantCount, rent, true, Utc(2026, 4, 1, 8), Now];
        }

        private static object?[] Image(string key, Guid? houseId, Guid? roomId, bool isCover, string objectKey, string caption, int sortOrder)
        {
            return [Id($"KFC-SCENARIO-IMG-{key}"), houseId, roomId, objectKey, $"/uploads/{objectKey}", caption, isCover, sortOrder, Utc(2026, 4, 1, 8)];
        }

        private static void SeedRentalLifecycle(MigrationBuilder migrationBuilder)
        {
            InsertRows(migrationBuilder, "rental_requests",
                ["id", "room_id", "tenant_user_id", "approved_by_landlord_id", "desired_start_date", "expected_end_date", "expected_occupant_count", "monthly_rent_snapshot", "deposit_amount_snapshot", "tenant_note", "status", "responded_at", "rejected_reason", "created_at", "updated_at"],
                [RentalRequest101, Room101, MainTenant, Landlord, LeaseStart, LeaseEnd, 3, RentThreePeople, DepositAmount, "Lê Quang Linh gửi yêu cầu thuê phòng KFC-101 cho 3 người ở từ ngày 20/04/2026 đến 20/04/2027.", "Accepted", Utc(2026, 4, 15, 10), null, Utc(2026, 4, 10, 9), Utc(2026, 4, 15, 10)]);

            InsertRows(migrationBuilder, "room_deposits",
                ["id", "rental_request_id", "room_id", "tenant_user_id", "landlord_user_id", "deposit_amount", "currency", "status", "payment_deadline_at", "paid_at", "refunded_at", "forfeited_at", "refund_amount", "forfeited_amount", "note", "payment_transfer_group_id", "refund_transfer_group_id", "created_at", "updated_at"],
                [Deposit101, RentalRequest101, Room101, MainTenant, Landlord, DepositAmount, "VND", "Paid", Utc(2026, 4, 17, 23, 59), Utc(2026, 4, 15, 11), null, null, null, null, "Lê Quang Linh đã thanh toán cọc 3.500.000 VND sau 1 tiếng kể từ khi Nguyễn Xuân Huấn accept.", DepositTransferGroup, null, Utc(2026, 4, 15, 10), Utc(2026, 4, 15, 11)]);

            InsertRows(migrationBuilder, "contracts",
                ["id", "rental_request_id", "room_deposit_id", "room_id", "main_tenant_user_id", "contract_number", "start_date", "end_date", "monthly_rent", "deposit_amount", "payment_day", "status", "room_snapshot", "signature_deadline_at", "activated_at", "termination_date", "termination_type", "status_reason", "created_at", "updated_at", "deleted_at"],
                [Contract101, RentalRequest101, Deposit101, Room101, MainTenant, "KFC-101-20260415", LeaseStart, LeaseEnd, RentThreePeople, DepositAmount, 5, "Active", Json("""{"RoomNumber":"KFC-101","RoomingHouseName":"Khu trọ KFC Riverside","MaxOccupants":3}"""), null, Utc(2026, 4, 15, 15), null, null, null, Utc(2026, 4, 15, 10), Utc(2026, 4, 15, 15), null]);

            InsertRows(migrationBuilder, "contract_occupants",
                ["id", "contract_id", "user_id", "guardian_occupant_id", "full_name", "phone_number", "date_of_birth", "relationship_to_main_tenant", "move_in_date", "move_out_date", "status", "created_at", "updated_at"],
                [OccupantMain, Contract101, MainTenant, null, "Lê Quang Linh", "0900000002", new DateOnly(1995, 1, 1), "Self", LeaseStart, null, "Active", Utc(2026, 4, 15, 10), Now],
                [OccupantCoTenant, Contract101, CoTenant, null, "Phan Văn Thành", "0900000003", new DateOnly(1995, 1, 1), "Bạn cùng phòng", LeaseStart, null, "Active", Utc(2026, 4, 15, 10), Now],
                [OccupantNoAccount, Contract101, null, null, "Nguyễn Hoàng Minh", "0900000099", new DateOnly(2005, 1, 1), "Em trai", LeaseStart, null, "Active", Utc(2026, 4, 15, 10), Now]);

            InsertRows(migrationBuilder, "contract_occupant_documents",
                ["id", "contract_occupant_id", "document_type", "document_number_masked", "document_number_hash", "document_number_encrypted", "front_image_object_key", "back_image_object_key", "extra_image_object_key", "uploaded_at", "created_at", "updated_at"],
                [Id("KFC-SCENARIO-OCC-DOC-NGUYEN-HOANG-MINH"), OccupantNoAccount, "CCCD", "079********099", HashToken("079000000099"), "encrypted-ngo-gia-bao", "kfc-scenario/occupants/ngo-gia-bao/front.jpg", "kfc-scenario/occupants/ngo-gia-bao/back.jpg", null, Utc(2026, 4, 15, 10), Utc(2026, 4, 15, 10), Now]);

            InsertRows(migrationBuilder, "contract_signatures",
                ["id", "contract_id", "appendix_id", "signer_user_id", "signer_role", "signature_method", "signature_text", "signed_at", "ip_address", "user_agent", "created_at"],
                [Id("KFC-SCENARIO-SIGNATURE-LANDLORD"), Contract101, null, Landlord, "Landlord", "ClickToSign", null, Utc(2026, 4, 15, 14), "127.0.0.42", "KFC Riverside fixture", Utc(2026, 4, 15, 14)],
                [Id("KFC-SCENARIO-SIGNATURE-TENANT"), Contract101, null, MainTenant, "Tenant", "ClickToSign", null, Utc(2026, 4, 15, 15), "127.0.0.42", "KFC Riverside fixture", Utc(2026, 4, 15, 15)]);
        }

        private static void SeedBilling(MigrationBuilder migrationBuilder)
        {
            InsertRows(migrationBuilder, "invoices",
                ["id", "contract_id", "room_id", "tenant_user_id", "landlord_user_id", "invoice_no", "billing_period_start", "billing_period_end", "issue_date", "due_date", "rent_amount", "utility_amount", "service_amount", "discount_amount", "total_amount", "status", "note", "sent_at", "paid_at", "cancelled_at", "cancel_reason", "wallet_transfer_group_id", "created_at", "updated_at"],
                [InvoiceApril, Contract101, Room101, MainTenant, Landlord, "KFC-SCENARIO-INV-202604-01", new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 30), new DateOnly(2026, 4, 30), new DateOnly(2026, 5, 5), InvoiceAprilAmount, 0m, 0m, 0m, InvoiceAprilAmount, "Paid", "Hóa đơn kỳ đầu phòng KFC-101 từ 20/04/2026 đến 30/04/2026, Lê Quang Linh đã thanh toán.", Utc(2026, 4, 30, 8), Utc(2026, 5, 1, 9), null, null, InvoiceAprilTransferGroup, Utc(2026, 4, 30, 8), Utc(2026, 5, 1, 9)],
                [InvoiceMay, Contract101, Room101, MainTenant, Landlord, "KFC-SCENARIO-INV-202605-01", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 5), InvoiceMayAmount, 0m, 0m, 0m, InvoiceMayAmount, "Overdue", "Hóa đơn tháng 5 phòng KFC-101 đã quá hạn và chưa thanh toán.", Utc(2026, 5, 31, 8), null, null, null, null, Utc(2026, 5, 31, 8), Utc(2026, 6, 5, 8)]);

            InsertRows(migrationBuilder, "invoice_items",
                ["id", "invoice_id", "service_type_id", "meter_reading_id", "item_type", "description", "quantity", "unit_price", "amount", "created_at"],
                [Id("KFC-SCENARIO-INVOICE-APRIL-RENT"), InvoiceApril, null, null, "Rent", "Tiền thuê phòng KFC-101 từ 20/04/2026 đến 30/04/2026", 1m, InvoiceAprilAmount, InvoiceAprilAmount, Utc(2026, 4, 30, 8)],
                [Id("KFC-SCENARIO-INVOICE-MAY-RENT"), InvoiceMay, null, null, "Rent", "Tiền thuê phòng KFC-101 tháng 05/2026", 1m, InvoiceMayAmount, InvoiceMayAmount, Utc(2026, 5, 30, 8)]);
        }

        private static void SeedWalletsAndPayments(MigrationBuilder migrationBuilder)
        {
            var tenantTopUpPayment = Id("KFC-SCENARIO-PAYMENT-TENANT-TOPUP");
            var landlordTopUpPayment = Id("KFC-SCENARIO-PAYMENT-LANDLORD-TOPUP");
            var demoTenantTopUpPayment = Id("DEMO-FULL-FLOW-PAYMENT-DEMO-TENANT-TOPUP");
            var tenantTopUpAt = Utc(2026, 4, 15, 9);
            var depositPaidAt = Utc(2026, 4, 15, 11);
            var invoicePaidAt = Utc(2026, 5, 1, 9);

            InsertRows(migrationBuilder, "wallet_accounts",
                ["id", "user_id", "balance", "reserved_balance", "currency", "status", "created_at", "updated_at"],
                [WalletAdmin, AdminUser, 0m, 0m, "VND", "Active", Utc(2026, 4, 1, 8), Now],
                [WalletLandlord, Landlord, LandlordFinalBalance, DepositAmount, "VND", "Active", Utc(2026, 4, 1, 8), Now],
                [WalletMainTenant, MainTenant, TenantFinalBalance, 0m, "VND", "Active", Utc(2026, 4, 1, 8), Now],
                [WalletCoTenant, CoTenant, 0m, 0m, "VND", "Active", Utc(2026, 4, 1, 8), Now],
                [WalletNoKycTenant, NoKycTenant, 0m, 0m, "VND", "Active", Utc(2026, 4, 1, 8), Now],
                [WalletDemoTenant, DemoTenant, DemoTenantBalance, 0m, "VND", "Active", Utc(2026, 4, 1, 8), Now]);

            InsertRows(migrationBuilder, "payment_transactions",
                ["id", "wallet_account_id", "payer_user_id", "idempotency_key", "amount", "currency", "payment_purpose", "payment_method", "provider_order_code", "provider_transaction_code", "provider_checkout_url", "provider_qr_code", "gateway_response_code", "gateway_response_message", "status", "expires_at", "paid_at", "failed_at", "confirmed_at", "created_at", "updated_at"],
                [tenantTopUpPayment, WalletMainTenant, MainTenant, "kfc-scenario:tenant-topup", TenantTopUpAmount, "VND", "WalletTopUp", "Mock", "KFC-SCENARIO-TOPUP-TENANT", "KFC-SCENARIO-TOPUP-TENANT-TXN", "https://pay.example/KFC-SCENARIO-TOPUP-TENANT", null, "00", "Nạp ví cho Lê Quang Linh trước khi thanh toán cọc và hóa đơn kỳ đầu.", "Succeeded", Utc(2026, 4, 15, 10), tenantTopUpAt, null, tenantTopUpAt, tenantTopUpAt, tenantTopUpAt],
                [landlordTopUpPayment, WalletLandlord, Landlord, "kfc-scenario:landlord-topup", LandlordTopUpAmount, "VND", "WalletTopUp", "Mock", "KFC-SCENARIO-TOPUP-LANDLORD", "KFC-SCENARIO-TOPUP-LANDLORD-TXN", "https://pay.example/KFC-SCENARIO-TOPUP-LANDLORD", null, "00", "Nạp ví cho Nguyễn Xuân Huấn để cân bằng số dư kịch bản.", "Succeeded", Utc(2026, 4, 15, 10), tenantTopUpAt, null, tenantTopUpAt, tenantTopUpAt, tenantTopUpAt],
                [demoTenantTopUpPayment, WalletDemoTenant, DemoTenant, "demo-full-flow:demo-tenant-topup", DemoTenantBalance, "VND", "WalletTopUp", "Mock", "DEMO-FULL-FLOW-TOPUP-DEMO-TENANT", "DEMO-FULL-FLOW-TOPUP-DEMO-TENANT-TXN", "https://pay.example/DEMO-FULL-FLOW-TOPUP-DEMO-TENANT", null, "00", "Nạp ví cho Demo Thuê Trọ để test luồng thuê phòng mới.", "Succeeded", Utc(2026, 4, 15, 10), tenantTopUpAt, null, tenantTopUpAt, tenantTopUpAt, tenantTopUpAt]);

            InsertRows(migrationBuilder, "wallet_transactions",
                ["id", "wallet_account_id", "user_id", "transfer_group_id", "transaction_type", "direction", "amount", "balance_before", "balance_after", "reserved_balance_before", "reserved_balance_after", "related_entity_type", "related_entity_id", "description", "status", "created_at"],
                [Id("KFC-SCENARIO-WTX-TENANT-TOPUP"), WalletMainTenant, MainTenant, null, "WalletTopUp", "Credit", TenantTopUpAmount, 0m, TenantTopUpAmount, 0m, 0m, "PaymentTransaction", tenantTopUpPayment, "Lê Quang Linh nạp ví để còn 50.000.000 VND sau khi thanh toán cọc và invoice kỳ đầu.", "Succeeded", tenantTopUpAt],
                [Id("KFC-SCENARIO-WTX-LANDLORD-TOPUP"), WalletLandlord, Landlord, null, "WalletTopUp", "Credit", LandlordTopUpAmount, 0m, LandlordTopUpAmount, 0m, 0m, "PaymentTransaction", landlordTopUpPayment, "Nguyễn Xuân Huấn nạp ví nền trước khi nhận cọc và invoice kỳ đầu.", "Succeeded", tenantTopUpAt],
                [Id("KFC-SCENARIO-WTX-TENANT-DEPOSIT"), WalletMainTenant, MainTenant, DepositTransferGroup, "DepositPayment", "Debit", DepositAmount, TenantTopUpAmount, TenantTopUpAmount - DepositAmount, 0m, 0m, "RoomDeposit", Deposit101, "Lê Quang Linh thanh toán cọc phòng KFC-101.", "Succeeded", depositPaidAt],
                [Id("KFC-SCENARIO-WTX-LANDLORD-DEPOSIT"), WalletLandlord, Landlord, DepositTransferGroup, "DepositReceive", "Credit", DepositAmount, LandlordTopUpAmount, LandlordTopUpAmount + DepositAmount, 0m, DepositAmount, "RoomDeposit", Deposit101, "Nguyễn Xuân Huấn nhận cọc phòng KFC-101, số tiền được nền tảng tạm giữ.", "Succeeded", depositPaidAt],
                [Id("KFC-SCENARIO-WTX-TENANT-INVOICE-APRIL"), WalletMainTenant, MainTenant, InvoiceAprilTransferGroup, "InvoicePayment", "Debit", InvoiceAprilAmount, TenantTopUpAmount - DepositAmount, TenantFinalBalance, 0m, 0m, "Invoice", InvoiceApril, "Lê Quang Linh thanh toán invoice kỳ đầu phòng KFC-101.", "Succeeded", invoicePaidAt],
                [Id("KFC-SCENARIO-WTX-LANDLORD-INVOICE-APRIL"), WalletLandlord, Landlord, InvoiceAprilTransferGroup, "InvoiceReceive", "Credit", InvoiceAprilAmount, LandlordTopUpAmount + DepositAmount, LandlordFinalBalance, DepositAmount, DepositAmount, "Invoice", InvoiceApril, "Nguyễn Xuân Huấn nhận tiền invoice kỳ đầu phòng KFC-101.", "Succeeded", invoicePaidAt],
                [Id("DEMO-FULL-FLOW-WTX-DEMO-TENANT-TOPUP"), WalletDemoTenant, DemoTenant, null, "WalletTopUp", "Credit", DemoTenantBalance, 0m, DemoTenantBalance, 0m, 0m, "PaymentTransaction", demoTenantTopUpPayment, "Demo Thuê Trọ nạp ví 50.000.000 VND để test thuê phòng mới.", "Succeeded", tenantTopUpAt]);
        }

        private static Guid Id(string input)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }

        private static void InsertRows(MigrationBuilder migrationBuilder, string table, string[] columns, params object?[][] rows)
        {
            if (rows.Length == 0)
            {
                return;
            }

            var columnList = string.Join(", ", columns);
            var values = string.Join(",\n", rows.Select(row => $"({string.Join(", ", row.Select(Value))})"));
            migrationBuilder.Sql($"""
                INSERT INTO {table} ({columnList})
                VALUES
                {values}
                ON CONFLICT DO NOTHING;
                """);
        }

        private static void DeleteByIds(MigrationBuilder migrationBuilder, string table, string idColumn, params Guid[] ids)
        {
            if (ids.Length == 0)
            {
                return;
            }

            migrationBuilder.Sql($"DELETE FROM {table} WHERE {idColumn} IN ({string.Join(", ", ids.Select(id => Value(id)))});");
        }

        private static SqlFragment Json(string json) => new($"{Quote(json)}::jsonb");

        private readonly record struct SqlFragment(string Text);

        private static string Value(object? value)
        {
            return value switch
            {
                null => "NULL",
                SqlFragment fragment => fragment.Text,
                string text => Quote(text),
                Guid guid => Quote(guid.ToString()),
                bool flag => flag ? "TRUE" : "FALSE",
                DateOnly date => $"DATE {Quote(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
                DateTimeOffset dateTime => $"TIMESTAMPTZ {Quote(dateTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z")}",
                decimal number => number.ToString(CultureInfo.InvariantCulture),
                int number => number.ToString(CultureInfo.InvariantCulture),
                long number => number.ToString(CultureInfo.InvariantCulture),
                _ => Quote(value.ToString() ?? string.Empty)
            };
        }

        private static string Quote(string text)
        {
            return $"'{text.Replace("'", "''")}'";
        }

        private static string PasswordHash()
        {
            return new PasswordHasher<object>().HashPassword(new object(), DefaultPassword);
        }

        private static string HashToken(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static string MaskCitizenId(string citizenId)
        {
            return citizenId.Length <= 8
                ? new string('*', citizenId.Length)
                : citizenId[..3] + new string('*', citizenId.Length - 6) + citizenId[^3..];
        }
    }
}

