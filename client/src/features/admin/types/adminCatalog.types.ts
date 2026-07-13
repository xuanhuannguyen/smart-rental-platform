// Paged Result mapping
export interface PagedResult<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// 1. Province & Ward
export interface AdminProvinceResponse {
  code: string;
  name: string;
  type: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AdminWardResponse {
  code: string;
  name: string;
  type: string;
  provinceCode: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProvinceRequest {
  code: string;
  name: string;
  type: string;
}

export interface UpdateProvinceRequest {
  name: string;
  type: string;
}

export interface CreateWardRequest {
  code: string;
  name: string;
  type: string;
  provinceCode: string;
}

export interface UpdateWardRequest {
  name: string;
  type: string;
}

// 2. Amenity
export interface AdminAmenityResponse {
  id: number;
  name: string;
  scope: string; // 'House', 'Room', 'Both'
  iconCode?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateAmenityRequest {
  name: string;
  scope: string;
  iconCode?: string;
}

export interface UpdateAmenityRequest {
  name: string;
  scope: string;
  iconCode?: string;
}

// 3. Billing Service Type
export interface AdminBillingServiceTypeResponse {
  id: number;
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateBillingServiceTypeRequest {
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
}

export interface UpdateBillingServiceTypeRequest {
  name: string;
  supportsMeterReading: boolean;
  meterUnitName: string;
}

// 4. Review Reports
export interface AdminReviewReportResponse {
  id: string;
  roomingHouseReviewId?: string;
  reviewId: string;
  reporterUserId: string;
  reporterDisplayName?: string;
  reporterName: string;
  reason: string;
  status: string; // 'Pending' | 'Resolved' | 'Dismissed'
  adminNote?: string;
  resolution?: string;
  review?: {
    id: string;
    rating: number;
    comment?: string | null;
    tenantDisplayName: string;
    tenantAvatarUrl?: string | null;
    images?: Array<{ id: string; imageUrl: string; caption?: string | null; sortOrder: number }>;
  } | null;
  reviewContent?: string;
  roomingHouseName?: string;
  reviewerName?: string;
  createdAt: string;
  resolvedAt?: string;
}

export interface ResolveReviewReportRequest {
  adminNote?: string;
  /** If true, hides the reported review */
  hideReview: boolean;
}

export interface AdminReviewModerationItemResponse {
  id: string;
  roomingHouseId: string;
  roomingHouseName: string;
  tenantUserId: string;
  tenantDisplayName: string;
  tenantAvatarUrl?: string | null;
  rating: number;
  comment?: string | null;
  moderationStatus: string;
  moderationReason?: string | null;
  aiModerationProvider?: string | null;
  aiModerationRiskLevel?: string | null;
  aiModerationCategories?: string | null;
  aiContentComment?: string | null;
  aiImageComment?: string | null;
  aiReviewedAt?: string | null;
  adminNote?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  images: Array<{ id: string; imageUrl: string; caption?: string | null; sortOrder: number }>;
}

