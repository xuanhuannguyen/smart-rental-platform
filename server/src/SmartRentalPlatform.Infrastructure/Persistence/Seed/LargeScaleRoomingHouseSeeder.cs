using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed
{
    public static class LargeScaleRoomingHouseSeeder
    {
        private static readonly string[] UnsplashPhotoIds = new[]
        {
            "photo-1522708323590-d24dbb6b0267", "photo-1502672260266-1c1ef2d93688", "photo-1493809842364-78817add7ffb", 
            "photo-1560448204-e02f11c3d0e2", "photo-1586023492125-27b2c045efd7", "photo-1598928506311-c55ded91a20c", 
            "photo-1554995207-c18c203602cb", "photo-1536376072261-38c75010e6c9", "photo-1582719478250-c89cae4dc85b", 
            "photo-1505691938895-1758d7feb511", "photo-1540518614846-7eded433c457", "photo-1512917774080-9991f1c4c750", 
            "photo-1513694203232-719a280e022f", "photo-1484154218962-a197022b5858", "photo-1524758631624-e2822e304c36", 
            "photo-1507089947368-19c1da9775ae", "photo-1616486338812-3dadae4b4ace", "photo-1617806118233-18e1db207f62", 
            "photo-1615876234886-fd9a39fda97f", "photo-1616046229478-9901c5536a45", "photo-1618221195710-dd6b41faaea6", 
            "photo-1618219908412-a29a1bb7b86e", "photo-1616486029423-aaa4789e8c9a", "photo-1615529182904-14819c35db37", 
            "photo-1615529162924-f8605388461d", "photo-1618221381711-42ca8ab6e908", "photo-1600585154340-be6161a56a0c", 
            "photo-1600607687939-ce8a6c25118c", "photo-1600210492486-724fe5c67fb0", "photo-1600210491892-03d54c0aaf87",
            "photo-1600566753190-17f0baa2a6c3", "photo-1600566753376-12c8ab7fb75b", "photo-1583608205776-bfd35f0d9f83",
            "photo-1512918728675-ed5a9ecdebfd", "photo-1584622650111-993a426fbf0a", "photo-1600585154526-990dced4db0d",
            "photo-1501183007986-d0d080b147f9", "photo-1600607687644-c7171b42498f", "photo-1556911220-e15b29be8c8f",
            "photo-1600210491369-e753c80af4d8"
        };

        private static readonly string[] Prefixes = new[]
        {
            "Nhà trọ"
        };

        private static readonly string[] Names = new[]
        {
            "Hương Sen", "Hướng Dương", "Bình Minh", "Cát Tường", "Thịnh Vượng", "An Nhiên", "Hòa Bình", "Đông Á", "Tây Hồ", "Sông Hàn", "Nam Long", "Khánh An", "Phương Nam", "Đại Việt", "Trường Sơn", "Bạch Đằng", "Hồng Hà", "Cửu Long", "Mai Hoa", "Trúc Xanh", "Vinh Quang", "Tâm An", "Thanh Xuân", "Gia Đình", "Đất Việt"
        };

        private static readonly string[] Streets = new[]
        {
            "Nguyễn Trãi", "Lê Lợi", "Trần Hưng Đạo", "Nguyễn Huệ", "Hai Bà Trưng", "Phan Chu Trinh", "Bùi Thị Xuân", "Nguyễn Thị Minh Khai", "Lê Hồng Phong", "Điện Biên Phủ", "Trần Phú", "Kim Mã", "Cầu Giấy", "Nguyễn Văn Cừ", "Phạm Văn Đồng", "Cách Mạng Tháng Tám", "Nam Kỳ Khởi Nghĩa"
        };

        public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
        {
            var defaultHouseGuids = new[]
            {
                "20000000-0000-0000-0000-000000000001", // ApprovedHouseId (Nhà trọ Hoa Sen)
                "20000000-0000-0000-0000-000000000002", // DraftHouseId (Nhà trọ Minh Anh)
                "20000000-0000-0000-0000-000000000003", // SunriseHouseId (Nhà trọ Sunrise)
                "20000000-0000-0000-0000-000000000004", // GreenViewHouseId (Nhà trọ Green View)
                "20000000-0000-0000-0000-000000000005", // PendingHouseId (Nhà trọ Garden Pending)
                "20000000-0000-0000-0000-000000000006"  // RejectedHouseId (Nhà trọ Old Town)
            };
            var defaultHouseIdsCsv = string.Join(",", defaultHouseGuids.Select(id => $"'{id}'"));
            var dummyLandlordId = Guid.Parse("10000000-0000-0000-0000-000000009999");
            var seededRoomingHouseSubquery =
                $"SELECT id FROM rooming_houses WHERE landlord_user_id = '{dummyLandlordId}' AND id NOT IN ({defaultHouseIdsCsv})";

            // Refresh only generated mock houses. Do not delete user-created rooming houses on development startup.
            // 1. contract_appendix_changes
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_appendix_changes 
                WHERE appendix_id IN (
                    SELECT id FROM contract_appendices 
                    WHERE contract_id IN (
                        SELECT id FROM contracts 
                        WHERE room_id IN (
                            SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                        )
                    )
                )
            ", cancellationToken);

            // 2. contract_appendices
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_appendices 
                WHERE contract_id IN (
                    SELECT id FROM contracts 
                    WHERE room_id IN (
                        SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                    )
                )
            ", cancellationToken);

            // 3. contract_signatures
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_signatures 
                WHERE contract_id IN (
                    SELECT id FROM contracts 
                    WHERE room_id IN (
                        SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                    )
                )
            ", cancellationToken);

            // 4. contract_files
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_files 
                WHERE contract_id IN (
                    SELECT id FROM contracts 
                    WHERE room_id IN (
                        SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                    )
                )
            ", cancellationToken);

            // 5. contract_occupant_documents
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_occupant_documents 
                WHERE contract_occupant_id IN (
                    SELECT id FROM contract_occupants 
                    WHERE contract_id IN (
                        SELECT id FROM contracts 
                        WHERE room_id IN (
                            SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                        )
                    )
                )
            ", cancellationToken);

            // 6. contract_occupants
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contract_occupants 
                WHERE contract_id IN (
                    SELECT id FROM contracts 
                    WHERE room_id IN (
                        SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                    )
                )
            ", cancellationToken);

            // 7. invoice_items
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM invoice_items 
                WHERE invoice_id IN (
                    SELECT id FROM invoices 
                    WHERE room_id IN (
                        SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                    )
                )
            ", cancellationToken);

            // 8. invoices
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM invoices 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // 9. contracts
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM contracts 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // 10. room_deposits
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM room_deposits 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // 11. rental_requests
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rental_requests 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // 12. viewing_appointments
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM viewing_appointments 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // 13. meter_readings
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM meter_readings 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // Xóa property_images liên quan đến phòng
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM property_images 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // Xóa property_images liên quan đến khu trọ
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM property_images 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa room_amenities
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM room_amenities 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // Xóa room_price_tiers
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM room_price_tiers 
                WHERE room_id IN (
                    SELECT id FROM rooms WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
                )
            ", cancellationToken);

            // Xóa rooms
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooms 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rooming_house_amenities
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooming_house_amenities 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rooming_house_legal_documents
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooming_house_legal_documents 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rental_policies
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rental_policies 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rooming_house_rules
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooming_house_rules 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rooming_house_service_prices
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooming_house_service_prices 
                WHERE rooming_house_id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Xóa rooming_houses
            await context.Database.ExecuteSqlRawAsync($@"
                DELETE FROM rooming_houses 
                WHERE id IN ({seededRoomingHouseSubquery})
            ", cancellationToken);

            // Kiểm tra số lượng khu trọ hiện tại
            var currentCount = await context.RoomingHouses.CountAsync(cancellationToken);
            if (currentCount >= 500)
            {
                return;
            }

            int targetCount = 500 - currentCount;

            // Lấy danh sách Tỉnh/Thành
            var provinces = await context.AdministrativeProvinces
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);
            var provinceCodes = provinces.Select(p => p.Code).ToList();

            // Lấy danh sách Phường/Xã
            var wards = await context.AdministrativeWards
                .Where(x => x.IsActive && provinceCodes.Contains(x.ProvinceCode))
                .ToListAsync(cancellationToken);

            if (provinces.Count == 0 || wards.Count == 0)
            {
                return; // Không có dữ liệu hành chính để seed
            }

            // Seeding a dedicated dummy landlord for mock rooming houses to avoid cluttering the demo landlord's dashboard
            var dummyUserExists = await context.Users.AnyAsync(u => u.Id == dummyLandlordId, cancellationToken);
            var landlordRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == RoleName.Landlord, cancellationToken);
            var seededAt = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero);

            if (!dummyUserExists)
            {
                context.Users.Add(new User
                {
                    Id = dummyLandlordId,
                    Email = "landlord.mock@example.com",
                    NormalizedEmail = "LANDLORD.MOCK@EXAMPLE.COM",
                    PasswordHash = "AQAAAAIAAYagAAAAEI0ztrbM7Wp7e4Uf+2Hw1aG6X/2GZx84d/wKzV2Tz6zYmC81SjB7Xg==", // Demo password hash
                    DisplayName = "Chủ trọ Mock",
                    Status = UserStatus.Active,
                    OnboardingStatus = OnboardingStatus.Completed,
                    EmailConfirmed = true,
                    PhoneConfirmed = false,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                });

                context.UserProfiles.Add(new UserProfile
                {
                    UserId = dummyLandlordId,
                    FullName = "Chủ trọ Mock",
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                });

                if (landlordRole != null)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = dummyLandlordId,
                        RoleId = landlordRole.Id,
                        CreatedAt = seededAt
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
            }

            // Lấy tất cả tiện ích
            var allAmenities = await context.Amenities.Where(x => x.IsActive).ToListAsync(cancellationToken);
            var houseAmenities = allAmenities.Where(x => x.Scope == AmenityScope.House || x.Scope == AmenityScope.Both).ToList();
            var roomAmenities = allAmenities.Where(x => x.Scope == AmenityScope.Room || x.Scope == AmenityScope.Both).ToList();

            var random = new Random(42); // Seed để random nhất quán

            // Tách danh sách phường xã thành Đà Nẵng và các tỉnh khác
            var danangWards = wards.Where(w => w.ProvinceCode == "48").ToList();
            var otherWards = wards.Where(w => w.ProvinceCode != "48").ToList();

            if (danangWards.Count == 0) danangWards = wards;
            if (otherWards.Count == 0) otherWards = wards;

            // Bảng tra cứu tọa độ địa lý thực tế của các tỉnh thành
            var provinceCoordinates = new Dictionary<string, (decimal Lat, decimal Lng)>
            {
                { "01", (21.0278m, 105.8342m) }, // Hà Nội
                { "04", (22.6710m, 106.2625m) }, // Cao Bằng
                { "08", (21.8159m, 105.2152m) }, // Tuyên Quang
                { "11", (21.3853m, 103.0195m) }, // Điện Biên
                { "12", (22.4045m, 103.4566m) }, // Lai Châu
                { "14", (21.3283m, 103.9103m) }, // Sơn La
                { "15", (22.4856m, 103.9607m) }, // Lào Cai
                { "19", (21.5939m, 105.8481m) }, // Thái Nguyên
                { "20", (21.8540m, 106.7610m) }, // Lạng Sơn
                { "22", (20.9599m, 107.0425m) }, // Quảng Ninh
                { "24", (21.1861m, 106.0763m) }, // Bắc Ninh
                { "25", (21.3228m, 105.2173m) }, // Phú Thọ
                { "31", (20.8449m, 106.6881m) }, // Hải Phòng
                { "33", (20.6465m, 106.0511m) }, // Hưng Yên
                { "37", (20.2522m, 105.9750m) }, // Ninh Bình
                { "38", (19.8076m, 105.7764m) }, // Thanh Hóa
                { "40", (18.6734m, 105.6924m) }, // Nghệ An
                { "42", (18.3559m, 105.9013m) }, // Hà Tĩnh
                { "44", (16.8163m, 107.0985m) }, // Quảng Trị
                { "46", (16.4637m, 107.5909m) }, // Huế
                { "48", (15.9754m, 108.2638m) }, // Đà Nẵng (Đại học FPT Đà Nẵng)
                { "51", (15.1205m, 108.8010m) }, // Quảng Ngãi
                { "52", (13.9829m, 108.0076m) }, // Gia Lai
                { "56", (12.2388m, 109.1967m) }, // Khánh Hòa
                { "66", (12.6796m, 108.0447m) }, // Đắk Lắk
                { "68", (11.9404m, 108.4583m) }, // Lâm Đồng
                { "75", (10.9574m, 106.8427m) }, // Đồng Nai
                { "79", (10.7769m, 106.7009m) }, // TP.HCM
                { "80", (11.3124m, 106.1245m) }, // Tây Ninh
                { "82", (10.4578m, 105.6372m) }, // Đồng Tháp
                { "86", (10.2536m, 105.9722m) }, // Vĩnh Long
                { "91", (10.3739m, 105.4363m) }, // An Giang
                { "92", (10.0452m, 105.7469m) }, // Cần Thơ
                { "96", (9.1769m, 105.1500m) }  // Cà Mau
            };

            for (int i = 0; i < targetCount; i++)
            {
                // Phân bổ: 100 nhà trọ mock đầu tiên ở Đà Nẵng, còn lại ở các tỉnh thành khác
                AdministrativeWard ward;
                if (i < 100)
                {
                    ward = danangWards[random.Next(danangWards.Count)];
                }
                else
                {
                    ward = otherWards[random.Next(otherWards.Count)];
                }

                var province = provinces.First(p => p.Code == ward.ProvinceCode);

                // 2. Random tên khu trọ tiếng Việt tự nhiên
                var prefix = Prefixes[random.Next(Prefixes.Length)];
                var name = Names[random.Next(Names.Length)];
                var codeNo = random.Next(10, 999);
                var houseName = $"{prefix} {name} #{codeNo}";

                // 3. Random địa chỉ
                var street = Streets[random.Next(Streets.Length)];
                var streetNo = random.Next(1, 450);
                var addressLine = $"{streetNo} {street}";
                var addressDisplay = $"{addressLine}, {ward.Name}, {province.Name}";

                var landlordId = dummyLandlordId;
                var houseId = Guid.NewGuid();

                // Lấy tọa độ gốc khớp thực tế của tỉnh thành đó
                decimal baseLat;
                decimal baseLng;
                if (provinceCoordinates.TryGetValue(province.Code, out var coords))
                {
                    baseLat = coords.Lat;
                    baseLng = coords.Lng;
                }
                else
                {
                    // Fallback mặc định về Đà Nẵng nếu không tìm thấy
                    baseLat = 15.9754m;
                    baseLng = 108.2638m;
                }

                var latOffset = Convert.ToDecimal((random.NextDouble() - 0.5) * 0.02);
                var lngOffset = Convert.ToDecimal((random.NextDouble() - 0.5) * 0.02);
                var latitude = baseLat + latOffset;
                var longitude = baseLng + lngOffset;

                // 4. Tạo thực thể RoomingHouse
                var house = new RoomingHouse
                {
                    Id = houseId,
                    LandlordUserId = landlordId,
                    Name = houseName,
                    Description = $"Khu trọ sạch sẽ thoáng mát tại khu vực {ward.Name}. Giao thông thuận lợi, an ninh tốt, gần chợ và các trường học. Đầy đủ tiện ích tiện nghi.",
                    AddressLine = addressLine,
                    WardCode = ward.Code,
                    ProvinceCode = province.Code,
                    AddressDisplay = addressDisplay,
                    Latitude = latitude,
                    Longitude = longitude,
                    ApprovalStatus = RoomingHouseApprovalStatus.Approved,
                    VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                };
                context.RoomingHouses.Add(house);

                // 5. Thêm PropertyImage (3-5 ảnh)
                int imgCount = random.Next(3, 6);
                var chosenPhotoIds = UnsplashPhotoIds.OrderBy(x => random.Next()).Take(imgCount).ToList();
                for (int j = 0; j < chosenPhotoIds.Count; j++)
                {
                    var imgUrl = $"https://images.unsplash.com/{chosenPhotoIds[j]}?auto=format&fit=crop&w=800&q=80&sig={random.Next(1, 10000)}";
                    context.PropertyImages.Add(new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseId = houseId,
                        ImageUrl = imgUrl,
                        ObjectKey = $"seed/houses/{houseId}/image_{j}.jpg",
                        Caption = $"Ảnh tổng quan {j + 1} của {houseName}",
                        IsCover = j == 0,
                        SortOrder = j,
                        CreatedAt = seededAt
                    });
                }

                // 6. Thêm RoomingHouseLegalDocument
                context.RoomingHouseLegalDocuments.Add(new RoomingHouseLegalDocument
                {
                    RoomingHouseId = houseId,
                    DocumentType = LegalDocumentType.LAND_USE_CERTIFICATE,
                    FrontImageObjectKey = $"seed/legal/{houseId}/front.jpg",
                    BackImageObjectKey = $"seed/legal/{houseId}/back.jpg",
                    DocumentNumberMasked = $"*****{random.Next(1000, 9999)}",
                    DocumentNumberHash = $"seed-legal-hash-{houseId}",
                    UploadedAt = seededAt,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                });

                // 7. Thêm Tiện ích khu trọ (5-8 tiện ích)
                int houseAmenityCount = random.Next(5, Math.Min(9, houseAmenities.Count + 1));
                var chosenHouseAmenities = houseAmenities.OrderBy(x => random.Next()).Take(houseAmenityCount).ToList();
                foreach (var amenity in chosenHouseAmenities)
                {
                    context.RoomingHouseAmenities.Add(new RoomingHouseAmenity
                    {
                        RoomingHouseId = houseId,
                        AmenityId = amenity.Id
                    });
                }

                // 8. Thêm Phòng trọ (5-10 phòng)
                int roomCount = random.Next(5, 11);
                for (int r = 1; r <= roomCount; r++)
                {
                    var roomId = Guid.NewGuid();
                    var roomNumber = $"{random.Next(1, 6)}{r:D2}";
                    var area = random.Next(18, 46);
                    var maxOccupants = area > 30 ? random.Next(3, 5) : random.Next(1, 3);
                    var price = random.Next(15, 121) * 100_000;

                    var room = new Room
                    {
                        Id = roomId,
                        RoomingHouseId = houseId,
                        RoomNumber = roomNumber,
                        Floor = int.Parse(roomNumber[0].ToString()),
                        AreaM2 = area,
                        MaxOccupants = maxOccupants,
                        IsTieredPricing = false,
                        Status = RoomStatus.Available,
                        Description = $"Phòng {roomNumber} rộng rãi sạch sẽ, thiết kế hiện đại, nhiều ánh sáng tự nhiên. Thích hợp cho thuê lâu dài.",
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    };
                    context.Rooms.Add(room);

                    // 9. Thêm PriceTier cho phòng
                    context.RoomPriceTiers.Add(new RoomPriceTier
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        MonthlyRent = price,
                        IsActive = true,
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    });

                    // 10. Thêm 3-5 ảnh cho mỗi phòng trọ
                    int roomImgCount = random.Next(3, 6);
                    var chosenRoomPhotoIds = UnsplashPhotoIds.OrderBy(x => random.Next()).Take(roomImgCount).ToList();
                    for (int k = 0; k < chosenRoomPhotoIds.Count; k++)
                    {
                        var roomImgUrl = $"https://images.unsplash.com/{chosenRoomPhotoIds[k]}?auto=format&fit=crop&w=800&q=80&sig={random.Next(1, 10000)}";
                        context.PropertyImages.Add(new PropertyImage
                        {
                            Id = Guid.NewGuid(),
                            RoomId = roomId,
                            ImageUrl = roomImgUrl,
                            ObjectKey = $"seed/rooms/{roomId}/image_{k}.jpg",
                            Caption = $"Ảnh phòng {roomNumber} - góc chụp {k + 1}",
                            IsCover = k == 0,
                            SortOrder = k,
                            CreatedAt = seededAt
                        });
                    }

                    // 11. Thêm tiện ích phòng trọ (3-5 tiện ích)
                    int roomAmenityCount = random.Next(3, Math.Min(6, roomAmenities.Count + 1));
                    var chosenRoomAmenities = roomAmenities.OrderBy(x => random.Next()).Take(roomAmenityCount).ToList();
                    foreach (var amenity in chosenRoomAmenities)
                    {
                        context.RoomAmenities.Add(new RoomAmenity
                        {
                            RoomId = roomId,
                            AmenityId = amenity.Id
                        });
                    }
                }

                // SaveChanges theo batch 50 bản ghi
                if (i % 50 == 0 && i > 0)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
