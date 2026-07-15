using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public class BillingService : IBillingService
{
    private readonly IAppDbContext context;
    private readonly IBillingContractReadService contractReadService;
    private readonly IInvoiceWalletPaymentService walletPaymentService;
    private readonly BillingPeriodResolver billingPeriodResolver;
    private readonly BillingInvoiceBuilder billingInvoiceBuilder;
    private readonly InvoiceQueryLoader invoiceQueryLoader;
    private readonly BillingContractContextResolver billingContractContextResolver;
    private readonly MeterReadingInputResolver meterReadingInputResolver;
    private readonly BillingWorkflowGuard billingWorkflowGuard;

    public BillingService(
        IAppDbContext context,
        IBillingContractReadService contractReadService,
        IInvoiceWalletPaymentService walletPaymentService,
        BillingPeriodResolver billingPeriodResolver,
        BillingInvoiceBuilder billingInvoiceBuilder,
        InvoiceQueryLoader invoiceQueryLoader,
        BillingContractContextResolver billingContractContextResolver,
        MeterReadingInputResolver meterReadingInputResolver,
        BillingWorkflowGuard billingWorkflowGuard)
    {
        this.context = context;
        this.contractReadService = contractReadService;
        this.walletPaymentService = walletPaymentService;
        this.billingPeriodResolver = billingPeriodResolver;
        this.billingInvoiceBuilder = billingInvoiceBuilder;
        this.invoiceQueryLoader = invoiceQueryLoader;
        this.billingContractContextResolver = billingContractContextResolver;
        this.meterReadingInputResolver = meterReadingInputResolver;
        this.billingWorkflowGuard = billingWorkflowGuard;
    }

    public async Task<List<BillingServiceTypeResponse>> GetBillingServiceTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var serviceTypes = await context.BillingServiceTypes
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return serviceTypes.Select(BillingResponseMapper.ToBillingServiceTypeResponse).ToList();
    }

    // =================================================================================================
    // ADMIN CRUD: BILLING SERVICE TYPES
    // =================================================================================================

    public async Task<PagedResult<AdminBillingServiceTypeResponse>> GetBillingServiceTypesAdminAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = context.BillingServiceTypes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(keyword));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminBillingServiceTypeResponse(
                x.Id,
                x.Name,
                x.SupportsMeterReading,
                x.MeterUnitName,
                x.IsActive,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminBillingServiceTypeResponse>
        {
            Items = items,
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminBillingServiceTypeResponse> GetBillingServiceTypeAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.BillingServiceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy loại dịch vụ.");

        return new AdminBillingServiceTypeResponse(entity.Id, entity.Name, entity.SupportsMeterReading, entity.MeterUnitName, entity.IsActive, entity.CreatedAt);
    }

    public async Task<AdminBillingServiceTypeResponse> CreateBillingServiceTypeAsync(CreateBillingServiceTypeRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var exists = await context.BillingServiceTypes.AnyAsync(x => x.Name == name, cancellationToken);
        if (exists)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Tên dịch vụ đã tồn tại.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new BillingServiceType
        {
            Name = name,
            MeterUnitName = request.MeterUnitName?.Trim(),
            SupportsMeterReading = request.SupportsMeterReading,
            IsActive = true,
            CreatedAt = now
        };

        context.BillingServiceTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new AdminBillingServiceTypeResponse(entity.Id, entity.Name, entity.SupportsMeterReading, entity.MeterUnitName, entity.IsActive, entity.CreatedAt);
    }

    public async Task<AdminBillingServiceTypeResponse> UpdateBillingServiceTypeAsync(Guid id, UpdateBillingServiceTypeRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await context.BillingServiceTypes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy loại dịch vụ.");

        var name = request.Name.Trim();
        if (entity.Name != name)
        {
            var exists = await context.BillingServiceTypes.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken);
            if (exists)
            {
                throw new ConflictException(ErrorCodes.ValidationError, "Tên dịch vụ đã tồn tại.");
            }
        }

        // Validate SupportsMeterReading change
        if (entity.SupportsMeterReading != request.SupportsMeterReading)
        {
            // Kiểm tra xem ServiceTypeId này đã được sử dụng ở RoomingHouseServicePrices với PricingUnit = MeterReading chưa
            var isUsedForMeterReading = await context.RoomingHouseServicePrices
                .AnyAsync(x => x.ServiceTypeId == id && x.PricingUnit == PricingUnit.MeterReading, cancellationToken);

            if (isUsedForMeterReading)
            {
                throw new BadRequestException(ErrorCodes.ValidationError, "Không thể thay đổi cờ hỗ trợ chốt đồng hồ vì dịch vụ này đã được thiết lập đo số lượng (MeterReading) tại một hoặc nhiều khu trọ.");
            }
        }

        entity.Name = name;
        entity.MeterUnitName = request.MeterUnitName?.Trim();
        entity.SupportsMeterReading = request.SupportsMeterReading;

        await context.SaveChangesAsync(cancellationToken);

        return new AdminBillingServiceTypeResponse(entity.Id, entity.Name, entity.SupportsMeterReading, entity.MeterUnitName, entity.IsActive, entity.CreatedAt);
    }

    public async Task ToggleBillingServiceTypeActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.BillingServiceTypes
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy loại dịch vụ.");

        entity.IsActive = !entity.IsActive;

        await context.SaveChangesAsync(cancellationToken);
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
            .WhereActiveForOccupiedOrReservedRoom()
            .Where(x => x.RoomId == roomId &&
                        x.Room.RoomingHouse.LandlordUserId == landlordUserId)
            .OrderByDescending(x => x.ActivatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.RentalContractNotFound,
                "Phòng này chưa có hợp đồng Active để tạo hóa đơn.");

        var today = BillingPeriodResolver.GetBusinessToday();
        var effectiveTerms = await billingContractContextResolver.ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            today,
            cancellationToken);
        var effectiveMonthlyRent = await billingContractContextResolver.ResolveEffectiveMonthlyRentAsync(
            contract.Id,
            contract.MonthlyRent,
            today,
            cancellationToken);
        var effectiveTenant = await billingContractContextResolver.ResolveEffectiveInvoiceTenantAsync(
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
            .WhereActiveForOccupiedOrReservedRoom()
            .Where(x => x.RoomId == roomId &&
                        x.Room.RoomingHouse.LandlordUserId == landlordUserId)
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
        var effectiveTerms = await billingContractContextResolver.ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            termsEffectiveOn,
            cancellationToken);
        var periodContext = await billingPeriodResolver.ResolveInvoicePeriodContextAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriodStart,
            billingPeriodEnd,
            cancellationToken);
        var billingPeriod = periodContext.BillingPeriod;
        var contractContext = await billingContractContextResolver.ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.MainTenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            billingPeriod.Start,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
        var rentAmount = BillingPeriodResolver.CalculatePeriodAmount(monthlyRent, billingPeriod);
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
        var occupantCount = contractContext.OccupantCount;
        var latestReadingByServiceType = await GetLatestReadingByServiceTypeAsync(
            contract.Id,
            billingPeriod.Start,
            cancellationToken);
        var generationBlockReason = periodContext.BlockReason;
        var missingPricesReason = GetMissingServicePricesBlockReason(prices, serviceTypes);
        if (missingPricesReason is not null)
        {
            generationBlockReason = missingPricesReason;
        }
        else if (BillingPeriodResolver.IsFutureBillingPeriod(billingPeriod))
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = billingInvoiceBuilder.BuildFixedServicePreviews(
            prices,
            serviceTypeById,
            billingPeriod,
            occupantCount);

        var meteredServices = billingInvoiceBuilder.BuildMeteredServicePreviews(
            prices,
            serviceTypeById,
            latestReadingByServiceType);

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
                BillingPeriodResolver.BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
                BillingPeriodResolver.GetPeriodQuantity(billingPeriod),
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
        await billingWorkflowGuard.GetOwnedTerminationBillingContractAsync(
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
        var effectiveTerms = await billingContractContextResolver.ResolveEffectiveContractTermsAsync(
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

        var periodContext = await billingPeriodResolver.ResolveInvoicePeriodContextAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            nextPeriodStart,
            terminationDate,
            cancellationToken);
        var billingPeriod = periodContext.BillingPeriod;
        DateOnly tenantEffectiveOn = billingPeriod.End == terminationDate
            ? terminationDate
            : billingPeriod.Start;
        var contractContext = await billingContractContextResolver.ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.MainTenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            tenantEffectiveOn,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
        var rentAmount = BillingPeriodResolver.CalculatePeriodAmount(monthlyRent, billingPeriod);
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
        var occupantCount = contractContext.OccupantCount;
        var latestReadingByServiceType = await GetLatestReadingByServiceTypeAsync(
            contract.Id,
            billingPeriod.Start,
            cancellationToken);
        var generationBlockReason = periodContext.BlockReason;
        var missingPricesReason = GetMissingServicePricesBlockReason(prices, serviceTypes);
        if (missingPricesReason is not null)
        {
            generationBlockReason = missingPricesReason;
        }
        else if (BillingPeriodResolver.IsFutureBillingPeriod(billingPeriod))
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = billingInvoiceBuilder.BuildFixedServicePreviews(
            prices,
            serviceTypeById,
            billingPeriod,
            occupantCount);

        var meteredServices = billingInvoiceBuilder.BuildMeteredServicePreviews(
            prices,
            serviceTypeById,
            latestReadingByServiceType);
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
                BillingPeriodResolver.BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
                BillingPeriodResolver.GetPeriodQuantity(billingPeriod),
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

    public async Task<List<InvoiceResponse>> GetLandlordInvoicesAsync(
        Guid landlordUserId,
        string? status = null,
        string? search = null,
        Guid? contractId = null,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: null, contractId: null, cancellationToken: cancellationToken);

        var query = invoiceQueryLoader.Query()
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

        return invoices.Select(BillingResponseMapper.ToInvoiceResponse).ToList();
    }

    public async Task<InvoiceResponse> GetLandlordInvoiceAsync(
        Guid landlordUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var invoice = await invoiceQueryLoader.Query()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        BillingWorkflowGuard.EnsureLandlordCanViewInvoice(invoice, landlordUserId);

        return BillingResponseMapper.ToInvoiceResponse(invoice);
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

        BillingWorkflowGuard.EnsureLandlordCanIssueInvoice(invoice, landlordUserId);

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssueDate = DateOnly.FromDateTime(now.UtcDateTime);
        invoice.SentAt = now;
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await invoiceQueryLoader.GetInvoiceResponseAsync(invoice.Id, cancellationToken);
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

        BillingWorkflowGuard.EnsureLandlordCanCancelInvoice(invoice, landlordUserId);

        var now = DateTimeOffset.UtcNow;

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = now;
        invoice.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return await invoiceQueryLoader.GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<List<InvoiceResponse>> GetMyInvoicesAsync(
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: null, contractId: null, cancellationToken: cancellationToken);

        var invoices = await invoiceQueryLoader.Query()
            .Where(x => x.TenantUserId == tenantUserId &&
                        x.Status != InvoiceStatus.Draft)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ToListAsync(cancellationToken);

        return invoices.Select(BillingResponseMapper.ToInvoiceResponse).ToList();
    }

    public async Task<List<InvoiceResponse>> GetMyContractInvoicesAsync(
        Guid tenantUserId,
        Guid contractId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: null, invoiceId: null, contractId: contractId, cancellationToken: cancellationToken);

        var query = invoiceQueryLoader.Query()
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
            if (await invoiceQueryLoader.CanTenantViewInvoiceAsync(invoice, tenantUserId, cancellationToken))
            {
                visibleInvoices.Add(invoice);
            }
        }

        return visibleInvoices.Select(BillingResponseMapper.ToInvoiceResponse).ToList();
    }

    public async Task<InvoiceResponse> GetMyInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: null, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var invoice = await invoiceQueryLoader.Query()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");

        if (!await invoiceQueryLoader.CanTenantViewInvoiceAsync(invoice, tenantUserId, cancellationToken))
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Bạn không có quyền xem hóa đơn này.");
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Không tìm thấy hóa đơn.");
        }

        return BillingResponseMapper.ToInvoiceResponse(invoice);
    }

    public async Task<InvoiceResponse> PayInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await invoiceQueryLoader.MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: invoiceId, contractId: null, cancellationToken: cancellationToken);

        var paymentResult = await walletPaymentService.PayInvoiceAsync(
            invoiceId,
            tenantUserId,
            cancellationToken);

        if (!paymentResult.Success)
        {
            throw new BadRequestException(
                ErrorCodes.WalletPaymentFailed,
                paymentResult.ErrorMessage ?? "Thanh toán ví thất bại.");
        }

        return await invoiceQueryLoader.GetInvoiceResponseAsync(invoiceId, cancellationToken);
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

        var contract = await billingWorkflowGuard.GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);
        var termsEffectiveOn = new DateOnly(
            request.BillingPeriodStart.Year,
            request.BillingPeriodStart.Month,
            DateTime.DaysInMonth(request.BillingPeriodStart.Year, request.BillingPeriodStart.Month));
        var effectiveTerms = await billingContractContextResolver.ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            termsEffectiveOn,
            cancellationToken);
        var billingPeriod = billingPeriodResolver.ResolveWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            request.BillingPeriodStart);

        if (BillingPeriodResolver.IsFutureBillingPeriod(billingPeriod))
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await billingPeriodResolver.InvoicePeriodExistsAsync(
            request.ContractId,
            billingPeriod,
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

        var missingPricesReason = GetMissingServicePricesBlockReason(prices, serviceTypes);
        if (missingPricesReason is not null)
        {
            throw new BadRequestException(ErrorCodes.BillingPriceNotFound, missingPricesReason);
        }

        var sequenceBlockReason = await billingPeriodResolver.GetInvoiceGenerationBlockReasonAsync(
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

        var meteredInputs = await meterReadingInputResolver.ResolveAsync(
            contract.Id,
            billingPeriod,
            request.MeterReadings,
            serviceTypeById,
            prices,
            isFinalInvoice: false,
            cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BillingPeriodResolver.BuildDueDate(billingPeriod.End, contract.PaymentDay);
        var contractContext = await billingContractContextResolver.ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.TenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            billingPeriod.Start,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
        var rentAmount = BillingPeriodResolver.CalculatePeriodAmount(monthlyRent, billingPeriod);

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

        billingInvoiceBuilder.AddRentInvoiceItem(invoice, monthlyRent, rentAmount, billingPeriod, now);
        billingInvoiceBuilder.AddMeteredServiceInvoiceItems(
            invoice,
            contract.RoomId,
            contract.Id,
            landlordUserId,
            billingPeriod,
            meteredInputs,
            now);
        await LinkMeterReadingProofMediaAssetsAsync(invoice, landlordUserId, now, cancellationToken);
        billingInvoiceBuilder.AddFixedServiceInvoiceItems(
            invoice,
            prices,
            serviceTypeById,
            billingPeriod,
            contractContext.OccupantCount,
            now);
        BillingInvoiceBuilder.CalculateAndValidateInvoiceTotal(invoice);

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

        return await invoiceQueryLoader.GetInvoiceResponseAsync(invoice.Id, cancellationToken);
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

        await billingWorkflowGuard.GetOwnedTerminationBillingContractAsync(
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

        var contract = await billingWorkflowGuard.GetOwnedTerminationBillingContractAsync(
            landlordUserId,
            contractId,
            terminationDate,
            allowActiveContract: true,
            cancellationToken);
        var effectiveTerms = await billingContractContextResolver.ResolveEffectiveContractTermsAsync(
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

        var billingPeriod = billingPeriodResolver.ResolveWithinContract(
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriodReferenceDate,
            terminationDate);

        if (BillingPeriodResolver.IsFutureBillingPeriod(billingPeriod))
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await billingPeriodResolver.InvoicePeriodExistsAsync(
            contractId,
            billingPeriod,
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(
                ErrorCodes.InvoiceDuplicatePeriod,
                "Hợp đồng đã có hóa đơn trong kỳ này.");
        }

        var sequenceBlockReason = await billingPeriodResolver.GetInvoiceGenerationBlockReasonAsync(
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

        var missingPricesReason = GetMissingServicePricesBlockReason(prices, serviceTypes);
        if (missingPricesReason is not null)
        {
            throw new BadRequestException(ErrorCodes.BillingPriceNotFound, missingPricesReason);
        }

        var meteredInputs = await meterReadingInputResolver.ResolveAsync(
            contract.Id,
            billingPeriod,
            meterReadings,
            serviceTypeById,
            prices,
            isFinalInvoice: true,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BillingPeriodResolver.BuildDueDate(billingPeriod.End, contract.PaymentDay);
        DateOnly tenantEffectiveOn = billingPeriod.End == terminationDate
            ? terminationDate
            : billingPeriod.Start;
        var contractContext = await billingContractContextResolver.ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.TenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            tenantEffectiveOn,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
        var rentAmount = BillingPeriodResolver.CalculatePeriodAmount(monthlyRent, billingPeriod);

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

        billingInvoiceBuilder.AddRentInvoiceItem(invoice, monthlyRent, rentAmount, billingPeriod, now);
        billingInvoiceBuilder.AddMeteredServiceInvoiceItems(
            invoice,
            contract.RoomId,
            contract.Id,
            landlordUserId,
            billingPeriod,
            meteredInputs,
            now);
        await LinkMeterReadingProofMediaAssetsAsync(invoice, landlordUserId, now, cancellationToken);
        billingInvoiceBuilder.AddFixedServiceInvoiceItems(
            invoice,
            prices,
            serviceTypeById,
            billingPeriod,
            contractContext.OccupantCount,
            now);
        BillingInvoiceBuilder.CalculateAndValidateInvoiceTotal(invoice);

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

        return await invoiceQueryLoader.GetInvoiceResponseAsync(invoice.Id, cancellationToken);
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
                x.ProofMediaAssetId,
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
                        latest.Consumption,
                        latest.ProofMediaAssetId,
                        BuildPrivateProofImageUrl(latest.ProofMediaAssetId));
                });
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

    private static string? GetMissingServicePricesBlockReason(
        List<RoomingHouseServicePrice> prices,
        List<BillingServiceType> serviceTypes)
    {
        var configuredServiceIds = prices.Select(x => x.ServiceTypeId).Distinct().ToList();
        if (configuredServiceIds.Count < serviceTypes.Count)
        {
            var missingServiceNames = serviceTypes
                .Where(x => !configuredServiceIds.Contains(x.Id))
                .Select(x => x.Name);

            return $"Vui lòng cấu hình bảng giá cho các dịch vụ còn thiếu trước khi tạo hóa đơn: {string.Join(", ", missingServiceNames)}.";
        }

        return null;
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

    public async Task<BulkInvoiceResultResponse> GenerateBulkInvoicesAsync(
        Guid landlordUserId,
        GenerateBulkInvoicesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RoomingHouseId == Guid.Empty)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Vui lòng chọn khu trọ.");
        }

        if (request.BillingPeriodEnd < request.BillingPeriodStart ||
            request.BillingPeriodStart.Year != request.BillingPeriodEnd.Year ||
            request.BillingPeriodStart.Month != request.BillingPeriodEnd.Month)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Kỳ hóa đơn không hợp lệ.");
        }

        if (request.Rooms is null || request.Rooms.Count == 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Vui lòng chọn ít nhất một phòng để tạo hóa đơn.");
        }

        var duplicatedContract = request.Rooms
            .GroupBy(x => x.ContractId)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatedContract is not null)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Mỗi phòng chỉ được gửi một lần trong yêu cầu tạo hàng loạt.");
        }

        var houseOwned = await context.RoomingHouses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.RoomingHouseId &&
                           x.LandlordUserId == landlordUserId &&
                           x.DeletedAt == null,
                cancellationToken);
        if (!houseOwned)
        {
            throw new NotFoundException(ErrorCodes.RoomNotFound, "Không tìm thấy khu trọ thuộc quyền quản lý.");
        }

        var activeContracts = await context.RentalContracts
            .AsNoTracking()
            .Include(x => x.Room)
            .WhereActiveForOccupiedOrReservedRoom()
            .Where(x => x.Room.RoomingHouseId == request.RoomingHouseId)
            .OrderBy(x => x.Room.RoomNumber)
            .Select(x => new { x.Id, x.RoomId, x.Room.RoomNumber })
            .ToListAsync(cancellationToken);

        if (activeContracts.Count == 0)
        {
            throw new BadRequestException(ErrorCodes.RentalContractNotFound, "Khu trọ không có phòng đang thuê với hợp đồng Active.");
        }

        var inputByContract = request.Rooms.ToDictionary(x => x.ContractId);
        var activeContractIds = activeContracts.Select(x => x.Id).ToHashSet();
        var results = new List<BulkInvoiceRoomResultResponse>();

        foreach (var contract in activeContracts)
        {
            if (!inputByContract.TryGetValue(contract.Id, out var roomInput))
            {
                results.Add(new BulkInvoiceRoomResultResponse(
                    contract.RoomId, contract.Id, contract.RoomNumber, "Skipped",
                    "Chủ trọ đã bỏ qua phòng này.", null));
                continue;
            }

            try
            {
                var invoice = await GenerateInvoiceWithReadingsAsync(
                    landlordUserId,
                    new GenerateInvoiceWithReadingsRequest(
                        contract.Id,
                        request.BillingPeriodStart,
                        request.BillingPeriodEnd,
                        roomInput.DiscountAmount,
                        roomInput.Note,
                        roomInput.MeterReadings ?? []),
                    cancellationToken);

                results.Add(new BulkInvoiceRoomResultResponse(
                    contract.RoomId, contract.Id, contract.RoomNumber, "Created",
                    "Đã tạo hóa đơn nháp.", invoice));
            }
            catch (ConflictException ex) when (ex.ErrorCode == ErrorCodes.InvoiceDuplicatePeriod)
            {
                results.Add(new BulkInvoiceRoomResultResponse(
                    contract.RoomId, contract.Id, contract.RoomNumber, "Skipped",
                    "Hóa đơn của phòng trong kỳ này đã tồn tại.", null));
            }
            catch (AppException ex)
            {
                results.Add(new BulkInvoiceRoomResultResponse(
                    contract.RoomId, contract.Id, contract.RoomNumber, "MissingData",
                    ex.Message, null));
            }
        }

        foreach (var unknownInput in request.Rooms.Where(x => !activeContractIds.Contains(x.ContractId)))
        {
            results.Add(new BulkInvoiceRoomResultResponse(
                Guid.Empty, unknownInput.ContractId, string.Empty, "MissingData",
                "Phòng không có hợp đồng Active trong khu trọ đã chọn.", null));
        }

        return new BulkInvoiceResultResponse(
            activeContracts.Count,
            results.Count(x => x.Status == "Created"),
            results.Count(x => x.Status == "Skipped"),
            results.Count(x => x.Status == "MissingData"),
            results);
    }

    private async Task LinkMeterReadingProofMediaAssetsAsync(
        Invoice invoice,
        Guid landlordUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var processedReadingIds = new HashSet<Guid>();
        foreach (var reading in invoice.Items
                     .Select(x => x.MeterReading)
                     .Where(x => x is not null)
                     .Select(x => x!))
        {
            if (!processedReadingIds.Add(reading.Id))
            {
                continue;
            }

            reading.ProofMediaAssetId = await EnsureMeterReadingProofMediaAssetAsync(
                reading.Id,
                landlordUserId,
                reading.ProofMediaAssetId,
                now,
                cancellationToken);
        }
    }

    private async Task<Guid?> EnsureMeterReadingProofMediaAssetAsync(
        Guid meterReadingId,
        Guid landlordUserId,
        Guid? proofMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!proofMediaAssetId.HasValue)
        {
            return null;
        }

        var linkedAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == proofMediaAssetId.Value, cancellationToken);

        if (linkedAsset is null)
        {
            return null;
        }

        linkedAsset.OwnerUserId = landlordUserId;
        linkedAsset.Scope = MediaScope.MeterReadingImage;
        linkedAsset.Visibility = MediaVisibility.Private;
        linkedAsset.Status = MediaStatus.Linked;
        linkedAsset.LinkedEntityType = nameof(MeterReading);
        linkedAsset.LinkedEntityId = meterReadingId;
        linkedAsset.DeletedAt = null;
        linkedAsset.UpdatedAt = now;
        return linkedAsset.Id;
    }

    private static string? BuildPrivateProofImageUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : null;
    }
}
