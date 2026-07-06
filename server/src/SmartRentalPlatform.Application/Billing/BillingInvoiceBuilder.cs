using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.Billing;

public sealed class BillingInvoiceBuilder
{
    private readonly IAppDbContext context;

    public BillingInvoiceBuilder(IAppDbContext context)
    {
        this.context = context;
    }

    internal List<FixedServicePreviewResponse> BuildFixedServicePreviews(
        List<RoomingHouseServicePrice> prices,
        IReadOnlyDictionary<Guid, BillingServiceType> serviceTypeById,
        ResolvedBillingPeriod billingPeriod,
        int occupantCount)
    {
        return prices
            .Where(x => x.PricingUnit is PricingUnit.PerMonth or PricingUnit.PerPersonPerMonth)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .Where(x => serviceTypeById.ContainsKey(x.ServiceTypeId))
            .OrderBy(x => serviceTypeById[x.ServiceTypeId].Name)
            .Select(price =>
            {
                var serviceType = serviceTypeById[price.ServiceTypeId];
                var quantity = GetFixedServiceQuantity(price.PricingUnit, billingPeriod, occupantCount);
                var amount = BillingPeriodResolver.RoundMoney(price.UnitPrice * quantity);
                return new FixedServicePreviewResponse(
                    serviceType.Id,
                    serviceType.Name,
                    price.PricingUnit.ToString(),
                    GetDisplayUnitName(price, serviceType),
                    price.UnitPrice,
                    quantity,
                    occupantCount,
                    amount);
            })
            .ToList();
    }

    internal List<MeteredServicePreviewResponse> BuildMeteredServicePreviews(
        List<RoomingHouseServicePrice> prices,
        IReadOnlyDictionary<Guid, BillingServiceType> serviceTypeById,
        IReadOnlyDictionary<Guid, LatestMeterReadingResponse> latestReadingByServiceType)
    {
        return prices
            .Where(x => x.PricingUnit == PricingUnit.MeterReading)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .Where(x => serviceTypeById.ContainsKey(x.ServiceTypeId))
            .OrderBy(x => serviceTypeById[x.ServiceTypeId].Name)
            .Select(price =>
            {
                var serviceType = serviceTypeById[price.ServiceTypeId];
                latestReadingByServiceType.TryGetValue(serviceType.Id, out var latestReading);
                return new MeteredServicePreviewResponse(
                    serviceType.Id,
                    serviceType.Name,
                    serviceType.MeterUnitName ?? string.Empty,
                    price.UnitPrice,
                    latestReading,
                    latestReading is null);
            })
            .ToList();
    }

    internal void AddRentInvoiceItem(
        Invoice invoice,
        decimal monthlyRent,
        decimal rentAmount,
        ResolvedBillingPeriod billingPeriod,
        DateTimeOffset now)
    {
        invoice.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            ItemType = InvoiceItemType.Rent,
            Description = BillingPeriodResolver.BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
            Quantity = BillingPeriodResolver.GetPeriodQuantity(billingPeriod),
            UnitPrice = monthlyRent,
            Amount = rentAmount,
            CreatedAt = now
        });
    }

    internal void AddMeteredServiceInvoiceItems(
        Invoice invoice,
        Guid roomId,
        Guid contractId,
        Guid landlordUserId,
        ResolvedBillingPeriod billingPeriod,
        IReadOnlyCollection<ResolvedMeterReadingInput> meteredInputs,
        DateTimeOffset now)
    {
        foreach (var input in meteredInputs)
        {
            var consumption = input.CurrentReading - input.PreviousReading;
            var amount = BillingPeriodResolver.RoundMoney(consumption * input.Price.UnitPrice);
            var reading = new MeterReading
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                ContractId = contractId,
                ServiceTypeId = input.ServiceType.Id,
                BillingPeriodStart = billingPeriod.Start,
                BillingPeriodEnd = billingPeriod.End,
                PreviousReading = input.PreviousReading,
                CurrentReading = input.CurrentReading,
                Consumption = consumption,
                ProofImageObjectKey = input.ProofImageObjectKey,
                RecordedByLandlordUserId = landlordUserId,
                ReadingAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.MeterReadings.Add(reading);
            invoice.UtilityAmount += amount;
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ServiceTypeId = input.ServiceType.Id,
                MeterReadingId = reading.Id,
                ItemType = InvoiceItemType.Service,
                Description = $"{input.ServiceType.Name} ({consumption} {GetDisplayUnitName(input.Price, input.ServiceType)})",
                Quantity = consumption,
                UnitPrice = input.Price.UnitPrice,
                Amount = amount,
                CreatedAt = now
            });
        }
    }

    internal void AddFixedServiceInvoiceItems(
        Invoice invoice,
        List<RoomingHouseServicePrice> prices,
        IReadOnlyDictionary<Guid, BillingServiceType> serviceTypeById,
        ResolvedBillingPeriod billingPeriod,
        int occupantCount,
        DateTimeOffset now)
    {
        var fixedPrices = prices
            .Where(x => x.PricingUnit is PricingUnit.PerMonth or PricingUnit.PerPersonPerMonth)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .OrderBy(x => serviceTypeById.TryGetValue(x.ServiceTypeId, out var serviceType) ? serviceType.Name : string.Empty);

        foreach (var price in fixedPrices)
        {
            if (!serviceTypeById.TryGetValue(price.ServiceTypeId, out var serviceType))
            {
                continue;
            }

            var quantity = GetFixedServiceQuantity(price.PricingUnit, billingPeriod, occupantCount);
            var serviceAmount = BillingPeriodResolver.RoundMoney(price.UnitPrice * quantity);
            invoice.ServiceAmount += serviceAmount;
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ServiceTypeId = serviceType.Id,
                ItemType = InvoiceItemType.Service,
                Description = BuildFixedServiceDescription(serviceType.Name, price.PricingUnit, billingPeriod, occupantCount),
                Quantity = quantity,
                UnitPrice = price.UnitPrice,
                Amount = serviceAmount,
                CreatedAt = now
            });
        }
    }

    internal static void CalculateAndValidateInvoiceTotal(Invoice invoice)
    {
        invoice.TotalAmount = BillingPeriodResolver.RoundMoney(invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount);
        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tổng tiền hóa đơn không được âm.");
        }
    }

    internal static string GetDisplayUnitName(RoomingHouseServicePrice price, BillingServiceType serviceType)
    {
        return price.PricingUnit switch
        {
            PricingUnit.MeterReading => serviceType.MeterUnitName ?? string.Empty,
            PricingUnit.PerMonth => "tháng",
            PricingUnit.PerPersonPerMonth => "người/tháng",
            _ => string.Empty
        };
    }

    private static decimal GetFixedServiceQuantity(
        PricingUnit pricingUnit,
        ResolvedBillingPeriod period,
        int occupantCount)
    {
        var periodQuantity = BillingPeriodResolver.GetPeriodQuantity(period);
        return pricingUnit == PricingUnit.PerPersonPerMonth
            ? occupantCount * periodQuantity
            : periodQuantity;
    }

    private static string BuildFixedServiceDescription(
        string description,
        PricingUnit pricingUnit,
        ResolvedBillingPeriod period,
        int occupantCount)
    {
        var baseDescription = BillingPeriodResolver.BuildPeriodDescription(description, period);
        return pricingUnit == PricingUnit.PerPersonPerMonth
            ? $"{baseDescription} ({occupantCount} nguoi)"
            : baseDescription;
    }
}

internal sealed record ResolvedMeterReadingInput(
    BillingServiceType ServiceType,
    RoomingHouseServicePrice Price,
    decimal PreviousReading,
    decimal CurrentReading,
    string? ProofImageObjectKey);
