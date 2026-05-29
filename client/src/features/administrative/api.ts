import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import type { Province, Ward } from './types';

export async function getProvinces(): Promise<Province[]> {
  return apiClient<Province[]>(
    '/api/administrative/provinces',
    { auth: true }
  );
}

export async function getWardsByProvince(provinceCode: string): Promise<Ward[]> {
  return apiClient<Ward[]>(
    `/api/administrative/provinces/${provinceCode}/wards`,
    { auth: true }
  );
}

