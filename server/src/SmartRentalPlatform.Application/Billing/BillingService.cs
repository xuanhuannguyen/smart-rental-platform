using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public class BillingService : IBillingService
{
    private readonly IAppDbContext context;
    private readonly IBillingContractReadService contractReadService;
    private readonly IInvoiceWalletPaymentService walletPaymentService;

    public BillingService(
        IAppDbContext context,
        IBillingContractReadService contractReadService,
        IInvoiceWalletPaymentService walletPaymentService)
    {
        this.context = context;
        this.contractReadService = contractReadService;
        this.walletPaymentService = walletPaymentService;
    }

    public async Task<List<BillingServiceTypeResponse>> GetBillingServiceTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return serviceTypes.Select(ToBillingServiceTypeResponse).ToList();
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

    public async Task<RoomBillingContextResponse> GetRoomBillingContextAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.MainTenantUser)
            .Where(x => x.RoomId == roomId &&
                        x.Status == RentalContractStatus.Active &&
                        (x.Room.Status == RoomStatus.Occupied ||
                         x.Room.Status == RoomStatus.Reserved) &&
                        x.Room.RoomingHouse.LandlordUserId == landlordUserId &&
                        x.Room.DeletedAt == null &&
                        x.Room.RoomingHouse.DeletedAt == null)
            .OrderByDescending(x => x.ActivatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.RentalContractNotFound,
                "Phòng này chưa có hợp đồng Active để tạo hóa đơn.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            today,
            cancellationToken);
        var effectiveMonthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            today,
            cancellationToken);
        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contract.Id,
            contract.MainTenantUserId,
            today,
            cancellationToken);
        var latestReadingByServiceType = await GetLatestReadingByServiceTypeAsync(
            contract.Id,
            beforeBillingPeriodStart: null,
            cancellationToken);

        return new RoomBillingContextResponse(
            contract.RoomId,
            contract.Room.RoomNumber,
            contract.Room.RoomingHouseId,
            contract.Id,
            contract.ContractNumber,
            effectiveTenant.UserId,
            effectiveTenant.DisplayName,
            effectiveTenant.Email,
            effectiveMonthlyRent,
            contract.PaymentDay,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            contract.Status.ToString(),
            latestReadingByServiceType);
    }

    public async Task<RoomInvoicePreviewResponse> GetRoomInvoicePreviewAsync(
        Guid landlordUserId,
        Guid roomId,
        DateOnly billingPeriodStart,
        DateOnly? billingPeriodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (billingPeriodEnd.HasValue && billingPeriodEnd.Value < billingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Kỳ hóa đơn không hợp lệ.");
        }

        if (billingPeriodEnd.HasValue &&
            (billingPeriodStart.Year != billingPeriodEnd.Value.Year ||
             billingPeriodStart.Month != billingPeriodEnd.Value.Month))
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Kỳ hóa đơn phải nằm trong cùng một tháng.");
        }

        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Where(x => x.RoomId == roomId &&
                        x.Status == RentalContractStatus.Active &&
                        (x.Room.Status == RoomStatus.Occupied ||
                         x.Room.Status == RoomStatus.Reserved) &&
                        x.Room.RoomingHouse.LandlordUserId == landlordUserId &&
                        x.Room.DeletedAt == null &&
                        x.Room.RoomingHouse.DeletedAt == null)
            .OrderByDescending(x => x.ActivatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.RentalContractNotFound,
                "Phòng này chưa có hợp đồng Active để tạo hóa đơn.");

        var termsEffectiveOn = billingPeriodEnd ??
                               new DateOnly(
                                   billingPeriodStart.Year,
                                   billingPeriodStart.Month,
                                   DateTime.DaysInMonth(billingPeriodStart.Year, billingPeriodStart.Month));
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            termsEffectiveOn,
            cancellationToken);
        var billingPeriod = ResolveBillingPeriodWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriodStart,
            billingPeriodEnd);
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            billingPeriod.Start,
            cancellationToken);
        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contract.Id,
            contract.MainTenantUserId,
            billingPeriod.Start,
            cancellationToken);
        var rentAmount = CalculatePeriodAmount(monthlyRent, billingPeriod);
        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var serviceTypeById = serviceTypes.ToDictionary(x => x.Id);
        var prices = await GetEffectivePricesAsync(
            contract.Room.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            billingPeriod.Start,
            cancellationToken);
        var occupantCount = await GetActiveOccupantCountAsync(contract.Id, billingPeriod, cancellationToken);
        var latestReadingByServiceType = await GetLatestReadingByServiceTypeAsync(
            contract.Id,
            billingPeriod.Start,
            cancellationToken);
        var generationBlockReason = await GetInvoiceGenerationBlockReasonAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriod,
            billingPeriodEnd,
            cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (billingPeriod.Start > today || billingPeriod.End > today)
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = prices
            .Where(x => x.PricingUnit is PricingUnit.PerMonth or PricingUnit.PerPersonPerMonth)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .Where(x => serviceTypeById.ContainsKey(x.ServiceTypeId))
            .OrderBy(x => serviceTypeById[x.ServiceTypeId].Name)
            .Select(price =>
            {
                var serviceType = serviceTypeById[price.ServiceTypeId];
                var quantity = GetFixedServiceQuantity(price.PricingUnit, billingPeriod, occupantCount);
                var amount = RoundMoney(price.UnitPrice * quantity);
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

        var meteredServices = prices
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

        var fixedServiceAmount = fixedServices.Sum(x => x.Amount);

        return new RoomInvoicePreviewResponse(
            contract.RoomId,
            contract.Room.RoomNumber,
            contract.Room.RoomingHouseId,
            contract.Id,
            contract.ContractNumber,
            effectiveTenant.UserId,
            effectiveTenant.DisplayName,
            effectiveTenant.Email,
            monthlyRent,
            contract.PaymentDay,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            contract.Status.ToString(),
            billingPeriod.Start,
            billingPeriod.End,
            billingPeriod.BillableDays,
            billingPeriod.DaysInMonth,
            billingPeriod.IsFullMonth,
            new InvoiceLinePreviewResponse(
                BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
                GetPeriodQuantity(billingPeriod),
                monthlyRent,
                rentAmount),
            fixedServices,
            meteredServices,
            rentAmount,
            fixedServiceAmount,
            0,
            rentAmount + fixedServiceAmount,
            generationBlockReason is null,
            generationBlockReason);
    }

    public async Task<RoomInvoicePreviewResponse> GetTerminationInvoicePreviewAsync(
        Guid landlordUserId,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var contractSnapshot = await contractReadService.GetContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");
        if (!contractSnapshot.TerminationDate.HasValue)
        {
            throw new ConflictException(
                ErrorCodes.FinalInvoiceNotAllowed,
                "Hợp đồng chưa có ngày chấm dứt.");
        }

        DateOnly terminationDate = contractSnapshot.TerminationDate.Value;
        await GetOwnedTerminationBillingContractAsync(
            landlordUserId,
            contractId,
            terminationDate,
            allowActiveContract: false,
            cancellationToken);

        var contract = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .FirstAsync(x => x.Id == contractId, cancellationToken);
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            terminationDate,
            cancellationToken);
        DateOnly? latestBilledThrough = await context.Invoices.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .Select(x => (DateOnly?)x.BillingPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        DateOnly nextPeriodStart = latestBilledThrough?.AddDays(1) ?? effectiveTerms.StartDate;
        if (nextPeriodStart > terminationDate)
        {
            throw new ConflictException(
                ErrorCodes.FinalInvoiceNotAllowed,
                "Hợp đồng đã được tạo đủ hóa đơn đến ngày chấm dứt.");
        }

        var billingPeriod = ResolveBillingPeriodWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            nextPeriodStart,
            terminationDate);
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            billingPeriod.Start,
            cancellationToken);
        DateOnly tenantEffectiveOn = billingPeriod.End == terminationDate
            ? terminationDate
            : billingPeriod.Start;
        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contract.Id,
            contract.MainTenantUserId,
            tenantEffectiveOn,
            cancellationToken);
        var rentAmount = CalculatePeriodAmount(monthlyRent, billingPeriod);
        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var serviceTypeById = serviceTypes.ToDictionary(x => x.Id);
        var prices = await GetEffectivePricesAsync(
            contract.Room.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            billingPeriod.Start,
            cancellationToken);
        var occupantCount = await GetActiveOccupantCountAsync(contract.Id, billingPeriod, cancellationToken);
        var latestReadingByServiceType = await GetLatestReadingByServiceTypeAsync(
            contract.Id,
            billingPeriod.Start,
            cancellationToken);
        var generationBlockReason = await GetInvoiceGenerationBlockReasonAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriod,
            terminationDate,
            cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (billingPeriod.Start > today || billingPeriod.End > today)
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = prices
            .Where(x => x.PricingUnit is PricingUnit.PerMonth or PricingUnit.PerPersonPerMonth)
            .GroupBy(x => x.ServiceTypeId)
            .Select(x => x.OrderByDescending(price => price.EffectiveFrom).First())
            .Where(x => serviceTypeById.ContainsKey(x.ServiceTypeId))
            .OrderBy(x => serviceTypeById[x.ServiceTypeId].Name)
            .Select(price =>
            {
                var serviceType = serviceTypeById[price.ServiceTypeId];
                var quantity = GetFixedServiceQuantity(price.PricingUnit, billingPeriod, occupantCount);
                var amount = RoundMoney(price.UnitPrice * quantity);
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

        var meteredServices = prices
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
        var fixedServiceAmount = fixedServices.Sum(x => x.Amount);

        return new RoomInvoicePreviewResponse(
            contract.RoomId,
            contract.Room.RoomNumber,
            contract.Room.RoomingHouseId,
            contract.Id,
            contract.ContractNumber,
            effectiveTenant.UserId,
            effectiveTenant.DisplayName,
            effectiveTenant.Email,
            monthlyRent,
            contract.PaymentDay,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            contract.Status.ToString(),
            billingPeriod.Start,
            billingPeriod.End,
            billingPeriod.BillableDays,
            billingPeriod.DaysInMonth,
            billingPeriod.IsFullMonth,
            new InvoiceLinePreviewResponse(
                BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
                GetPeriodQuantity(billingPeriod),
                monthlyRent,
                rentAmount),
            fixedServices,
            meteredServices,
            rentAmount,
            fixedServiceAmount,
            0,
            rentAmount + fixedServiceAmount,
            generationBlockReason is null,
            generationBlockReason);
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
        var effectiveFrom = GetNextBillingPeriodStart(DateOnly.FromDateTime(now.UtcDateTime));

        var scheduledPrice = await context.RoomingHouseServicePrices
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        x.ServiceTypeId == serviceType.Id &&
                        x.EffectiveFrom == effectiveFrom)
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

        var activePrice = await context.RoomingHouseServicePrices
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        x.ServiceTypeId == serviceType.Id &&
                        x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePrice is not null)
        {
            if (effectiveFrom <= activePrice.EffectiveFrom)
            {
                activePrice.PricingUnit = pricingUnit;
                activePrice.UnitPrice = request.UnitPrice;
                activePrice.EffectiveFrom = effectiveFrom;
                activePrice.EffectiveTo = null;
                activePrice.IsActive = true;
                activePrice.Note = request.Note;
                activePrice.UpdatedAt = now;

                await context.SaveChangesAsync(cancellationToken);

                activePrice.ServiceType = serviceType;
                return ToServicePriceResponse(activePrice);
            }

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

#if false
    public async Task<MeterReadingResponse> CreateMeterReadingAsync(
        Guid landlordUserId,
        CreateMeterReadingRequest request,
        CancellationToken cancellationToken = default)
    {
        var serviceCode = ParseServiceCode(request.ServiceCode);
        if (!MeteredServices.Contains(serviceCode))
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Chỉ được nhập chỉ số cho dịch vụ Điện và Nước.");
        }

        if (request.BillingPeriodEnd < request.BillingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Kỳ hóa đơn không hợp lệ.");
        }

        if (request.CurrentReading < request.PreviousReading)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ.");
        }

        var contract = await GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);
        if (contract.RoomId != request.RoomId)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Phòng không khớp với hợp đồng đang active.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.BillingPeriodStart > today || request.BillingPeriodEnd > today)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        if (request.BillingPeriodStart < contract.StartDate || request.BillingPeriodEnd > contract.EndDate)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số phải nằm trong thời hạn hợp đồng.");
        }

        var serviceType = await GetServiceTypeAsync(serviceCode, cancellationToken);
        var duplicate = await context.MeterReadings.AnyAsync(
            x => x.ContractId == request.ContractId &&
                 x.ServiceTypeId == serviceType.Id &&
                 x.BillingPeriodStart == request.BillingPeriodStart &&
                 x.BillingPeriodEnd == request.BillingPeriodEnd,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.MeterReadingInvalid,
                "Chỉ số của dịch vụ trong kỳ này đã tồn tại.");
        }

        var overlapping = await context.MeterReadings.AnyAsync(
            x => x.ContractId == request.ContractId &&
                 x.ServiceTypeId == serviceType.Id &&
                 x.BillingPeriodStart <= request.BillingPeriodEnd &&
                 x.BillingPeriodEnd >= request.BillingPeriodStart,
            cancellationToken);

        if (overlapping)
        {
            throw new ConflictException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số bị trùng hoặc chồng lấn với bản ghi đã có.");
        }

        var now = DateTimeOffset.UtcNow;
        var reading = new MeterReading
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            ContractId = request.ContractId,
            ServiceTypeId = serviceType.Id,
            BillingPeriodStart = request.BillingPeriodStart,
            BillingPeriodEnd = request.BillingPeriodEnd,
            PreviousReading = request.PreviousReading,
            CurrentReading = request.CurrentReading,
            Consumption = request.CurrentReading - request.PreviousReading,
            ProofImageObjectKey = request.ProofImageObjectKey,
            Status = MeterReadingStatus.Draft,
            RecordedByLandlordUserId = landlordUserId,
            ReadingAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.MeterReadings.Add(reading);
        await context.SaveChangesAsync(cancellationToken);

        reading.ServiceType = serviceType;
        return ToMeterReadingResponse(reading);
    }

    public async Task<InvoiceResponse> GenerateDraftInvoiceAsync(
        Guid landlordUserId,
        GenerateInvoiceDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BillingPeriodEnd < request.BillingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Kỳ hóa đơn không hợp lệ.");
        }

        if (!IsFullCalendarMonth(request.BillingPeriodStart, request.BillingPeriodEnd))
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Hóa đơn tháng phải bắt đầu ngày 01 và kết thúc vào ngày cuối cùng của cùng tháng.");
        }

        if (request.DiscountAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Số tiền giảm trừ không được âm.");
        }

        var contract = await GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);
        var duplicate = await context.Invoices.AnyAsync(
            x => x.ContractId == request.ContractId &&
                 x.BillingPeriodStart == request.BillingPeriodStart &&
                 x.BillingPeriodEnd == request.BillingPeriodEnd,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var serviceTypeByCode = serviceTypes.ToDictionary(x => x.Code);
        var prices = await GetEffectivePricesAsync(
            contract.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            request.BillingPeriodEnd,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(request.BillingPeriodEnd, contract.PaymentDay);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            RoomId = contract.RoomId,
            TenantUserId = contract.TenantUserId,
            LandlordUserId = contract.LandlordUserId,
            InvoiceNo = GenerateInvoiceNo(),
            BillingPeriodStart = request.BillingPeriodStart,
            BillingPeriodEnd = request.BillingPeriodEnd,
            DueDate = dueDate,
            RentAmount = contract.MonthlyRent,
            DiscountAmount = request.DiscountAmount,
            Status = InvoiceStatus.Draft,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        invoice.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            ItemType = InvoiceItemType.Rent,
            Description = "Tiền thuê phòng",
            Quantity = 1,
            UnitPrice = contract.MonthlyRent,
            Amount = contract.MonthlyRent,
            CreatedAt = now
        });

        var meterReadings = await context.MeterReadings
            .Include(x => x.ServiceType)
            .Where(x => x.ContractId == contract.Id &&
                        x.BillingPeriodStart == request.BillingPeriodStart &&
                        x.BillingPeriodEnd == request.BillingPeriodEnd &&
                        x.Status == MeterReadingStatus.Draft)
            .ToListAsync(cancellationToken);

        foreach (var code in MeteredServices)
        {
            if (!serviceTypeByCode.TryGetValue(code, out var serviceType))
            {
                continue;
            }

            var reading = meterReadings.FirstOrDefault(x => x.ServiceTypeId == serviceType.Id);
            if (reading is null)
            {
                continue;
            }

            var price = GetEffectivePriceOrThrow(prices, serviceType.Id, code);
            var itemType = code == BillingServiceCode.Electric ? InvoiceItemType.Electricity : InvoiceItemType.Water;
            var amount = reading.Consumption * price.UnitPrice;

            invoice.UtilityAmount += amount;
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ServiceTypeId = serviceType.Id,
                MeterReadingId = reading.Id,
                ItemType = itemType,
                Description = $"{serviceType.Name} ({reading.Consumption} {price.UnitName})",
                Quantity = reading.Consumption,
                UnitPrice = price.UnitPrice,
                Amount = amount,
                CreatedAt = now
            });

            reading.Status = MeterReadingStatus.UsedInInvoice;
            reading.UpdatedAt = now;
        }

        foreach (var code in FixedServices)
        {
            if (!serviceTypeByCode.TryGetValue(code, out var serviceType))
            {
                continue;
            }

            var price = GetEffectivePriceOrThrow(prices, serviceType.Id, code);
            var itemType = code == BillingServiceCode.Wifi ? InvoiceItemType.Wifi : InvoiceItemType.Garbage;

            invoice.ServiceAmount += price.UnitPrice;
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ServiceTypeId = serviceType.Id,
                ItemType = itemType,
                Description = serviceType.Name,
                Quantity = 1,
                UnitPrice = price.UnitPrice,
                Amount = price.UnitPrice,
                CreatedAt = now
            });
        }

        invoice.TotalAmount = invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount;
        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tổng tiền hóa đơn không được âm.");
        }

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

#endif

    public async Task<List<InvoiceResponse>> GetLandlordInvoicesAsync(
        Guid landlordUserId,
        string? status = null,
        string? search = null,
        Guid? contractId = null,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: null, contractId: null, cancellationToken: cancellationToken);

        var query = BuildInvoiceQuery()
            .Where(x => x.LandlordUserId == landlordUserId);

        if (contractId.HasValue)
        {
            query = query.Where(x => x.ContractId == contractId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus) &&
            Enum.IsDefined(parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.InvoiceNo.Contains(keyword) ||
                                     x.RoomId.ToString().Contains(keyword) ||
                                     x.TenantUserId.ToString().Contains(keyword));
        }

        var invoices = await query
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToInvoiceResponse).ToList();
    }

    public async Task<InvoiceResponse> GetLandlordInvoiceAsync(
        Guid landlordUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền xem hóa đơn này.");
        }

        return ToInvoiceResponse(invoice);
    }

    public async Task<InvoiceResponse> IssueInvoiceAsync(
        Guid landlordUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await context.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền phát hành hóa đơn này.");
        }

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chỉ có thể phát hành hóa đơn nháp (Draft).");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssueDate = DateOnly.FromDateTime(now.UtcDateTime);
        invoice.SentAt = now;
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceResponse> CancelInvoiceAsync(
        Guid landlordUserId,
        Guid invoiceId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var invoice = await context.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền hủy hóa đơn này.");
        }

        if (invoice.Status == InvoiceStatus.Paid ||
            invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chỉ có thể hủy hóa đơn chưa thanh toán.");
        }

        var now = DateTimeOffset.UtcNow;

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = now;
        invoice.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<List<InvoiceResponse>> GetMyInvoicesAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: null, contractId: null, cancellationToken: cancellationToken);

        var invoices = await BuildInvoiceQuery()
            .Where(x => x.TenantUserId == tenantUserId &&
                        x.Status != InvoiceStatus.Draft)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToInvoiceResponse).ToList();
    }

    public async Task<List<InvoiceResponse>> GetMyContractInvoicesAsync(
        Guid tenantUserId,
        Guid contractId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: null, invoiceId: null, contractId: contractId, cancellationToken: cancellationToken);

        var query = BuildInvoiceQuery()
            .Where(x => x.ContractId == contractId &&
                        x.Status != InvoiceStatus.Draft);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus) &&
            Enum.IsDefined(parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        var invoices = await query
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ToListAsync(cancellationToken);

        var visibleInvoices = new List<Invoice>();
        foreach (var invoice in invoices)
        {
            if (await CanTenantViewInvoiceAsync(invoice, tenantUserId, cancellationToken))
            {
                visibleInvoices.Add(invoice);
            }
        }

        return visibleInvoices.Select(ToInvoiceResponse).ToList();
    }

    public async Task<InvoiceResponse> GetMyInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: null, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (!await CanTenantViewInvoiceAsync(invoice, tenantUserId, cancellationToken))
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền xem hóa đơn này.");
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");
        }

        return ToInvoiceResponse(invoice);
    }

    public async Task<InvoiceResponse> PayInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var invoice = await context.Invoices
            .Include(x => x.Payment)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (invoice.TenantUserId != tenantUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền thanh toán hóa đơn này.");
        }

        if (invoice.Status == InvoiceStatus.Paid || invoice.Payment is not null)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Hóa đơn đã được thanh toán.");
        }

        if (invoice.Status != InvoiceStatus.Issued &&
            invoice.Status != InvoiceStatus.Overdue)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chỉ có thể thanh toán hóa đơn đã phát hành.");
        }

        var paymentResult = await walletPaymentService.PayInvoiceAsync(
            invoice.Id,
            invoice.TenantUserId,
            invoice.LandlordUserId,
            invoice.TotalAmount,
            cancellationToken);

        if (!paymentResult.Success || paymentResult.TransferGroupId is null)
        {
            throw new BadRequestException(
                ErrorCodes.WalletPaymentFailed,
                paymentResult.ErrorMessage ?? "Thanh toán ví thất bại.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        context.InvoicePayments.Add(new InvoicePayment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            TenantUserId = invoice.TenantUserId,
            LandlordUserId = invoice.LandlordUserId,
            Amount = invoice.TotalAmount,
            WalletTransferGroupId = paymentResult.TransferGroupId.Value,
            Status = InvoicePaymentStatus.Succeeded,
            PaidAt = now,
            CreatedAt = now
        });

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceResponse> GenerateInvoiceWithReadingsAsync(
        Guid landlordUserId,
        GenerateInvoiceWithReadingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BillingPeriodEnd < request.BillingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Kỳ hóa đơn không hợp lệ.");
        }

        if (request.BillingPeriodStart.Year != request.BillingPeriodEnd.Year ||
            request.BillingPeriodStart.Month != request.BillingPeriodEnd.Month)
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Kỳ hóa đơn phải nằm trong cùng một tháng.");
        }

        if (request.DiscountAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Số tiền giảm trừ không được âm.");
        }

        if (request.MeterReadings is null)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Danh sách chỉ số dịch vụ không hợp lệ.");
        }

        var contract = await GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);
        var termsEffectiveOn = new DateOnly(
            request.BillingPeriodStart.Year,
            request.BillingPeriodStart.Month,
            DateTime.DaysInMonth(request.BillingPeriodStart.Year, request.BillingPeriodStart.Month));
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            termsEffectiveOn,
            cancellationToken);
        var billingPeriod = ResolveBillingPeriodWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            request.BillingPeriodStart);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (billingPeriod.Start > today || billingPeriod.End > today)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await context.Invoices.AnyAsync(
            x => x.ContractId == request.ContractId &&
                 x.BillingPeriodStart == billingPeriod.Start &&
                 x.BillingPeriodEnd == billingPeriod.End &&
                 x.Status != InvoiceStatus.Cancelled,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var serviceTypeById = serviceTypes.ToDictionary(x => x.Id);

        var prices = await GetEffectivePricesAsync(
            contract.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            billingPeriod.Start,
            cancellationToken);

        var sequenceBlockReason = await GetInvoiceGenerationBlockReasonAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriod,
            billingPeriodEndOverride: null,
            cancellationToken);
        if (sequenceBlockReason is not null)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, sequenceBlockReason);
        }

        var duplicatedInputService = request.MeterReadings
            .Select(x => x.ServiceTypeId)
            .GroupBy(x => x)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatedInputService is not null)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Một dịch vụ chỉ số bị nhập trùng trong cùng hóa đơn.");
        }

        var meterReadingByServiceType = request.MeterReadings.ToDictionary(x => x.ServiceTypeId);
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
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Vui lòng nhập chỉ số cho dịch vụ {missingServiceType.Name} trước khi tạo hóa đơn.");
            }
        }

        var meteredInputs = new List<ResolvedMeterReadingInput>();
        foreach (var input in request.MeterReadings)
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
                x => x.ContractId == contract.Id &&
                     x.ServiceTypeId == serviceType.Id &&
                     x.BillingPeriodStart <= billingPeriod.End &&
                     x.BillingPeriodEnd >= billingPeriod.Start &&
                     x.InvoiceItems.Any(item => item.Invoice.Status != InvoiceStatus.Cancelled),
                cancellationToken);

            if (overlapping)
            {
                throw new ConflictException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Kỳ ghi chỉ số của dịch vụ {serviceType.Name} bị trùng hoặc chồng lấn với bản ghi đã có.");
            }

            var latestReading = await context.MeterReadings
                .AsNoTracking()
                .Where(x => x.ContractId == contract.Id &&
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

            meteredInputs.Add(new ResolvedMeterReadingInput(
                serviceType,
                price,
                previousReading.Value,
                input.CurrentReading,
                input.ProofImageObjectKey));
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(billingPeriod.End, contract.PaymentDay);
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            billingPeriod.Start,
            cancellationToken);
        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contract.Id,
            contract.TenantUserId,
            billingPeriod.Start,
            cancellationToken);
        var rentAmount = CalculatePeriodAmount(monthlyRent, billingPeriod);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            RoomId = contract.RoomId,
            TenantUserId = effectiveTenant.UserId,
            LandlordUserId = contract.LandlordUserId,
            InvoiceNo = GenerateInvoiceNo(),
            BillingPeriodStart = billingPeriod.Start,
            BillingPeriodEnd = billingPeriod.End,
            DueDate = dueDate,
            RentAmount = rentAmount,
            DiscountAmount = request.DiscountAmount,
            Status = InvoiceStatus.Draft,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        invoice.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            ItemType = InvoiceItemType.Rent,
            Description = BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
            Quantity = GetPeriodQuantity(billingPeriod),
            UnitPrice = monthlyRent,
            Amount = rentAmount,
            CreatedAt = now
        });

        foreach (var input in meteredInputs)
        {
            var consumption = input.CurrentReading - input.PreviousReading;
            var amount = RoundMoney(consumption * input.Price.UnitPrice);
            var reading = new MeterReading
            {
                Id = Guid.NewGuid(),
                RoomId = contract.RoomId,
                ContractId = contract.Id,
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

        var occupantCount = await GetActiveOccupantCountAsync(contract.Id, billingPeriod, cancellationToken);
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
            var serviceAmount = RoundMoney(price.UnitPrice * quantity);
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

        invoice.TotalAmount = RoundMoney(invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount);
        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tổng tiền hóa đơn không được âm.");
        }

        context.Invoices.Add(invoice);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsInvoicePeriodUniqueViolation(ex))
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceResponse> CreateNextTerminationInvoiceAsync(
        Guid landlordUserId,
        Guid contractId,
        CreateTerminationInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractReadService.GetContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");
        if (!contract.TerminationDate.HasValue)
        {
            throw new ConflictException(
                ErrorCodes.FinalInvoiceNotAllowed,
                "Hợp đồng chưa có ngày chấm dứt.");
        }

        await GetOwnedTerminationBillingContractAsync(
            landlordUserId,
            contractId,
            contract.TerminationDate.Value,
            allowActiveContract: false,
            cancellationToken);

        return await CreateFinalInvoiceForTerminationAsync(
            landlordUserId,
            contractId,
            contract.TerminationDate.Value,
            request.DiscountAmount,
            request.Note,
            request.MeterReadings,
            cancellationToken);
    }

    public async Task<InvoiceResponse> CreateFinalInvoiceForTerminationAsync(
        Guid landlordUserId,
        Guid contractId,
        DateOnly terminationDate,
        decimal discountAmount,
        string? note,
        IReadOnlyCollection<MeterReadingInput> meterReadings,
        CancellationToken cancellationToken = default)
    {
        if (discountAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Số tiền giảm trừ không được âm.");
        }

        if (meterReadings is null)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Danh sách chỉ số dịch vụ không hợp lệ.");
        }

        var contract = await GetOwnedTerminationBillingContractAsync(
            landlordUserId,
            contractId,
            terminationDate,
            allowActiveContract: true,
            cancellationToken);
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            terminationDate,
            cancellationToken);
        DateOnly billingPeriodReferenceDate = terminationDate;
        if (contract.Status == RentalContractStatus.Cancelled)
        {
            DateOnly? latestBilledThrough = await context.Invoices.AsNoTracking()
                .Where(x => x.ContractId == contractId && x.Status != InvoiceStatus.Cancelled)
                .OrderByDescending(x => x.BillingPeriodEnd)
                .Select(x => (DateOnly?)x.BillingPeriodEnd)
                .FirstOrDefaultAsync(cancellationToken);
            billingPeriodReferenceDate = latestBilledThrough?.AddDays(1) ?? effectiveTerms.StartDate;
            if (billingPeriodReferenceDate > terminationDate)
            {
                throw new ConflictException(
                    ErrorCodes.FinalInvoiceNotAllowed,
                    "Hợp đồng đã được tạo đủ hóa đơn đến ngày chấm dứt.");
            }
        }

        var billingPeriod = ResolveBillingPeriodWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriodReferenceDate,
            terminationDate);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (billingPeriod.Start > today || billingPeriod.End > today)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await context.Invoices.AnyAsync(
            x => x.ContractId == contractId &&
                 x.BillingPeriodStart == billingPeriod.Start &&
                 x.BillingPeriodEnd == billingPeriod.End &&
                 x.Status != InvoiceStatus.Cancelled,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        var sequenceBlockReason = await GetInvoiceGenerationBlockReasonAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriod,
            terminationDate,
            cancellationToken);
        if (sequenceBlockReason is not null)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, sequenceBlockReason);
        }

        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var serviceTypeById = serviceTypes.ToDictionary(x => x.Id);

        var prices = await GetEffectivePricesAsync(
            contract.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            billingPeriod.Start,
            cancellationToken);

        var duplicatedInputService = meterReadings
            .Select(x => x.ServiceTypeId)
            .GroupBy(x => x)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatedInputService is not null)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Một dịch vụ chỉ số bị nhập trùng trong cùng hóa đơn.");
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
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Vui lòng nhập chỉ số cho dịch vụ {missingServiceType.Name} trước khi tạo hóa đơn kỳ cuối.");
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
                x => x.ContractId == contract.Id &&
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
                .Where(x => x.ContractId == contract.Id &&
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

            meteredInputs.Add(new ResolvedMeterReadingInput(
                serviceType,
                price,
                previousReading.Value,
                input.CurrentReading,
                input.ProofImageObjectKey));
        }

        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(billingPeriod.End, contract.PaymentDay);
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            billingPeriod.Start,
            cancellationToken);
        DateOnly tenantEffectiveOn = billingPeriod.End == terminationDate
            ? terminationDate
            : billingPeriod.Start;
        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contract.Id,
            contract.TenantUserId,
            tenantEffectiveOn,
            cancellationToken);
        var rentAmount = CalculatePeriodAmount(monthlyRent, billingPeriod);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            RoomId = contract.RoomId,
            TenantUserId = effectiveTenant.UserId,
            LandlordUserId = contract.LandlordUserId,
            InvoiceNo = GenerateInvoiceNo(),
            BillingPeriodStart = billingPeriod.Start,
            BillingPeriodEnd = billingPeriod.End,
            DueDate = dueDate,
            RentAmount = rentAmount,
            DiscountAmount = discountAmount,
            Status = InvoiceStatus.Issued,
            IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
            Note = note,
            CreatedAt = now,
            UpdatedAt = now
        };

        invoice.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            ItemType = InvoiceItemType.Rent,
            Description = BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
            Quantity = GetPeriodQuantity(billingPeriod),
            UnitPrice = monthlyRent,
            Amount = rentAmount,
            CreatedAt = now
        });

        foreach (var input in meteredInputs)
        {
            var consumption = input.CurrentReading - input.PreviousReading;
            var amount = RoundMoney(consumption * input.Price.UnitPrice);
            var reading = new MeterReading
            {
                Id = Guid.NewGuid(),
                RoomId = contract.RoomId,
                ContractId = contract.Id,
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

        var occupantCount = await GetActiveOccupantCountAsync(contract.Id, billingPeriod, cancellationToken);
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
            var serviceAmount = RoundMoney(price.UnitPrice * quantity);
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

        invoice.TotalAmount = RoundMoney(invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount);
        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tổng tiền hóa đơn không được âm.");
        }

        context.Invoices.Add(invoice);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsInvoicePeriodUniqueViolation(ex))
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

#if false
    private async Task<InvoiceResponse> GenerateInvoiceWithReadingsLegacyAsync(
        Guid landlordUserId,
        GenerateInvoiceWithReadingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Validate input cơ bản ──────────────────────────────────────────
        if (request.BillingPeriodEnd < request.BillingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Kỳ hóa đơn không hợp lệ.");
        }

        if (!IsFullCalendarMonth(request.BillingPeriodStart, request.BillingPeriodEnd))
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Hóa đơn tháng phải bắt đầu ngày 01 và kết thúc vào ngày cuối cùng của cùng tháng.");
        }

        if (request.DiscountAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Số tiền giảm trừ không được âm.");
        }

        // ── 2. Kiểm tra contract + quyền sở hữu ──────────────────────────────
        var contract = await GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);

        // ── 3. Kiểm tra duplicate invoice (loại trừ Cancelled) ────────────────
        var duplicate = await context.Invoices.AnyAsync(
            x => x.ContractId == request.ContractId &&
                 x.BillingPeriodStart == request.BillingPeriodStart &&
                 x.BillingPeriodEnd == request.BillingPeriodEnd &&
                 x.Status != InvoiceStatus.Cancelled,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        // ── 4. Lấy danh sách service types và bảng giá hiệu lực ──────────────
        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var serviceTypeByCode = serviceTypes.ToDictionary(x => x.Code);

        var prices = await GetEffectivePricesAsync(
            contract.RoomingHouseId,
            serviceTypes.Select(x => x.Id).ToList(),
            request.BillingPeriodEnd,
            cancellationToken);

        // ── 5. Validate từng meter reading đầu vào ────────────────────────────
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var input in request.MeterReadings)
        {
            var code = ParseServiceCode(input.ServiceCode);

            if (!MeteredServices.Contains(code))
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Dịch vụ '{input.ServiceCode}' không phải dịch vụ đo chỉ số.");
            }

            if (input.PreviousReading < 0)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số không được âm cho dịch vụ {input.ServiceCode}.");
            }

            if (input.CurrentReading < input.PreviousReading)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số cuối kỳ phải lớn hơn hoặc bằng chỉ số đầu kỳ cho dịch vụ {input.ServiceCode}.");
            }

            if (request.BillingPeriodStart > today || request.BillingPeriodEnd > today)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    "Kỳ ghi chỉ số không được nằm trong tương lai.");
            }

            if (request.BillingPeriodStart < contract.StartDate ||
                request.BillingPeriodEnd > contract.EndDate)
            {
                throw new BadRequestException(
                    ErrorCodes.MeterReadingInvalid,
                    "Kỳ ghi chỉ số phải nằm trong thời hạn hợp đồng.");
            }
        }

        // ── 6. Kiểm tra duplicate meter reading trong DB ──────────────────────
        foreach (var input in request.MeterReadings)
        {
            var code = ParseServiceCode(input.ServiceCode);
            if (!serviceTypeByCode.TryGetValue(code, out var st)) continue;

            var alreadyExists = await context.MeterReadings.AnyAsync(
                x => x.ContractId == contract.Id &&
                     x.ServiceTypeId == st.Id &&
                     x.BillingPeriodStart == request.BillingPeriodStart &&
                     x.BillingPeriodEnd == request.BillingPeriodEnd,
                cancellationToken);

            if (alreadyExists)
            {
                throw new ConflictException(
                    ErrorCodes.MeterReadingInvalid,
                    $"Chỉ số dịch vụ '{input.ServiceCode}' trong kỳ này đã tồn tại. Vui lòng kiểm tra lại.");
            }
        }

        // ── 7. Bắt đầu transaction ────────────────────────────────────────────
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(request.BillingPeriodEnd, contract.PaymentDay);

        // ── 8. Tạo Invoice entity ─────────────────────────────────────────────
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            RoomId = contract.RoomId,
            TenantUserId = contract.TenantUserId,
            LandlordUserId = contract.LandlordUserId,
            InvoiceNo = GenerateInvoiceNo(),
            BillingPeriodStart = request.BillingPeriodStart,
            BillingPeriodEnd = request.BillingPeriodEnd,
            DueDate = dueDate,
            RentAmount = contract.MonthlyRent,
            DiscountAmount = request.DiscountAmount,
            Status = InvoiceStatus.Draft,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Item: tiền thuê phòng
        invoice.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            ItemType = InvoiceItemType.Rent,
            Description = "Tiền thuê phòng",
            Quantity = 1,
            UnitPrice = contract.MonthlyRent,
            Amount = contract.MonthlyRent,
            CreatedAt = now
        });

        // ── 9. Tạo MeterReading + thêm item cho dịch vụ Metered ──────────────
        foreach (var input in request.MeterReadings)
        {
            var code = ParseServiceCode(input.ServiceCode);
            if (!serviceTypeByCode.TryGetValue(code, out var serviceType)) continue;

            // Bỏ qua nếu không có bảng giá (không lỗi)
            var price = prices
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefault(x => x.ServiceTypeId == serviceType.Id);
            if (price is null) continue;

            var consumption = input.CurrentReading - input.PreviousReading;
            var amount = consumption * price.UnitPrice;
            var itemType = code == BillingServiceCode.Electric
                ? InvoiceItemType.Electricity
                : InvoiceItemType.Water;

            // Tạo MeterReading với trạng thái UsedInInvoice ngay (atomic trong transaction)
            var reading = new MeterReading
            {
                Id = Guid.NewGuid(),
                RoomId = contract.RoomId,
                ContractId = contract.Id,
                ServiceTypeId = serviceType.Id,
                BillingPeriodStart = request.BillingPeriodStart,
                BillingPeriodEnd = request.BillingPeriodEnd,
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
                ServiceTypeId = serviceType.Id,
                MeterReadingId = reading.Id,
                ItemType = itemType,
                Description = $"{serviceType.Name} ({consumption} {price.UnitName})",
                Quantity = consumption,
                UnitPrice = price.UnitPrice,
                Amount = amount,
                CreatedAt = now
            });
        }

        // ── 10. Thêm item cho dịch vụ Fixed (bỏ qua nếu chưa có giá) ─────────
        foreach (var code in FixedServices)
        {
            if (!serviceTypeByCode.TryGetValue(code, out var serviceType)) continue;

            var price = prices
                .OrderByDescending(x => x.EffectiveFrom)
                .FirstOrDefault(x => x.ServiceTypeId == serviceType.Id);
            if (price is null) continue;

            var itemType = code == BillingServiceCode.Wifi
                ? InvoiceItemType.Wifi
                : InvoiceItemType.Garbage;

            invoice.ServiceAmount += price.UnitPrice;
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                ServiceTypeId = serviceType.Id,
                ItemType = itemType,
                Description = serviceType.Name,
                Quantity = 1,
                UnitPrice = price.UnitPrice,
                Amount = price.UnitPrice,
                CreatedAt = now
            });
        }

        // ── 11. Tính tổng và kiểm tra âm ─────────────────────────────────────
        invoice.TotalAmount = invoice.RentAmount
                            + invoice.UtilityAmount
                            + invoice.ServiceAmount
                            - invoice.DiscountAmount;

        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Tổng tiền hóa đơn không được âm.");
        }

        // ── 12. Lưu và commit ─────────────────────────────────────────────────
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

#endif

    private sealed record ResolvedMeterReadingInput(
        BillingServiceType ServiceType,
        RoomingHouseServicePrice Price,
        decimal PreviousReading,
        decimal CurrentReading,
        string? ProofImageObjectKey);

    private sealed record ResolvedBillingPeriod(
        DateOnly Start,
        DateOnly End,
        DateOnly MonthStart,
        DateOnly MonthEnd,
        int BillableDays,
        int DaysInMonth,
        bool IsFullMonth);

    private sealed record ResolvedInvoiceTenant(
        Guid UserId,
        string DisplayName,
        string Email);

    private sealed record ResolvedContractTerms(
        DateOnly StartDate,
        DateOnly EndDate);

    private async Task<ResolvedInvoiceTenant> ResolveEffectiveInvoiceTenantAsync(
        Guid contractId,
        Guid currentContractTenantUserId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var tenantChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null)
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.SortOrder,
                x.OldValue,
                x.NewValue,
                x.FieldName
            })
            .ToListAsync(cancellationToken);

        var mainTenantChanges = tenantChanges
            .Where(x => NormalizeAppendixFieldName(x.FieldName) == "maintenantuserid")
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.SortOrder)
            .ToList();

        var effectiveTenantUserId = currentContractTenantUserId;
        var latestAppliedChange = mainTenantChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.SortOrder)
            .FirstOrDefault();

        if (latestAppliedChange is not null &&
            TryExtractGuid(latestAppliedChange.NewValue, out var appliedTenantUserId))
        {
            effectiveTenantUserId = appliedTenantUserId;
        }
        else if (mainTenantChanges.Count > 0 &&
                 effectiveOn < mainTenantChanges[0].EffectiveDate &&
                 TryExtractGuid(mainTenantChanges[0].OldValue, out var oldTenantUserId))
        {
            effectiveTenantUserId = oldTenantUserId;
        }

        var tenant = await context.Users
            .AsNoTracking()
            .Where(x => x.Id == effectiveTenantUserId && x.DeletedAt == null)
            .Select(x => new ResolvedInvoiceTenant(x.Id, x.DisplayName, x.Email))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.NotFound,
                "Không tìm thấy người thuê chính hiện tại của hợp đồng.");

        return tenant;
    }

    private async Task<IReadOnlyDictionary<Guid, LatestMeterReadingResponse>> GetLatestReadingByServiceTypeAsync(
        Guid contractId,
        DateOnly? beforeBillingPeriodStart,
        CancellationToken cancellationToken)
    {
        var query = context.MeterReadings
            .AsNoTracking()
            .Where(x => x.ContractId == contractId &&
                        x.InvoiceItems.Any(item => item.Invoice.Status != InvoiceStatus.Cancelled));

        if (beforeBillingPeriodStart.HasValue)
        {
            query = query.Where(x => x.BillingPeriodEnd < beforeBillingPeriodStart.Value);
        }

        var readings = await query
            .Select(x => new
            {
                x.ServiceTypeId,
                x.BillingPeriodStart,
                x.BillingPeriodEnd,
                x.PreviousReading,
                x.CurrentReading,
                x.Consumption,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return readings
            .GroupBy(x => x.ServiceTypeId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var latest = group
                        .OrderByDescending(x => x.BillingPeriodEnd)
                        .ThenByDescending(x => x.CreatedAt)
                        .First();

                    return new LatestMeterReadingResponse(
                        latest.ServiceTypeId,
                        latest.BillingPeriodStart,
                        latest.BillingPeriodEnd,
                        latest.PreviousReading,
                        latest.CurrentReading,
                        latest.Consumption);
                });
    }

    private static ResolvedBillingPeriod ResolveBillingPeriodWithinContract(
        DateOnly contractStart,
        DateOnly contractEnd,
        DateOnly requestedMonth,
        DateOnly? billingPeriodEndOverride = null)
    {
        var monthStart = new DateOnly(requestedMonth.Year, requestedMonth.Month, 1);
        var monthEnd = new DateOnly(
            requestedMonth.Year,
            requestedMonth.Month,
            DateTime.DaysInMonth(requestedMonth.Year, requestedMonth.Month));
        var start = contractStart > monthStart ? contractStart : monthStart;
        var end = contractEnd < monthEnd ? contractEnd : monthEnd;
        if (billingPeriodEndOverride.HasValue && billingPeriodEndOverride.Value < end)
        {
            end = billingPeriodEndOverride.Value;
        }

        if (start > end)
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Tháng hóa đơn không nằm trong thời hạn hợp đồng.");
        }

        var billableDays = end.DayNumber - start.DayNumber + 1;
        var daysInMonth = monthEnd.DayNumber - monthStart.DayNumber + 1;
        var isFullMonth = start == monthStart && end == monthEnd;

        return new ResolvedBillingPeriod(
            start,
            end,
            monthStart,
            monthEnd,
            billableDays,
            daysInMonth,
            isFullMonth);
    }

    private async Task<string?> GetInvoiceGenerationBlockReasonAsync(
        Guid contractId,
        DateOnly contractStart,
        DateOnly contractEnd,
        ResolvedBillingPeriod billingPeriod,
        DateOnly? billingPeriodEndOverride,
        CancellationToken cancellationToken)
    {
        var latestInvoice = await context.Invoices
            .AsNoTracking()
            .Where(x => x.ContractId == contractId &&
                        x.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ThenByDescending(x => x.BillingPeriodStart)
            .Select(x => new
            {
                x.BillingPeriodStart,
                x.BillingPeriodEnd
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestInvoice is null)
        {
            var expectedFirstPeriod = ResolveBillingPeriodWithinContract(
                contractStart,
                contractEnd,
                contractStart);

            return IsSameBillingPeriod(expectedFirstPeriod, billingPeriod)
                ? null
                : BuildExpectedInvoicePeriodMessage(expectedFirstPeriod, billingPeriod);
        }

        if (latestInvoice.BillingPeriodEnd >= billingPeriod.Start)
        {
            return $"Đã có hóa đơn kỳ {FormatPeriod(latestInvoice.BillingPeriodStart, latestInvoice.BillingPeriodEnd)}. Vui lòng hủy hóa đơn kỳ này hoặc các kỳ sau trước khi tạo lại kỳ {FormatPeriod(billingPeriod.Start, billingPeriod.End)}.";
        }

        var expectedStart = latestInvoice.BillingPeriodEnd.AddDays(1);
        if (expectedStart > contractEnd)
        {
            return "Hợp đồng đã có hóa đơn đến hết thời hạn.";
        }

        var expectedPeriod = ResolveBillingPeriodWithinContract(
            contractStart,
            contractEnd,
            expectedStart,
            billingPeriodEndOverride);

        return IsSameBillingPeriod(expectedPeriod, billingPeriod)
            ? null
            : BuildExpectedInvoicePeriodMessage(expectedPeriod, billingPeriod);
    }

    private static bool IsSameBillingPeriod(ResolvedBillingPeriod left, ResolvedBillingPeriod right)
    {
        return left.Start == right.Start && left.End == right.End;
    }

    private static string BuildExpectedInvoicePeriodMessage(
        ResolvedBillingPeriod expectedPeriod,
        ResolvedBillingPeriod requestedPeriod)
    {
        return $"Vui lòng tạo hóa đơn kỳ {FormatPeriod(expectedPeriod.Start, expectedPeriod.End)} trước khi tạo kỳ {FormatPeriod(requestedPeriod.Start, requestedPeriod.End)}.";
    }

    private static string FormatPeriod(DateOnly start, DateOnly end)
    {
        return $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";
    }

    private static decimal CalculatePeriodAmount(decimal monthlyAmount, ResolvedBillingPeriod period)
    {
        if (period.IsFullMonth)
        {
            return monthlyAmount;
        }

        return RoundMoney(monthlyAmount * period.BillableDays / period.DaysInMonth);
    }

    private static decimal GetPeriodQuantity(ResolvedBillingPeriod period)
    {
        if (period.IsFullMonth)
        {
            return 1;
        }

        return Math.Round((decimal)period.BillableDays / period.DaysInMonth, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<int> GetActiveOccupantCountAsync(
        Guid contractId,
        ResolvedBillingPeriod period,
        CancellationToken cancellationToken)
    {
        var count = await context.ContractOccupants.CountAsync(
            x => x.RentalContractId == contractId &&
                 (x.Status == ContractOccupantStatus.Active ||
                  x.Status == ContractOccupantStatus.PendingMoveIn ||
                  x.Status == ContractOccupantStatus.MoveOut) &&
                 x.MoveInDate <= period.End &&
                 (x.MoveOutDate == null || x.MoveOutDate >= period.Start),
            cancellationToken);

        return Math.Max(count, 1);
    }

    private static decimal GetFixedServiceQuantity(
        PricingUnit pricingUnit,
        ResolvedBillingPeriod period,
        int occupantCount)
    {
        var periodQuantity = GetPeriodQuantity(period);
        return pricingUnit == PricingUnit.PerPersonPerMonth
            ? occupantCount * periodQuantity
            : periodQuantity;
    }

    private static string BuildPeriodDescription(string description, ResolvedBillingPeriod period)
    {
        return period.IsFullMonth
            ? description
            : $"{description} ({period.BillableDays}/{period.DaysInMonth} ngay)";
    }

    private static string BuildFixedServiceDescription(
        string description,
        PricingUnit pricingUnit,
        ResolvedBillingPeriod period,
        int occupantCount)
    {
        var baseDescription = BuildPeriodDescription(description, period);
        return pricingUnit == PricingUnit.PerPersonPerMonth
            ? $"{baseDescription} ({occupantCount} nguoi)"
            : baseDescription;
    }

    private static decimal RoundMoney(decimal amount)
    {
        return Math.Round(amount, 0, MidpointRounding.AwayFromZero);
    }

    private async Task<ResolvedContractTerms> ResolveEffectiveContractTermsAsync(
        Guid contractId,
        DateOnly currentContractStartDate,
        DateOnly currentContractEndDate,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var termChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null)
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.SortOrder,
                x.OldValue,
                x.NewValue,
                x.FieldName
            })
            .ToListAsync(cancellationToken);

        var startDate = ResolveEffectiveAppendixDate(
            termChanges
                .Where(x => NormalizeAppendixFieldName(x.FieldName) == "startdate")
                .Select(x => new AppendixDateChange(x.EffectiveDate, x.SortOrder, x.OldValue, x.NewValue)),
            currentContractStartDate,
            effectiveOn);
        var endDate = ResolveEffectiveAppendixDate(
            termChanges
                .Where(x => NormalizeAppendixFieldName(x.FieldName) == "enddate")
                .Select(x => new AppendixDateChange(x.EffectiveDate, x.SortOrder, x.OldValue, x.NewValue)),
            currentContractEndDate,
            effectiveOn);

        return new ResolvedContractTerms(startDate, endDate);
    }

    private sealed record AppendixDateChange(
        DateOnly EffectiveDate,
        int SortOrder,
        string? OldValue,
        string? NewValue);

    private static DateOnly ResolveEffectiveAppendixDate(
        IEnumerable<AppendixDateChange> changes,
        DateOnly currentValue,
        DateOnly effectiveOn)
    {
        var orderedChanges = changes
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.SortOrder)
            .ToList();

        if (orderedChanges.Count == 0)
        {
            return currentValue;
        }

        var latestAppliedChange = orderedChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.SortOrder)
            .FirstOrDefault();
        if (latestAppliedChange is not null &&
            TryParseAppendixDate(latestAppliedChange.NewValue, out var appliedValue))
        {
            return appliedValue;
        }

        var firstChange = orderedChanges[0];
        if (effectiveOn < firstChange.EffectiveDate &&
            TryParseAppendixDate(firstChange.OldValue, out var oldValue))
        {
            return oldValue;
        }

        return currentValue;
    }

    private async Task<decimal> ResolveEffectiveMonthlyRentAsync(
        Guid contractId,
        decimal currentContractMonthlyRent,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var rentChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null &&
                        x.FieldName.ToLower() == "monthlyrent")
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.OldValue,
                x.NewValue
            })
            .OrderBy(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);

        if (rentChanges.Count == 0)
        {
            return currentContractMonthlyRent;
        }

        var latestAppliedChange = rentChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefault();
        if (latestAppliedChange is not null &&
            TryParseAppendixDecimal(latestAppliedChange.NewValue, out var appliedRent))
        {
            return appliedRent;
        }

        var firstChange = rentChanges[0];
        if (effectiveOn < firstChange.EffectiveDate &&
            TryParseAppendixDecimal(firstChange.OldValue, out var oldRent))
        {
            return oldRent;
        }

        return currentContractMonthlyRent;
    }

    private static bool TryParseAppendixDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseAppendixDate(string? value, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        return DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ||
               DateOnly.TryParse(normalized, out result);
    }

    private static string NormalizeAppendixFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool TryExtractGuid(string? value, out Guid result)
    {
        result = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out result))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return Guid.TryParse(root.GetString(), out result);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var propertyName in new[] { "id", "userId", "tenantUserId", "mainTenantUserId", "value" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.GetString(), out result))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private async Task<BillingContractSnapshot> GetOwnedActiveContractAsync(
        Guid landlordUserId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var contract = await contractReadService.GetActiveContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng đang hoạt động.");

        if (contract.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền truy cập hợp đồng này.");
        }

        return contract;
    }

    private async Task<BillingContractSnapshot> GetOwnedTerminationBillingContractAsync(
        Guid landlordUserId,
        Guid contractId,
        DateOnly terminationDate,
        bool allowActiveContract,
        CancellationToken cancellationToken)
    {
        var contract = await contractReadService.GetContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");

        if (contract.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền truy cập hợp đồng này.");
        }

        if (allowActiveContract && contract.Status == RentalContractStatus.Active)
        {
            return contract;
        }

        if (contract.Status != RentalContractStatus.Cancelled ||
            contract.TerminationType != ContractTerminationType.TenantUnilateral ||
            !contract.TerminationDate.HasValue ||
            contract.TerminationDate.Value != terminationDate ||
            terminationDate < contract.StartDate)
        {
            throw new ConflictException(
                ErrorCodes.FinalInvoiceNotAllowed,
                "Hợp đồng không thuộc trường hợp được tạo hóa đơn sau khi chấm dứt.");
        }

        return contract;
    }

    private async Task EnsureRoomingHouseOwnerAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CancellationToken cancellationToken)
    {
        var ownsHouse = await context.RoomingHouses.AnyAsync(
            x => x.Id == roomingHouseId &&
                 x.LandlordUserId == landlordUserId &&
                 x.DeletedAt == null,
            cancellationToken);

        if (!ownsHouse)
        {
            throw new NotFoundException(ErrorCodes.HouseNotFound, "Không tìm thấy khu trọ hoặc bạn không có quyền truy cập.");
        }
    }

    private async Task<BillingServiceType> GetServiceTypeAsync(
        Guid serviceTypeId,
        CancellationToken cancellationToken)
    {
        return await context.BillingServiceTypes
            .FirstOrDefaultAsync(x => x.Id == serviceTypeId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.BillingServiceInvalid, "Không tìm thấy loại dịch vụ.");
    }

    private async Task<List<RoomingHouseServicePrice>> GetEffectivePricesAsync(
        Guid roomingHouseId,
        List<Guid> serviceTypeIds,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        return await context.RoomingHouseServicePrices
            .AsNoTracking()
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        serviceTypeIds.Contains(x.ServiceTypeId) &&
                        x.EffectiveFrom <= effectiveOn &&
                        (x.EffectiveTo == null || x.EffectiveTo >= effectiveOn))
            .ToListAsync(cancellationToken);
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

    private async Task<InvoiceResponse> GetInvoiceResponseAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        return ToInvoiceResponse(invoice);
    }

    private async Task<bool> CanTenantViewInvoiceAsync(
        Invoice invoice,
        Guid tenantUserId,
        CancellationToken cancellationToken)
    {
        if (invoice.TenantUserId == tenantUserId)
        {
            return true;
        }

        return await context.ContractOccupants.AnyAsync(
            x => x.RentalContractId == invoice.ContractId &&
                 x.UserId == tenantUserId &&
                 x.Status != ContractOccupantStatus.Voided &&
                 x.MoveInDate <= invoice.BillingPeriodEnd &&
                 (x.MoveOutDate == null || x.MoveOutDate >= invoice.BillingPeriodStart),
            cancellationToken);
    }

    private async Task MarkOverdueInvoicesAsync(
        Guid? landlordUserId,
        Guid? tenantUserId,
        Guid? invoiceId,
        Guid? contractId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = context.Invoices
            .Where(x => x.DueDate < today &&
                        x.Status == InvoiceStatus.Issued);

        if (landlordUserId.HasValue)
        {
            query = query.Where(x => x.LandlordUserId == landlordUserId.Value);
        }

        if (tenantUserId.HasValue)
        {
            query = query.Where(x => x.TenantUserId == tenantUserId.Value);
        }

        if (invoiceId.HasValue)
        {
            query = query.Where(x => x.Id == invoiceId.Value);
        }

        if (contractId.HasValue)
        {
            query = query.Where(x => x.ContractId == contractId.Value);
        }

        var overdueInvoices = await query.ToListAsync(cancellationToken);
        if (overdueInvoices.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var invoice in overdueInvoices)
        {
            invoice.Status = InvoiceStatus.Overdue;
            invoice.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Invoice> BuildInvoiceQuery()
    {
        return context.Invoices
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.Tenant)
            .Include(x => x.Items)
                .ThenInclude(x => x.ServiceType)
            .Include(x => x.Payment);
    }

    private static PricingUnit ParsePricingUnit(string value)
    {
        if (string.Equals(value, "MeterBased", StringComparison.OrdinalIgnoreCase))
        {
            return PricingUnit.MeterReading;
        }

        if (string.Equals(value, "Metered", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "MeterReading", StringComparison.OrdinalIgnoreCase))
        {
            return PricingUnit.MeterReading;
        }

        if (string.Equals(value, "PerMonth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Fixed", StringComparison.OrdinalIgnoreCase))
        {
            return PricingUnit.PerMonth;
        }

        if (string.Equals(value, "PerPerson", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "PerPersonPerMonth", StringComparison.OrdinalIgnoreCase))
        {
            return PricingUnit.PerPersonPerMonth;
        }

        if (!Enum.TryParse<PricingUnit>(value, true, out var pricingUnit) ||
            !Enum.IsDefined(pricingUnit))
        {
            throw new BadRequestException(ErrorCodes.BillingPriceInvalid, "Phương thức tính giá không hợp lệ.");
        }

        return pricingUnit;
    }

    private static void ValidatePricingUnitForServiceType(BillingServiceType serviceType, PricingUnit pricingUnit)
    {
        if (pricingUnit != PricingUnit.MeterReading)
        {
            return;
        }

        if (!serviceType.SupportsMeterReading || string.IsNullOrWhiteSpace(serviceType.MeterUnitName))
        {
            throw new BadRequestException(
                ErrorCodes.BillingPriceInvalid,
                $"Dịch vụ {serviceType.Name} không hỗ trợ tính tiền theo chỉ số.");
        }
    }

    private static string GetDisplayUnitName(RoomingHouseServicePrice price, BillingServiceType serviceType)
    {
        return price.PricingUnit switch
        {
            PricingUnit.MeterReading => serviceType.MeterUnitName ?? string.Empty,
            PricingUnit.PerMonth => "tháng",
            PricingUnit.PerPersonPerMonth => "người/tháng",
            _ => string.Empty
        };
    }

    private static DateOnly GetNextBillingPeriodStart(DateOnly currentDate)
    {
        var nextMonth = currentDate.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, 1);
    }

    private static DateOnly BuildDueDate(DateOnly billingPeriodEnd, int paymentDay)
    {
        var normalizedDay = Math.Clamp(paymentDay, 1, 28);
        var nextMonth = billingPeriodEnd.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, normalizedDay);
    }

    private static bool IsFullCalendarMonth(DateOnly start, DateOnly end)
    {
        return start.Day == 1 &&
               start.Year == end.Year &&
               start.Month == end.Month &&
               end.Day == DateTime.DaysInMonth(end.Year, end.Month);
    }

    private static string GenerateInvoiceNo()
    {
        return $"INV-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(100, 999)}";
    }

    private static bool IsInvoicePeriodUniqueViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString();
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString();

            if (!string.Equals(sqlState, "23505", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(constraintName) &&
                constraintName.Contains("invoices_contract_id_billing_period", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.Message.Contains("invoices_contract_id", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("billing_period", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ServicePriceResponse ToServicePriceResponse(RoomingHouseServicePrice price)
    {
        return new ServicePriceResponse(
            price.Id,
            price.RoomingHouseId,
            price.ServiceTypeId,
            price.ServiceType.Name,
            price.ServiceType.SupportsMeterReading,
            price.ServiceType.MeterUnitName,
            price.PricingUnit.ToString(),
            GetDisplayUnitName(price, price.ServiceType),
            price.UnitPrice,
            price.EffectiveFrom,
            price.EffectiveTo,
            price.IsActive,
            price.Note,
            price.CreatedAt,
            price.UpdatedAt);
    }

    private static BillingServiceTypeResponse ToBillingServiceTypeResponse(BillingServiceType serviceType)
    {
        return new BillingServiceTypeResponse(
            serviceType.Id,
            serviceType.Name,
            serviceType.SupportsMeterReading,
            serviceType.MeterUnitName,
            serviceType.IsActive);
    }

    private static InvoiceResponse ToInvoiceResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.ContractId,
            invoice.RoomId,
            invoice.Room.RoomNumber,
            invoice.Room.RoomingHouseId,
            invoice.Room.RoomingHouse.Name,
            invoice.TenantUserId,
            invoice.Tenant.DisplayName,
            invoice.Tenant.Email,
            invoice.LandlordUserId,
            invoice.InvoiceNo,
            invoice.BillingPeriodStart,
            invoice.BillingPeriodEnd,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.RentAmount,
            invoice.UtilityAmount,
            invoice.ServiceAmount,
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.Status.ToString(),
            invoice.Note,
            invoice.SentAt,
            invoice.PaidAt,
            invoice.Items.OrderBy(x => x.CreatedAt).Select(ToInvoiceItemResponse).ToList(),
            invoice.Payment is null ? null : ToInvoicePaymentResponse(invoice.Payment));
    }

    private static InvoiceItemResponse ToInvoiceItemResponse(InvoiceItem item)
    {
        return new InvoiceItemResponse(
            item.Id,
            item.ServiceTypeId,
            item.ServiceType?.Name,
            item.MeterReadingId,
            item.ItemType.ToString(),
            item.Description,
            item.Quantity,
            item.UnitPrice,
            item.Amount);
    }

    private static InvoicePaymentResponse ToInvoicePaymentResponse(InvoicePayment payment)
    {
        return new InvoicePaymentResponse(
            payment.Id,
            payment.InvoiceId,
            payment.Amount,
            payment.WalletTransferGroupId,
            payment.Status.ToString(),
            payment.PaidAt);
    }
}
