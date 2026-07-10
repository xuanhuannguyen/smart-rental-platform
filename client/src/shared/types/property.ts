export interface Amenity {
  id: number;
  name: string;
  scope: string;
  iconCode?: string | null;
}

export interface PropertyImage {
  id: string;
  mediaAssetId?: string | null;
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

export interface FileUploadResponse {
  objectKey: string;
  url: string;
}

export interface RoomingHouseOnboarding<TRoomingHouse = any> {
  status: string;
  hasRoomingHouse: boolean;
  canCreateDraft: boolean;
  canEdit: boolean;
  canSubmit: boolean;
  canEnterLandlordDashboard: boolean;
  roomingHouseId?: string | null;
  roomingHouse?: TRoomingHouse | null;
}

export interface RoomingHouseBasicInfoRequest {
  name: string;
  description?: string | null;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
  googleMapUrl?: string | null;
}

export interface UpdateRentalPolicyRequest {
  minRentalMonths: number;
  maxRentalMonths: number;
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  defaultPaymentDay: number;
}
