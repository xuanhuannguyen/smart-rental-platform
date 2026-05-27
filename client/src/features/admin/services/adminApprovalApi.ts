import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AdminKycDetail,
  AdminKycListItem,
  AdminRoomingHouseDetail,
  AdminRoomingHouseListItem,
  AdminUserListItem,
  AdminUserDetail,
  PagedResponse
} from '../types/adminApproval.types';

export const adminApprovalApi = {
  getPendingKyc(pageNumber = 1, pageSize = 20) {
    return apiClient<ApiResponse<PagedResponse<AdminKycListItem>>>(
      `${ENDPOINTS.ADMIN.KYC_PENDING}?pageNumber=${pageNumber}&pageSize=${pageSize}`,
      { auth: true }
    );
  },

  getKycDetail(id: string) {
    return apiClient<ApiResponse<AdminKycDetail>>(ENDPOINTS.ADMIN.KYC_DETAIL(id), {
      auth: true
    });
  },

  approveKyc(id: string) {
    return apiClient<ApiResponse<object>>(ENDPOINTS.ADMIN.KYC_APPROVE(id), {
      method: 'POST',
      auth: true
    });
  },

  rejectKyc(id: string, reason: string) {
    return apiClient<ApiResponse<object>>(ENDPOINTS.ADMIN.KYC_REJECT(id), {
      method: 'POST',
      auth: true,
      body: { reason }
    });
  },

  getPendingRoomingHouses(pageNumber = 1, pageSize = 20) {
    return apiClient<ApiResponse<PagedResponse<AdminRoomingHouseListItem>>>(
      `${ENDPOINTS.ADMIN.ROOMING_HOUSES_PENDING}?pageNumber=${pageNumber}&pageSize=${pageSize}`,
      { auth: true }
    );
  },

  getRoomingHouseDetail(id: string) {
    return apiClient<ApiResponse<AdminRoomingHouseDetail>>(ENDPOINTS.ADMIN.ROOMING_HOUSE_DETAIL(id), {
      auth: true
    });
  },

  approveRoomingHouse(id: string) {
    return apiClient<ApiResponse<object>>(ENDPOINTS.ADMIN.ROOMING_HOUSE_APPROVE(id), {
      method: 'POST',
      auth: true
    });
  },

  rejectRoomingHouse(id: string, reason: string) {
    return apiClient<ApiResponse<object>>(ENDPOINTS.ADMIN.ROOMING_HOUSE_REJECT(id), {
      method: 'POST',
      auth: true,
      body: { reason }
    });
  },

  getUsers(pageNumber = 1, pageSize = 20) {
    return apiClient<ApiResponse<PagedResponse<AdminUserListItem>>>(
      `${ENDPOINTS.ADMIN.USERS}?pageNumber=${pageNumber}&pageSize=${pageSize}`,
      { auth: true }
    );
  },

  getKycHistory(userId: string) {
    return apiClient<ApiResponse<AdminKycDetail[]>>(
      ENDPOINTS.ADMIN.KYC_HISTORY(userId),
      { auth: true }
    );
  },

  getPublicRoomingHouses(pageNumber = 1, pageSize = 20) {
    return apiClient<ApiResponse<PagedResponse<AdminRoomingHouseListItem>>>(
      `${ENDPOINTS.ADMIN.ROOMING_HOUSES_PUBLIC}?pageNumber=${pageNumber}&pageSize=${pageSize}`,
      { auth: true }
    );
  },

  getUserDetail(userId: string) {
    return apiClient<ApiResponse<AdminUserDetail>>(
      ENDPOINTS.ADMIN.USER_DETAIL(userId),
      { auth: true }
    );
  }
};
