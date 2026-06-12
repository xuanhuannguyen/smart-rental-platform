export interface Province {
  code: string;
  name: string;
  type: string;
}

export interface Ward {
  code: string;
  provinceCode: string;
  name: string;
  type: string;
}

export interface Amenity {
  id: number;
  name: string;
  scope: string;
  iconCode?: string | null;
}

export interface RoomingHouseBasicInfoRequest {
  name: string;
  description?: string | null;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface PropertyImageItemRequest {
  id?: string;
  objectKey: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
}

export interface PropertyImage {
  id: string;
  objectKey: string;
  imageUrl: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
  createdAt: string;
}

export interface LegalDocument {
  roomingHouseId: string;
  documentType: string;
  frontImageObjectKey: string;
  backImageObjectKey: string;
  extraImageObjectKey?: string | null;
  documentNumberMasked: string;
  uploadedAt: string;
  createdAt: string;
  updatedAt: string;
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

export interface RoomingHouseOnboarding {
  status: 'None' | 'Draft' | 'Pending' | 'Rejected' | 'Approved' | string;
  hasRoomingHouse: boolean;
  canCreateDraft: boolean;
  canEdit: boolean;
  canSubmit: boolean;
  canEnterLandlordDashboard: boolean;
  roomingHouseId?: string | null;
  roomingHouse?: RoomingHouseDetail | null;
}

export interface FileUploadResponse {
  objectKey: string;
  url: string;
}

export interface RentalPolicy {
  id: string;
  roomingHouseId: string;
  minRentalMonths: number;
  maxRentalMonths: number;
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  defaultPaymentDay: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateRentalPolicyRequest {
  minRentalMonths: number;
  maxRentalMonths: number;
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  defaultPaymentDay: number;
}
