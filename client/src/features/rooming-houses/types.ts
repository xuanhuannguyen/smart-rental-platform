export type RoomingHouseSummary = {
  id: string;
  landlordUserId: string;
  name: string;
  addressDisplay: string;
  approvalStatus: string;
  visibilityStatus: string;
  rejectedReason?: string | null;
  createdAt: string;
  updatedAt: string;
  coverImageUrl?: string | null;
  totalRooms?: number;
  availableRooms?: number;
};

export type Amenity = {
  id: number;
  name: string;
  scope: string;
  iconCode?: string | null;
};

export type PropertyImage = {
  id: string;
  objectKey: string;
  imageUrl: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
  createdAt: string;
};

export type PropertyImageRequest = {
  id?: string;
  objectKey: string;
  imageUrl?: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
};

export type LegalDocument = {
  roomingHouseId: string;
  documentType: string;
  frontImageObjectKey: string;
  backImageObjectKey: string;
  extraImageObjectKey?: string | null;
  documentNumberMasked: string;
  uploadedAt: string;
  createdAt: string;
  updatedAt: string;
};

export type UpdateLegalDocumentRequest = {
  documentType: string;
  frontImageObjectKey: string;
  backImageObjectKey: string;
  extraImageObjectKey?: string | null;
  documentNumber: string;
};

export type RoomingHouseOnboarding = {
  status: string;
  hasRoomingHouse: boolean;
  canCreateDraft: boolean;
  canEdit: boolean;
  canSubmit: boolean;
  canEnterLandlordDashboard: boolean;
  roomingHouseId?: string | null;
  roomingHouse?: RoomingHouseDetail | null;
};

export type RoomingHouseBasicInfoRequest = {
  name: string;
  description?: string;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
};

export type RoomingHouseDetail = RoomingHouseSummary & {
  description?: string;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
  legalDocument?: LegalDocument | null;
  rentalPolicy?: RentalPolicy | null;
  images: PropertyImage[];
  amenities: Amenity[];
};

export type RentalPolicy = {
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
};

export type UpdateRentalPolicyRequest = {
  minRentalMonths: number;
  maxRentalMonths: number;
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  defaultPaymentDay: number;
};
