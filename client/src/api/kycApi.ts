import { apiClient, type ApiResponse } from './apiClient';

export type DocumentType = 'CCCD' | 'Passport';
export type SelfieCaptureMethod = 'Webcam' | 'MobileCamera' | 'Upload';

export interface KycSubmissionResult {
  kycId: string;
  status: string;
  ekycResult: string;
  riskLevel: string;
  documentType: string;
  ocrFullName?: string;
  ocrCitizenIdMasked?: string;
  message: string;
}

export interface KycStatus {
  hasSubmission: boolean;
  kycId?: string;
  status?: string;
  ekycResult?: string;
  riskLevel?: string;
  documentType?: string;
  ocrFullName?: string;
  ocrCitizenIdMasked?: string;
  faceMatchScore?: number;
  livenessResult?: string;
  submittedAt?: string;
  reviewedAt?: string;
  rejected_reason?: string;
  rejectedReason?: string;
}

export interface KycHistoryItem {
  kycId: string;
  status: string;
  ekycResult: string;
  riskLevel: string;
  documentType: string;
  submittedAt: string;
  rejected_reason?: string;
  rejectedReason?: string;
}

export interface SubmitKycPayload {
  documentType: DocumentType;
  selfieCaptureMethod: SelfieCaptureMethod;
  frontImage: File;
  backImage: File;
  selfieImage: File;
}

export async function submitKyc(payload: SubmitKycPayload) {
  const form = new FormData();
  form.append('DocumentType', payload.documentType);
  form.append('SelfieCaptureMethod', payload.selfieCaptureMethod);
  form.append('FrontImage', payload.frontImage);
  form.append('BackImage', payload.backImage);
  form.append('SelfieImage', payload.selfieImage);

  const { data } = await apiClient.post<ApiResponse<KycSubmissionResult>>(
    '/api/kyc/submissions',
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );
  return data;
}

export async function getMyKycStatus() {
  const { data } = await apiClient.get<ApiResponse<KycStatus>>('/api/kyc/my-status');
  return data;
}

export async function getMyKycHistory() {
  const { data } = await apiClient.get<ApiResponse<KycHistoryItem[]>>('/api/kyc/my-history');
  return data;
}
