using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.Billing;

public sealed class MeterReadingInputResolver
{
    private readonly IAppDbContext context;

    public MeterReadingInputResolver(IAppDbContext context)
    {
        this.context = context;
    }

    internal async Task<List<ResolvedMeterReadingInput>> ResolveAsync(
        Guid contractId,
        ResolvedBillingPeriod billingPeriod,
        IReadOnlyCollection<MeterReadingInput> meterReadings,
        IReadOnlyDictionary<Guid, BillingServiceType> serviceTypeById,
        List<RoomingHouseServicePrice> prices,
        bool isFinalInvoice,
        CancellationToken cancellationToken)
    {
        var duplicatedInputService = meterReadings
            .Select(x => x.ServiceTypeId)
            .GroupBy(x => x)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatedInputService is not null)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Mỗi dịch vụ chỉ số chỉ được nhập một lần trong cùng hóa đơn.");
        }

        var meterReadingByServiceType = meterReadings.ToDictionary(x => x.ServiceTypeId);
        var requiredMeteredPrices = prices
            .Where(x => x.PricingUnit == PricingUnit.MeterReading)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .ToList();

        foreach (var price in requiredMeteredPrices)
        {
            if (!meterReadingByServiceType.ContainsKey(price.ServiceTypeId) &&
                serviceTypeById.TryGetValue(price.ServiceTypeId, out var missingServiceType))
            {
                var suffix = isFinalInvoice ? " kỳ cuối" : string.Empty;
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Vui lòng nhập chỉ số cho dịch vụ {missingServiceType.Name} trước khi tạo hóa đơn{suffix}.");
            }
        }

        var meteredInputs = new List<ResolvedMeterReadingInput>();
        foreach (var input in meterReadings)
        {
            if (!serviceTypeById.TryGetValue(input.ServiceTypeId, out var serviceType))
            {
                throw new NotFoundException(
                    ErrorCodes.BillingServiceInvalid,
                    "Không tìm thấy loại dịch vụ đang hoạt động.");
            }

            var price = GetEffectivePriceOrThrow(prices, serviceType.Id, serviceType.Name);
            if (price.PricingUnit != PricingUnit.MeterReading)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Dịch vụ {serviceType.Name} không được cấu hình tính tiền theo chỉ số trong kỳ này.");
            }

            if (!serviceType.SupportsMeterReading || string.IsNullOrWhiteSpace(serviceType.MeterUnitName))
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Dịch vụ {serviceType.Name} không hỗ trợ nhập chỉ số.");
            }

            if (input.PreviousReading.HasValue && input.PreviousReading.Value < 0)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số đầu kỳ không được âm cho dịch vụ {serviceType.Name}.");
            }

            if (input.CurrentReading < 0)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số cuối kỳ không được âm cho dịch vụ {serviceType.Name}.");
            }

            var overlapping = await context.MeterReadings.AnyAsync(
                x => x.ContractId == contractId &&
                     x.ServiceTypeId == serviceType.Id &&
                     x.BillingPeriodStart <= billingPeriod.End &&
                     x.BillingPeriodEnd >= billingPeriod.Start &&
                     x.InvoiceItems.Any(item => item.Invoice.Status != InvoiceStatus.Cancelled),
                cancellationToken);

            if (overlapping)
            {
                throw new ConflictException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Kỳ ghi chỉ số của dịch vụ {serviceType.Name} bị trùng hoặc chồng lên với bản ghi đã có.");
            }

            var latestReading = await context.MeterReadings
                .AsNoTracking()
                .Where(x => x.ContractId == contractId &&
                            x.ServiceTypeId == serviceType.Id &&
                            x.BillingPeriodEnd < billingPeriod.Start &&
                            x.InvoiceItems.Any(item => item.Invoice.Status != InvoiceStatus.Cancelled))
                .OrderByDescending(x => x.BillingPeriodEnd)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var previousReading = latestReading?.CurrentReading ?? input.PreviousReading;
            if (!previousReading.HasValue)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Lần tạo hóa đơn đầu tiên của dịch vụ {serviceType.Name} phải nhập chỉ số đầu kỳ.");
            }

            if (latestReading is not null &&
                input.PreviousReading.HasValue &&
                input.PreviousReading.Value != latestReading.CurrentReading)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số đầu kỳ của {serviceType.Name} phải bằng chỉ số cuối kỳ gần nhất ({latestReading.CurrentReading}).");
            }

            if (input.CurrentReading < previousReading.Value)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ cho dịch vụ {serviceType.Name}.");
            }

            if (!string.IsNullOrWhiteSpace(input.ProofImageObjectKey) &&
                !input.ProofImageObjectKey.StartsWith("meter-readings/", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    "Ảnh minh chứng chỉ số điện nước phải thuộc phạm vi meter-readings.");
            }

            if (input.AiReading.HasValue && input.AiReading.Value < 0)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    "Chỉ số đọc được từ AI không được âm.");
            }

            var resolvedAiRawText = string.IsNullOrWhiteSpace(input.AiRawText)
                ? null
                : input.AiRawText.Trim()[..Math.Min(input.AiRawText.Trim().Length, 4000)];

            meteredInputs.Add(new ResolvedMeterReadingInput(
                serviceType,
                price,
                previousReading.Value,
                input.CurrentReading,
                input.ProofImageObjectKey?.Trim(),
                input.AiReading,
                resolvedAiRawText));
        }

        return meteredInputs;
    }

    private static RoomingHouseServicePrice GetEffectivePriceOrThrow(
        List<RoomingHouseServicePrice> prices,
        Guid serviceTypeId,
        string serviceName)
    {
        return prices
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault(x => x.ServiceTypeId == serviceTypeId)
            ?? throw new NotFoundException(
                ErrorCodes.BillingPriceNotFound,
                $"Chưa có bảng giá hiệu lực cho dịch vụ {serviceName}.");
    }
}
