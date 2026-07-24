import { ROUTE_PATHS } from '../../app/router/routePaths';
import type { Notification } from './types';

export type NotificationRole = 'tenant' | 'landlord';
export type NotificationCategory = 'rental' | 'appointment' | 'billing' | 'feedback' | 'property' | 'general';

export function getNotificationRole(notification: Notification): NotificationRole {
  const type = notification.type;
  const title = notification.title.toLowerCase();
  const body = notification.body.toLowerCase();
  const isViewingAppointment =
    notification.referenceType === 'ViewingAppointment' ||
    type === 'NewViewingAppointment' ||
    type.startsWith('ViewingAppointment');

  if (isViewingAppointment) {
    if (type === 'NewViewingAppointment') {
      return 'landlord';
    }

    if (body.includes('khách thuê') || title.includes('khách thuê')) {
      return 'landlord';
    }

    if (body.includes('chủ trọ') || title.includes('chủ trọ')) {
      return 'tenant';
    }

    return type === 'ViewingAppointmentRequested' ? 'landlord' : 'tenant';
  }

  if (
    type === 'ContractAwaitingLandlordSignature' ||
    type === 'NewRentalRequest' ||
    title.includes('khách thuê') ||
    body.includes('chủ trọ ký') ||
    body.includes('khu trọ của bạn') ||
    body.includes('muốn xem phòng') ||
    body.includes('đã gửi yêu cầu thuê') ||
    title.includes('nhận thanh toán') ||
    body.includes('nhận được thanh toán')
  ) {
    return 'landlord';
  }

  return 'tenant';
}

export function getNotificationLink(notification: Notification): string {
  if (notification.id.startsWith('mock-')) return '#';

  const role = getNotificationRole(notification);

  if (notification.referenceType === 'Conversation' && notification.referenceId) {
    const base = ROUTE_PATHS.MESSAGES;
    return `${base}?conversationId=${notification.referenceId}`;
  }

  if (
    notification.referenceId &&
    (notification.referenceType === 'RoomingHouse' || notification.type === 'RoomingHouseReviewRejected')
  ) {
    return `/rooming-houses/${notification.referenceId}?review=1#review-section`;
  }

  if (notification.referenceType === 'RentalContract' && notification.referenceId) {
    return role === 'landlord'
      ? ROUTE_PATHS.LANDLORD.CONTRACT_DETAIL(notification.referenceId)
      : ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY_DETAIL(notification.referenceId);
  }

  if (role === 'landlord') {
    if (notification.referenceType === 'ViewingAppointment') {
      return ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS;
    }
    if (notification.referenceType === 'RentalRequest' && notification.referenceId) {
      return `/landlord/rental-requests/${notification.referenceId}`;
    }
    if (isBillingNotification(notification) && notification.referenceId) {
      return `/landlord/invoices/${notification.referenceId}`;
    }
    return ROUTE_PATHS.LANDLORD.DASHBOARD;
  }

  if (notification.referenceType === 'ViewingAppointment') {
    return ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS;
  }
  if (notification.referenceType === 'RentalRequest' && notification.referenceId) {
    return `/account/rental-requests/${notification.referenceId}`;
  }
  if (isBillingNotification(notification) && notification.referenceId) {
    return `/account/invoices/${notification.referenceId}`;
  }
  return ROUTE_PATHS.ACCOUNT.PROFILE;
}

export function getNotificationCategory(notification: Notification): NotificationCategory {
  const type = notification.type.toLowerCase();
  const title = notification.title.toLowerCase();
  const body = notification.body.toLowerCase();

  if (
    notification.referenceType === 'RentalRequest' ||
    notification.referenceType === 'RentalContract' ||
    type.includes('rentalrequest') ||
    type.includes('contract') ||
    title.includes('yêu cầu thuê')
  ) {
    return 'rental';
  }

  if (
    notification.referenceType === 'ViewingAppointment' ||
    type.includes('viewing') ||
    title.includes('lịch hẹn')
  ) {
    return 'appointment';
  }

  if (isBillingNotification(notification)) {
    return 'billing';
  }

  if (title.includes('phản hồi') || body.includes('phản hồi')) {
    return 'feedback';
  }

  if (body.includes('khu trọ') || body.includes('phòng')) {
    return 'property';
  }

  return 'general';
}

export function isBillingNotification(notification: Notification): boolean {
  const title = notification.title.toLowerCase();
  return notification.referenceType === 'Billing' ||
    notification.referenceType === 'Invoice' ||
    notification.referenceType === 'Payment' ||
    title.includes('hóa đơn') ||
    title.includes('thanh toán');
}
