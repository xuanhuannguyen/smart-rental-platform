using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.LandlordDashboard.Responses;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.LandlordDashboard;

public sealed class LandlordDashboardService(IAppDbContext context) : ILandlordDashboardService
{
    public async Task<LandlordDashboardResponse> GetDashboardAsync(
        Guid landlordUserId,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var selectedYear = year.GetValueOrDefault(today.Year);
        var selectedMonth = month.GetValueOrDefault(today.Month);
        if (selectedYear < 2000 || selectedYear > 2100 || selectedMonth < 1 || selectedMonth > 12)
        {
            selectedYear = today.Year;
            selectedMonth = today.Month;
        }

        var monthStart = new DateOnly(selectedYear, selectedMonth, 1);
        var nextMonthStart = monthStart.AddMonths(1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var expiringSoonDate = today.AddDays(30);
        var revenueStart = monthStart.AddMonths(-5);

        var houses = await context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.AddressDisplay,
                x.ApprovalStatus,
                x.VisibilityStatus
            })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var houseIds = houses.Select(x => x.Id).ToHashSet();

        var roomStats = await context.Rooms
            .AsNoTracking()
            .Where(x => houseIds.Contains(x.RoomingHouseId) && x.DeletedAt == null)
            .GroupBy(x => x.RoomingHouseId)
            .Select(group => new
            {
                RoomingHouseId = group.Key,
                Total = group.Count(),
                Occupied = group.Count(x => x.Status == RoomStatus.Occupied),
                Available = group.Count(x => x.Status == RoomStatus.Available),
                Maintenance = group.Count(x => x.Status == RoomStatus.Maintenance)
            })
            .ToListAsync(cancellationToken);

        var activeContractStats = await context.RentalContracts
            .AsNoTracking()
            .Where(x => x.Status == RentalContractStatus.Active &&
                        x.DeletedAt == null &&
                        houseIds.Contains(x.Room.RoomingHouseId))
            .GroupBy(x => x.Room.RoomingHouseId)
            .Select(group => new
            {
                RoomingHouseId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var rentalRequestStats = await context.RentalRequests
            .AsNoTracking()
            .Where(x => houseIds.Contains(x.Room.RoomingHouseId))
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var invoiceStats = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId)
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var currentMonthRevenue = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        x.Status == InvoiceStatus.Paid &&
                        x.PaidAt != null &&
                        x.BillingPeriodStart >= monthStart &&
                        x.BillingPeriodStart < nextMonthStart)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        var previousMonthRevenue = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        x.Status == InvoiceStatus.Paid &&
                        x.PaidAt != null &&
                        x.BillingPeriodStart >= previousMonthStart &&
                        x.BillingPeriodStart < monthStart)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        var totalPaidRevenue = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        x.Status == InvoiceStatus.Paid &&
                        x.PaidAt != null)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        var pendingCollectionAmount = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.Overdue))
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        var contractStats = await context.RentalContracts
            .AsNoTracking()
            .Where(x => houseIds.Contains(x.Room.RoomingHouseId) && x.DeletedAt == null)
            .Select(x => new
            {
                x.Status,
                x.EndDate
            })
            .ToListAsync(cancellationToken);

        var appointmentStats = await context.ViewingAppointments
            .AsNoTracking()
            .Where(x => houseIds.Contains(x.Room.RoomingHouseId))
            .Select(x => new
            {
                x.Status,
                x.ScheduledAt
            })
            .ToListAsync(cancellationToken);

        var paidInvoices = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        x.Status == InvoiceStatus.Paid &&
                        x.BillingPeriodStart >= revenueStart &&
                        x.BillingPeriodStart < nextMonthStart)
            .Select(x => new
            {
                x.BillingPeriodStart,
                x.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var revenueSeries = Enumerable.Range(0, 6)
            .Select(index => revenueStart.AddMonths(index))
            .Select(period => new LandlordDashboardRevenuePointResponse(
                $"{period.Month:00}/{period.Year}",
                paidInvoices
                    .Where(x => x.BillingPeriodStart.Year == period.Year &&
                                x.BillingPeriodStart.Month == period.Month)
                    .Sum(x => x.TotalAmount)))
            .ToList();

        var houseFinancials = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId &&
                        houseIds.Contains(x.Room.RoomingHouseId))
            .GroupBy(x => x.Room.RoomingHouseId)
            .Select(group => new
            {
                RoomingHouseId = group.Key,
                CurrentMonthRevenue = group
                    .Where(x => x.Status == InvoiceStatus.Paid &&
                                x.BillingPeriodStart >= monthStart &&
                                x.BillingPeriodStart < nextMonthStart)
                    .Sum(x => x.TotalAmount),
                PendingCollectionAmount = group
                    .Where(x => x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.Overdue)
                    .Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var roomStatsByHouse = roomStats.ToDictionary(x => x.RoomingHouseId);
        var activeContractsByHouse = activeContractStats.ToDictionary(x => x.RoomingHouseId, x => x.Count);
        var financialsByHouse = houseFinancials.ToDictionary(x => x.RoomingHouseId);

        var houseResponses = houses.Select(house =>
        {
            roomStatsByHouse.TryGetValue(house.Id, out var stats);
            financialsByHouse.TryGetValue(house.Id, out var financials);

            return new LandlordDashboardHouseResponse(
                house.Id,
                house.Name,
                house.AddressDisplay,
                house.ApprovalStatus.ToString(),
                house.VisibilityStatus.ToString(),
                stats?.Total ?? 0,
                stats?.Occupied ?? 0,
                stats?.Available ?? 0,
                activeContractsByHouse.GetValueOrDefault(house.Id),
                financials?.CurrentMonthRevenue ?? 0m,
                financials?.PendingCollectionAmount ?? 0m);
        }).ToList();

        var recentInvoices = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new LandlordDashboardInvoiceResponse(
                x.Id,
                x.InvoiceNo,
                x.Room.RoomingHouse.Name,
                x.Room.RoomNumber,
                x.Tenant.UserProfile != null && x.Tenant.UserProfile.FullName != null
                    ? x.Tenant.UserProfile.FullName
                    : x.Tenant.DisplayName,
                x.BillingPeriodStart,
                x.BillingPeriodEnd,
                x.DueDate,
                x.TotalAmount,
                x.Status.ToString()))
            .ToListAsync(cancellationToken);

        var overdueInvoices = await context.Invoices
            .AsNoTracking()
            .Where(x => x.LandlordUserId == landlordUserId)
            .Where(x => x.Status == InvoiceStatus.Overdue ||
                        (x.Status == InvoiceStatus.Issued && x.DueDate < today))
            .OrderBy(x => x.DueDate)
            .Take(5)
            .Select(x => new LandlordDashboardInvoiceResponse(
                x.Id,
                x.InvoiceNo,
                x.Room.RoomingHouse.Name,
                x.Room.RoomNumber,
                x.Tenant.UserProfile != null && x.Tenant.UserProfile.FullName != null
                    ? x.Tenant.UserProfile.FullName
                    : x.Tenant.DisplayName,
                x.BillingPeriodStart,
                x.BillingPeriodEnd,
                x.DueDate,
                x.TotalAmount,
                x.Status.ToString()))
            .ToListAsync(cancellationToken);

        var totalRooms = roomStats.Sum(x => x.Total);
        var occupiedRooms = roomStats.Sum(x => x.Occupied);
        var overview = new LandlordDashboardOverviewResponse(
            houses.Count,
            houses.Count(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved),
            totalRooms,
            occupiedRooms,
            roomStats.Sum(x => x.Available),
            roomStats.Sum(x => x.Maintenance),
            activeContractStats.Sum(x => x.Count),
            rentalRequestStats.FirstOrDefault(x => x.Status == RentalRequestStatus.Pending)?.Count ?? 0,
            rentalRequestStats.FirstOrDefault(x => x.Status == RentalRequestStatus.Accepted)?.Count ?? 0,
            rentalRequestStats.FirstOrDefault(x => x.Status == RentalRequestStatus.Rejected)?.Count ?? 0,
            invoiceStats.FirstOrDefault(x => x.Status == InvoiceStatus.Draft)?.Count ?? 0,
            invoiceStats.FirstOrDefault(x => x.Status == InvoiceStatus.Issued)?.Count ?? 0,
            invoiceStats.FirstOrDefault(x => x.Status == InvoiceStatus.Paid)?.Count ?? 0,
            (invoiceStats.FirstOrDefault(x => x.Status == InvoiceStatus.Overdue)?.Count ?? 0) +
            await context.Invoices.AsNoTracking().CountAsync(
                x => x.LandlordUserId == landlordUserId &&
                     x.Status == InvoiceStatus.Issued &&
                     x.DueDate < today,
                cancellationToken),
            contractStats.Count(x => x.Status == RentalContractStatus.Active &&
                                     x.EndDate >= today &&
                                     x.EndDate <= expiringSoonDate),
            contractStats.Count(x => x.Status == RentalContractStatus.Expired ||
                                     (x.Status == RentalContractStatus.Active && x.EndDate < today)),
            appointmentStats.Count(x => DateOnly.FromDateTime(x.ScheduledAt.LocalDateTime) == today),
            appointmentStats.Count(x => x.Status == ViewingAppointmentStatus.Confirmed &&
                                        DateOnly.FromDateTime(x.ScheduledAt.LocalDateTime) > today),
            appointmentStats.Count(x => x.Status == ViewingAppointmentStatus.Completed),
            currentMonthRevenue,
            totalPaidRevenue,
            previousMonthRevenue,
            pendingCollectionAmount,
            totalRooms == 0 ? 0m : Math.Round(occupiedRooms * 100m / totalRooms, 1));

        return new LandlordDashboardResponse(
            overview,
            revenueSeries,
            houseResponses,
            recentInvoices,
            overdueInvoices);
    }

}
