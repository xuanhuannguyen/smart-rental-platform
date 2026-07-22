import {
  Amenity,
  PropertyImage,
  LegalDocument,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  RentalPolicy,
  UpdateRentalPolicyRequest
} from '../../shared/types';

export type {
  Amenity,
  PropertyImage,
  LegalDocument,
  RoomingHouseOnboarding,
  RoomingHouseBasicInfoRequest,
  RentalPolicy,
  UpdateRentalPolicyRequest
};

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
  averageRating?: number;
  totalReviews?: number;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  metadata?: unknown;
};



export type PropertyImageRequest = {
  id?: string;
  objectKey: string;
  imageUrl?: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
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

export type RoomingHouseServicePrice = {
  id: string;
  serviceTypeId: string;
  serviceTypeName: string;
  pricingUnit: string;
  unitPrice: number;
  note?: string | null;
  meterUnitName?: string | null;
  isActive: boolean;
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
  houseRule?: RoomingHouseRule | null;
  rentalPolicy?: RentalPolicy | null;
  images: PropertyImage[];
  amenities: Amenity[];
  rooms: RoomInHouseDetail[];
  servicePrices: RoomingHouseServicePrice[];
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
  averageRating?: number;
  totalReviews?: number;
};

export type RoomingHouseSearchMetadata = {
  aiAssisted: boolean;
  originalQuery?: string | null;
  interpretedQuery?: string | null;
  relaxedFields: string[];
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
  recentRoomingHouseIds?: string[];
  preferredAmenityIds?: number[];
  preferredRoomAmenityIds?: number[];
  centerLat?: number;
  centerLng?: number;
  radiusKm?: number;
  sortBy?: string;
  page?: number;
  pageSize?: number;
};

export type GuestRoomingHouseRecommendationRequest = {
  recentQueries: string[];
  recentRoomingHouseIds: string[];
  clickedRoomingHouseIds: string[];
  preferredAmenityIds: number[];
  preferredRoomAmenityIds: number[];
  provinceCode?: string | null;
  wardCode?: string | null;
  minPrice?: number | null;
  maxPrice?: number | null;
  minAreaM2?: number | null;
  maxAreaM2?: number | null;
  pageSize: number;
};

export type RoomingHouseRecommendationResponse = {
  items: RoomingHouseSearchItem[];
  reasons: Record<string, string>;
  aiAssisted: boolean;
  fallbackReason?: string | null;
};

export type RoomingHouseAiChatRequest = {
  message: string;
  context: 'home' | 'search' | 'detail';
  roomingHouseId?: string | null;
  mode?: 'fast' | 'detailed';
  conversationId?: string | null;
  chatHistory?: RoomingHouseAiChatHistoryMessage[];
};

export type RoomingHouseAiChatHistoryMessage = {
  role: 'assistant' | 'user';
  text: string;
};

export type NearbyPlace = {
  name: string;
  address?: string | null;
  displayAddress?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  distanceKm?: number | null;
  category?: string | null;
};

export type RoomingHouseAiChatResponse = {
  reply: string;
  intent: string;
  confidence: number;
  aiAssisted: boolean;
  conversationId: string;
  roomingHouses: RoomingHouseSearchItem[];
  nearbyPlaces: NearbyPlace[];
  followUpQuestions: string[];
  missingInformation: string[];
  usedSources: string[];
};

/** Lightweight listing item for home page cards. */
export type RoomingHouseListingItem = {
  id: string;
  name: string;
  addressDisplay: string;
  coverImageUrl?: string | null;
  availableRooms: number;
  minMonthlyRent?: number | null;
  maxMonthlyRent?: number | null;
  minAreaM2?: number | null;
  maxAreaM2?: number | null;
  amenities: Amenity[];
  createdAt: string;
  averageRating?: number;
  totalReviews?: number;
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

// Reviews
export type RoomingHouseReviewResponse = {
  id: string;
  rentalContractId: string;
  roomNumber?: string | null;
  contractStartDate?: string | null;
  contractEndDate?: string | null;
  tenantUserId: string;
  tenantDisplayName: string;
  tenantAvatarUrl?: string | null;
  rating: number;
  comment?: string | null;
  landlordReply?: string | null;
  landlordReplyCreatedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  isReported?: boolean;
  moderationStatus?: string;
  moderationReason?: string | null;
  aiModerationProvider?: string | null;
  aiModerationRiskLevel?: string | null;
  adminNote?: string | null;
  images: PropertyImageRequest[];
};

export type ReviewEligibilityResponse = {
  isEligible: boolean;
  reason?: string | null;
};

export interface CreateRoomingHouseReviewRequest {
  rating: number;
  comment: string;
  images?: File[];
}

export interface RoomingHouseReviewEligibilitySummaryResponse {
  isEligible: boolean;
  contractId?: string | null;
  reason?: string | null;
  existingReview?: RoomingHouseReviewResponse | null;
  reviewableContracts: ReviewableContractResponse[];
};

export interface ReviewableContractResponse {
  contractId: string;
  roomNumber: string;
  startDate: string;
  endDate?: string | null;
  status: string;
  canReview: boolean;
  reviewStatus?: string | null;
  reviewId?: string | null;
  review?: RoomingHouseReviewResponse | null;
}

export type UpdateRoomingHouseReviewRequest = {
  rating: number;
  comment?: string | null;
  retainedImageIds?: string[];
  newImages?: File[];
};

export type ReplyRoomingHouseReviewRequest = {
  reply: string;
};

export type RoomingHouseReviewListResponse = {
  averageRating: number;
  totalReviews: number;
  ratingDistribution: Record<number, number>;
  reviews: RoomingHouseReviewResponse[];
};

export type CreateReviewReportRequest = {
  reason: string;
};

export type ReviewReportResponse = {
  id: string;
  roomingHouseReviewId: string;
  reporterUserId: string;
  reporterDisplayName: string;
  reason: string;
  status: string;
  adminNote?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
  review?: RoomingHouseReviewResponse | null;
};
