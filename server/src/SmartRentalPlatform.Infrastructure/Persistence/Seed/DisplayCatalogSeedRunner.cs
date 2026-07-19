using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed
{
    public class DisplayCatalogSeedRunner
    {
        private readonly AppDbContext _context;
        private readonly IMediaStorageService _mediaStorageService;
        private readonly IMediaObjectKeyFactory _mediaObjectKeyFactory;
        private readonly IPasswordService _passwordService;

        private static readonly Guid LandlordUserId = Guid.Parse("10000000-0000-0000-0000-000000008888");
        private static readonly Guid[] TenantUserIds = new[]
        {
            Guid.Parse("10000000-0000-0000-0000-000000008001"),
            Guid.Parse("10000000-0000-0000-0000-000000008002"),
            Guid.Parse("10000000-0000-0000-0000-000000008003"),
            Guid.Parse("10000000-0000-0000-0000-000000008004"),
            Guid.Parse("10000000-0000-0000-0000-000000008005")
        };

        private static readonly string[] Prefixes = new[]
        {
            "Nhà trọ", "Khu trọ", "Nhà nguyên căn", "Căn hộ dịch vụ", "Căn hộ mini", "Chung cư mini", "Phòng trọ cao cấp", "Khu phòng trọ"
        };

        private static readonly string[] CoreNames = new[]
        {
            "An Nhiên", "Trúc Xanh", "Bình Minh", "Hoa Sữa", "Thanh Xuân", "Hương Sen", "Cát Tường", "Thịnh Vượng", "Hòa Bình", "Nam Long", "Khánh An", "Phương Nam", "Đại Việt", "Trường Sơn", "Bạch Đằng", "Hồng Hà", "Cửu Long", "Mai Hoa", "Tâm An", "Gia Đình", "Đất Việt", "Hướng Dương", "Khánh Hội", "Song Hành", "Phú Mỹ", "Hưng Thịnh", "Vinh Quang", "Vạn Xuân", "Kim Liên", "Bình An", "Hạnh Phúc", "An Khang", "Gia Hòa", "Vĩnh Tiến", "Thành Công", "Đại Nam", "Âu Cơ", "Lạc Long Quân", "Phú Gia", "Thiên Phú"
        };

        private static readonly string[] Streets = new[]
        {
            "Nguyễn Trãi", "Lê Lợi", "Trần Hưng Đạo", "Nguyễn Huệ", "Hai Bà Trưng", "Phan Chu Trinh", "Bùi Thị Xuân", "Nguyễn Thị Minh Khai", "Lê Hồng Phong", "Điện Biên Phủ", "Trần Phú", "Kim Mã", "Cầu Giấy", "Nguyễn Văn Cừ", "Phạm Văn Đồng", "Cách Mạng Tháng Tám", "Nam Kỳ Khởi Nghĩa", "Nguyễn Thị Thập", "Tôn Thất Thuyết", "Trần Não", "Lê Văn Việt", "Nguyễn Hữu Thọ", "Kha Vạn Cân", "Võ Văn Ngân", "Hoàng Hoa Thám", "Xuân Thủy", "Trần Duy Hưng", "Chùa Láng", "Giải Phóng", "Đê La Thành"
        };

        private static readonly string[] Rating5Comments = new[]
        {
            "Phòng trọ cực kỳ an ninh, cổng vân tay rất an tâm. Ban đêm yên tĩnh không ồn ào.",
            "Khu vực an ninh tốt, camera giám sát 24/7 nên rất yên tâm khi đi làm về muộn.",
            "Phòng trọ rất sạch sẽ, thoáng mát, đầy đủ ánh sáng tự nhiên. Wifi căng đét, lướt web vèo vèo.",
            "Phòng mới, thiết bị vệ sinh cao cấp và có gác lửng rộng rãi. Chỗ phơi đồ thoáng mát, nhiều nắng.",
            "Chủ nhà thân thiện, xử lý các vấn đề hỏng hóc rất nhanh. Mọi người ở đây hòa đồng.",
            "Landlord siêu nhiệt tình, hôm trước vòi nước hỏng báo cái là qua sửa liền trong chiều.",
            "Giá cả hợp lý so với chất lượng phòng và khu vực. Gần chợ và trạm xe buýt tiện đi lại.",
            "Phòng đẹp giá rẻ, chi phí điện nước tính rất rõ ràng và minh bạch. Rất đáng tiền.",
            "Hệ thống khóa cửa vân tay thông minh rất tiện, an ninh tốt. Phòng sạch sẽ ngăn nắp.",
            "Căn hộ mini thiết kế hợp lý, có ban công thoáng mát và chỗ phơi đồ riêng biệt."
        };

        private static readonly string[] Rating4Comments = new[]
        {
            "Khu trọ sạch sẽ, an ninh tốt. Chỗ để xe hơi chật vào buổi tối nhưng phòng trọ rất sạch.",
            "Phòng đẹp đầy đủ tiện nghi, chủ trọ dễ tính. Mỗi tội khu nhà xe hơi hẹp tí giờ cao điểm.",
            "Nước nôi ổn định, điện nước giá hợp lý. Có máy giặt chung tiện lợi nhưng wifi thỉnh thoảng hơi chậm.",
            "Không gian yên tĩnh, an ninh tốt. Internet ổn định, phòng sạch đẹp khi bàn giao.",
            "Phòng đầy đủ tiện nghi, giờ giấc tự do thoải mái. Hơi xa trung tâm một chút nhưng chấp nhận được.",
            "Chất lượng dịch vụ khá ổn, phòng ốc vệ sinh sạch sẽ, chủ nhà hỗ trợ nhiệt tình.",
            "Khu trọ gần trường học nên rất tiện đi lại. Phòng khá rộng rãi, điện nước ổn định.",
            "Chủ nhà phản hồi nhanh, phòng mới sơn lại sạch sẽ. Hơi ồn nhẹ vào ban ngày."
        };

        private static readonly string[] Rating3Comments = new[]
        {
            "Phòng tạm ổn, tuy nhiên mùa hè hơi nóng. Chủ nhà nên lắp thêm điều hòa công suất lớn hơn.",
            "An ninh khá tốt nhưng nhà xe đôi lúc lộn xộn. Hy vọng chủ trọ sớm cải thiện.",
            "Phòng hơi nhỏ so với ảnh chụp nhưng sạch sẽ. Hệ thống thoát nước nhà vệ sinh thỉnh thoảng hơi chậm.",
            "Hơi ồn ào vào giờ cao điểm do gần đường lớn. Giá cả ở mức chấp nhận được, tạm hài lòng.",
            "Wifi buổi tối khá yếu, khó học tập. Phòng ốc ở mức trung bình, đủ đáp ứng nhu cầu cơ bản.",
            "Chỗ để xe chật chội và không có người dọn dẹp thường xuyên. Cần cải thiện vệ sinh hành lang."
        };

        private static readonly string[] LandlordReplies = new[]
        {
            "Cảm ơn em đã tin tưởng và lựa chọn căn hộ của anh/chị. Chúc em học tập và làm việc tốt!",
            "Cảm ơn bạn đã phản hồi tốt! Chúc bạn có trải nghiệm lưu trú tuyệt vời tại đây.",
            "Cảm ơn phản hồi của em. Vấn đề xe cộ/nước/wifi anh/chị sẽ sớm tìm cách khắc phục và nâng cấp.",
            "Cảm ơn em đã góp ý chân thành, anh/chị sẽ cho thợ qua sửa chữa và nâng cấp ngay trong tuần này.",
            "Cảm ơn em rất nhiều. Sự hài lòng của em là động lực cho cả khu trọ phát triển.",
            "Rất vui vì em hài lòng với phòng trọ và sự hỗ trợ của anh/chị. Cần gì cứ nhắn trực tiếp nhé.",
            "Cảm ơn em nhé. Anh/chị sẽ cố gắng cải thiện chất lượng dịch vụ hơn nữa.",
            "Anh/chị rất trân trọng những góp ý này và sẽ cho nhân viên kiểm tra, sắp xếp lại khu vực để xe.",
            "Chào em, anh/chị rất xin lỗi về sự bất tiện này. Anh/chị sẽ sớm nâng cấp và cải thiện trong tuần tới.",
            "Cảm ơn em đã phản hồi. Mong em thông cảm và đồng hành cùng khu trọ để anh/chị hoàn thiện dịch vụ."
        };

        private static readonly string[] OccupantNames = new[]
        {
            "Trần Huy Hoàng", "Lê Minh Thư", "Phạm Quốc Anh", "Nguyễn Thảo Nguyên", "Vũ Hoàng Nam",
            "Đặng Thu Thảo", "Bùi Anh Tuấn", "Đỗ Hải Yến", "Ngô Việt Hùng", "Phan Khánh Chi",
            "Lê Tuấn Kiệt", "Trịnh Mai Phương", "Nguyễn Minh Đức", "Vũ Khánh Linh", "Đoàn Quốc Bảo"
        };

        private static readonly string[] Relationships = new[]
        {
            "Bạn cùng phòng", "Bạn học", "Anh/Em", "Vợ/Chồng"
        };

        private static readonly Dictionary<string, (decimal Lat, decimal Lng)> TargetProvinceCoordinates = new()
        {
            { "01", (21.0362m, 105.7825m) }, // Hà Nội (Cầu Giấy)
            { "79", (10.8752m, 106.8007m) }, // TP.HCM (Thủ Đức)
            { "48", (15.9754m, 108.2638m) }, // Đà Nẵng (Ngũ Hành Sơn)
            { "92", (10.0299m, 105.7684m) }, // Cần Thơ
            { "46", (16.4627m, 107.5905m) }, // Huế
            { "31", (20.8384m, 106.6908m) }, // Hải Phòng
            { "19", (21.5852m, 105.8065m) }, // Thái Nguyên
            { "40", (18.6631m, 105.6946m) }, // Nghệ An
            { "38", (19.7891m, 105.7952m) }, // Thanh Hóa
            { "24", (21.1712m, 106.0594m) }, // Bắc Ninh
            { "51", (15.1147m, 108.8045m) }, // Quảng Ngãi
            { "52", (13.9774m, 108.0121m) }, // Gia Lai
            { "56", (12.2682m, 109.2023m) }, // Khánh Hòa
            { "68", (11.9542m, 108.4447m) }, // Lâm Đồng
            { "66", (12.6639m, 108.0772m) }, // Đắk Lắk
            { "75", (10.9622m, 106.8123m) }, // Đồng Nai
            { "86", (10.2458m, 105.9612m) }, // Vĩnh Long
            { "91", (10.3712m, 105.4325m) }  // An Giang
        };

        public DisplayCatalogSeedRunner(
            AppDbContext context,
            IMediaStorageService mediaStorageService,
            IMediaObjectKeyFactory mediaObjectKeyFactory,
            IPasswordService passwordService)
        {
            _context = context;
            _mediaStorageService = mediaStorageService;
            _mediaObjectKeyFactory = mediaObjectKeyFactory;
            _passwordService = passwordService;
        }

        public async Task RunSeedAsync(
            int targetHouseCount,
            int targetAssetCount,
            bool uploadMedia,
            string version,
            string? mediaSourceDirectory = null,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Starting Display Catalog Seeding (Count: {targetHouseCount}, Assets: {targetAssetCount}, S3 Upload: {uploadMedia}, Version: {version}, MediaSource: {mediaSourceDirectory ?? "generated-svg"})");

            var manifestPath = ResolveManifestPath(version);
            DisplaySeedManifest manifest;

            if (File.Exists(manifestPath))
            {
                Console.WriteLine($"Loading existing manifest from {manifestPath}");
                var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                manifest = JsonSerializer.Deserialize<DisplaySeedManifest>(json)
                           ?? throw new InvalidOperationException("Failed to deserialize manifest.");
            }
            else
            {
                Console.WriteLine($"Creating new manifest and generating IDs...");
                manifest = await GenerateManifestStructureAsync(targetHouseCount, targetAssetCount, version, mediaSourceDirectory, cancellationToken);
                await SaveManifestAsync(manifest, manifestPath, cancellationToken);
            }

            // 1. Ensure Landlord and Tenants exist
            await EnsureSeedUsersAsync(cancellationToken);

            // 2. Fetch dependencies
            var allAmenities = await _context.Amenities.Where(x => x.IsActive).ToListAsync(cancellationToken);
            var houseAmenitiesPool = allAmenities.Where(x => x.Scope == AmenityScope.House || x.Scope == AmenityScope.Both).ToList();
            var roomAmenitiesPool = allAmenities.Where(x => x.Scope == AmenityScope.Room || x.Scope == AmenityScope.Both).ToList();
            var serviceTypes = await _context.BillingServiceTypes.ToListAsync(cancellationToken);

            if (houseAmenitiesPool.Count == 0 || roomAmenitiesPool.Count == 0)
            {
                Console.WriteLine("Warning: Amenities have not been seeded in the DB. Make sure you run migration seeds first.");
            }

            // 3. Upload Media Assets (if requested)
            var assets = manifest.Assets;
            int uploadedCount = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset.Uploaded)
                {
                    continue;
                }

                if (uploadMedia)
                {
                    Console.Write($"Uploading asset {i + 1}/{assets.Count} ({asset.Pool} - {asset.FileName})... ");
                    
                    try
                    {
                        var content = await LoadAssetContentAsync(asset, i, cancellationToken);
                        var storedResult = await UploadAssetAsync(
                            _mediaStorageService,
                            asset.ObjectKey,
                            asset.FileName,
                            content.Bytes,
                            content.ContentType,
                            content.FileSize,
                            cancellationToken);
                        asset.Uploaded = true;
                        asset.S3Url = PublicMediaPathBuilder.Build(Guid.Parse(asset.Id));
                        asset.BucketName = storedResult.BucketName;
                        asset.ContentType = content.ContentType;
                        asset.FileSize = content.FileSize;
                        uploadedCount++;
                        Console.WriteLine("Done.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed: {ex.Message}");
                        throw;
                    }

                    if (uploadedCount % 20 == 0)
                    {
                        await SaveManifestAsync(manifest, manifestPath, cancellationToken);
                    }
                }
            }
            await SaveManifestAsync(manifest, manifestPath, cancellationToken);

            // 4. Save Media Assets in database
            var seededAt = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero);
            foreach (var asset in manifest.Assets)
            {
                var assetGuid = Guid.Parse(asset.Id);
                var exists = await _context.MediaAssets.AnyAsync(m => m.Id == assetGuid, cancellationToken);
                if (!exists)
                {
                    var scope = asset.Pool switch
                    {
                        "Exterior" => MediaScope.RoomingHouseImage,
                        "Common" => MediaScope.RoomingHouseImage,
                        "Room" => MediaScope.RoomImage,
                        _ => MediaScope.RoomingHouseImage // Review images
                    };

                    _context.MediaAssets.Add(new MediaAsset
                    {
                        Id = assetGuid,
                        OwnerUserId = asset.Pool == "Review" ? TenantUserIds[0] : LandlordUserId,
                        BucketName = string.IsNullOrWhiteSpace(asset.BucketName) ? _mediaStorageService.GetBucketName() : asset.BucketName,
                        ObjectKey = asset.ObjectKey,
                        OriginalFileName = asset.FileName,
                        StoredFileName = Path.GetFileName(asset.ObjectKey),
                        ContentType = string.IsNullOrWhiteSpace(asset.ContentType) ? ResolveContentType(asset.FileName) : asset.ContentType,
                        FileSize = asset.FileSize > 0 ? asset.FileSize : 4096,
                        Scope = scope,
                        Visibility = MediaVisibility.Public,
                        Status = MediaStatus.Linked,
                        LinkedEntityType = nameof(PropertyImage),
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    });
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            // 5. Seed Houses in Batches of 20
            var houses = manifest.Houses;
            var random = new Random(42);

            for (int i = 0; i < houses.Count; i++)
            {
                var seedHouse = houses[i];
                var houseGuid = Guid.Parse(seedHouse.Id);

                var houseExists = await _context.RoomingHouses.AnyAsync(h => h.Id == houseGuid, cancellationToken);
                if (houseExists)
                {
                    continue;
                }

                // Create RoomingHouse
                var house = new RoomingHouse
                {
                    Id = houseGuid,
                    LandlordUserId = LandlordUserId,
                    Name = seedHouse.Name,
                    Description = $"Cơ sở lưu trú chất lượng cao, an ninh đảm bảo tại khu vực {seedHouse.Name}. Phòng rộng thoáng mát, giá rẻ sinh viên.",
                    AddressLine = $"Số {random.Next(1, 200)} Đường {Streets[i % Streets.Length]}",
                    WardCode = seedHouse.WardCode,
                    ProvinceCode = seedHouse.ProvinceCode,
                    AddressDisplay = $"{seedHouse.Name}, Việt Nam",
                    Latitude = (decimal)seedHouse.Latitude,
                    Longitude = (decimal)seedHouse.Longitude,
                    ApprovalStatus = RoomingHouseApprovalStatus.Approved,
                    VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                };
                _context.RoomingHouses.Add(house);

                // House Gallery Property Images
                for (int j = 0; j < seedHouse.HouseAssetIds.Count; j++)
                {
                    var assetId = Guid.Parse(seedHouse.HouseAssetIds[j]);
                    _context.PropertyImages.Add(new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseId = houseGuid,
                        RoomId = null,
                        RoomingHouseReviewId = null,
                        MediaAssetId = assetId,
                        ImageUrl = PublicMediaPathBuilder.Build(assetId),
                        Caption = $"Ảnh khu trọ {j + 1}",
                        IsCover = j == 0,
                        SortOrder = j,
                        CreatedAt = seededAt
                    });
                }

                // Amenities
                int houseAmenityCount = random.Next(4, Math.Min(8, houseAmenitiesPool.Count + 1));
                var chosenHouseAmenities = houseAmenitiesPool.OrderBy(x => random.Next()).Take(houseAmenityCount).ToList();
                foreach (var am in chosenHouseAmenities)
                {
                    _context.RoomingHouseAmenities.Add(new RoomingHouseAmenity
                    {
                        RoomingHouseId = houseGuid,
                        AmenityId = am.Id
                    });
                }

                // House Rule
                _context.RoomingHouseRules.Add(new RoomingHouseRule
                {
                    Id = Guid.NewGuid(),
                    RoomingHouseId = houseGuid,
                    SourceType = RoomingHouseRuleSourceType.FormGenerated,
                    GeneralRules = "Giữ gìn vệ sinh chung. Giờ giấc tự do nhưng hạn chế làm ồn sau 23h.",
                    QuietHours = "23:00 - 06:00",
                    SecurityPolicy = "Khóa cửa cổng sau khi ra vào.",
                    CleaningPolicy = "Dọn phòng định kỳ hàng tuần.",
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                });

                // Rental Policy
                _context.RentalPolicies.Add(new RentalPolicy
                {
                    Id = Guid.NewGuid(),
                    RoomingHouseId = houseGuid,
                    MinRentalMonths = 6,
                    MaxRentalMonths = 24,
                    AllowShortTermRenewal = true,
                    RenewalNoticeDays = 30,
                    DepositMonths = 1,
                    DefaultPaymentDay = 5,
                    IsActive = true,
                    CreatedAt = seededAt,
                    UpdatedAt = seededAt
                });

                // Service Prices
                foreach (var serviceType in serviceTypes)
                {
                    decimal price = serviceType.Name switch
                    {
                        "Điện" => 3500m,
                        "Nước" => 15000m,
                        "Internet" => 100000m,
                        _ => 30000m
                    };
                    var pricingUnit = serviceType.SupportsMeterReading ? PricingUnit.MeterReading : PricingUnit.PerMonth;

                    _context.RoomingHouseServicePrices.Add(new RoomingHouseServicePrice
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseId = houseGuid,
                        ServiceTypeId = serviceType.Id,
                        PricingUnit = pricingUnit,
                        UnitPrice = price,
                        EffectiveFrom = new DateOnly(2026, 1, 1),
                        IsActive = true,
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    });
                }

                // Rooms
                foreach (var seedRoom in seedHouse.Rooms)
                {
                    var roomGuid = Guid.Parse(seedRoom.Id);
                    var floor = int.Parse(seedRoom.RoomNumber[0].ToString());
                    var room = new Room
                    {
                        Id = roomGuid,
                        RoomingHouseId = houseGuid,
                        RoomNumber = seedRoom.RoomNumber,
                        Floor = floor,
                        AreaM2 = random.Next(18, 35),
                        MaxOccupants = random.Next(2, 4),
                        IsTieredPricing = false,
                        Status = RoomStatus.Available,
                        Description = $"Phòng trọ {seedRoom.RoomNumber} đầy đủ tiện nghi, thoáng mát.",
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    };
                    _context.Rooms.Add(room);

                    // Price Tier
                    _context.RoomPriceTiers.Add(new RoomPriceTier
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomGuid,
                        MonthlyRent = seedRoom.Price,
                        IsActive = true,
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt
                    });

                    // Room Images
                    for (int j = 0; j < seedRoom.RoomAssetIds.Count; j++)
                    {
                        var assetId = Guid.Parse(seedRoom.RoomAssetIds[j]);
                        _context.PropertyImages.Add(new PropertyImage
                        {
                            Id = Guid.NewGuid(),
                            RoomingHouseId = null,
                            RoomId = roomGuid,
                            RoomingHouseReviewId = null,
                            MediaAssetId = assetId,
                            ImageUrl = PublicMediaPathBuilder.Build(assetId),
                            Caption = $"Ảnh phòng {j + 1}",
                            IsCover = j == 0,
                            SortOrder = j,
                            CreatedAt = seededAt
                        });
                    }

                    // Room Amenities
                    int roomAmenityCount = random.Next(3, Math.Min(6, roomAmenitiesPool.Count + 1));
                    var chosenRoomAmenities = roomAmenitiesPool.OrderBy(x => random.Next()).Take(roomAmenityCount).ToList();
                    foreach (var am in chosenRoomAmenities)
                    {
                        _context.RoomAmenities.Add(new RoomAmenity
                        {
                            RoomId = roomGuid,
                            AmenityId = am.Id
                        });
                    }
                }

                // Reviews
                for (int j = 0; j < seedHouse.Reviews.Count; j++)
                {
                    var seedReview = seedHouse.Reviews[j];
                    var reviewGuid = Guid.Parse(seedReview.Id);
                    var contractGuid = Guid.Parse(seedReview.ContractId);
                    var reqGuid = Guid.Parse(seedReview.RentalRequestId);
                    var depositGuid = Guid.Parse(seedReview.RoomDepositId);
                    var tenantGuid = Guid.Parse(seedReview.TenantUserId);

                    var firstRoomInHouse = Guid.Parse(seedHouse.Rooms[0].Id);

                    // Parse contract start and end dates
                    var startStr = !string.IsNullOrEmpty(seedReview.ContractStartDate) ? seedReview.ContractStartDate : "2026-01-01";
                    var endStr = !string.IsNullOrEmpty(seedReview.ContractEndDate) ? seedReview.ContractEndDate : "2026-12-31";
                    var startDate = DateOnly.Parse(startStr);
                    var endDate = DateOnly.Parse(endStr);

                    // Determine contract status based on dates compared to the base date
                    var isExpired = endDate < DateOnly.FromDateTime(seededAt.DateTime);
                    var contractStatus = isExpired ? RentalContractStatus.Expired : RentalContractStatus.Active;

                    // Review created date and landlord reply date
                    var reviewOffset = seedReview.CreatedAtOffsetDays != 0 ? seedReview.CreatedAtOffsetDays : -10;
                    var reviewCreatedAt = seededAt.AddDays(reviewOffset);
                    var landlordReplyCreatedAt = reviewCreatedAt.AddDays(1);

                    // Rental Request
                    _context.RentalRequests.Add(new RentalRequest
                    {
                        Id = reqGuid,
                        RoomId = firstRoomInHouse,
                        TenantUserId = tenantGuid,
                        ApprovedByLandlordId = LandlordUserId,
                        DesiredStartDate = startDate,
                        ExpectedEndDate = endDate,
                        ExpectedOccupantCount = seedReview.Occupants != null && seedReview.Occupants.Count > 0 ? seedReview.Occupants.Count : 1,
                        MonthlyRentSnapshot = seedHouse.Rooms[0].Price,
                        DepositAmountSnapshot = seedHouse.Rooms[0].Price,
                        Status = RentalRequestStatus.Accepted,
                        RespondedAt = seededAt.AddDays(1),
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt.AddDays(1)
                    });

                    // Room Deposit
                    _context.RoomDeposits.Add(new RoomDeposit
                    {
                        Id = depositGuid,
                        RentalRequestId = reqGuid,
                        RoomId = firstRoomInHouse,
                        TenantUserId = tenantGuid,
                        LandlordUserId = LandlordUserId,
                        DepositAmount = seedHouse.Rooms[0].Price,
                        Status = RoomDepositStatus.Paid,
                        PaidAt = seededAt.AddDays(2),
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt.AddDays(2)
                    });

                    // Room Snapshot JSON
                    var snapshotObj = new
                    {
                        Id = firstRoomInHouse,
                        RoomingHouseId = houseGuid,
                        RoomingHouseName = seedHouse.Name,
                        RoomNumber = seedHouse.Rooms[0].RoomNumber,
                        Address = $"{seedHouse.Name}, Việt Nam",
                        AreaM2 = 25.0,
                        MonthlyRent = seedHouse.Rooms[0].Price,
                        DepositAmount = seedHouse.Rooms[0].Price,
                        PaymentDay = 5,
                        MaxOccupants = 2
                    };
                    string snapshotJson = JsonSerializer.Serialize(snapshotObj);

                    // Rental Contract
                    _context.RentalContracts.Add(new RentalContract
                    {
                        Id = contractGuid,
                        RentalRequestId = reqGuid,
                        RoomDepositId = depositGuid,
                        RoomId = firstRoomInHouse,
                        MainTenantUserId = tenantGuid,
                        ContractNumber = $"DISPLAY-CTR-{contractGuid.ToString("N")[^8..].ToUpper()}",
                        StartDate = startDate,
                        EndDate = endDate,
                        MonthlyRent = seedHouse.Rooms[0].Price,
                        DepositAmount = seedHouse.Rooms[0].Price,
                        PaymentDay = 5,
                        Status = contractStatus,
                        RoomSnapshot = snapshotJson,
                        ActivatedAt = seededAt.AddDays(3),
                        CreatedAt = seededAt,
                        UpdatedAt = seededAt.AddDays(3)
                    });

                    // Occupants
                    if (seedReview.Occupants != null && seedReview.Occupants.Count > 0)
                    {
                        foreach (var seedOcc in seedReview.Occupants)
                        {
                            _context.ContractOccupants.Add(new ContractOccupant
                            {
                                Id = Guid.Parse(seedOcc.Id),
                                RentalContractId = contractGuid,
                                UserId = seedOcc.RelationshipToMainTenant == "Self" ? tenantGuid : null,
                                FullName = seedOcc.FullName,
                                PhoneNumber = seedOcc.PhoneNumber,
                                DateOfBirth = DateOnly.Parse(seedOcc.DateOfBirth),
                                RelationshipToMainTenant = seedOcc.RelationshipToMainTenant == "Self" ? null : seedOcc.RelationshipToMainTenant,
                                MoveInDate = DateOnly.Parse(seedOcc.MoveInDate),
                                MoveOutDate = !string.IsNullOrEmpty(seedOcc.MoveOutDate) ? DateOnly.Parse(seedOcc.MoveOutDate) : null,
                                Status = seedOcc.Status == "Active" ? ContractOccupantStatus.Active : ContractOccupantStatus.MoveOut,
                                CreatedAt = seededAt,
                                UpdatedAt = seededAt
                            });
                        }
                    }
                    else
                    {
                        // Fallback occupant (main tenant) if occupants list is empty in manifest
                        string tenantName = j switch
                        {
                            0 => "Nguyễn Văn An",
                            1 => "Trần Thị Bình",
                            2 => "Lê Văn Cường",
                            3 => "Phạm Thị Dung",
                            _ => "Hoàng Văn Em"
                        };
                        _context.ContractOccupants.Add(new ContractOccupant
                        {
                            Id = Guid.NewGuid(),
                            RentalContractId = contractGuid,
                            UserId = tenantGuid,
                            FullName = tenantName,
                            PhoneNumber = $"091234567{j}",
                            DateOfBirth = new DateOnly(1998, 5, 15),
                            RelationshipToMainTenant = null,
                            MoveInDate = startDate,
                            MoveOutDate = isExpired ? endDate : null,
                            Status = isExpired ? ContractOccupantStatus.MoveOut : ContractOccupantStatus.Active,
                            CreatedAt = seededAt,
                            UpdatedAt = seededAt
                        });
                    }

                    // Review
                    _context.RoomingHouseReviews.Add(new RoomingHouseReview
                    {
                        Id = reviewGuid,
                        RoomingHouseId = houseGuid,
                        TenantUserId = tenantGuid,
                        RentalContractId = contractGuid,
                        Rating = seedReview.Rating,
                        Comment = seedReview.Comment,
                        LandlordReply = seedReview.LandlordReply,
                        LandlordReplyCreatedAt = string.IsNullOrWhiteSpace(seedReview.LandlordReply) ? null : landlordReplyCreatedAt,
                        IsHidden = false,
                        ModerationStatus = RoomingHouseReviewModerationStatus.Approved,
                        ModerationReason = "Approved automatically",
                        CreatedAt = reviewCreatedAt,
                        UpdatedAt = reviewCreatedAt
                    });

                    // Review Images
                    var reviewAssetsToSeed = seedReview.ReviewAssetIds != null && seedReview.ReviewAssetIds.Count > 0 
                        ? seedReview.ReviewAssetIds 
                        : new List<string> { seedReview.ReviewAssetId };

                    for (int aIdx = 0; aIdx < reviewAssetsToSeed.Count; aIdx++)
                    {
                        if (string.IsNullOrEmpty(reviewAssetsToSeed[aIdx])) continue;
                        var assetId = Guid.Parse(reviewAssetsToSeed[aIdx]);
                        _context.PropertyImages.Add(new PropertyImage
                        {
                            Id = Guid.NewGuid(),
                            RoomingHouseId = null,
                            RoomId = null,
                            RoomingHouseReviewId = reviewGuid,
                            MediaAssetId = assetId,
                            ImageUrl = PublicMediaPathBuilder.Build(assetId),
                            Caption = "Ảnh từ người dùng",
                            IsCover = false,
                            SortOrder = aIdx,
                            CreatedAt = reviewCreatedAt
                        });
                    }
                }
            
                if ((i + 1) % 20 == 0 || (i + 1) == houses.Count)
                {
                    Console.WriteLine($"Saving house seed batch to database ({i + 1}/{houses.Count})...");
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            Console.WriteLine("Updating house statistics (AverageRating and TotalReviews)...");
            foreach (var seedHouse in houses)
            {
                var houseGuid = Guid.Parse(seedHouse.Id);
                await RoomingHouseRatingHelper.UpdateRatingAsync(_context, houseGuid, cancellationToken);
            }
            await _context.SaveChangesAsync(cancellationToken);

            Console.WriteLine("Display Catalog Seeding completed successfully!");
        }

        public async Task<DisplaySeedValidationReport> RunValidateAsync(string version, CancellationToken cancellationToken = default)
        {
            var manifestPath = ResolveManifestPath(version);
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Seed manifest file not found: {manifestPath}");
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<DisplaySeedManifest>(json)
                           ?? throw new InvalidOperationException("Failed to deserialize manifest.");

            Console.WriteLine($"Validating Display Seed Catalog (Version: {version})");

            var report = new DisplaySeedValidationReport();

            var houseIds = manifest.Houses.Select(h => Guid.Parse(h.Id)).ToList();
            var dbHouses = await _context.RoomingHouses
                .Include(h => h.Rooms)
                .Include(h => h.Images)
                .Where(h => houseIds.Contains(h.Id))
                .ToListAsync(cancellationToken);

            report.TotalHousesExpected = manifest.Houses.Count;
            report.TotalHousesInDb = dbHouses.Count;

            int expectedContracts = manifest.Houses.Count * 5; // 5 reviews per house, each has a contract
            report.TotalContractsExpected = expectedContracts;

            foreach (var house in dbHouses)
            {
                if (ContainsDemoKeywords(house.Name) || house.Name.Contains("#"))
                {
                    report.InvalidHouseNames.Add(house.Name);
                }

                int houseImageCount = house.Images.Count;
                if (houseImageCount < 3 || houseImageCount > 5)
                {
                    report.HousesWithInvalidImageCount.Add((house.Id, house.Name, houseImageCount));
                }

                int roomCount = house.Rooms.Count;
                if (roomCount < 5 || roomCount > 8)
                {
                    report.HousesWithInvalidRoomCount.Add((house.Id, house.Name, roomCount));
                }

                var rooms = await _context.Rooms
                    .Include(r => r.Images)
                    .Where(r => r.RoomingHouseId == house.Id)
                    .ToListAsync(cancellationToken);

                foreach (var room in rooms)
                {
                    int roomImgCount = room.Images.Count;
                    if (roomImgCount < 3 || roomImgCount > 5)
                    {
                        report.RoomsWithInvalidImageCount.Add((room.Id, room.RoomNumber, roomImgCount));
                    }
                }

                var reviews = await _context.RoomingHouseReviews
                    .Where(r => r.RoomingHouseId == house.Id)
                    .ToListAsync(cancellationToken);

                if (reviews.Count != 5)
                {
                    report.HousesWithInvalidReviewCount.Add((house.Id, house.Name, reviews.Count));
                }

                var replyCount = reviews.Count(rev =>
                    !string.IsNullOrWhiteSpace(rev.LandlordReply) &&
                    rev.LandlordReplyCreatedAt.HasValue);
                if (replyCount < 3 || replyCount > 5)
                {
                    report.HousesWithInvalidReplyCount.Add((house.Id, house.Name, replyCount));
                }

                foreach (var rev in reviews)
                {
                    if (rev.ModerationStatus != RoomingHouseReviewModerationStatus.Approved)
                    {
                        report.UnapprovedReviews.Add(rev.Id);
                    }
                    if ((!string.IsNullOrWhiteSpace(rev.LandlordReply) && !rev.LandlordReplyCreatedAt.HasValue) ||
                        (string.IsNullOrWhiteSpace(rev.LandlordReply) && rev.LandlordReplyCreatedAt.HasValue))
                    {
                        report.ReviewsMissingLandlordReply.Add(rev.Id);
                    }

                    // Validate Rental Contract, Room Snapshot, and Occupants
                    if (rev.RentalContractId != Guid.Empty)
                    {
                        var contract = await _context.RentalContracts
                            .Include(c => c.Occupants)
                            .FirstOrDefaultAsync(c => c.Id == rev.RentalContractId, cancellationToken);

                        if (contract != null)
                        {
                            report.TotalContractsInDb++;
                            if (contract.Status == RentalContractStatus.Expired)
                            {
                                report.TotalExpiredContractsInDb++;
                            }
                            else if (contract.Status == RentalContractStatus.Active)
                            {
                                report.TotalActiveContractsInDb++;
                            }

                            // Check RoomSnapshot JSON
                            if (string.IsNullOrWhiteSpace(contract.RoomSnapshot))
                            {
                                report.InvalidRoomSnapshots.Add(contract.Id);
                            }
                            else
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(contract.RoomSnapshot);
                                    var root = doc.RootElement;
                                    if (!root.TryGetProperty("RoomNumber", out _) || !root.TryGetProperty("MaxOccupants", out _))
                                    {
                                        report.InvalidRoomSnapshots.Add(contract.Id);
                                    }
                                }
                                catch
                                {
                                    report.InvalidRoomSnapshots.Add(contract.Id);
                                }
                            }

                            // Validate occupants
                            report.TotalOccupantsInDb += contract.Occupants.Count;
                            foreach (var occ in contract.Occupants)
                            {
                                // Main tenant checks
                                if (occ.UserId == contract.MainTenantUserId)
                                {
                                    if (occ.RelationshipToMainTenant != null)
                                    {
                                        report.InvalidOccupantRelations.Add(contract.Id);
                                    }
                                }
                                else // Secondary occupants
                                {
                                    if (string.IsNullOrWhiteSpace(occ.RelationshipToMainTenant) || occ.RelationshipToMainTenant == "Self")
                                    {
                                        report.InvalidOccupantRelations.Add(contract.Id);
                                    }
                                }

                                // Status match check
                                if (contract.Status == RentalContractStatus.Expired && occ.Status != ContractOccupantStatus.MoveOut)
                                {
                                    report.InvalidOccupantRelations.Add(contract.Id);
                                }
                                if (contract.Status == RentalContractStatus.Active && occ.Status != ContractOccupantStatus.Active)
                                {
                                    report.InvalidOccupantRelations.Add(contract.Id);
                                }
                            }
                        }
                    }
                }
            }

            var mediaAssetIds = manifest.Assets.Select(a => Guid.Parse(a.Id)).ToList();
            var dbAssetsCount = await _context.MediaAssets
                .CountAsync(m => mediaAssetIds.Contains(m.Id), cancellationToken);
            report.TotalAssetsExpected = manifest.Assets.Count;
            report.TotalAssetsInDb = dbAssetsCount;

            report.PrintToConsole();
            return report;
        }

        

        

        private static bool ContainsDemoKeywords(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("demo") || lower.Contains("mock") || lower.Contains("test");
        }

        private string ResolveManifestPath(string version)
        {
            var root = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(root) && !File.Exists(Path.Combine(root, "SmartRentalPlatform.slnx")))
            {
                var parent = Directory.GetParent(root);
                if (parent == null) break;
                root = parent.FullName;
            }

            var dir = Path.Combine(root, "artifacts", "display-seed");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, $"{version}-manifest.json");
        }

        private async Task SaveManifestAsync(DisplaySeedManifest manifest, string path, CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(manifest, options);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }

        private async Task EnsureSeedUsersAsync(CancellationToken cancellationToken)
        {
            var landlordRole = await _context.Roles.FirstOrDefaultAsync(r => r.Id == RoleSeed.LandlordRoleId, cancellationToken);
            var tenantRole = await _context.Roles.FirstOrDefaultAsync(r => r.Id == RoleSeed.TenantRoleId, cancellationToken);

            var passwordHash = _passwordService.HashPassword("DisplayPassword@123");

            var landlordExists = await _context.Users.AnyAsync(u => u.Id == LandlordUserId, cancellationToken);
            if (!landlordExists)
            {
                var user = new User
                {
                    Id = LandlordUserId,
                    Email = "display-landlord@displayseed.com",
                    NormalizedEmail = "DISPLAY-LANDLORD@DISPLAYSEED.COM",
                    PasswordHash = passwordHash,
                    DisplayName = "Chủ trọ Display",
                    Status = UserStatus.Active,
                    OnboardingStatus = OnboardingStatus.Completed,
                    EmailConfirmed = true,
                    PhoneConfirmed = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _context.Users.Add(user);
                _context.UserProfiles.Add(new UserProfile
                {
                    UserId = LandlordUserId,
                    FullName = "Chủ trọ Display",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                if (landlordRole != null)
                {
                    _context.UserRoles.Add(new UserRole { UserId = LandlordUserId, RoleId = landlordRole.Id, CreatedAt = DateTimeOffset.UtcNow });
                }
            }

            for (int i = 0; i < TenantUserIds.Length; i++)
            {
                var tenantId = TenantUserIds[i];
                var tenantExists = await _context.Users.AnyAsync(u => u.Id == tenantId, cancellationToken);
                if (!tenantExists)
                {
                    string email = $"display-tenant-{i + 1}@displayseed.com";
                    string name = i switch
                    {
                        0 => "Nguyễn Văn An",
                        1 => "Trần Thị Bình",
                        2 => "Lê Văn Cường",
                        3 => "Phạm Thị Dung",
                        _ => "Hoàng Văn Em"
                    };

                    var user = new User
                    {
                        Id = tenantId,
                        Email = email,
                        NormalizedEmail = email.ToUpperInvariant(),
                        PasswordHash = passwordHash,
                        DisplayName = name,
                        Status = UserStatus.Active,
                        OnboardingStatus = OnboardingStatus.Completed,
                        EmailConfirmed = true,
                        PhoneConfirmed = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Users.Add(user);
                    _context.UserProfiles.Add(new UserProfile
                    {
                        UserId = tenantId,
                        FullName = name,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                    if (tenantRole != null)
                    {
                        _context.UserRoles.Add(new UserRole { UserId = tenantId, RoleId = tenantRole.Id, CreatedAt = DateTimeOffset.UtcNow });
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<DisplaySeedManifest> GenerateManifestStructureAsync(int targetHouseCount, int targetAssetCount, string version, string? mediaSourceDirectory, CancellationToken cancellationToken)
        {
            var manifest = new DisplaySeedManifest
            {
                Version = version,
                SeedBatchId = Guid.NewGuid().ToString()
            };

            var provinces = await _context.AdministrativeProvinces
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);

            var targetProvinceNames = new[] { "Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Thừa Thiên Huế", "Hải Phòng", "Thái Nguyên", "Nghệ An", "Thanh Hóa", "Bắc Ninh", "Quảng Ngãi", "Gia Lai", "Khánh Hòa", "Lâm Đồng", "Đắk Lắk", "Đồng Nai", "Vĩnh Long", "An Giang" };
            var targetProvinces = provinces
                .Where(p => targetProvinceNames.Any(name => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (targetProvinces.Count == 0)
            {
                targetProvinces = provinces;
            }

            var targetProvinceCodes = targetProvinces.Select(p => p.Code).ToList();
            var wards = await _context.AdministrativeWards
                .Where(x => x.IsActive && targetProvinceCodes.Contains(x.ProvinceCode))
                .ToListAsync(cancellationToken);

            if (wards.Count == 0)
            {
                throw new InvalidOperationException("No administrative wards found in the database. Ensure database has administrative divisions seeded.");
            }

            var sourceImages = ResolveSourceImages(mediaSourceDirectory)
                .Take(targetAssetCount)
                .ToList();

            if (sourceImages.Count > 0)
            {
                targetAssetCount = sourceImages.Count;
                Console.WriteLine($"Using {sourceImages.Count} real image files from {mediaSourceDirectory}.");
            }

            int extCount = sourceImages.Count > 0 ? Math.Max(10, (int)(targetAssetCount * 0.24)) : 120;
            int comCount = sourceImages.Count > 0 ? Math.Max(10, (int)(targetAssetCount * 0.16)) : 80;
            int romCount = sourceImages.Count > 0 ? Math.Max(20, (int)(targetAssetCount * 0.44)) : 220;
            int revCount = sourceImages.Count > 0 ? targetAssetCount - extCount - comCount - romCount : 80;

            if (targetAssetCount < 500)
            {
                extCount = Math.Max(5, (int)(targetAssetCount * 0.24));
                comCount = Math.Max(5, (int)(targetAssetCount * 0.16));
                romCount = Math.Max(10, (int)(targetAssetCount * 0.44));
                revCount = Math.Max(5, targetAssetCount - extCount - comCount - romCount);
            }

            var exteriorAssets = new List<DisplaySeedAsset>();
            var commonAssets = new List<DisplaySeedAsset>();
            var roomAssets = new List<DisplaySeedAsset>();
            var reviewAssets = new List<DisplaySeedAsset>();

            var today = DateTimeOffset.UtcNow;

            if (sourceImages.Count > 0)
            {
                var imageIndex = 0;
                exteriorAssets.AddRange(CreateRealImageAssets(sourceImages, ref imageIndex, extCount, "Exterior", today));
                commonAssets.AddRange(CreateRealImageAssets(sourceImages, ref imageIndex, comCount, "Common", today));
                roomAssets.AddRange(CreateRealImageAssets(sourceImages, ref imageIndex, romCount, "Room", today));
                reviewAssets.AddRange(CreateRealImageAssets(sourceImages, ref imageIndex, revCount, "Review", today));
            }
            else
            {
                for (int i = 0; i < extCount; i++)
            {
                var id = Guid.NewGuid();
                exteriorAssets.Add(new DisplaySeedAsset
                {
                    Id = id.ToString(),
                    Pool = "Exterior",
                    FileName = $"exterior-{i + 1}.svg",
                    ObjectKey = $"public/rooming-house-images/{today:yyyy/MM/dd}/{id:N}.svg",
                    Uploaded = false
                });
            }

                for (int i = 0; i < comCount; i++)
            {
                var id = Guid.NewGuid();
                commonAssets.Add(new DisplaySeedAsset
                {
                    Id = id.ToString(),
                    Pool = "Common",
                    FileName = $"common-{i + 1}.svg",
                    ObjectKey = $"public/rooming-house-images/{today:yyyy/MM/dd}/{id:N}.svg",
                    Uploaded = false
                });
            }

                for (int i = 0; i < romCount; i++)
            {
                var id = Guid.NewGuid();
                roomAssets.Add(new DisplaySeedAsset
                {
                    Id = id.ToString(),
                    Pool = "Room",
                    FileName = $"room-{i + 1}.svg",
                    ObjectKey = $"public/room-images/{today:yyyy/MM/dd}/{id:N}.svg",
                    Uploaded = false
                });
            }

                for (int i = 0; i < revCount; i++)
            {
                var id = Guid.NewGuid();
                reviewAssets.Add(new DisplaySeedAsset
                {
                    Id = id.ToString(),
                    Pool = "Review",
                    FileName = $"review-{i + 1}.svg",
                    ObjectKey = $"public/rooming-house-images/{today:yyyy/MM/dd}/{id:N}.svg",
                    Uploaded = false
                });
            }
            }

            manifest.Assets.AddRange(exteriorAssets);
            manifest.Assets.AddRange(commonAssets);
            manifest.Assets.AddRange(roomAssets);
            manifest.Assets.AddRange(reviewAssets);

            var random = new Random(1337);
            var houseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < targetHouseCount; i++)
            {
                var ward = wards[random.Next(wards.Count)];
                var province = targetProvinces.First(p => p.Code == ward.ProvinceCode);

                string houseName;
                int nameRetry = 0;
                do
                {
                    var prefix = Prefixes[random.Next(Prefixes.Length)];
                    var core = CoreNames[random.Next(CoreNames.Length)];
                    string suffix;
                    if (nameRetry % 3 == 0) suffix = $" {ward.Name}";
                    else if (nameRetry % 3 == 1) suffix = $" Đường {Streets[random.Next(Streets.Length)]}";
                    else suffix = $" {province.Name}";

                    houseName = $"{prefix} {core}{suffix}";
                    nameRetry++;
                } while (houseNames.Contains(houseName) && nameRetry < 50);

                houseNames.Add(houseName);

                decimal baseLat = 15.9754m;
                decimal baseLng = 108.2638m;
                if (TargetProvinceCoordinates.TryGetValue(province.Code, out var coords))
                {
                    baseLat = coords.Lat;
                    baseLng = coords.Lng;
                }

                var latOffset = (decimal)(random.NextDouble() - 0.5) * 0.01m;
                var lngOffset = (decimal)(random.NextDouble() - 0.5) * 0.01m;

                var seedHouse = new DisplaySeedHouse
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = houseName,
                    ProvinceCode = province.Code,
                    WardCode = ward.Code,
                    Latitude = (double)(baseLat + latOffset),
                    Longitude = (double)(baseLng + lngOffset)
                };

                int houseImgCount = 3 + (i % 3);
                var chosenHouseAssetIds = new List<string> { exteriorAssets[i % exteriorAssets.Count].Id };
                int houseAttempts = 0;
                while (chosenHouseAssetIds.Count < houseImgCount && houseAttempts < 100)
                {
                    houseAttempts++;
                    string candidateId = (chosenHouseAssetIds.Count % 2 == 1)
                        ? commonAssets[(i * 7 + chosenHouseAssetIds.Count + houseAttempts) % commonAssets.Count].Id
                        : exteriorAssets[(i * 13 + chosenHouseAssetIds.Count + houseAttempts) % exteriorAssets.Count].Id;

                    if (!chosenHouseAssetIds.Contains(candidateId))
                    {
                        chosenHouseAssetIds.Add(candidateId);
                    }
                }
                seedHouse.HouseAssetIds.AddRange(chosenHouseAssetIds);

                int roomCount = 5 + (i % 4);
                bool isBigCity = province.Name.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) ||
                                 province.Name.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) ||
                                 province.Name.Contains("Đà Nẵng", StringComparison.OrdinalIgnoreCase) ||
                                 province.Name.Contains("Cần Thơ", StringComparison.OrdinalIgnoreCase) ||
                                 province.Name.Contains("Hải Phòng", StringComparison.OrdinalIgnoreCase);

                for (int r = 1; r <= roomCount; r++)
                {
                    decimal price = isBigCity ? (random.Next(25, 66) * 100_000) : (random.Next(15, 36) * 100_000);
                    var roomId = Guid.NewGuid();
                    var seedRoom = new DisplaySeedRoom
                    {
                        Id = roomId.ToString(),
                        RoomNumber = $"{random.Next(1, 6)}{r:D2}",
                        Price = price
                    };

                    int roomImgCount = 3 + (r % 3);
                    int startAssetIdx = (i * 10 + r * 5);
                    var chosenRoomAssetIds = new List<string> { roomAssets[startAssetIdx % roomAssets.Count].Id };
                    int roomAttempts = 0;
                    while (chosenRoomAssetIds.Count < roomImgCount && roomAttempts < 100)
                    {
                        roomAttempts++;
                        var candidateId = roomAssets[(startAssetIdx + chosenRoomAssetIds.Count + roomAttempts) % roomAssets.Count].Id;
                        if (!chosenRoomAssetIds.Contains(candidateId))
                        {
                            chosenRoomAssetIds.Add(candidateId);
                        }
                    }
                    seedRoom.RoomAssetIds.AddRange(chosenRoomAssetIds);

                    seedHouse.Rooms.Add(seedRoom);
                }

                int[] ratings = (i % 4) switch
                {
                    0 => new[] { 5, 5, 5, 4, 4 },
                    1 => new[] { 5, 5, 4, 4, 3 },
                    2 => new[] { 5, 5, 5, 4, 4 },
                    _ => new[] { 5, 5, 5, 4, 3 }
                };

                var repliesToSeed = 3 + (i % 3);
                for (int rIdx = 0; rIdx < 5; rIdx++)
                {
                    int rating = ratings[rIdx];
                    string comment = rating switch
                    {
                        5 => Rating5Comments[(i * 5 + rIdx) % Rating5Comments.Length],
                        4 => Rating4Comments[(i * 4 + rIdx) % Rating4Comments.Length],
                        _ => Rating3Comments[(i * 3 + rIdx) % Rating3Comments.Length]
                    };
                    string reply = rIdx < repliesToSeed
                        ? LandlordReplies[(i * 7 + rIdx) % LandlordReplies.Length]
                        : string.Empty;

                    var reviewAssetIds = new List<string>();
                    int reviewImgCount = 1 + ((i + rIdx) % 3);
                    for (int imgIdx = 0; imgIdx < reviewImgCount; imgIdx++)
                    {
                        string candidateId = (imgIdx % 2 == 0)
                            ? reviewAssets[(i * 5 + rIdx * 2 + imgIdx) % reviewAssets.Count].Id
                            : commonAssets[(i * 3 + rIdx * 3 + imgIdx) % commonAssets.Count].Id;
                        if (!reviewAssetIds.Contains(candidateId))
                        {
                            reviewAssetIds.Add(candidateId);
                        }
                    }

                    string mainTenantName = rIdx switch
                    {
                        0 => "Nguyễn Văn An",
                        1 => "Trần Thị Bình",
                        2 => "Lê Văn Cường",
                        3 => "Phạm Thị Dung",
                        _ => "Hoàng Văn Em"
                    };

                    string contractStart, contractEnd;
                    int createdAtOffset;
                    if (rIdx < 4) 
                    {
                        int startMonth = 1 + (rIdx * 2);
                        contractStart = $"2025-{startMonth:D2}-10";
                        int endMonth = startMonth + 6;
                        int endYear = 2025;
                        if (endMonth > 12)
                        {
                            endMonth -= 12;
                            endYear = 2026;
                        }
                        contractEnd = $"{endYear}-{endMonth:D2}-10";
                        createdAtOffset = rIdx switch
                        {
                            0 => -345,
                            1 => -263,
                            2 => -171,
                            _ => -81  
                        };
                    }
                    else 
                    {
                        contractStart = "2026-01-15";
                        contractEnd = "2026-07-15";
                        createdAtOffset = -30; 
                    }

                    var occupants = new List<DisplaySeedOccupant>();
                    occupants.Add(new DisplaySeedOccupant
                    {
                        Id = Guid.NewGuid().ToString(),
                        FullName = mainTenantName,
                        PhoneNumber = $"091234567{rIdx}",
                        DateOfBirth = $"1998-05-1{rIdx}",
                        RelationshipToMainTenant = "Self",
                        MoveInDate = contractStart,
                        MoveOutDate = rIdx < 4 ? contractEnd : null,
                        Status = rIdx < 4 ? "MoveOut" : "Active"
                    });

                    if ((i + rIdx) % 5 < 2)
                    {
                        string occupantName = OccupantNames[(i * 5 + rIdx) % OccupantNames.Length];
                        string relationship = Relationships[(i * 3 + rIdx) % Relationships.Length];
                        occupants.Add(new DisplaySeedOccupant
                        {
                            Id = Guid.NewGuid().ToString(),
                            FullName = occupantName,
                            PhoneNumber = $"098765432{rIdx}",
                            DateOfBirth = $"1999-08-1{rIdx}",
                            RelationshipToMainTenant = relationship,
                            MoveInDate = contractStart,
                            MoveOutDate = rIdx < 4 ? contractEnd : null,
                            Status = rIdx < 4 ? "MoveOut" : "Active"
                        });
                    }

                    seedHouse.Reviews.Add(new DisplaySeedReview
                    {
                        Id = Guid.NewGuid().ToString(),
                        ContractId = Guid.NewGuid().ToString(),
                        RentalRequestId = Guid.NewGuid().ToString(),
                        RoomDepositId = Guid.NewGuid().ToString(),
                        TenantUserId = TenantUserIds[rIdx].ToString(),
                        Rating = rating,
                        Comment = comment,
                        LandlordReply = reply,
                        CreatedAtOffsetDays = createdAtOffset,
                        ContractStartDate = contractStart,
                        ContractEndDate = contractEnd,
                        Occupants = occupants,
                        ReviewAssetIds = reviewAssetIds,
                        ReviewAssetId = reviewAssetIds[0]
                    });
                }

                manifest.Houses.Add(seedHouse);
            }

            return manifest;
        }

        private static List<string> ResolveSourceImages(string? mediaSourceDirectory)
        {
            if (string.IsNullOrWhiteSpace(mediaSourceDirectory))
            {
                return new List<string>();
            }

            var directory = mediaSourceDirectory;
            if (!Path.IsPathRooted(directory))
            {
                directory = Path.Combine(FindSolutionRoot(), directory);
            }

            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Media source directory not found: {directory}");
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            return Directory
                .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => allowedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<DisplaySeedAsset> CreateRealImageAssets(
            IReadOnlyList<string> sourceImages,
            ref int imageIndex,
            int count,
            string pool,
            DateTimeOffset today)
        {
            var assets = new List<DisplaySeedAsset>();
            for (int i = 0; i < count && imageIndex < sourceImages.Count; i++, imageIndex++)
            {
                var sourcePath = sourceImages[imageIndex];
                var id = Guid.NewGuid();
                var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                var objectPrefix = pool == "Room"
                    ? "public/room-images"
                    : "public/rooming-house-images";

                assets.Add(new DisplaySeedAsset
                {
                    Id = id.ToString(),
                    Pool = pool,
                    FileName = Path.GetFileName(sourcePath),
                    ObjectKey = $"{objectPrefix}/{today:yyyy/MM/dd}/{id:N}{extension}",
                    SourcePath = sourcePath,
                    ContentType = ResolveContentType(sourcePath),
                    FileSize = new FileInfo(sourcePath).Length,
                    Uploaded = false
                });
            }

            return assets;
        }

        private static async Task<DisplaySeedAssetContent> LoadAssetContentAsync(
            DisplaySeedAsset asset,
            int index,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(asset.SourcePath))
            {
                var bytes = await File.ReadAllBytesAsync(asset.SourcePath, cancellationToken);
                var contentType = string.IsNullOrWhiteSpace(asset.ContentType)
                    ? ResolveContentType(asset.SourcePath)
                    : asset.ContentType;

                return new DisplaySeedAssetContent(bytes, contentType, bytes.Length);
            }

            var svgBytes = GenerateSvg(asset.Pool, index, asset.FileName.Replace(".svg", "").Replace("-", " "));
            return new DisplaySeedAssetContent(svgBytes, "image/svg+xml", svgBytes.Length);
        }

        private static string ResolveContentType(string pathOrFileName)
        {
            return Path.GetExtension(pathOrFileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        private static string FindSolutionRoot()
        {
            var root = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(root) && !File.Exists(Path.Combine(root, "SmartRentalPlatform.slnx")))
            {
                var parent = Directory.GetParent(root);
                if (parent == null)
                {
                    break;
                }

                root = parent.FullName;
            }

            return root;
        }

        private static byte[] GenerateSvg(string pool, int index, string label)
        {
            string gradientStart = "#14b8a6";
            string gradientMid = "#2563eb";
            string gradientEnd = "#f59e0b";
            string poolNameEn = pool;

            if (pool == "Exterior")
            {
                gradientStart = "#f97316";
                gradientMid = "#ec4899";
                gradientEnd = "#8b5cf6";
            }
            else if (pool == "Common")
            {
                gradientStart = "#10b981";
                gradientMid = "#06b6d4";
                gradientEnd = "#3b82f6";
            }
            else if (pool == "Room")
            {
                gradientStart = "#6366f1";
                gradientMid = "#a855f7";
                gradientEnd = "#ec4899";
            }
            else
            {
                gradientStart = "#eab308";
                gradientMid = "#f97316";
                gradientEnd = "#ef4444";
            }

            var labelClean = label.Replace(".svg", "").Replace("-", " ");
            labelClean = char.ToUpper(labelClean[0]) + labelClean.Substring(1);

            var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""960"" height=""640"" viewBox=""0 0 960 640"">
              <defs>
                <linearGradient id=""grad_{pool}_{index}"" x1=""0"" y1=""0"" x2=""1"" y2=""1"">
                  <stop offset=""0%"" stop-color=""{gradientStart}"" />
                  <stop offset=""50%"" stop-color=""{gradientMid}"" />
                  <stop offset=""100%"" stop-color=""{gradientEnd}"" />
                </linearGradient>
              </defs>
              <rect width=""960"" height=""640"" fill=""url(#grad_{pool}_{index})"" />
              <rect x=""80"" y=""80"" width=""800"" height=""480"" rx=""24"" fill=""#ffffff"" fill-opacity=""0.15"" stroke=""#ffffff"" stroke-width=""2"" stroke-opacity=""0.3"" />
              <circle cx=""300"" cy=""320"" r=""120"" fill=""#ffffff"" fill-opacity=""0.1"" />
              <rect x=""480"" y=""200"" width=""240"" height=""240"" rx=""16"" fill=""#ffffff"" fill-opacity=""0.08"" transform=""rotate(15, 600, 320)"" />
              <rect x=""120"" y=""130"" width=""180"" height=""40"" rx=""20"" fill=""#ffffff"" fill-opacity=""0.25"" />
              <text x=""210"" y=""155"" font-family=""'Segoe UI', Roboto, sans-serif"" font-size=""16"" font-weight=""bold"" fill=""#ffffff"" text-anchor=""middle"">{poolNameEn.ToUpper()}</text>
              <text x=""120"" y=""400"" font-family=""'Segoe UI', Roboto, sans-serif"" font-size=""42"" font-weight=""bold"" fill=""#ffffff"">{labelClean}</text>
              <text x=""120"" y=""460"" font-family=""'Segoe UI', Roboto, sans-serif"" font-size=""20"" fill=""#f1f5f9"">ID: {pool.ToLower()}_{index:D3}</text>
            </svg>";

            return System.Text.Encoding.UTF8.GetBytes(svg);
        }

        private static async Task<MediaStoredObjectResult> UploadAssetAsync(
            IMediaStorageService mediaStorageService,
            string objectKey,
            string fileName,
            byte[] contentBytes,
            string contentType,
            long fileSize,
            CancellationToken cancellationToken)
        {
            await using var content = new MemoryStream(contentBytes, writable: false);
            return await mediaStorageService.UploadAsync(
                new MediaUploadRequest
                {
                    Content = content,
                    OriginalFileName = fileName,
                    ContentType = contentType,
                    FileSize = fileSize,
                    ObjectKey = objectKey,
                    Visibility = MediaVisibility.Public
                },
                cancellationToken);
        }
    }

    public class DisplaySeedManifest
    {
        public string Version { get; set; } = string.Empty;
        public string SeedBatchId { get; set; } = string.Empty;
        public List<DisplaySeedAsset> Assets { get; set; } = new();
        public List<DisplaySeedHouse> Houses { get; set; } = new();
    }

    public class DisplaySeedAsset
    {
        public string Id { get; set; } = string.Empty;
        public string Pool { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool Uploaded { get; set; }
        public string S3Url { get; set; } = string.Empty;
    }

    internal sealed record DisplaySeedAssetContent(byte[] Bytes, string ContentType, long FileSize);

    public class DisplaySeedHouse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProvinceCode { get; set; } = string.Empty;
        public string WardCode { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<DisplaySeedRoom> Rooms { get; set; } = new();
        public List<DisplaySeedReview> Reviews { get; set; } = new();
        public List<string> HouseAssetIds { get; set; } = new();
    }

    public class DisplaySeedRoom
    {
        public string Id { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public List<string> RoomAssetIds { get; set; } = new();
    }

    public class DisplaySeedReview
    {
        public string Id { get; set; } = string.Empty;
        public string ContractId { get; set; } = string.Empty;
        public string RentalRequestId { get; set; } = string.Empty;
        public string RoomDepositId { get; set; } = string.Empty;
        public string TenantUserId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string LandlordReply { get; set; } = string.Empty;
        public int CreatedAtOffsetDays { get; set; }
        public string ContractStartDate { get; set; } = string.Empty;
        public string ContractEndDate { get; set; } = string.Empty;
        public List<DisplaySeedOccupant> Occupants { get; set; } = new();
        public List<string> ReviewAssetIds { get; set; } = new();
        public string ReviewAssetId { get; set; } = string.Empty; // Fallback compatibility
    }

    public class DisplaySeedOccupant
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string RelationshipToMainTenant { get; set; } = string.Empty;
        public string MoveInDate { get; set; } = string.Empty;
        public string? MoveOutDate { get; set; }
        public string Status { get; set; } = string.Empty; // Active, MoveOut
    }


    public class DisplaySeedValidationReport
    {
        public int TotalHousesExpected { get; set; }
        public int TotalHousesInDb { get; set; }
        public int TotalAssetsExpected { get; set; }
        public int TotalAssetsInDb { get; set; }
        public int TotalContractsExpected { get; set; }
        public int TotalContractsInDb { get; set; }
        public int TotalExpiredContractsInDb { get; set; }
        public int TotalActiveContractsInDb { get; set; }
        public int TotalOccupantsInDb { get; set; }
        public List<string> InvalidHouseNames { get; set; } = new();
        public List<(Guid Id, string Name, int Count)> HousesWithInvalidImageCount { get; set; } = new();
        public List<(Guid Id, string Name, int Count)> HousesWithInvalidRoomCount { get; set; } = new();
        public List<(Guid Id, string RoomNumber, int Count)> RoomsWithInvalidImageCount { get; set; } = new();
        public List<(Guid Id, string Name, int Count)> HousesWithInvalidReviewCount { get; set; } = new();
        public List<(Guid Id, string Name, int Count)> HousesWithInvalidReplyCount { get; set; } = new();
        public List<Guid> UnapprovedReviews { get; set; } = new();
        public List<Guid> ReviewsMissingLandlordReply { get; set; } = new();
        public List<Guid> InvalidRoomSnapshots { get; set; } = new();
        public List<Guid> InvalidOccupantRelations { get; set; } = new();

        public bool IsValid =>
            TotalHousesInDb == TotalHousesExpected &&
            TotalAssetsInDb == TotalAssetsExpected &&
            TotalContractsInDb == TotalContractsExpected &&
            InvalidHouseNames.Count == 0 &&
            HousesWithInvalidImageCount.Count == 0 &&
            HousesWithInvalidRoomCount.Count == 0 &&
            RoomsWithInvalidImageCount.Count == 0 &&
            HousesWithInvalidReviewCount.Count == 0 &&
            HousesWithInvalidReplyCount.Count == 0 &&
            UnapprovedReviews.Count == 0 &&
            ReviewsMissingLandlordReply.Count == 0 &&
            InvalidRoomSnapshots.Count == 0 &&
            InvalidOccupantRelations.Count == 0;

        public void PrintToConsole()
        {
            var housesMatchStatus = TotalHousesExpected == TotalHousesInDb ? "PASS" : "FAIL";
            var assetsMatchStatus = TotalAssetsExpected == TotalAssetsInDb ? "PASS" : "FAIL";
            var contractsMatchStatus = TotalContractsExpected == TotalContractsInDb ? "PASS" : "FAIL";
            var invalidNamesStatus = InvalidHouseNames.Count == 0 ? "PASS" : "FAIL";
            var houseImageCountStatus = HousesWithInvalidImageCount.Count == 0 ? "PASS" : "FAIL";
            var houseRoomCountStatus = HousesWithInvalidRoomCount.Count == 0 ? "PASS" : "FAIL";
            var roomImageCountStatus = RoomsWithInvalidImageCount.Count == 0 ? "PASS" : "FAIL";
            var houseReviewCountStatus = HousesWithInvalidReviewCount.Count == 0 ? "PASS" : "FAIL";
            var houseReplyCountStatus = HousesWithInvalidReplyCount.Count == 0 ? "PASS" : "FAIL";
            var unapprovedReviewsStatus = UnapprovedReviews.Count == 0 ? "PASS" : "FAIL";
            var missingRepliesStatus = ReviewsMissingLandlordReply.Count == 0 ? "PASS" : "FAIL";
            var roomSnapshotsStatus = InvalidRoomSnapshots.Count == 0 ? "PASS" : "FAIL";
            var occupantRelationsStatus = InvalidOccupantRelations.Count == 0 ? "PASS" : "FAIL";
            var overallStatus = IsValid ? "SUCCESS" : "FAILED";

            Console.WriteLine("================ DISPLAY CATALOG SEED VALIDATION REPORT ================");
            Console.WriteLine($"Houses Expectation Match: Expected {TotalHousesExpected}, Found {TotalHousesInDb} - {housesMatchStatus}");
            Console.WriteLine($"Assets Expectation Match: Expected {TotalAssetsExpected}, Found {TotalAssetsInDb} - {assetsMatchStatus}");
            Console.WriteLine($"Contracts Expectation Match: Expected {TotalContractsExpected}, Found {TotalContractsInDb} - {contractsMatchStatus}");
            Console.WriteLine($"  Active Contracts: {TotalActiveContractsInDb}, Expired: {TotalExpiredContractsInDb}");
            Console.WriteLine($"  Total Occupants Seeded: {TotalOccupantsInDb}");
            Console.WriteLine($"Invalid House Names (Contains 'demo', 'mock', 'test', '#'): {InvalidHouseNames.Count} - {invalidNamesStatus}");
            if (InvalidHouseNames.Count > 0)
            {
                Console.WriteLine("  Invalid names: " + string.Join(", ", InvalidHouseNames.Take(5)));
            }

            Console.WriteLine($"Houses with Invalid Image Count (Not 3-5 images): {HousesWithInvalidImageCount.Count} - {houseImageCountStatus}");
            Console.WriteLine($"Houses with Invalid Room Count (Not 5-8 rooms): {HousesWithInvalidRoomCount.Count} - {houseRoomCountStatus}");
            Console.WriteLine($"Rooms with Invalid Image Count (Not 3-5 images): {RoomsWithInvalidImageCount.Count} - {roomImageCountStatus}");
            Console.WriteLine($"Houses with Invalid Review Count (Not exactly 5 reviews): {HousesWithInvalidReviewCount.Count} - {houseReviewCountStatus}");
            Console.WriteLine($"Houses with Invalid Reply Count (Not 3-5 replies): {HousesWithInvalidReplyCount.Count} - {houseReplyCountStatus}");
            Console.WriteLine($"Unapproved Reviews: {UnapprovedReviews.Count} - {unapprovedReviewsStatus}");
            Console.WriteLine($"Reviews with inconsistent landlord reply/date: {ReviewsMissingLandlordReply.Count} - {missingRepliesStatus}");
            Console.WriteLine($"Invalid Room Snapshots JSON: {InvalidRoomSnapshots.Count} - {roomSnapshotsStatus}");
            Console.WriteLine($"Invalid Occupant Relations or Status: {InvalidOccupantRelations.Count} - {occupantRelationsStatus}");
            Console.WriteLine($"Overall Validation Result: {overallStatus}");
            Console.WriteLine("=========================================================================");
        }
    }


}
