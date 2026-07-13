import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type { LandlordDashboard } from '../types/dashboard.types';

export const landlordDashboardApi = {
  getDashboard(params?: { year?: number; month?: number }) {
    const query = new URLSearchParams();
    if (params?.year) {
      query.set('year', params.year.toString());
    }
    if (params?.month) {
      query.set('month', params.month.toString());
    }

    const url = `${ENDPOINTS.LANDLORD_DASHBOARD.ROOT}${query.toString() ? `?${query.toString()}` : ''}`;
    return apiClient<ApiResponse<LandlordDashboard>>(url, {
      auth: true
    });
  }
};
