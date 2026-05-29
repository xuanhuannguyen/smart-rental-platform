import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import type {
  Amenity,
  LeasePolicy,
  PropertyImageRequest,
  RoomingHouseBasicInfoRequest,
  RoomingHouseDetail,
  RoomingHouseOnboarding,
  RoomingHouseSummary,
  UpdateLeasePolicyRequest,
  UpdateLegalDocumentRequest,
} from './types';

export async function getMyRoomingHouses(): Promise<RoomingHouseSummary[]> {
  const data = await apiClient<ApiResponse<RoomingHouseSummary[]>>(
    '/api/rooming-houses/my',
    { auth: true }
  );
  return data.data;
}

export async function getMyRoomingHouseOnboarding(): Promise<RoomingHouseOnboarding> {
  const data = await apiClient<ApiResponse<RoomingHouseOnboarding>>(
    '/api/rooming-houses/my/onboarding',
    { auth: true }
  );
  return data.data;
}

export async function getAmenities(scope?: 'House' | 'Room' | 'Both'): Promise<Amenity[]> {
  const query = scope ? `?scope=${scope}` : '';
  const data = await apiClient<ApiResponse<Amenity[]>>(
    `/api/amenities${query}`,
    { auth: true }
  );
  return data.data;
}

export async function getRoomingHouseDetail(id: string): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}`,
    { auth: true }
  );
  return data.data;
}

export async function createRoomingHouseDraft(
  request: RoomingHouseBasicInfoRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    '/api/rooming-houses/draft',
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomingHouseBasicInfo(
  id: string,
  request: RoomingHouseBasicInfoRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomingHouseImages(
  id: string,
  images: PropertyImageRequest[]
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/images`,
    { method: 'PUT', auth: true, body: { images } }
  );
  return data.data;
}

export async function updateRoomingHouseAmenities(
  id: string,
  amenityIds: number[]
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/amenities`,
    { method: 'PUT', auth: true, body: { amenityIds } }
  );
  return data.data;
}

export async function updateRoomingHouseLegalDocument(
  id: string,
  request: UpdateLegalDocumentRequest
): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/legal-document`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function getRoomingHouseLeasePolicy(id: string): Promise<LeasePolicy | null> {
  const data = await apiClient<ApiResponse<LeasePolicy | null>>(
    `/api/rooming-houses/${id}/lease-policy`,
    { auth: true }
  );
  return data.data;
}

export async function updateRoomingHouseLeasePolicy(
  id: string,
  request: UpdateLeasePolicyRequest
): Promise<LeasePolicy> {
  const data = await apiClient<ApiResponse<LeasePolicy>>(
    `/api/rooming-houses/${id}/lease-policy`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function submitRoomingHouse(id: string): Promise<RoomingHouseDetail> {
  const data = await apiClient<ApiResponse<RoomingHouseDetail>>(
    `/api/rooming-houses/${id}/submit`,
    { method: 'POST', auth: true }
  );
  return data.data;
}
