export type KycDocumentType = 'CCCD' | 'Passport';

export type SelfieCaptureMethod = 'Webcam' | 'MobileCamera' | 'Upload';

export interface SubmitKycRequest {
  documentType: KycDocumentType;
  selfieCaptureMethod: SelfieCaptureMethod;
  frontImage: File;
  backImage: File;
  selfieImage: File;
  manualCitizenId?: string;
  manualFullName?: string;
  manualDateOfBirth?: string;
  manualGender?: string;
  manualAddress?: string;
}

export interface KycSubmissionResponse {
  kycId: string;
  status: string;
  ekycResult: string;
  riskLevel: string;
  documentType: string;
  ocrFullName?: string | null;
  ocrCitizenIdMasked?: string | null;
  ocrDateOfBirth?: string | null;
  ocrGender?: string | null;
  ocrAddress?: string | null;
  ocrConfidence?: number | null;
  documentCheckResult?: string | null;
  faceMatchScore?: number | null;
  faceMatchResult?: string | null;
  livenessResult?: string | null;
  ekycErrorCode?: string | null;
  ekycErrorMessage?: string | null;
  requiresManualInput?: boolean;
  submittedWithManualFallback?: boolean;
  submittedAt: string;
  message: string;
}

export interface KycStatusResponse {
  hasSubmission: boolean;
  kycId?: string | null;
  status?: string | null;
  ekycResult?: string | null;
  riskLevel?: string | null;
  documentType?: string | null;
  frontMediaAssetId?: string | null;
  backMediaAssetId?: string | null;
  selfieMediaAssetId?: string | null;
  ocrFullName?: string | null;
  ocrCitizenIdMasked?: string | null;
  ocrDateOfBirth?: string | null;
  ocrGender?: string | null;
  ocrAddress?: string | null;
  faceMatchScore?: number | null;
  livenessResult?: string | null;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  rejectedReason?: string | null;
}

export interface KycHistoryItemResponse {
  kycId: string;
  status: string;
  ekycResult: string;
  riskLevel: string;
  documentType: string;
  ocrFullName?: string | null;
  ocrCitizenIdMasked?: string | null;
  ocrAddress?: string | null;
  faceMatchScore?: number | null;
  livenessResult?: string | null;
  submittedAt: string;
  reviewedAt?: string | null;
  rejectedReason?: string | null;
}
