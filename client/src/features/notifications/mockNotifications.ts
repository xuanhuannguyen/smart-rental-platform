import type { Notification } from './types';

const MOCK_NOTIFICATIONS: Notification[] = [
  {
    id: 'mock-1',
    type: 'NewRentalRequest',
    title: 'Yêu cầu thuê phòng Phòng 105 đã được gửi thành công',
    body: 'Khu trọ: Nhà trọ Tây Hồ #742',
    isRead: false,
    createdAt: new Date(Date.now() - 2 * 60 * 1000).toISOString(),
    referenceType: 'RentalRequest',
    referenceId: '1'
  },
  {
    id: 'mock-2',
    type: 'ViewingAppointmentConfirmed',
    title: 'Lịch hẹn xem phòng đã được xác nhận',
    body: 'Thời gian: 09:00 - 29/06/2026',
    isRead: false,
    createdAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
    referenceType: 'ViewingAppointment',
    referenceId: '2'
  },
  {
    id: 'mock-3',
    type: 'RentalRequestApproved',
    title: 'Chủ trọ đã phản hồi yêu cầu của bạn',
    body: 'Vui lòng kiểm tra chi tiết phản hồi',
    isRead: false,
    createdAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    referenceType: 'RentalRequest',
    referenceId: '3'
  },
  {
    id: 'mock-4',
    type: 'BillingInvoice',
    title: 'Hóa đơn tháng này sắp đến hạn thanh toán',
    body: 'Hạn thanh toán: 05/07/2026',
    isRead: true,
    createdAt: new Date(Date.now() - 24 * 60 * 1000).toISOString(),
    referenceType: 'Billing',
    referenceId: '4'
  }
];

const STORAGE_KEY = 'notifications_mock_deleted';

export function getMockDeletedIds(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return new Set<string>(raw ? JSON.parse(raw) : []);
  } catch {
    return new Set<string>();
  }
}

export function saveMockDeletedId(id: string) {
  try {
    const ids = getMockDeletedIds();
    ids.add(id);
    localStorage.setItem(STORAGE_KEY, JSON.stringify([...ids]));
  } catch {
    // Silently fail
  }
}

export function getMockNotifications(): Notification[] {
  const deletedIds = getMockDeletedIds();
  return MOCK_NOTIFICATIONS.filter(n => !deletedIds.has(n.id));
}
