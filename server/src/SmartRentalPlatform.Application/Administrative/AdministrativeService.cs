using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Administrative;
using SmartRentalPlatform.Contracts.Administrative.Requests;
using SmartRentalPlatform.Contracts.Administrative.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Application.Administrative;

public class AdministrativeService : IAdministrativeService
{
    private readonly IAppDbContext context;

    public AdministrativeService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<List<ProvinceResponse>> GetActiveProvincesAsync(CancellationToken cancellationToken = default)
    {
        return await context.AdministrativeProvinces
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ProvinceResponse
            {
                Code = x.Code,
                Name = x.Name,
                Type = x.Type.ToString()
            }).ToListAsync(cancellationToken);
    }

    public async Task<List<WardResponse>> GetWardsByProvinceAsync(string provinceCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return new List<WardResponse>();
        }

        return await context.AdministrativeWards
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProvinceCode == provinceCode)
            .OrderBy(x => x.Name)
            .Select(x => new WardResponse
            {
                Code = x.Code,
                ProvinceCode = x.ProvinceCode,
                Name = x.Name,
                Type = x.Type.ToString()
            }).ToListAsync(cancellationToken);
    }

    // =================================================================================================
    // ADMIN CRUD: PROVINCES
    // =================================================================================================

    public async Task<PagedResult<AdminProvinceResponse>> GetProvincesAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = context.AdministrativeProvinces.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(keyword) || x.Name.ToLower().Contains(keyword));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminProvinceResponse(
                x.Code,
                x.Name,
                x.Type.ToString(),
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminProvinceResponse>
        {
            Items = items,
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminProvinceResponse> GetProvinceAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeProvinces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin tỉnh/thành phố.");

        return new AdminProvinceResponse(entity.Code, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AdminProvinceResponse> CreateProvinceAsync(CreateProvinceRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ProvinceType>(request.Type, true, out var provinceType))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Loại tỉnh/thành phố không hợp lệ.");
        }

        var code = request.Code.Trim();
        var exists = await context.AdministrativeProvinces.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Mã tỉnh/thành phố đã tồn tại.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new AdministrativeProvince
        {
            Code = code,
            Name = request.Name.Trim(),
            Type = provinceType,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.AdministrativeProvinces.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new AdminProvinceResponse(entity.Code, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AdminProvinceResponse> UpdateProvinceAsync(string code, UpdateProvinceRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeProvinces
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin tỉnh/thành phố.");

        if (!Enum.TryParse<ProvinceType>(request.Type, true, out var provinceType))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Loại tỉnh/thành phố không hợp lệ.");
        }

        entity.Name = request.Name.Trim();
        entity.Type = provinceType;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return new AdminProvinceResponse(entity.Code, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task ToggleProvinceActiveAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeProvinces
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin tỉnh/thành phố.");

        // If trying to disable, we must ensure it's safe
        if (entity.IsActive)
        {
            // Block if any active wards depend on this province
            var hasActiveWards = await context.AdministrativeWards
                .AnyAsync(x => x.ProvinceCode == code && x.IsActive, cancellationToken);
            if (hasActiveWards)
            {
                throw new BadRequestException(ErrorCodes.ValidationError, "Không thể vô hiệu hóa tỉnh/thành phố này vì đang có các phường/xã thuộc tỉnh này đang hoạt động.");
            }

            // Block if any active rooming houses depend on this province
            var hasRoomingHouses = await context.RoomingHouses
                .AnyAsync(x => x.ProvinceCode == code && x.DeletedAt == null, cancellationToken);
            if (hasRoomingHouses)
            {
                throw new BadRequestException(ErrorCodes.ValidationError, "Không thể vô hiệu hóa tỉnh/thành phố này vì đang có các khu trọ đăng ký địa chỉ tại đây.");
            }
        }

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    // =================================================================================================
    // ADMIN CRUD: WARDS
    // =================================================================================================

    public async Task<PagedResult<AdminWardResponse>> GetWardsAsync(string provinceCode, int page, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = context.AdministrativeWards.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(provinceCode))
        {
            query = query.Where(x => x.ProvinceCode == provinceCode);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(keyword) || x.Name.ToLower().Contains(keyword));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminWardResponse(
                x.Code,
                x.ProvinceCode,
                x.Name,
                x.Type.ToString(),
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminWardResponse>
        {
            Items = items,
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminWardResponse> GetWardAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeWards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin phường/xã.");

        return new AdminWardResponse(entity.Code, entity.ProvinceCode, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AdminWardResponse> CreateWardAsync(CreateWardRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<WardType>(request.Type, true, out var wardType))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Loại phường/xã không hợp lệ.");
        }

        var provinceExists = await context.AdministrativeProvinces
            .AnyAsync(x => x.Code == request.ProvinceCode, cancellationToken);
        if (!provinceExists)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Mã tỉnh/thành phố không hợp lệ.");
        }

        var code = request.Code.Trim();
        var exists = await context.AdministrativeWards.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Mã phường/xã đã tồn tại.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new AdministrativeWard
        {
            Code = code,
            ProvinceCode = request.ProvinceCode.Trim(),
            Name = request.Name.Trim(),
            Type = wardType,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.AdministrativeWards.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new AdminWardResponse(entity.Code, entity.ProvinceCode, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task<AdminWardResponse> UpdateWardAsync(string code, UpdateWardRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeWards
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin phường/xã.");

        if (!Enum.TryParse<WardType>(request.Type, true, out var wardType))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Loại phường/xã không hợp lệ.");
        }

        entity.Name = request.Name.Trim();
        entity.Type = wardType;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return new AdminWardResponse(entity.Code, entity.ProvinceCode, entity.Name, entity.Type.ToString(), entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
    }

    public async Task ToggleWardActiveAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await context.AdministrativeWards
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy thông tin phường/xã.");

        // If trying to disable, we must ensure it's safe
        if (entity.IsActive)
        {
            // Block if any active rooming houses depend on this ward
            var hasRoomingHouses = await context.RoomingHouses
                .AnyAsync(x => x.WardCode == code && x.DeletedAt == null, cancellationToken);
            if (hasRoomingHouses)
            {
                throw new BadRequestException(ErrorCodes.ValidationError, "Không thể vô hiệu hóa phường/xã này vì đang có các khu trọ đăng ký địa chỉ tại đây.");
            }
        }

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}
