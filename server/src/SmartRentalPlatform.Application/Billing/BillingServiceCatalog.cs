using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Billing.Requests;
using SmartRentalPlatform.Contracts.Billing.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Application.Billing;

public partial class BillingService
{
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

}
