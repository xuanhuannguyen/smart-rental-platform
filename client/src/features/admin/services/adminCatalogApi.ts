import { apiClient } from '../../../shared/api/apiClient';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import type {
  PagedResult,
  AdminProvinceResponse,
  AdminWardResponse,
  CreateProvinceRequest,
  UpdateProvinceRequest,
  CreateWardRequest,
  UpdateWardRequest,
  AdminAmenityResponse,
  CreateAmenityRequest,
  UpdateAmenityRequest,
  AdminBillingServiceTypeResponse,
  CreateBillingServiceTypeRequest,
  UpdateBillingServiceTypeRequest
} from '../types/adminCatalog.types';

export const adminCatalogApi = {
  // Provinces
  getProvinces: async (page: number = 1, pageSize: number = 10, search?: string) => {
    let url = `/api/admin/administrative/provinces?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return await apiClient<ApiResponse<PagedResult<AdminProvinceResponse>>>(url, { auth: true });
  },

  createProvince: async (data: CreateProvinceRequest) => {
    return await apiClient<ApiResponse<AdminProvinceResponse>>('/api/admin/administrative/provinces', {
      method: 'POST',
      body: data,
      auth: true
    });
  },

  updateProvince: async (code: string, data: UpdateProvinceRequest) => {
    return await apiClient<ApiResponse<AdminProvinceResponse>>(`/api/admin/administrative/provinces/${code}`, {
      method: 'PUT',
      body: data,
      auth: true
    });
  },

  toggleProvinceStatus: async (code: string) => {
    return await apiClient<ApiResponse<any>>(`/api/admin/administrative/provinces/${code}/toggle-active`, {
      method: 'PATCH',
      auth: true
    });
  },

  // Wards
  getWards: async (provinceCode: string, page: number = 1, pageSize: number = 10, search?: string) => {
    let url = `/api/admin/administrative/wards?provinceCode=${provinceCode}&page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return await apiClient<ApiResponse<PagedResult<AdminWardResponse>>>(url, { auth: true });
  },

  createWard: async (data: CreateWardRequest) => {
    return await apiClient<ApiResponse<AdminWardResponse>>('/api/admin/administrative/wards', {
      method: 'POST',
      body: data,
      auth: true
    });
  },

  updateWard: async (code: string, data: UpdateWardRequest) => {
    return await apiClient<ApiResponse<AdminWardResponse>>(`/api/admin/administrative/wards/${code}`, {
      method: 'PUT',
      body: data,
      auth: true
    });
  },

  toggleWardStatus: async (code: string) => {
    return await apiClient<ApiResponse<any>>(`/api/admin/administrative/wards/${code}/toggle-active`, {
      method: 'PATCH',
      auth: true
    });
  },

  // Amenities
  getAmenities: async (page: number = 1, pageSize: number = 10, search?: string) => {
    let url = `/api/admin/amenities?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return await apiClient<ApiResponse<PagedResult<AdminAmenityResponse>>>(url, { auth: true });
  },

  createAmenity: async (data: CreateAmenityRequest) => {
    return await apiClient<ApiResponse<AdminAmenityResponse>>('/api/admin/amenities', {
      method: 'POST',
      body: data,
      auth: true
    });
  },

  updateAmenity: async (id: number, data: UpdateAmenityRequest) => {
    return await apiClient<ApiResponse<AdminAmenityResponse>>(`/api/admin/amenities/${id}`, {
      method: 'PUT',
      body: data,
      auth: true
    });
  },

  toggleAmenityStatus: async (id: number) => {
    return await apiClient<ApiResponse<any>>(`/api/admin/amenities/${id}/toggle-active`, {
      method: 'PATCH',
      auth: true
    });
  },

  // Billing Service Types
  getBillingServiceTypes: async (page: number = 1, pageSize: number = 10, search?: string) => {
    let url = `/api/admin/billing-service-types?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return await apiClient<ApiResponse<PagedResult<AdminBillingServiceTypeResponse>>>(url, { auth: true });
  },

  createBillingServiceType: async (data: CreateBillingServiceTypeRequest) => {
    return await apiClient<ApiResponse<AdminBillingServiceTypeResponse>>('/api/admin/billing-service-types', {
      method: 'POST',
      body: data,
      auth: true
    });
  },

  updateBillingServiceType: async (id: number, data: UpdateBillingServiceTypeRequest) => {
    return await apiClient<ApiResponse<AdminBillingServiceTypeResponse>>(`/api/admin/billing-service-types/${id}`, {
      method: 'PUT',
      body: data,
      auth: true
    });
  },

  toggleBillingServiceTypeStatus: async (id: number) => {
    return await apiClient<ApiResponse<any>>(`/api/admin/billing-service-types/${id}/toggle-active`, {
      method: 'PATCH',
      auth: true
    });
  }
};
