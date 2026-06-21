import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  ApproveRentalRequestRequest,
  CreateRentalRequestRequest,
  RejectRentalRequestRequest,
  RentalRequestResponse,
  RoomDepositResponse
} from './types';

export const rentalRequestApi = {
  createRentalRequest: (roomId: string, payload: CreateRentalRequestRequest) =>
    apiClient<ApiResponse<RentalRequestResponse>>(ENDPOINTS.RENTAL_REQUESTS.CREATE(roomId), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  getMyRentalRequests: () =>
    apiClient<ApiResponse<RentalRequestResponse[]>>(ENDPOINTS.RENTAL_REQUESTS.MY, {
      method: 'GET',
      auth: true
    }),

  getIncomingRentalRequests: () =>
    apiClient<ApiResponse<RentalRequestResponse[]>>(ENDPOINTS.RENTAL_REQUESTS.INCOMING, {
      method: 'GET',
      auth: true
    }),

  approveRentalRequest: (id: string, payload: ApproveRentalRequestRequest) =>
    apiClient<ApiResponse<RentalRequestResponse>>(ENDPOINTS.RENTAL_REQUESTS.APPROVE(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  rejectRentalRequest: (id: string, payload: RejectRentalRequestRequest) =>
    apiClient<ApiResponse<RentalRequestResponse>>(ENDPOINTS.RENTAL_REQUESTS.REJECT(id), {
      method: 'POST',
      auth: true,
      body: payload
    }),

  cancelRentalRequest: (id: string) =>
    apiClient<ApiResponse<RentalRequestResponse>>(ENDPOINTS.RENTAL_REQUESTS.CANCEL(id), {
      method: 'POST',
      auth: true
    }),

  payDeposit: (id: string) =>
    apiClient<ApiResponse<RoomDepositResponse>>(ENDPOINTS.ROOM_DEPOSITS.PAY(id), {
      method: 'POST',
      auth: true
    })
};
