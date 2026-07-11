using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Amenities.Requests;
using SmartRentalPlatform.Contracts.Amenities.Responses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartRentalPlatform.Application.Amenities;

public class AmenityService : IAmenityService
{
    private readonly IAppDbContext context;

    public AmenityService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<List<AmenityResponse>> GetActiveAmenitiesAsync(
        AmenityScope? scope,
        CancellationToken cancellationToken = default)
    {
        var query = context.Amenities
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (scope is AmenityScope.House or AmenityScope.Room)
        {
            query = query.Where(x => x.Scope == scope || x.Scope == AmenityScope.Both);
        }
        else if (scope is AmenityScope.Both)
        {
            query = query.Where(x => x.Scope == AmenityScope.Both);
        }

        return await query
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Name)
            .Select(x => new AmenityResponse
            {
                Id = x.Id,
                Name = x.Name,
                Scope = x.Scope.ToString(),
                IconCode = x.IconCode
            })
            .ToListAsync(cancellationToken);
    }

    // =================================================================================================
    // ADMIN CRUD
    // =================================================================================================

    public async Task<PagedResult<AdminAmenityResponse>> GetAmenitiesAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken = default)
    {
        var query = context.Amenities.AsNoTracking();

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
            .Select(x => new AdminAmenityResponse(
                x.Id,
                x.Name,
                x.Scope.ToString(),
                x.IconCode,
                x.IsActive,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminAmenityResponse>
        {
            Items = items,
            TotalItems = totalItems,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminAmenityResponse> GetAmenityAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Amenities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy tiện ích.");

        return new AdminAmenityResponse(entity.Id, entity.Name, entity.Scope.ToString(), entity.IconCode, entity.IsActive, entity.CreatedAt);
    }

    public async Task<AdminAmenityResponse> CreateAmenityAsync(CreateAmenityRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AmenityScope>(request.Scope, true, out var scope))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Phạm vi tiện ích không hợp lệ.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new SmartRentalPlatform.Domain.Entities.Properties.Amenity
        {
            Name = request.Name.Trim(),
            Scope = scope,
            IconCode = request.IconCode.Trim(),
            IsActive = true,
            CreatedAt = now
        };

        context.Amenities.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return new AdminAmenityResponse(entity.Id, entity.Name, entity.Scope.ToString(), entity.IconCode, entity.IsActive, entity.CreatedAt);
    }

    public async Task<AdminAmenityResponse> UpdateAmenityAsync(int id, UpdateAmenityRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await context.Amenities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy tiện ích.");

        if (!Enum.TryParse<AmenityScope>(request.Scope, true, out var scope))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Phạm vi tiện ích không hợp lệ.");
        }

        // Cấm thay đổi Scope (thu hẹp) nếu đang có dữ liệu map với tiện ích này
        if (entity.Scope != scope)
        {
            if (entity.Scope == AmenityScope.Both && scope == AmenityScope.Room)
            {
                // Thay đổi từ Both -> Room: Cấm nếu có House nào đang dùng
                var isUsedByHouse = await context.RoomingHouseAmenities.AnyAsync(x => x.AmenityId == id, cancellationToken);
                if (isUsedByHouse)
                {
                    throw new BadRequestException(ErrorCodes.ValidationError, "Không thể chuyển scope thành 'Phòng' vì đang có Khu trọ sử dụng tiện ích này.");
                }
            }
            else if (entity.Scope == AmenityScope.Both && scope == AmenityScope.House)
            {
                // Thay đổi từ Both -> House: Cấm nếu có Room nào đang dùng
                var isUsedByRoom = await context.RoomAmenities.AnyAsync(x => x.AmenityId == id, cancellationToken);
                if (isUsedByRoom)
                {
                    throw new BadRequestException(ErrorCodes.ValidationError, "Không thể chuyển scope thành 'Khu trọ' vì đang có Phòng sử dụng tiện ích này.");
                }
            }
            else if ((entity.Scope == AmenityScope.House && scope == AmenityScope.Room) ||
                     (entity.Scope == AmenityScope.Room && scope == AmenityScope.House))
            {
                var isUsedByHouse = await context.RoomingHouseAmenities.AnyAsync(x => x.AmenityId == id, cancellationToken);
                var isUsedByRoom = await context.RoomAmenities.AnyAsync(x => x.AmenityId == id, cancellationToken);
                if (isUsedByHouse || isUsedByRoom)
                {
                    throw new BadRequestException(ErrorCodes.ValidationError, "Không thể đảo ngược scope vì đang có dữ liệu sử dụng tiện ích này.");
                }
            }
        }

        entity.Name = request.Name.Trim();
        entity.Scope = scope;
        entity.IconCode = request.IconCode.Trim();
        entity.IconCode = request.IconCode.Trim();

        await context.SaveChangesAsync(cancellationToken);

        return new AdminAmenityResponse(entity.Id, entity.Name, entity.Scope.ToString(), entity.IconCode, entity.IsActive, entity.CreatedAt);
    }

    public async Task ToggleAmenityActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Amenities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.NotFound, "Không tìm thấy tiện ích.");

        entity.IsActive = !entity.IsActive;

        await context.SaveChangesAsync(cancellationToken);
    }
}
