using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.Billing;

public class BillingService : IBillingService
{
    private static readonly BillingServiceCode[] MeteredServices =
    [
        BillingServiceCode.Electric,
        BillingServiceCode.Water
    ];

    private static readonly BillingServiceCode[] FixedServices =
    [
        BillingServiceCode.Wifi,
        BillingServiceCode.Trash
    ];

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
            .OrderBy(x => x.ServiceType.Code)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return prices.Select(ToServicePriceResponse).ToList();
    }

    public async Task<RoomBillingContextResponse> GetRoomBillingContextAsync(
        Guid landlordUserId,
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var contract = await context.Contracts
            .AsNoTracking()
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
            .Include(x => x.MainTenant)
            .Where(x => x.RoomId == roomId &&
                        x.Status == ContractStatus.Active &&
                        x.Room.Status == RoomStatus.Occupied &&
                        x.Room.RoomingHouse.LandlordUserId == landlordUserId &&
                        x.Room.DeletedAt == null &&
                        x.Room.RoomingHouse.DeletedAt == null)
            .OrderByDescending(x => x.ActivatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.ContractNotFound,
                "Phong nay chua co hop dong Active de tao hoa don.");

        return new RoomBillingContextResponse(
            contract.RoomId,
            contract.Room.RoomNumber,
            contract.Room.RoomingHouseId,
            contract.Id,
            contract.ContractNumber,
            contract.MainTenantUserId,
            contract.MainTenant.DisplayName,
            contract.MainTenant.Email,
            contract.MonthlyRent,
            contract.PaymentDay,
            contract.StartDate,
            contract.EndDate,
            contract.Status.ToString());
    }

    public async Task<ServicePriceResponse> CreateServicePriceAsync(
        Guid landlordUserId,
        Guid roomingHouseId,
        CreateServicePriceRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoomingHouseOwnerAsync(landlordUserId, roomingHouseId, cancellationToken);

        var serviceCode = ParseServiceCode(request.ServiceCode);
        var billingMethod = ParseBillingMethod(request.BillingMethod);
        var serviceType = await GetServiceTypeAsync(serviceCode, cancellationToken);

        var validBillingMethod = serviceCode switch
        {
            BillingServiceCode.Electric => billingMethod == BillingMethod.Metered,
            BillingServiceCode.Water => billingMethod is BillingMethod.Metered or BillingMethod.Fixed,
            BillingServiceCode.Wifi or BillingServiceCode.Trash => billingMethod == BillingMethod.Fixed,
            _ => false
        };

        if (!validBillingMethod)
        {
            throw new BadRequestException(
                ErrorCodes.BillingPriceInvalid,
                "Phuong thuc tinh gia khong hop le voi loai dich vu.");
        }

        if (request.UnitPrice < 0)
        {
            throw new BadRequestException(ErrorCodes.BillingPriceInvalid, "Don gia dich vu khong duoc am.");
        }

        var now = DateTimeOffset.UtcNow;
        var effectiveFrom = GetNextBillingPeriodStart(DateOnly.FromDateTime(now.UtcDateTime));
        var unitName = request.UnitName.Trim();

        var scheduledPrice = await context.RoomingHouseServicePrices
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        x.ServiceTypeId == serviceType.Id &&
                        x.EffectiveFrom == effectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (scheduledPrice is not null)
        {
            scheduledPrice.BillingMethod = billingMethod;
            scheduledPrice.UnitName = unitName;
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
                activePrice.BillingMethod = billingMethod;
                activePrice.UnitName = unitName;
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
            BillingMethod = billingMethod,
            UnitName = unitName,
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
                "Chi duoc nhap chi so cho dich vu Dien va Nuoc.");
        }

        if (request.BillingPeriodEnd < request.BillingPeriodStart)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Ky hoa don khong hop le.");
        }

        if (request.CurrentReading < request.PreviousReading)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Chi so cuoi ky phai lon hon hoac bang chi so dau ky.");
        }

        var contract = await GetOwnedActiveContractAsync(landlordUserId, request.ContractId, cancellationToken);
        if (contract.RoomId != request.RoomId)
        {
            throw new BadRequestException(ErrorCodes.MeterReadingInvalid, "Phong khong khop voi hop dong dang active.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.BillingPeriodStart > today || request.BillingPeriodEnd > today)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Ky ghi chi so khong duoc nam trong tuong lai.");
        }

        if (request.BillingPeriodStart < contract.StartDate || request.BillingPeriodEnd > contract.EndDate)
        {
            throw new BadRequestException(
                ErrorCodes.MeterReadingInvalid,
                "Ky ghi chi so phai nam trong thoi han hop dong.");
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
                "Chi so cua dich vu trong ky nay da ton tai.");
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
                "Ky ghi chi so bi trung hoac chong lan voi ban ghi da co.");
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
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Ky hoa don khong hop le.");
        }

        if (!IsFullCalendarMonth(request.BillingPeriodStart, request.BillingPeriodEnd))
        {
            throw new BadRequestException(
                ErrorCodes.InvoiceInvalidStatus,
                "Hoa don thang phai bat dau ngay 01 va ket thuc vao ngay cuoi cung cua cung thang.");
        }

        if (request.DiscountAmount < 0)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "So tien giam tru khong duoc am.");
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
            Description = "Tien thue phong",
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
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Tong tien hoa don khong duoc am.");
        }

        invoice.PaidAmount = 0;
        invoice.RemainingAmount = invoice.TotalAmount;

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    public async Task<List<InvoiceResponse>> GetLandlordInvoicesAsync(
        Guid landlordUserId,
        string? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: null, cancellationToken);

        var query = BuildInvoiceQuery()
            .Where(x => x.LandlordUserId == landlordUserId);

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
        await MarkOverdueInvoicesAsync(landlordUserId: landlordUserId, tenantUserId: null, invoiceId: invoiceId, cancellationToken);

        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen xem hoa don nay.");
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
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen phat hanh hoa don nay.");
        }

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chi co the phat hanh hoa don Draft.");
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
        var invoice = await context.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        if (invoice.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen huy hoa don nay.");
        }

        if (invoice.Status == InvoiceStatus.Paid ||
            invoice.Status == InvoiceStatus.PartiallyPaid ||
            invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chi co the huy hoa don chua thanh toan.");
        }

        var now = DateTimeOffset.UtcNow;
        var meterReadingIds = invoice.Items
            .Where(x => x.MeterReadingId.HasValue)
            .Select(x => x.MeterReadingId!.Value)
            .Distinct()
            .ToList();

        if (meterReadingIds.Count > 0)
        {
            var readings = await context.MeterReadings
                .Where(x => meterReadingIds.Contains(x.Id) &&
                            x.Status == MeterReadingStatus.UsedInInvoice)
                .ToListAsync(cancellationToken);

            foreach (var reading in readings)
            {
                reading.Status = MeterReadingStatus.Draft;
                reading.UpdatedAt = now;
            }
        }

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
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: null, cancellationToken);

        var invoices = await BuildInvoiceQuery()
            .Where(x => x.TenantUserId == tenantUserId &&
                        x.Status != InvoiceStatus.Draft &&
                        x.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(x => x.BillingPeriodEnd)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToInvoiceResponse).ToList();
    }

    public async Task<InvoiceResponse> GetMyInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: invoiceId, cancellationToken);

        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        if (invoice.TenantUserId != tenantUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen xem hoa don nay.");
        }

        if (invoice.Status == InvoiceStatus.Draft ||
            invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");
        }

        return ToInvoiceResponse(invoice);
    }

    public async Task<InvoiceResponse> PayInvoiceAsync(
        Guid tenantUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await MarkOverdueInvoicesAsync(landlordUserId: null, tenantUserId: tenantUserId, invoiceId: invoiceId, cancellationToken);

        var invoice = await context.Invoices
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        if (invoice.TenantUserId != tenantUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen thanh toan hoa don nay.");
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Hoa don da duoc thanh toan.");
        }

        if (invoice.Status != InvoiceStatus.Issued &&
            invoice.Status != InvoiceStatus.Overdue &&
            invoice.Status != InvoiceStatus.PartiallyPaid)
        {
            throw new BadRequestException(ErrorCodes.InvoiceInvalidStatus, "Chi co the thanh toan hoa don da phat hanh.");
        }

        var paymentResult = await walletPaymentService.PayInvoiceAsync(
            invoice.Id,
            invoice.TenantUserId,
            invoice.LandlordUserId,
            invoice.RemainingAmount,
            cancellationToken);

        if (!paymentResult.Success || paymentResult.TransferGroupId is null)
        {
            throw new BadRequestException(
                ErrorCodes.WalletPaymentFailed,
                paymentResult.ErrorMessage ?? "Thanh toan vi that bai.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        context.InvoicePayments.Add(new InvoicePayment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            TenantUserId = invoice.TenantUserId,
            LandlordUserId = invoice.LandlordUserId,
            Amount = invoice.RemainingAmount,
            WalletTransferGroupId = paymentResult.TransferGroupId.Value,
            Status = InvoicePaymentStatus.Succeeded,
            PaidAt = now,
            CreatedAt = now
        });

        invoice.PaidAmount = invoice.TotalAmount;
        invoice.RemainingAmount = 0;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;
        invoice.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetInvoiceResponseAsync(invoice.Id, cancellationToken);
    }

    private async Task<BillingContractSnapshot> GetOwnedActiveContractAsync(
        Guid landlordUserId,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        var contract = await contractReadService.GetActiveContractAsync(contractId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.ContractNotFound, "Khong tim thay hop dong Active.");

        if (contract.LandlordUserId != landlordUserId)
        {
            throw new ForbiddenException(ErrorCodes.Forbidden, "Ban khong co quyen truy cap hop dong nay.");
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
            throw new NotFoundException(ErrorCodes.HouseNotFound, "Khong tim thay khu tro hoac ban khong co quyen truy cap.");
        }
    }

    private async Task<BillingServiceType> GetServiceTypeAsync(
        BillingServiceCode code,
        CancellationToken cancellationToken)
    {
        return await context.BillingServiceTypes
            .FirstOrDefaultAsync(x => x.Code == code && x.IsActive, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.BillingServiceInvalid, "Khong tim thay loai dich vu.");
    }

    private async Task<List<RoomingHouseServicePrice>> GetEffectivePricesAsync(
        Guid roomingHouseId,
        List<Guid> serviceTypeIds,
        DateOnly billingPeriodEnd,
        CancellationToken cancellationToken)
    {
        return await context.RoomingHouseServicePrices
            .AsNoTracking()
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        serviceTypeIds.Contains(x.ServiceTypeId) &&
                        x.EffectiveFrom <= billingPeriodEnd &&
                        (x.EffectiveTo == null || x.EffectiveTo >= billingPeriodEnd))
            .ToListAsync(cancellationToken);
    }

    private static RoomingHouseServicePrice GetEffectivePriceOrThrow(
        List<RoomingHouseServicePrice> prices,
        Guid serviceTypeId,
        BillingServiceCode code)
    {
        return prices
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault(x => x.ServiceTypeId == serviceTypeId)
            ?? throw new NotFoundException(
                ErrorCodes.BillingPriceNotFound,
                $"Chua co bang gia hieu luc cho dich vu {code}.");
    }

    private async Task<InvoiceResponse> GetInvoiceResponseAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await BuildInvoiceQuery()
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.InvoiceNotFound, "Khong tim thay hoa don.");

        return ToInvoiceResponse(invoice);
    }

    private async Task MarkOverdueInvoicesAsync(
        Guid? landlordUserId,
        Guid? tenantUserId,
        Guid? invoiceId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = context.Invoices
            .Where(x => x.RemainingAmount > 0 &&
                        x.DueDate < today &&
                        (x.Status == InvoiceStatus.Issued ||
                         x.Status == InvoiceStatus.PartiallyPaid));

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
            .Include(x => x.Tenant)
            .Include(x => x.Items)
            .Include(x => x.Payments);
    }

    private static BillingServiceCode ParseServiceCode(string value)
    {
        if (!Enum.TryParse<BillingServiceCode>(value, true, out var code) ||
            !Enum.IsDefined(code))
        {
            throw new BadRequestException(ErrorCodes.BillingServiceInvalid, "Dich vu chi ho tro Electric, Water, Wifi, Trash.");
        }

        return code;
    }

    private static BillingMethod ParseBillingMethod(string value)
    {
        if (string.Equals(value, "MeterBased", StringComparison.OrdinalIgnoreCase))
        {
            return BillingMethod.Metered;
        }

        if (string.Equals(value, "PerMonth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "PerPerson", StringComparison.OrdinalIgnoreCase))
        {
            return BillingMethod.Fixed;
        }

        if (!Enum.TryParse<BillingMethod>(value, true, out var method) ||
            !Enum.IsDefined(method))
        {
            throw new BadRequestException(ErrorCodes.BillingPriceInvalid, "Phuong thuc tinh gia khong hop le.");
        }

        return method;
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

    private static ServicePriceResponse ToServicePriceResponse(RoomingHouseServicePrice price)
    {
        return new ServicePriceResponse(
            price.Id,
            price.RoomingHouseId,
            price.ServiceTypeId,
            price.ServiceType.Code.ToString(),
            price.ServiceType.Name,
            price.BillingMethod.ToString(),
            price.UnitName,
            price.UnitPrice,
            price.EffectiveFrom,
            price.EffectiveTo,
            price.IsActive,
            price.Note,
            price.CreatedAt,
            price.UpdatedAt);
    }

    private static MeterReadingResponse ToMeterReadingResponse(MeterReading reading)
    {
        return new MeterReadingResponse(
            reading.Id,
            reading.RoomId,
            reading.ContractId,
            reading.ServiceTypeId,
            reading.ServiceType.Code.ToString(),
            reading.BillingPeriodStart,
            reading.BillingPeriodEnd,
            reading.PreviousReading,
            reading.CurrentReading,
            reading.Consumption,
            reading.ProofImageObjectKey,
            reading.Status.ToString(),
            reading.RecordedByLandlordUserId,
            reading.ReadingAt);
    }

    private static InvoiceResponse ToInvoiceResponse(Invoice invoice)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.ContractId,
            invoice.RoomId,
            invoice.Room.RoomNumber,
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
            invoice.PaidAmount,
            invoice.RemainingAmount,
            invoice.Status.ToString(),
            invoice.Note,
            invoice.SentAt,
            invoice.PaidAt,
            invoice.Items.OrderBy(x => x.CreatedAt).Select(ToInvoiceItemResponse).ToList(),
            invoice.Payments.OrderByDescending(x => x.PaidAt).Select(ToInvoicePaymentResponse).ToList());
    }

    private static InvoiceItemResponse ToInvoiceItemResponse(InvoiceItem item)
    {
        return new InvoiceItemResponse(
            item.Id,
            item.ServiceTypeId,
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
