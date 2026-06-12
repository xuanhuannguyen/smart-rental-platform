import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  ViewingAppointment,
  CreateViewingAppointmentRequest,
  ConfirmViewingAppointmentRequest,
  RejectViewingAppointmentRequest,
  CancelViewingAppointmentRequest,
  ConflictCheckResponse,
} from './types';

export async function createViewingAppointment(
  request: CreateViewingAppointmentRequest
): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.CREATE,
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function getTenantAppointments(): Promise<ViewingAppointment[]> {
  const data = await apiClient<ApiResponse<ViewingAppointment[]>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.MY_APPOINTMENTS,
    { auth: true }
  );
  return data.data;
}

export async function getLandlordAppointments(status?: string): Promise<ViewingAppointment[]> {
  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  const data = await apiClient<ApiResponse<ViewingAppointment[]>>(
    `${ENDPOINTS.VIEWING_APPOINTMENTS.LANDLORD_APPOINTMENTS}${query}`,
    { auth: true }
  );
  return data.data;
}

export async function checkConflict(id: string): Promise<ConflictCheckResponse> {
  const data = await apiClient<ApiResponse<ConflictCheckResponse>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.CONFLICT_CHECK(id),
    { auth: true }
  );
  return data.data;
}

export async function confirmViewingAppointment(
  id: string,
  request: ConfirmViewingAppointmentRequest
): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.CONFIRM(id),
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function rejectViewingAppointment(
  id: string,
  request: RejectViewingAppointmentRequest
): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.REJECT(id),
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function cancelViewingAppointmentByTenant(
  id: string,
  request: CancelViewingAppointmentRequest
): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.CANCEL_BY_TENANT(id),
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function cancelViewingAppointmentByLandlord(
  id: string,
  request: CancelViewingAppointmentRequest
): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.CANCEL_BY_LANDLORD(id),
    { method: 'POST', auth: true, body: request }
  );
  return data.data;
}

export async function completeViewingAppointment(id: string): Promise<ViewingAppointment> {
  const data = await apiClient<ApiResponse<ViewingAppointment>>(
    ENDPOINTS.VIEWING_APPOINTMENTS.COMPLETE(id),
    { method: 'POST', auth: true }
  );
  return data.data;
}
