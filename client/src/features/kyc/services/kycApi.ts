import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  KycHistoryItemResponse,
  KycStatusResponse,
  KycSubmissionResponse,
  SubmitKycRequest
} from '../types/kyc.types';

export const kycApi = {
  submit: (payload: SubmitKycRequest) => {
    const formData = new FormData();
    formData.append('DocumentType', payload.documentType);
    formData.append('SelfieCaptureMethod', payload.selfieCaptureMethod);
    formData.append('FrontImage', payload.frontImage);
    formData.append('BackImage', payload.backImage);
    formData.append('SelfieImage', payload.selfieImage);

    return apiClient<ApiResponse<KycSubmissionResponse>>(ENDPOINTS.KYC.SUBMISSIONS, {
      method: 'POST',
      body: formData,
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
