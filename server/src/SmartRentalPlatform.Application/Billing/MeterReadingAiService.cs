using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.Billing;

public sealed class MeterReadingAiService : IMeterReadingAiService
{
    private static readonly Guid ElectricityServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid WaterServiceTypeId = Guid.Parse("60000000-0000-0000-0000-000000000002");

    private readonly IAppDbContext context;
    private readonly IMeterAiClient aiClient;
    private readonly IFileStorageService storage;

    public MeterReadingAiService(IAppDbContext context, IMeterAiClient aiClient, IFileStorageService storage)
    {
        this.context = context;
        this.aiClient = aiClient;
        this.storage = storage;
    }

    public async Task<MeterAiResponse> ReadAsync(
        Guid landlordUserId,
        Guid contractId,
        Guid serviceTypeId,
        DateOnly billingPeriodStart,
        ImageUploadFile image,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
            .FirstOrDefaultAsync(x => x.Id == contractId &&
                                      x.Room.RoomingHouse.LandlordUserId == landlordUserId,
                cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng thuộc quyền quản lý.");

        var isMetered = await context.RoomingHouseServicePrices
            .AsNoTracking()
            .AnyAsync(x => x.RoomingHouseId == contract.Room.RoomingHouseId &&
                           x.ServiceTypeId == serviceTypeId &&
                           x.PricingUnit == PricingUnit.MeterReading &&
                           x.EffectiveFrom <= billingPeriodStart &&
                           (x.EffectiveTo == null || x.EffectiveTo >= billingPeriodStart),
                cancellationToken);

        if (!isMetered)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Dịch vụ này không được cấu hình tính theo chỉ số đồng hồ trong kỳ hóa đơn.");
        }

        // Use independent streams because HttpClient owns/disposes its multipart content.
        await using var buffer = new MemoryStream();
        await image.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var aiImage = new ImageUploadFile
        {
            Content = new MemoryStream(bytes, writable: false),
            FileName = image.FileName,
            ContentType = image.ContentType,
            Length = bytes.Length
        };
        var storedImage = new ImageUploadFile
        {
            Content = new MemoryStream(bytes, writable: false),
            FileName = image.FileName,
            ContentType = image.ContentType,
            Length = bytes.Length
        };

        // Run AI first: an unreadable/non-meter image must not be persisted as valid proof.
        var aiResult = await aiClient.ReadMeterAsync(aiImage, cancellationToken);
        var normalizedReading = NormalizeReading(serviceTypeId, aiResult.Reading);
        var upload = await storage.UploadImageAsync(storedImage, FileUploadScope.MeterReading, cancellationToken);
        return new MeterAiResponse(normalizedReading, aiResult.RawText, upload.MediaAssetId, upload.Url);
    }

    /// <summary>
    /// Đồng hồ điện có một chữ số thập phân, đồng hồ nước có ba chữ số thập phân.
    /// Làm tròn midpoint theo hướng tăng để trả về chỉ số nguyên dùng lập hóa đơn.
    /// </summary>
    public static decimal NormalizeReading(Guid serviceTypeId, decimal rawReading)
    {
        if (serviceTypeId == ElectricityServiceTypeId)
        {
            return decimal.Round(rawReading / 10m, 0, MidpointRounding.AwayFromZero);
        }

        if (serviceTypeId == WaterServiceTypeId)
        {
            return decimal.Round(rawReading / 1000m, 0, MidpointRounding.AwayFromZero);
        }

        return rawReading;
    }
}
