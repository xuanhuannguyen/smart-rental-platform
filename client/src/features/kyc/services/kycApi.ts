import { apiClient } from '../../../shared/api/apiClient';
import { uploadImage } from '../../files/api';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  KycHistoryItemResponse,
  KycStatusResponse,
  KycSubmissionResponse,
  SubmitKycRequest
} from '../types/kyc.types';

export const kycApi = {
  submit: async (payload: SubmitKycRequest) => {
    // 1. Upload images via media workflow first
    const [frontUpload, backUpload, selfieUpload] = await Promise.all([
      uploadImage(payload.frontImage, 'KycDocument'),
      uploadImage(payload.backImage, 'KycDocument'),
      uploadImage(payload.selfieImage, 'KycDocument'),
    ]);

    if (!frontUpload.mediaAssetId || !backUpload.mediaAssetId || !selfieUpload.mediaAssetId) {
      throw new Error('Upload ảnh thất bại.');
    }

    // 2. Submit the KYC request with MediaAssetIds
    const requestBody = {
      documentType: payload.documentType,
      selfieCaptureMethod: payload.selfieCaptureMethod,
      frontMediaAssetId: frontUpload.mediaAssetId,
      backMediaAssetId: backUpload.mediaAssetId,
      selfieMediaAssetId: selfieUpload.mediaAssetId,
      manualCitizenId: payload.manualCitizenId?.trim() || null,
      manualFullName: payload.manualFullName?.trim() || null,
      manualDateOfBirth: payload.manualDateOfBirth || null,
      manualGender: payload.manualGender?.trim() || null,
      manualAddress: payload.manualAddress?.trim() || null,
    };

    return apiClient<ApiResponse<KycSubmissionResponse>>(ENDPOINTS.KYC.SUBMISSIONS, {
      method: 'POST',
      body: requestBody,
      auth: true
    });
  },

  getMyStatus: () =>
    apiClient<ApiResponse<KycStatusResponse>>(ENDPOINTS.KYC.MY_STATUS, {
      method: 'GET',
      auth: true
    }),

  getMyHistory: () =>
    apiClient<ApiResponse<KycHistoryItemResponse[]>>(ENDPOINTS.KYC.MY_HISTORY, {
      method: 'GET',
      auth: true
    })
};
