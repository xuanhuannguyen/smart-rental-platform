import { ENDPOINTS } from '../../../shared/api/endpoints';
import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import type {
  LandlordEligibilityResponse,
  UpdateUserProfileRequest,
  UserProfileResponse,
  UserSession
} from '../types/profile.types';

export const profileApi = {
  getProfile: () =>
    apiClient<ApiResponse<UserProfileResponse>>(ENDPOINTS.USERS.PROFILE, {
      method: 'GET',
      auth: true
    }),

  updateProfile: (payload: UpdateUserProfileRequest) =>
    apiClient<ApiResponse<UserProfileResponse>>(ENDPOINTS.USERS.PROFILE, {
      method: 'PUT',
      auth: true,
      body: payload
    }),

  getLandlordEligibility: () =>
    apiClient<ApiResponse<LandlordEligibilityResponse>>(ENDPOINTS.USERS.LANDLORD_ELIGIBILITY, {
      method: 'GET',
      auth: true
    }),

  getActiveSessions: () =>
    apiClient<ApiResponse<UserSession[]>>(ENDPOINTS.USERS.SESSIONS, {
      method: 'GET',
      auth: true
    }),

  revokeSession: (sessionId: string) =>
    apiClient<ApiResponse<unknown>>(ENDPOINTS.USERS.REVOKE_SESSION(sessionId), {
      method: 'DELETE',
      auth: true
    })
};
