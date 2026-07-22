namespace SmartRentalPlatform.Contracts.Dashboard.Responses;

public sealed class LandlordDashboardResponse
{
    public string Period { get; set; } = string.Empty;
    public int TotalRoomingHouses { get; set; }
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int AvailableRooms { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal PreviousMonthRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ActiveContracts { get; set; }
    public int ExpiringContracts { get; set; }
    public int ExpiredContracts { get; set; }
    public int PendingRequests { get; set; }
    public int AcceptedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int TodayAppointments { get; set; }
    public int UpcomingAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int DraftInvoices { get; set; }
    public int IssuedInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public List<DashboardRevenuePointResponse> RevenueChart { get; set; } = [];
    public List<DashboardInvoiceResponse> LatestInvoices { get; set; } = [];
}

public sealed class DashboardRevenuePointResponse
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public sealed class DashboardInvoiceResponse
{
    public Guid Id { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
}
