import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import type { PropertyImageRequest } from '../rooming-houses/types';
import type { CreateRoomRequest, Room, RoomPriceTierRequest } from './types';
import type { ContractDetailResponse, ContractOccupantResponse } from '../contracts/types';

export async function createRoom(
  roomingHouseId: string,
  request: CreateRoomRequest
): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooming-houses/${roomingHouseId}/rooms`,
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function getRoomsByRoomingHouse(roomingHouseId: string): Promise<Room[]> {
  const data = await apiClient<ApiResponse<Room[]>>(
    `/api/rooming-houses/${roomingHouseId}/rooms`,
    { auth: true }
  );
  return data.data;
}

export async function getRoomDetail(id: string): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}`,
    { auth: true }
  );
  return data.data;
}

export async function updateRoom(id: string, request: CreateRoomRequest): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}`,
    { method: 'PUT', auth: true, body: request }
  );
  return data.data;
}

export async function updateRoomImages(id: string, images: PropertyImageRequest[]): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}/images`,
    { method: 'PUT', auth: true, body: { images } }
  );
  return data.data;
}

export async function updateRoomAmenities(id: string, amenityIds: number[]): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}/amenities`,
    { method: 'PUT', auth: true, body: { amenityIds } }
  );
  return data.data;
}

export async function updateRoomPriceTiers(
  id: string,
  priceTiers: RoomPriceTierRequest[]
): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}/price-tiers`,
    { method: 'PUT', auth: true, body: { priceTiers } }
  );
  return data.data;
}

export async function submitRoom(id: string): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}/submit`,
    { method: 'POST', auth: true }
  );
  return data.data;
}

export async function updateRoomStatus(id: string, status: string): Promise<Room> {
  const data = await apiClient<ApiResponse<Room>>(
    `/api/rooms/${id}/status`,
    { method: 'PUT', auth: true, body: { status } }
  );
  return data.data;
}

export async function getActiveContractByRoomId(id: string): Promise<ContractDetailResponse> {
  const data = await apiClient<ApiResponse<ContractDetailResponse>>(
    `/api/rooms/${id}/active-contract`,
    { auth: true }
  );
  return data.data;
}

export async function getActiveTenantsByRoomId(id: string): Promise<ContractOccupantResponse[]> {
  const data = await apiClient<ApiResponse<ContractOccupantResponse[]>>(
    `/api/rooms/${id}/tenants`,
    { auth: true }
  );
  return data.data;
}
