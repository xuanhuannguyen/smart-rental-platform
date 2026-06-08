import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  CreatePayOSTopUpRequest,
  CreatePayOSTopUpResponse,
  MockPaymentResponse,
  PagedWalletTransactions,
  Wallet
} from './types';

export const walletApi = {
  getWallet: () =>
    apiClient<ApiResponse<Wallet>>(ENDPOINTS.WALLET.ME, {
      method: 'GET',
      auth: true
    }),

  getTransactions: (page = 1, pageSize = 20) =>
    apiClient<ApiResponse<PagedWalletTransactions>>(
      `${ENDPOINTS.WALLET.TRANSACTIONS}?page=${page}&pageSize=${pageSize}`,
      {
        method: 'GET',
        auth: true
      }
    ),

  createPayOSTopUp: (payload: CreatePayOSTopUpRequest) =>
    apiClient<ApiResponse<CreatePayOSTopUpResponse>>(ENDPOINTS.WALLET.TOPUP_PAYOS, {
      method: 'POST',
      body: payload,
      auth: true
    }),

  mockSuccess: (paymentTransactionId: string, amount?: number) =>
    apiClient<ApiResponse<MockPaymentResponse>>(ENDPOINTS.MOCK_PAYMENTS.SUCCESS(paymentTransactionId), {
      method: 'POST',
      body: amount ? { amount } : {},
      auth: true
    }),

  mockFailed: (paymentTransactionId: string, amount?: number) =>
    apiClient<ApiResponse<MockPaymentResponse>>(ENDPOINTS.MOCK_PAYMENTS.FAILED(paymentTransactionId), {
      method: 'POST',
      body: amount ? { amount } : {},
      auth: true
    })
};
