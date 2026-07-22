namespace SmartRentalPlatform.Contracts.LandlordDashboard.Responses;

public sealed record LandlordDashboardResponse(
    LandlordDashboardOverviewResponse Overview,
    IReadOnlyList<LandlordDashboardRevenuePointResponse> RevenueSeries,
    IReadOnlyList<LandlordDashboardHouseResponse> Houses,
    IReadOnlyList<LandlordDashboardInvoiceResponse> RecentInvoices,
    IReadOnlyList<LandlordDashboardInvoiceResponse> OverdueInvoices);

public sealed record LandlordDashboardOverviewResponse(
    int RoomingHouseCount,
    int ApprovedRoomingHouseCount,
    int TotalRoomCount,
    int OccupiedRoomCount,
    int AvailableRoomCount,
    int MaintenanceRoomCount,
    int ActiveContractCount,
    int PendingRentalRequestCount,
    int AcceptedRentalRequestCount,
    int RejectedRentalRequestCount,
    int DraftInvoiceCount,
    int IssuedInvoiceCount,
    int PaidInvoiceCount,
    int OverdueInvoiceCount,
    int ExpiringContractCount,
    int ExpiredContractCount,
    int TodayAppointmentCount,
    int UpcomingAppointmentCount,
    int CompletedAppointmentCount,
    decimal CurrentMonthRevenue,
    decimal TotalPaidRevenue,
    decimal PreviousMonthRevenue,
    decimal PendingCollectionAmount,
    decimal OccupancyRate);

public sealed record LandlordDashboardRevenuePointResponse(
    string Period,
    decimal Revenue);

public sealed record LandlordDashboardHouseResponse(
    Guid Id,
    string Name,
    string AddressDisplay,
    string ApprovalStatus,
    string VisibilityStatus,
    int TotalRooms,
    int OccupiedRooms,
    int AvailableRooms,
    int ActiveContracts,
    decimal CurrentMonthRevenue,
    decimal PendingCollectionAmount);

public sealed record LandlordDashboardInvoiceResponse(
    Guid Id,
    string InvoiceNo,
    string RoomingHouseName,
    string RoomNumber,
    string TenantName,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    DateOnly DueDate,
    decimal TotalAmount,
    string Status);
