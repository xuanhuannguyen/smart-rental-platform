import { apiClient } from '../../shared/api/apiClient';
import { getApiErrorMessage } from '../../shared/api/apiError';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import { tokenStorage } from '../../shared/api/tokenStorage';
import { env } from '../../config/env';
import type {
  ContractDetailResponse,
  ContractFileResponse,
  ContractFileViewUrlResponse,
  ContractHistoryItemResponse,
  ContractPreviewResponse,
  RejectContractRequest,
  RequestContractRevisionRequest,
  SubmitContractOccupantsRequest,
  TerminateContractRequest,
  UpdateContractTermsRequest,
  ContractAppendixResponse,
  CreateContractAppendixRequest,
  StartESignEnvelopeRequest,
  StartESignEnvelopeResponse,
  ESignEnvelopeResponse,
  ESignOtpMethod,
  RequestESignOtpResponse,
  SubmitESignOtpRequest
} from './types';

export const contractApi = {
  getMyHistory: () =>
    apiClient<ApiResponse<ContractHistoryItemResponse[]>>(ENDPOINTS.CONTRACTS.MY_HISTORY, {
      method: 'GET',
      auth: true
    }),

  getLandlordContracts: () =>
    apiClient<ApiResponse<ContractHistoryItemResponse[]>>(ENDPOINTS.CONTRACTS.LANDLORD, {
      method: 'GET',
      auth: true
    }),

  getContract: (id: string) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.BY_ID(id), {
      method: 'GET',
      auth: true
    }),

  getContractPreview: (id: string) =>
    apiClient<ApiResponse<ContractPreviewResponse>>(ENDPOINTS.CONTRACTS.PREVIEW(id), {
      method: 'GET',
      auth: true
    }),

  submitContractOccupants: (id: string, payload: SubmitContractOccupantsRequest) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.SUBMIT_OCCUPANTS(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  updateContractTerms: (id: string, payload: UpdateContractTermsRequest) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.TERMS(id), {
      method: 'PUT',
      auth: true,
      body: payload
    }),

  generateContractFile: (id: string) =>
    apiClient<ApiResponse<ContractFileResponse>>(ENDPOINTS.CONTRACTS.GENERATE_FILE(id), {
      method: 'POST',
      auth: true
    }),

  getContractFiles: (id: string) =>
    apiClient<ApiResponse<ContractFileResponse[]>>(ENDPOINTS.CONTRACTS.FILES(id), {
      method: 'GET',
      auth: true
    }),

  getContractFileViewUrl: (id: string, fileId: string) =>
    apiClient<ApiResponse<ContractFileViewUrlResponse>>(ENDPOINTS.CONTRACTS.VIEW_FILE_URL(id, fileId), {
      method: 'GET',
      auth: true
    }),

  downloadContractFile: async (id: string, fileId: string) => {
    const headers = new Headers();
    const accessToken = tokenStorage.getAccessToken();

    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }

    const response = await fetch(`${env.apiBaseUrl}${ENDPOINTS.CONTRACTS.DOWNLOAD_FILE(id, fileId)}`, {
      method: 'GET',
      headers
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      throw new Error(getApiErrorMessage(payload, 'Không tải được file hợp đồng.'));
    }

    return response.blob();
  },

  requestContractRevision: (id: string, payload: RequestContractRevisionRequest) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.REVISION_REQUEST(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  rejectContract: (id: string, payload: RejectContractRequest) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.REJECT(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  terminateContract: (id: string, payload: TerminateContractRequest) =>
    apiClient<ApiResponse<ContractDetailResponse>>(ENDPOINTS.CONTRACTS.TERMINATE(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  createAppendix: (id: string, payload: CreateContractAppendixRequest) =>
    apiClient<ApiResponse<ContractAppendixResponse>>(ENDPOINTS.CONTRACTS.APPENDICES(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  getAppendices: (id: string) =>
    apiClient<ApiResponse<ContractAppendixResponse[]>>(ENDPOINTS.CONTRACTS.APPENDICES(id), {
      method: 'GET',
      auth: true
    }),

  getAppendix: (id: string, appendixId: string) =>
    apiClient<ApiResponse<ContractAppendixResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_BY_ID(id, appendixId), {
      method: 'GET',
      auth: true
    }),

  deleteAppendix: (id: string, appendixId: string) =>
    apiClient<void>(ENDPOINTS.CONTRACTS.APPENDIX_BY_ID(id, appendixId), {
      method: 'DELETE',
      auth: true
    }),

  updateAppendix: (id: string, appendixId: string, payload: CreateContractAppendixRequest) =>
    apiClient<ApiResponse<ContractAppendixResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_BY_ID(id, appendixId), {
      method: 'PUT',
      auth: true,
      body: payload
    }),

  getAppendixPreviewPdf: async (id: string, appendixId: string) => {
    const headers = new Headers();
    const accessToken = tokenStorage.getAccessToken();

    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }

    const response = await fetch(`${env.apiBaseUrl}${ENDPOINTS.CONTRACTS.APPENDIX_PREVIEW(id, appendixId)}`, {
      method: 'GET',
      headers
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      throw new Error(getApiErrorMessage(payload, 'Không tải được preview phụ lục.'));
    }

    return response.blob();
  },

  rejectAppendix: (id: string, appendixId: string, payload: RejectContractRequest) =>
    apiClient<ApiResponse<ContractAppendixResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_REJECT(id, appendixId), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  requestAppendixRevision: (id: string, appendixId: string, payload: RequestContractRevisionRequest) =>
    apiClient<ApiResponse<ContractAppendixResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_REVISION_REQUEST(id, appendixId), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  startESignEnvelope: (id: string, payload: StartESignEnvelopeRequest) =>
    apiClient<ApiResponse<StartESignEnvelopeResponse>>(ENDPOINTS.CONTRACTS.ESIGN_ENVELOPE(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  requestESignOtp: (id: string, method: ESignOtpMethod) =>
    apiClient<ApiResponse<RequestESignOtpResponse>>(ENDPOINTS.CONTRACTS.ESIGN_REQUEST_OTP(id), {
      method: 'POST',
      auth: true,
      body: { method }
    }),

  submitESignOtp: (id: string, payload: SubmitESignOtpRequest) =>
    apiClient<ApiResponse<boolean>>(ENDPOINTS.CONTRACTS.ESIGN_SUBMIT_OTP(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  getESignEnvelopeStatus: (id: string, envelopeId: string) =>
    apiClient<ApiResponse<ESignEnvelopeResponse>>(ENDPOINTS.CONTRACTS.ESIGN_ENVELOPE_STATUS(id, envelopeId), {
      method: 'GET',
      auth: true
    }),

  startAppendixESignEnvelope: (id: string, appendixId: string, payload: StartESignEnvelopeRequest) =>
    apiClient<ApiResponse<StartESignEnvelopeResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_ESIGN_ENVELOPE(id, appendixId), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  requestAppendixESignOtp: (id: string, appendixId: string, method: ESignOtpMethod) =>
    apiClient<ApiResponse<RequestESignOtpResponse>>(ENDPOINTS.CONTRACTS.APPENDIX_ESIGN_REQUEST_OTP(id, appendixId), {
      method: 'POST',
      auth: true,
      body: { method }
    }),

  submitAppendixESignOtp: (id: string, appendixId: string, payload: SubmitESignOtpRequest) =>
    apiClient<ApiResponse<boolean>>(ENDPOINTS.CONTRACTS.APPENDIX_ESIGN_SUBMIT_OTP(id, appendixId), {
      method: 'POST',
      auth: true,
      body: payload
    })
};
