using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Api.Extensions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Dashboard.Responses;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Api.Controllers.Dashboard;

[ApiController]
[Route("api/landlord/dashboard")]
[Authorize(Roles = "Landlord")]
public sealed class LandlordDashboardController : ControllerBase
{
    private readonly IAppDbContext dbContext;
    private readonly ICurrentUserService currentUserService;

    public LandlordDashboardController(IAppDbContext dbContext, ICurrentUserService currentUserService)
    {
        this.dbContext = dbContext;
        this.currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<LandlordDashboardResponse>>> GetDashboard([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var landlordId = currentUserService.GetRequiredUserId("Không tìm thấy người dùng đang đăng nhập.");
        if (!TryParseMonth(month, out var periodStart))
        {
            return BadRequest(new ApiErrorResponse { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Tháng thống kê không hợp lệ. Định dạng yêu cầu: YYYY-MM.", Details = new { field = "month" } });
        }

        var periodEnd = periodStart.AddMonths(1);
        var previousStart = periodStart.AddMonths(-1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var expiringDate = today.AddDays(30);
        var todayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var tomorrowStart = new DateTimeOffset(tomorrow.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var houses = dbContext.RoomingHouses.AsNoTracking().Where(x => x.LandlordUserId == landlordId && x.DeletedAt == null);
        var rooms = dbContext.Rooms.AsNoTracking().Where(x => x.RoomingHouse.LandlordUserId == landlordId && x.DeletedAt == null && x.RoomingHouse.DeletedAt == null);
        var contracts = dbContext.RentalContracts.AsNoTracking().Where(x => x.Room.RoomingHouse.LandlordUserId == landlordId && x.DeletedAt == null);
        var invoices = dbContext.Invoices.AsNoTracking().Where(x => x.LandlordUserId == landlordId);
        var requests = dbContext.RentalRequests.AsNoTracking().Where(x => x.Room.RoomingHouse.LandlordUserId == landlordId);
        var appointments = dbContext.ViewingAppointments.AsNoTracking().Where(x => x.Room.RoomingHouse.LandlordUserId == landlordId);

        var totalRooms = await rooms.CountAsync(cancellationToken);
        var occupiedRooms = await contracts.Where(x => x.Status == RentalContractStatus.Active).Select(x => x.RoomId).Distinct().CountAsync(cancellationToken);
        var availableRooms = await rooms.CountAsync(x => x.Status == RoomStatus.Available && !contracts.Any(c => c.RoomId == x.Id && c.Status == RentalContractStatus.Active), cancellationToken);
        var paidInvoices = invoices.Where(x => x.Status == InvoiceStatus.Paid && x.PaidAt != null);
        var monthlyRevenue = await paidInvoices.Where(x => x.PaidAt >= periodStart && x.PaidAt < periodEnd).SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var previousMonthRevenue = await paidInvoices.Where(x => x.PaidAt >= previousStart && x.PaidAt < periodStart).SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var chartStart = periodStart.AddMonths(-5);
        var chartRows = await paidInvoices.Where(x => x.PaidAt >= chartStart && x.PaidAt < periodEnd).Select(x => new { x.PaidAt, x.TotalAmount }).ToListAsync(cancellationToken);
        var chart = Enumerable.Range(0, 6).Select(index =>
        {
            var pointStart = chartStart.AddMonths(index);
            var pointEnd = pointStart.AddMonths(1);
            return new DashboardRevenuePointResponse { Month = pointStart.ToString("MM/yyyy"), Revenue = chartRows.Where(x => x.PaidAt >= pointStart && x.PaidAt < pointEnd).Sum(x => x.TotalAmount) };
        }).ToList();

        var latestInvoices = await invoices.OrderByDescending(x => x.CreatedAt).Take(5).Select(x => new DashboardInvoiceResponse
        {
            Id = x.Id, InvoiceCode = x.InvoiceNo, RoomName = x.Room.RoomNumber, Status = x.Status.ToString(), Amount = x.TotalAmount, DueDate = x.DueDate
        }).ToListAsync(cancellationToken);

        var result = new LandlordDashboardResponse
        {
            Period = periodStart.ToString("yyyy-MM"), TotalRoomingHouses = await houses.CountAsync(cancellationToken), TotalRooms = totalRooms,
            OccupiedRooms = occupiedRooms, AvailableRooms = availableRooms, OccupancyRate = totalRooms == 0 ? 0 : Math.Round(occupiedRooms * 100m / totalRooms, 1),
            MonthlyRevenue = monthlyRevenue, PreviousMonthRevenue = previousMonthRevenue,
            TotalRevenue = await paidInvoices.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0,
            ActiveContracts = await contracts.CountAsync(x => x.Status == RentalContractStatus.Active, cancellationToken),
            ExpiringContracts = await contracts.CountAsync(x => x.Status == RentalContractStatus.Active && x.EndDate >= today && x.EndDate <= expiringDate, cancellationToken),
            ExpiredContracts = await contracts.CountAsync(x => x.Status == RentalContractStatus.Expired, cancellationToken),
            PendingRequests = await requests.CountAsync(x => x.Status == RentalRequestStatus.Pending, cancellationToken),
            AcceptedRequests = await requests.CountAsync(x => x.Status == RentalRequestStatus.Accepted, cancellationToken),
            RejectedRequests = await requests.CountAsync(x => x.Status == RentalRequestStatus.Rejected, cancellationToken),
            TodayAppointments = await appointments.CountAsync(x => x.ScheduledAt >= todayStart && x.ScheduledAt < tomorrowStart && (x.Status == ViewingAppointmentStatus.Pending || x.Status == ViewingAppointmentStatus.Confirmed), cancellationToken),
            UpcomingAppointments = await appointments.CountAsync(x => x.ScheduledAt >= tomorrowStart && (x.Status == ViewingAppointmentStatus.Pending || x.Status == ViewingAppointmentStatus.Confirmed), cancellationToken),
            CompletedAppointments = await appointments.CountAsync(x => x.Status == ViewingAppointmentStatus.Completed, cancellationToken),
            DraftInvoices = await invoices.CountAsync(x => x.Status == InvoiceStatus.Draft, cancellationToken), IssuedInvoices = await invoices.CountAsync(x => x.Status == InvoiceStatus.Issued, cancellationToken),
            PaidInvoices = await invoices.CountAsync(x => x.Status == InvoiceStatus.Paid, cancellationToken), OverdueInvoices = await invoices.CountAsync(x => x.Status == InvoiceStatus.Overdue, cancellationToken),
            RevenueChart = chart, LatestInvoices = latestInvoices
        };

        return Ok(new ApiResponse<LandlordDashboardResponse> { Success = true, Message = "Tải thống kê dashboard thành công.", Data = result });
    }

    private static bool TryParseMonth(string? value, out DateTimeOffset start)
    {
        if (string.IsNullOrWhiteSpace(value)) { var now = DateTimeOffset.UtcNow; start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero); return true; }
        if (DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) { start = new DateTimeOffset(parsed.Year, parsed.Month, 1, 0, 0, 0, TimeSpan.Zero); return true; }
        start = default; return false;
    }
}
