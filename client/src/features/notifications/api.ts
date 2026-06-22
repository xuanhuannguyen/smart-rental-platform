import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type { Notification } from './types';

export async function getNotifications(limit = 20): Promise<Notification[]> {
  const data = await apiClient<ApiResponse<Notification[]>>(
    `${ENDPOINTS.NOTIFICATIONS.LIST}?limit=${limit}`,
    { auth: true }
  );
  return data.data;
}

export async function getUnreadCount(): Promise<number> {
  const data = await apiClient<ApiResponse<number>>(
    ENDPOINTS.NOTIFICATIONS.UNREAD_COUNT,
    { auth: true }
  );
  return data.data;
}

export async function markAsRead(id: string): Promise<void> {
  await apiClient<ApiResponse<unknown>>(
    ENDPOINTS.NOTIFICATIONS.MARK_READ(id),
    { method: 'PATCH', auth: true }
  );
}

export async function markAllAsRead(): Promise<void> {
  await apiClient<ApiResponse<unknown>>(
    ENDPOINTS.NOTIFICATIONS.MARK_ALL_READ,
    { method: 'PATCH', auth: true }
  );
}
