using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseQueryService : IRoomingHouseQueryService
{
    private readonly IAppDbContext context;

    public RoomingHouseQueryService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var houses = await BuildRoomingHouseQuery()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var house = houses
            .OrderBy(x => GetOnboardingPriority(x.ApprovalStatus))
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (house is null)
        {
            return new RoomingHouseOnboardingResponse
            {
                Status = RoomingHouseOnboardingStatus.None,
                HasRoomingHouse = false,
                CanCreateDraft = true,
                CanEdit = false,
                CanSubmit = false,
                CanEnterLandlordDashboard = false
            };
        }

        return new RoomingHouseOnboardingResponse
        {
            Status = house.ApprovalStatus.ToString(),
            HasRoomingHouse = true,
            CanCreateDraft = CanCreateDraft(houses),
            CanEdit = CanEditRejectedOrDraft(house),
            CanSubmit = CanSubmit(house),
            CanEnterLandlordDashboard = houses.Any(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved),
            RoomingHouseId = house.Id,
            RoomingHouse = RoomingHouseReadModelMapper.ToDetailResponse(house)
        };
    }

    public async Task<List<RoomingHouseDetailResponse>> GetPublicAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        var houses = await context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.Images)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity)
            .Include(x => x.Rooms)
            .Where(x => x.DeletedAt == null &&
                        x.ApprovalStatus == RoomingHouseApprovalStatus.Approved &&
                        x.VisibilityStatus == RoomingHouseVisibilityStatus.Visible &&
                        x.Rooms.Any(r => r.Status == RoomStatus.Available && r.DeletedAt == null))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return houses.Select(RoomingHouseReadModelMapper.ToDetailResponse).ToList();
    }

    public async Task<List<RoomingHouseResponse>> GetByLandlordAsync(
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var houses = await BuildRoomingHouseQuery()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return houses.Select(RoomingHouseReadModelMapper.ToResponse).ToList();
    }

    public async Task<RoomingHouseDetailResponse?> GetByIdAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var house = await BuildRoomingHouseQuery()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        return house is null ? null : RoomingHouseReadModelMapper.ToDetailResponse(house);
    }

    private IQueryable<RoomingHouse> BuildRoomingHouseQuery()
    {
        return context.RoomingHouses
            .AsNoTracking()
            .Include(x => x.Province)
            .Include(x => x.Ward)
            .Include(x => x.LegalDocument)
            .Include(x => x.RentalPolicy)
            .Include(x => x.Images)
            .Include(x => x.Rooms)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity);
    }

    private static int GetOnboardingPriority(RoomingHouseApprovalStatus status)
    {
        return status switch
        {
            RoomingHouseApprovalStatus.Draft => 0,
            RoomingHouseApprovalStatus.Rejected => 1,
            RoomingHouseApprovalStatus.Pending => 2,
            RoomingHouseApprovalStatus.Approved => 3,
            _ => 4
        };
    }

    private static bool CanEditRejectedOrDraft(RoomingHouse house)
    {
        return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanSubmit(RoomingHouse house)
    {
        return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
    }

    private static bool CanCreateDraft(IEnumerable<RoomingHouse> houses)
    {
        return !houses.Any(x =>
            x.ApprovalStatus is RoomingHouseApprovalStatus.Draft
                or RoomingHouseApprovalStatus.Pending
                or RoomingHouseApprovalStatus.Rejected);
    }
}
