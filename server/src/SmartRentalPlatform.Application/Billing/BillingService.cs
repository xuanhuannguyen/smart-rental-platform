using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Media;
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

        var today = GetBusinessToday();
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
        var effectiveTerms = await ResolveEffectiveContractTermsAsync(
            contract.Id,
            contract.StartDate,
            contract.EndDate,
            termsEffectiveOn,
            cancellationToken);
        var periodContext = await ResolveInvoicePeriodContextAsync(
            contract.Id,
            effectiveTerms.StartDate,
            effectiveTerms.EndDate,
            billingPeriodStart,
            billingPeriodEnd,
            cancellationToken);
        var billingPeriod = periodContext.BillingPeriod;
        var contractContext = await ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.MainTenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            billingPeriod.Start,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
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
        else if (IsFutureBillingPeriod(billingPeriod))
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = BuildFixedServicePreviews(
            prices,
            serviceTypeById,
            billingPeriod,
            occupantCount);

        var meteredServices = BuildMeteredServicePreviews(
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

        var periodContext = await ResolveInvoicePeriodContextAsync(
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
        var contractContext = await ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.MainTenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            tenantEffectiveOn,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
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
        else if (IsFutureBillingPeriod(billingPeriod))
        {
            generationBlockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        var fixedServices = BuildFixedServicePreviews(
            prices,
            serviceTypeById,
            billingPeriod,
            occupantCount);

        var meteredServices = BuildMeteredServicePreviews(
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

        return await GetInvoiceResponseAsync(invoiceId, cancellationToken);
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

        if (IsFutureBillingPeriod(billingPeriod))
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await InvoicePeriodExistsAsync(
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

        var meteredInputs = await ResolveMeterReadingInputsAsync(
            contract.Id,
            billingPeriod,
            request.MeterReadings,
            serviceTypeById,
            prices,
            isFinalInvoice: false,
            cancellationToken);

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(billingPeriod.End, contract.PaymentDay);
        var contractContext = await ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.TenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            billingPeriod.Start,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
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

        AddRentInvoiceItem(invoice, monthlyRent, rentAmount, billingPeriod, now);
        await AddMeteredServiceInvoiceItems(
            invoice,
            contract.RoomId,
            contract.Id,
            landlordUserId,
            billingPeriod,
            meteredInputs,
            now,
            cancellationToken);
        AddFixedServiceInvoiceItems(
            invoice,
            prices,
            serviceTypeById,
            billingPeriod,
            contractContext.OccupantCount,
            now);
        CalculateAndValidateInvoiceTotal(invoice);

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

        if (IsFutureBillingPeriod(billingPeriod))
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Kỳ ghi chỉ số không được nằm trong tương lai.");
        }

        var duplicate = await InvoicePeriodExistsAsync(
            contractId,
            billingPeriod,
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

        var missingPricesReason = GetMissingServicePricesBlockReason(prices, serviceTypes);
        if (missingPricesReason is not null)
        {
            throw new BadRequestException(ErrorCodes.BillingPriceNotFound, missingPricesReason);
        }

        var meteredInputs = await ResolveMeterReadingInputsAsync(
            contract.Id,
            billingPeriod,
            meterReadings,
            serviceTypeById,
            prices,
            isFinalInvoice: true,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueDate = BuildDueDate(billingPeriod.End, contract.PaymentDay);
        DateOnly tenantEffectiveOn = billingPeriod.End == terminationDate
            ? terminationDate
            : billingPeriod.Start;
        var contractContext = await ResolveInvoiceContractContextAsync(
            contract.Id,
            contract.TenantUserId,
            contract.MonthlyRent,
            billingPeriod,
            tenantEffectiveOn,
            cancellationToken);
        var monthlyRent = contractContext.MonthlyRent;
        var effectiveTenant = contractContext.EffectiveTenant;
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

        AddRentInvoiceItem(invoice, monthlyRent, rentAmount, billingPeriod, now);
        await AddMeteredServiceInvoiceItems(
            invoice,
            contract.RoomId,
            contract.Id,
            landlordUserId,
            billingPeriod,
            meteredInputs,
            now,
            cancellationToken);
        AddFixedServiceInvoiceItems(
            invoice,
            prices,
            serviceTypeById,
            billingPeriod,
            contractContext.OccupantCount,
            now);
        CalculateAndValidateInvoiceTotal(invoice);

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

    private sealed record ResolvedMeterReadingInput(
        BillingServiceType ServiceType,
        RoomingHouseServicePrice Price,
        decimal PreviousReading,
        decimal CurrentReading,
        string? ProofImageObjectKey,
        Guid? ProofMediaAssetId);

    private sealed record ResolvedBillingPeriod(
        DateOnly Start,
        DateOnly End,
        DateOnly MonthStart,
        DateOnly MonthEnd,
        int BillableDays,
        int DaysInMonth,
        bool IsFullMonth);

    private sealed record ResolvedInvoicePeriodContext(
        ResolvedBillingPeriod BillingPeriod,
        string? BlockReason);

    private sealed record ResolvedInvoiceTenant(
        Guid UserId,
        string DisplayName,
        string Email);

    private sealed record ResolvedInvoiceContractContext(
        ResolvedInvoiceTenant EffectiveTenant,
        decimal MonthlyRent,
        int OccupantCount);

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

    private async Task<ResolvedInvoiceContractContext> ResolveInvoiceContractContextAsync(
        Guid contractId,
        Guid currentContractTenantUserId,
        decimal currentMonthlyRent,
        ResolvedBillingPeriod billingPeriod,
        DateOnly tenantEffectiveOn,
        CancellationToken cancellationToken)
    {
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contractId,
            currentMonthlyRent,
            billingPeriod.Start,
            cancellationToken);

        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contractId,
            currentContractTenantUserId,
            tenantEffectiveOn,
            cancellationToken);

        var occupantCount = await GetActiveOccupantCountAsync(
            contractId,
            billingPeriod,
            cancellationToken);

        return new ResolvedInvoiceContractContext(
            effectiveTenant,
            monthlyRent,
            occupantCount);
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

    private async Task<ResolvedInvoicePeriodContext> ResolveInvoicePeriodContextAsync(
        Guid contractId,
        DateOnly contractStart,
        DateOnly contractEnd,
        DateOnly requestedMonth,
        DateOnly? billingPeriodEndOverride,
        CancellationToken cancellationToken)
    {
        var billingPeriod = ResolveBillingPeriodWithinContract(
            contractStart,
            contractEnd,
            requestedMonth,
            billingPeriodEndOverride);

        var blockReason = await GetInvoiceGenerationBlockReasonAsync(
            contractId,
            contractStart,
            contractEnd,
            billingPeriod,
            billingPeriodEndOverride,
            cancellationToken);

        if (IsFutureBillingPeriod(billingPeriod))
        {
            blockReason = "Kỳ hóa đơn chưa kết thúc nên chưa thể tạo hóa đơn.";
        }

        return new ResolvedInvoicePeriodContext(billingPeriod, blockReason);
    }

    private async Task<bool> InvoicePeriodExistsAsync(
        Guid contractId,
        ResolvedBillingPeriod billingPeriod,
        CancellationToken cancellationToken)
    {
        return await context.Invoices.AnyAsync(
            x => x.ContractId == contractId &&
                 x.BillingPeriodStart == billingPeriod.Start &&
                 x.BillingPeriodEnd == billingPeriod.End &&
                 x.Status != InvoiceStatus.Cancelled,
            cancellationToken);
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
                contractStart,
                billingPeriodEndOverride);

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

    private static bool IsFutureBillingPeriod(ResolvedBillingPeriod period)
    {
        var today = GetBusinessToday();
        return period.Start > today || period.End > today;
    }

    private static DateOnly GetBusinessToday()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
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

    private static List<FixedServicePreviewResponse> BuildFixedServicePreviews(
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
    }

    private static List<MeteredServicePreviewResponse> BuildMeteredServicePreviews(
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

    private static void AddRentInvoiceItem(
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
            Description = BuildPeriodDescription("Tiền thuê phòng", billingPeriod),
            Quantity = GetPeriodQuantity(billingPeriod),
            UnitPrice = monthlyRent,
            Amount = rentAmount,
            CreatedAt = now
        });
    }

    private async Task AddMeteredServiceInvoiceItems(
        Invoice invoice,
        Guid roomId,
        Guid contractId,
        Guid landlordUserId,
        ResolvedBillingPeriod billingPeriod,
        IReadOnlyCollection<ResolvedMeterReadingInput> meteredInputs,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var input in meteredInputs)
        {
            var consumption = input.CurrentReading - input.PreviousReading;
            var amount = RoundMoney(consumption * input.Price.UnitPrice);
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

            reading.ProofMediaAssetId = await EnsureMeterReadingProofMediaAssetAsync(
                reading.Id,
                landlordUserId,
                input.ProofImageObjectKey,
                input.ProofMediaAssetId,
                now,
                cancellationToken);

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

    private static void AddFixedServiceInvoiceItems(
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
    }

    private static void CalculateAndValidateInvoiceTotal(Invoice invoice)
    {
        invoice.TotalAmount = RoundMoney(invoice.RentAmount + invoice.UtilityAmount + invoice.ServiceAmount - invoice.DiscountAmount);
        if (invoice.TotalAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tổng tiền hóa đơn không được âm.");
        }
    }

    private async Task<List<ResolvedMeterReadingInput>> ResolveMeterReadingInputsAsync(
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

            meteredInputs.Add(new ResolvedMeterReadingInput(
                serviceType,
                price,
                previousReading.Value,
                input.CurrentReading,
                input.ProofImageObjectKey,
                input.ProofMediaAssetId));
        }

        return meteredInputs;
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
        var today = GetBusinessToday();
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
            .Include(x => x.Items)
                .ThenInclude(x => x.MeterReading);
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
            invoice.WalletTransferGroupId);
    }

    private static InvoiceItemResponse ToInvoiceItemResponse(InvoiceItem item)
    {
        return new InvoiceItemResponse(
            item.Id,
            item.ServiceTypeId,
            item.ServiceType?.Name,
            item.MeterReadingId,
            item.MeterReading?.ProofMediaAssetId,
            BuildPrivateProofImageUrl(item.MeterReading?.ProofMediaAssetId),
            item.ItemType.ToString(),
            item.Description,
            item.Quantity,
            item.UnitPrice,
            item.Amount);
    }

    private async Task<Guid?> EnsureMeterReadingProofMediaAssetAsync(
        Guid meterReadingId,
        Guid landlordUserId,
        string? objectKey,
        Guid? proofMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (proofMediaAssetId.HasValue)
        {
            var linkedAsset = await context.MediaAssets
                .FirstOrDefaultAsync(x => x.Id == proofMediaAssetId.Value, cancellationToken);

            if (linkedAsset is not null)
            {
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
        }

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        var mediaAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.ObjectKey == normalizedObjectKey, cancellationToken);

        if (mediaAsset is null)
        {
            mediaAsset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                OwnerUserId = landlordUserId,
                BucketName = "legacy-private-storage",
                ObjectKey = normalizedObjectKey,
                OriginalFileName = Path.GetFileName(normalizedObjectKey),
                StoredFileName = Path.GetFileName(normalizedObjectKey),
                ContentType = GuessContentType(normalizedObjectKey),
                FileSize = 0,
                Scope = MediaScope.MeterReadingImage,
                Visibility = MediaVisibility.Private,
                Status = MediaStatus.Linked,
                LinkedEntityType = nameof(MeterReading),
                LinkedEntityId = meterReadingId,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.MediaAssets.Add(mediaAsset);
            return mediaAsset.Id;
        }

        mediaAsset.OwnerUserId = landlordUserId;
        mediaAsset.Scope = MediaScope.MeterReadingImage;
        mediaAsset.Visibility = MediaVisibility.Private;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(MeterReading);
        mediaAsset.LinkedEntityId = meterReadingId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return mediaAsset.Id;
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string GuessContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string? BuildPrivateProofImageUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : null;
    }

}
