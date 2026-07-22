import {
  Province,
  Ward,
  Amenity,
  PropertyImage,
  LegalDocument,
  RentalPolicy,
  FileUploadResponse,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest
} from '../../../shared/types';

export type {
  Province,
  Ward,
  Amenity,
  PropertyImage,
  LegalDocument,
  RentalPolicy,
  FileUploadResponse,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  UpdateRentalPolicyRequest
};

export interface PropertyImageItemRequest {
  id?: string;
  objectKey: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
}

export interface RoomingHouseDetail {
  id: string;
  landlordUserId: string;
  name: string;
  description?: string | null;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  addressDisplay: string;
  latitude?: number | null;
  longitude?: number | null;
  approvalStatus: string;
  visibilityStatus: string;
  rejectedReason?: string | null;
  reviewedByAdminId?: string | null;
  reviewedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  legalDocument?: LegalDocument | null;
  rentalPolicy?: RentalPolicy | null;
  images: PropertyImage[];
  amenities: Amenity[];
}

export interface DashboardRevenuePoint {
  month: string;
  revenue: number;
}

export interface DashboardInvoice {
  id: string;
  invoiceCode: string;
  roomName: string;
  status: 'Draft' | 'Issued' | 'Paid' | 'Overdue' | 'Cancelled';
  amount: number;
  dueDate: string;
}

export interface LandlordDashboardData {
  period: string;
  totalRoomingHouses: number;
  totalRooms: number;
  occupiedRooms: number;
  availableRooms: number;
  occupancyRate: number;
  monthlyRevenue: number;
  previousMonthRevenue: number;
  totalRevenue: number;
  activeContracts: number;
  expiringContracts: number;
  expiredContracts: number;
  pendingRequests: number;
  acceptedRequests: number;
  rejectedRequests: number;
  todayAppointments: number;
  upcomingAppointments: number;
  completedAppointments: number;
  draftInvoices: number;
  issuedInvoices: number;
  paidInvoices: number;
  overdueInvoices: number;
  revenueChart: DashboardRevenuePoint[];
  latestInvoices: DashboardInvoice[];
}
