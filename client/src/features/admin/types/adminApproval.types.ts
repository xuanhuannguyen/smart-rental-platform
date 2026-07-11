export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  pageSize: number;
  pageNumber: number;
}

export interface AdminUserListItem {
  id: string;
  email: string;
  displayName: string;
  phoneNumber?: string | null;
  roles: string[];
  status: string;
  onboardingStatus: string;
  createdAt: string;
}

export interface AdminKycListItem {
  id: string;
  userId: string;
  userEmail: string;
  userDisplayName: string;
  ocrFullName?: string | null;
  ocrCitizenIdMasked?: string | null;
  status: string;
  riskLevel: string;
  submittedAt: string;
}

export interface AdminKycDetail extends AdminKycListItem {
  documentType: string;
  ekycProvider: string;
  ekycSessionId?: string | null;
  ocrDateOfBirth?: string | null;
  ocrGender?: string | null;
  ocrAddress?: string | null;
  ocrConfidence?: number | null;
  documentCheckResult?: string | null;
  faceMatchScore?: number | null;
  faceMatchResult?: string | null;
  livenessResult?: string | null;
  ekycResult: string;
  ekycErrorCode?: string | null;
  ekycErrorMessage?: string | null;
  frontImageUrl: string;
  backImageUrl: string;
  selfieImageUrl: string;
  rejectedReason?: string | null;
  reviewedByAdminId?: string | null;
  reviewedAt?: string | null;
}

export interface AdminRoomingHouseListItem {
  id: string;
  landlordUserId: string;
  landlordEmail: string;
  landlordName: string;
  name: string;
  addressDisplay: string;
  approvalStatus: string;
  visibilityStatus: string;
  createdAt: string;
}

export interface AdminRoomingHouseDetail extends AdminRoomingHouseListItem {
  description?: string | null;
  addressLine: string;
  provinceCode: string;
  wardCode: string;
  latitude?: number | null;
  longitude?: number | null;
  rejectedReason?: string | null;
  reviewedByAdminId?: string | null;
  reviewedAt?: string | null;
  legalDocument?: {
    frontMediaAssetId?: string | null;
    backMediaAssetId?: string | null;
    extraMediaAssetId?: string | null;
    documentType: string;
    frontImageObjectKey: string;
    backImageObjectKey: string;
    extraImageObjectKey?: string | null;
    frontImageUrl?: string | null;
    backImageUrl?: string | null;
    extraImageUrl?: string | null;
    documentNumberMasked: string;
    uploadedAt: string;
  } | null;
  images: Array<{
    id: string;
    objectKey: string;
    imageUrl: string;
    caption?: string | null;
    isCover: boolean;
    sortOrder: number;
  }>;
  amenities: Array<{
    id: number;
    name: string;
    scope: string;
    iconCode?: string | null;
  }>;
  rooms: Array<{
    id: string;
    roomNumber: string;
    floor: number;
    areaM2?: number | null;
    maxOccupants: number;
    status: string;
  }>;
}

export interface AdminKycInfo {
  kycId: string;
  frontImageUrl: string;
  backImageUrl: string;
  selfieImageUrl: string;
  ocrFullName?: string | null;
  ocrCitizenIdMasked?: string | null;
  ocrDateOfBirth?: string | null;
  ocrGender?: string | null;
  ocrAddress?: string | null;
  faceMatchScore?: number | null;
  ekycResult: string;
  riskLevel: string;
  submittedAt: string;
  approvedAt?: string | null;
}

export interface AdminUserDetail extends AdminUserListItem {
  fullName?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  addressLine?: string | null;
  verifiedCitizenIdMasked?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  kycInfo?: AdminKycInfo | null;
}
