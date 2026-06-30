import { ENDPOINTS } from '../../shared/api/endpoints';
import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import type { 
  WalletResponse, 
  WalletTransactionResponse, 
  CreatePayOSTopUpRequest, 
  CreatePayOSTopUpResponse,
  WalletTopUpResponse,
  PagedResult
} from './types';

export const walletApi = {
  getMyWallet: () =>
    apiClient<ApiResponse<WalletResponse>>(ENDPOINTS.WALLET.ROOT, {
      method: 'GET',
      auth: true
    }),

  getTransactions: (page: number = 1, pageSize: number = 20) =>
    apiClient<ApiResponse<PagedResult<WalletTransactionResponse>>>(
      `${ENDPOINTS.WALLET.TRANSACTIONS}?page=${page}&pageSize=${pageSize}`,
      {
        method: 'GET',
        auth: true
      }
    ),

  getTopUp: (id: string) =>
    apiClient<ApiResponse<WalletTopUpResponse>>(ENDPOINTS.WALLET.TOP_UP_BY_ID(id), {
      method: 'GET',
      auth: true
    }),

  getTopUps: (page: number = 1, pageSize: number = 10) =>
    apiClient<ApiResponse<PagedResult<WalletTopUpResponse>>>(
      `${ENDPOINTS.WALLET.TOP_UPS}?page=${page}&pageSize=${pageSize}`,
      {
        method: 'GET',
        auth: true
      }
    ),

  createPayOSTopUp: (payload: CreatePayOSTopUpRequest) =>
    apiClient<ApiResponse<CreatePayOSTopUpResponse>>(ENDPOINTS.WALLET.CREATE_PAYOS_TOPUP, {
      method: 'POST',
      auth: true,
      body: payload
    })
};
