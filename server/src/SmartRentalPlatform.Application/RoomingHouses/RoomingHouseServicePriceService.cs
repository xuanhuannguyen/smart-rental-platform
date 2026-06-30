using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseServicePriceService : IRoomingHouseServicePriceService
{
    private readonly IAppDbContext context;

    public RoomingHouseServicePriceService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<List<ServicePriceResponse>> GetServicePricesAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoomingHouseOwnerAsync(landlordUserId, roomingHouseId, cancellationToken);

        var prices = await context.RoomingHouseServicePrices
            .AsNoTracking()
            .Include(x => x.ServiceType)
            .Where(x => x.RoomingHouseId == roomingHouseId)
            .OrderBy(x => x.ServiceType.Name)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return prices.Select(ToServicePriceResponse).ToList();
    }

    public async Task<ServicePriceResponse> CreateServicePriceAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CreateServicePriceRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoomingHouseOwnerAsync(landlordUserId, roomingHouseId, cancellationToken);

        var pricingUnit = ParsePricingUnit(request.PricingUnit);
        var serviceType = await GetServiceTypeAsync(request.ServiceTypeId, cancellationToken);
        ValidatePricingUnitForServiceType(serviceType, pricingUnit);

        if (request.UnitPrice < 0)
        {
            throw new BadRequestException(ErrorCodes.BillingPriceInvalid, "Đơn giá dịch vụ không được âm.");
        }

        var now = DateTimeOffset.UtcNow;
        var nowUtc = now.UtcDateTime;
        var today = DateOnly.FromDateTime(nowUtc);

        DateOnly effectiveFrom;
        if (request.EffectiveFrom != default)
        {
            // Use the effectiveFrom sent by the client (FE already calculates the correct date)
            effectiveFrom = request.EffectiveFrom;
        }
        else
        {
            // Fallback: auto-calculate (for direct API calls without effectiveFrom)
            var hasAnyPrice = await context.RoomingHouseServicePrices
                .AnyAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

            if (!hasAnyPrice)
            {
                // First time: effective from 1st of current month to cover the entire month
                effectiveFrom = new DateOnly(nowUtc.Year, nowUtc.Month, 1);
            }
            else if (nowUtc.Day == 1)
            {
                // If today is the 1st, apply from 1st of this month
                effectiveFrom = new DateOnly(nowUtc.Year, nowUtc.Month, 1);
            }
            else
            {
                // From 2nd onwards, apply from 1st of next month
                effectiveFrom = GetNextBillingPeriodStart(today);
            }
        }

        var activePrice = await context.RoomingHouseServicePrices
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        x.ServiceTypeId == serviceType.Id &&
                        x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        var scheduledPrice = await context.RoomingHouseServicePrices
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        x.ServiceTypeId == serviceType.Id &&
                        x.EffectiveFrom == effectiveFrom &&
                        x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (scheduledPrice is not null)
        {
            scheduledPrice.PricingUnit = pricingUnit;
            scheduledPrice.UnitPrice = request.UnitPrice;
            scheduledPrice.Note = request.Note;
            scheduledPrice.IsActive = true;
            scheduledPrice.EffectiveTo = null;
            scheduledPrice.UpdatedAt = now;

            var otherActivePrices = await context.RoomingHouseServicePrices
                .Where(x => x.RoomingHouseId == roomingHouseId &&
                            x.ServiceTypeId == serviceType.Id &&
                            x.Id != scheduledPrice.Id &&
                            x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var otherPrice in otherActivePrices)
            {
                otherPrice.IsActive = false;
                otherPrice.EffectiveTo = effectiveFrom.AddDays(-1);
                otherPrice.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);

            scheduledPrice.ServiceType = serviceType;
            return ToServicePriceResponse(scheduledPrice);
        }

        if (activePrice is not null)
        {
            if (effectiveFrom == activePrice.EffectiveFrom)
            {
                // Same effective period: update the existing price record
                activePrice.PricingUnit = pricingUnit;
                activePrice.UnitPrice = request.UnitPrice;
                activePrice.EffectiveTo = null;
                activePrice.IsActive = true;
                activePrice.Note = request.Note;
                activePrice.UpdatedAt = now;

                await context.SaveChangesAsync(cancellationToken);

                activePrice.ServiceType = serviceType;
                return ToServicePriceResponse(activePrice);
            }

            // New effective period: deactivate the old price
            activePrice.IsActive = false;
            activePrice.EffectiveTo = effectiveFrom.AddDays(-1);
            activePrice.UpdatedAt = now;
        }

        var price = new RoomingHouseServicePrice
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = roomingHouseId,
            ServiceTypeId = serviceType.Id,
            PricingUnit = pricingUnit,
            UnitPrice = request.UnitPrice,
            EffectiveFrom = effectiveFrom,
            IsActive = true,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.RoomingHouseServicePrices.Add(price);
        await context.SaveChangesAsync(cancellationToken);

        price.ServiceType = serviceType;
        return ToServicePriceResponse(price);
    }

    private async Task EnsureRoomingHouseOwnerAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var ownsHouse = await context.RoomingHouses.AnyAsync(
            x => x.Id == roomingHouseId && x.LandlordUserId == landlordUserId,
            cancellationToken);

        if (!ownsHouse)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền quản lý khu trọ này.");
        }
    }

    private async Task<BillingServiceType> GetServiceTypeAsync(
        Guid serviceTypeId,
        CancellationToken cancellationToken)
    {
        return await context.BillingServiceTypes
            .FirstOrDefaultAsync(x => x.Id == serviceTypeId && x.IsActive, cancellationToken)
            ?? throw new BadRequestException(ErrorCodes.BillingServiceInvalid, "Loại dịch vụ không hợp lệ hoặc đã bị vô hiệu hóa.");
    }

    private static PricingUnit ParsePricingUnit(string value)
    {
        if (string.Equals(value, "MeterBased", StringComparison.OrdinalIgnoreCase))
        {
            return PricingUnit.MeterReading;
        }

        if (Enum.TryParse<PricingUnit>(value, true, out var result))
        {
            return result;
        }

        throw new BadRequestException(ErrorCodes.BillingPriceInvalid, "Đơn vị tính giá không hợp lệ.");
    }

    private static void ValidatePricingUnitForServiceType(BillingServiceType serviceType, PricingUnit pricingUnit)
    {
        if (pricingUnit != PricingUnit.MeterReading)
        {
            return;
        }

        if (!serviceType.SupportsMeterReading)
        {
            throw new BadRequestException(
                ErrorCodes.BillingPriceInvalid,
                "Loại dịch vụ này không hỗ trợ tính phí theo chỉ số đo lường.");
        }
    }

    private static DateOnly GetNextBillingPeriodStart(DateOnly date)
    {
        if (date.Month == 12)
        {
            return new DateOnly(date.Year + 1, 1, 1);
        }

        return new DateOnly(date.Year, date.Month + 1, 1);
    }

    private static ServicePriceResponse ToServicePriceResponse(RoomingHouseServicePrice price)
    {
        var pricingUnitName = price.PricingUnit.ToString(); // Or mapped to Vietnamese if there is an extension method

        return new ServicePriceResponse(
            price.Id,
            price.RoomingHouseId,
            price.ServiceTypeId,
            price.ServiceType?.Name ?? string.Empty,
            price.ServiceType?.SupportsMeterReading ?? false,
            price.ServiceType?.MeterUnitName,
            price.PricingUnit.ToString(),
            pricingUnitName, // DisplayUnitName
            price.UnitPrice,
            price.EffectiveFrom,
            price.EffectiveTo,
            price.IsActive,
            price.Note,
            price.CreatedAt,
            price.UpdatedAt);
    }
}
