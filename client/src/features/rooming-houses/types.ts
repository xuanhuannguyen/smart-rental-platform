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

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
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

export type HouseRuleSourceType = 'PdfUpload' | 'FormGenerated';

export type RoomingHouseRule = {
  id: string;
  roomingHouseId: string;
  sourceType: HouseRuleSourceType;
  pdfObjectKey: string;
  generalRules?: string | null;
  quietHours?: string | null;
  securityPolicy?: string | null;
  cleaningPolicy?: string | null;
  guestPolicy?: string | null;
  parkingPolicy?: string | null;
  utilityPolicy?: string | null;
  damageCompensationPolicy?: string | null;
  additionalNotes?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type UpsertRoomingHouseRuleRequest = {
  sourceType: HouseRuleSourceType;
  pdfObjectKey?: string | null;
  generalRules?: string | null;
  quietHours?: string | null;
  securityPolicy?: string | null;
  cleaningPolicy?: string | null;
  guestPolicy?: string | null;
  parkingPolicy?: string | null;
  utilityPolicy?: string | null;
  damageCompensationPolicy?: string | null;
  additionalNotes?: string | null;
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
  googleMapUrl?: string | null;
};

export type RoomPriceTierSummary = {
  id: string;
  occupantCount: number;
  monthlyRent: number;
  isActive: boolean;
};

export type RoomInHouseDetail = {
  id: string;
  roomingHouseId: string;
  roomNumber: string;
  floor: number;
  areaM2?: number | null;
  maxOccupants: number;
  isTieredPricing: boolean;
  status: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
  priceTiers: RoomPriceTierSummary[];
  images: PropertyImage[];
  amenities: Amenity[];
};

export type RoomingHouseDetail = RoomingHouseSummary & {
  description?: string;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
  googleMapUrl?: string | null;
  legalDocument?: LegalDocument | null;
  leasePolicy?: LeasePolicy | null;
  houseRule?: RoomingHouseRule | null;
  images: PropertyImage[];
  amenities: Amenity[];
  rooms: RoomInHouseDetail[];
};

export type RoomingHouseSearchItem = {
  id: string;
  name: string;
  addressDisplay: string;
  latitude?: number | null;
  longitude?: number | null;
  distanceKm?: number | null;
  coverImageUrl?: string | null;
  availableRooms: number;
  totalRooms: number;
  minMonthlyRent?: number | null;
  maxMonthlyRent?: number | null;
  minAreaM2?: number | null;
  maxAreaM2?: number | null;
  amenities: Amenity[];
  createdAt: string;
};

export type RoomingHouseSearchParams = {
  q?: string;
  provinceCode?: string;
  wardCode?: string;
  minPrice?: number;
  maxPrice?: number;
  minAreaM2?: number;
  maxAreaM2?: number;
  minOccupants?: number;
  amenityIds?: number[];
  roomAmenityIds?: number[];
  centerLat?: number;
  centerLng?: number;
  radiusKm?: number;
  sortBy?: string;
  page?: number;
  pageSize?: number;
};

export type LocationSearchResult = {
  refId?: string | null;
  displayAddress: string;
  name?: string | null;
  address?: string | null;
  latitude: number;
  longitude: number;
};

export type LocationSuggestion = LocationSearchResult;

export type LeasePolicy = {
  id: string;
  roomingHouseId: string;
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  discount6MonthsPercent: number;
  discount9MonthsPercent: number;
  discount12MonthsPercent: number;
  discount24MonthsPercent: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
};

export type UpdateLeasePolicyRequest = {
  allowShortTermRenewal: boolean;
  renewalNoticeDays: number;
  depositMonths: number;
  discount6MonthsPercent: number;
  discount9MonthsPercent: number;
  discount12MonthsPercent: number;
  discount24MonthsPercent: number;
};
