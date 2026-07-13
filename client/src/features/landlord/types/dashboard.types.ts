export interface LandlordDashboardOverview {
  roomingHouseCount: number;
  approvedRoomingHouseCount: number;
  totalRoomCount: number;
  occupiedRoomCount: number;
  availableRoomCount: number;
  maintenanceRoomCount: number;
  activeContractCount: number;
  pendingRentalRequestCount: number;
  acceptedRentalRequestCount: number;
  rejectedRentalRequestCount: number;
  draftInvoiceCount: number;
  issuedInvoiceCount: number;
  paidInvoiceCount: number;
  overdueInvoiceCount: number;
  expiringContractCount: number;
  expiredContractCount: number;
  todayAppointmentCount: number;
  upcomingAppointmentCount: number;
  completedAppointmentCount: number;
  currentMonthRevenue: number;
  totalPaidRevenue: number;
  previousMonthRevenue: number;
  pendingCollectionAmount: number;
  occupancyRate: number;
}

export interface LandlordDashboardRevenuePoint {
  period: string;
  revenue: number;
}

export interface LandlordDashboardHouse {
  id: string;
  name: string;
  addressDisplay: string;
  approvalStatus: string;
  visibilityStatus: string;
  totalRooms: number;
  occupiedRooms: number;
  availableRooms: number;
  activeContracts: number;
  currentMonthRevenue: number;
  pendingCollectionAmount: number;
}

export interface LandlordDashboardInvoice {
  id: string;
  invoiceNo: string;
  roomingHouseName: string;
  roomNumber: string;
  tenantName: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  dueDate: string;
  totalAmount: number;
  status: string;
}

export interface LandlordDashboard {
  overview: LandlordDashboardOverview;
  revenueSeries: LandlordDashboardRevenuePoint[];
  houses: LandlordDashboardHouse[];
  recentInvoices: LandlordDashboardInvoice[];
  overdueInvoices: LandlordDashboardInvoice[];
}
