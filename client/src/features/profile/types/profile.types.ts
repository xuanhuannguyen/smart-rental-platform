export interface UserProfileResponse {
  userId: string;
  displayName?: string | null;
  phoneNumber?: string | null;
  fullName?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  addressLine?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  verifiedCitizenIdMasked?: string | null;
  kycStatus?: string | null;
  kycReviewedAt?: string | null;
  identityVerified: boolean;
  profileCompleted: boolean;
  avatarUrl?: string | null;
  avatarMediaAssetId?: string | null;
  isGoogleUser?: boolean;
}

export interface UpdateUserProfileRequest {
  displayName: string;
  phoneNumber?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  avatarMediaAssetId?: string | null;
}

export interface LandlordEligibilityResponse {
  canContinue: boolean;
  nextStep: string;
  reason?: string | null;
}

export interface UserSession {
  id: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
  expiresAt: string;
  isCurrentSession: boolean;
}
