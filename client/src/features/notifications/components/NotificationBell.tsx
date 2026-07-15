import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import {
  getNotifications, markAsRead, deleteNotification
} from '../api';
import { getMockNotifications, saveMockDeletedId } from '../mockNotifications';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import type { Notification } from '../types';
import './NotificationBell.css';

function formatTime(isoString: string): string {
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);

  if (diffMin < 1) return 'Vừa xong';
  if (diffMin < 60) return `${diffMin} phút trước`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour} giờ trước`;
  const diffDay = Math.floor(diffHour / 24);
  if (diffDay < 7) return `${diffDay} ngày trước`;
  return date.toLocaleDateString('vi-VN');
}

function isChatNotification(notification: Notification): boolean {
  return notification.referenceType === 'Conversation' || notification.type === 'NewChatMessage';
}

function getNotificationRole(notification: Notification): 'tenant' | 'landlord' {
  const type = notification.type;
  const title = notification.title.toLowerCase();
  const body = notification.body.toLowerCase();
  
  if (
    type === 'NewRentalRequest' ||
    type === 'NewViewingAppointment' ||
    title.includes('khách thuê') ||
    body.includes('muốn xem phòng') ||
    body.includes('đã gửi yêu cầu thuê') ||
    title.includes('nhận thanh toán') ||
    body.includes('nhận được thanh toán')
  ) {
    return 'landlord';
  }
  
  return 'tenant';
}

function getNotificationLink(notification: Notification): string {
  if (notification.id.startsWith('mock-')) return '#';

  if (notification.referenceType === 'Conversation' && notification.referenceId) {
    const currentRole = window.location.pathname.startsWith('/landlord') ? 'landlord' : 'tenant';
    const base = ROUTE_PATHS.MESSAGES;
    return `${base}?conversationId=${notification.referenceId}`;
  }

  if (
    notification.referenceId &&
    (notification.referenceType === 'RoomingHouse' || notification.type === 'RoomingHouseReviewRejected')
  ) {
    return `/rooming-houses/${notification.referenceId}?review=1#review-section`;
  }
  
  const role = getNotificationRole(notification);
  
  if (role === 'landlord') {
    if (notification.referenceType === 'ViewingAppointment') {
      return ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS;
    }
    if (notification.referenceType === 'RentalRequest' && notification.referenceId) {
      return `/landlord/rental-requests/${notification.referenceId}`;
    }
    if ((notification.referenceType === 'Billing' || notification.referenceType === 'Invoice' || notification.referenceType === 'Payment') && notification.referenceId) {
      return `/landlord/invoices/${notification.referenceId}`;
    }
    return ROUTE_PATHS.LANDLORD.DASHBOARD;
  } else {
    if (notification.referenceType === 'ViewingAppointment') {
      return ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS;
    }
    if (notification.referenceType === 'RentalRequest' && notification.referenceId) {
      return `/account/rental-requests/${notification.referenceId}`;
    }
    if ((notification.referenceType === 'Billing' || notification.referenceType === 'Invoice' || notification.referenceType === 'Payment') && notification.referenceId) {
      return `/account/invoices/${notification.referenceId}`;
    }
    return ROUTE_PATHS.ACCOUNT.PROFILE;
  }
}

function getNotificationIconInfo(notification: Notification) {
  const isViewingAppointment = notification.referenceType === 'ViewingAppointment' || notification.type.toLowerCase().includes('viewing') || notification.title.toLowerCase().includes('lịch hẹn');
  const isBilling = notification.referenceType === 'Billing' || notification.referenceType === 'Invoice' || notification.title.toLowerCase().includes('hóa đơn') || notification.title.toLowerCase().includes('thanh toán');
  const isFeedback = notification.title.toLowerCase().includes('phản hồi') || notification.body.toLowerCase().includes('phản hồi');
  const isRentalRequest = notification.referenceType === 'RentalRequest' || notification.type.toLowerCase().includes('rentalrequest') || notification.title.toLowerCase().includes('yêu cầu thuê');

  if (isRentalRequest) {
    return {
      className: 'icon-box--blue',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <line x1="22" y1="2" x2="11" y2="13" />
          <polygon points="22 2 15 22 11 13 2 9 22 2" />
        </svg>
      )
    };
  }

  if (isViewingAppointment) {
    const isCancelOrReject = notification.type.toLowerCase().includes('rejected') || notification.type.toLowerCase().includes('cancelled');
    if (isCancelOrReject) {
      return {
        className: 'icon-box--red',
        icon: (
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
            <line x1="10" y1="13" x2="14" y2="17" />
            <line x1="14" y1="13" x2="10" y2="17" />
          </svg>
        )
      };
    }
    return {
      className: 'icon-box--green',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
          <polyline points="8 14 11 17 16 12" />
        </svg>
      )
    };
  }

  if (isFeedback) {
    return {
      className: 'icon-box--orange',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
        </svg>
      )
    };
  }

  if (isBilling) {
    return {
      className: 'icon-box--purple',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 12V8H6a2 2 0 0 1-2-2c0-1.1.9-2 2-2h12v4" />
          <path d="M4 6v12a2 2 0 0 0 2 2h14v-4" />
          <path d="M18 12a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h4v-6H18z" />
        </svg>
      )
    };
  }

  return {
    className: 'icon-box--slate',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
      </svg>
    )
  };
}

function renderNotificationSubtitle(notification: Notification) {
  const isViewingAppointment = notification.referenceType === 'ViewingAppointment' || notification.type.toLowerCase().includes('viewing') || notification.title.toLowerCase().includes('lịch hẹn');
  const isBilling = notification.referenceType === 'Billing' || notification.referenceType === 'Invoice' || notification.title.toLowerCase().includes('hóa đơn') || notification.title.toLowerCase().includes('thanh toán');
  const isFeedback = notification.title.toLowerCase().includes('phản hồi') || notification.body.toLowerCase().includes('phản hồi');

  if (isViewingAppointment) {
    return (
      <span className="subtitle-content">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="subtitle-inline-icon">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
        <span>{notification.body}</span>
      </span>
    );
  }

  if (isBilling) {
    return (
      <span className="subtitle-content">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="subtitle-inline-icon">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
        <span>{notification.body}</span>
      </span>
    );
  }

  if (isFeedback) {
    return (
      <span className="subtitle-content">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="subtitle-inline-icon">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
        </svg>
        <span>{notification.body}</span>
      </span>
    );
  }

  if (notification.body.toLowerCase().includes('khu trọ') || notification.body.toLowerCase().includes('phòng')) {
    return (
      <span className="subtitle-content">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="subtitle-inline-icon">
          <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
          <line x1="9" y1="22" x2="9" y2="16" />
          <line x1="15" y1="22" x2="15" y2="16" />
        </svg>
        <span>{notification.body}</span>
      </span>
    );
  }

  return <span>{notification.body}</span>;
}

export interface NotificationBellProps {
  navigateOnly?: boolean;
  navigateTo?: string;
}

export function NotificationBell({ navigateOnly = false, navigateTo }: NotificationBellProps) {
  const { currentUser } = useAuth();
  const navigate = useNavigate();
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [activeRole, setActiveRole] = useState<'tenant' | 'landlord'>('tenant');
  const dropdownRef = useRef<HTMLDivElement>(null);

  const loadUnreadCount = useCallback(async () => {
    try {
      const items = await getNotifications(50);
      setUnreadCount(items.filter(n => !isChatNotification(n) && !n.isRead).length);
    } catch {
      setUnreadCount(notifications.filter(n => !isChatNotification(n) && !n.isRead).length);
    }
  }, [notifications]);

  const loadNotifications = useCallback(async () => {
    try {
      let items = await getNotifications(20);
      if (items.length === 0) {
        items = getMockNotifications();
        const visibleItems = items.filter(n => !isChatNotification(n));
        setNotifications(visibleItems);
        setUnreadCount(visibleItems.filter(n => !n.isRead).length);
      } else {
        const visibleItems = items.filter(n => !isChatNotification(n));
        setNotifications(visibleItems);
        setUnreadCount(visibleItems.filter(n => !n.isRead).length);
      }
    } catch {
      const items = getMockNotifications().filter(n => !isChatNotification(n));
      setNotifications(items);
      setUnreadCount(items.filter(n => !n.isRead).length);
    }
  }, []);

  // Poll unread count every 30 seconds
  useEffect(() => {
    if (!currentUser) return;

    void loadUnreadCount();
    const interval = setInterval(loadUnreadCount, 30000);
    return () => clearInterval(interval);
  }, [currentUser, loadUnreadCount]);

  // Poll notifications list when dropdown is open
  useEffect(() => {
    if (!currentUser || !isOpen || navigateOnly) return;

    const interval = setInterval(loadNotifications, 30000);
    return () => clearInterval(interval);
  }, [currentUser, isOpen, navigateOnly, loadNotifications]);

  // Close dropdown on outside click
  useEffect(() => {
    if (navigateOnly) return;
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [navigateOnly]);

  const handleToggle = async () => {
    if (navigateOnly) {
      if (navigateTo) {
        navigate(navigateTo);
        return;
      }
      const currentRole = window.location.pathname.startsWith('/landlord') ? 'landlord' : 'tenant';
      navigate(`${ROUTE_PATHS.ACCOUNT.NOTIFICATIONS}?role=${currentRole}`);
      return;
    }
    const nextOpen = !isOpen;
    setIsOpen(nextOpen);
    if (nextOpen) {
      await loadNotifications();
    }
  };

  const handleItemClick = async (notification: Notification) => {
    if (notification.id.startsWith('mock-')) {
      setNotifications(prev =>
        prev.map(n => (n.id === notification.id ? { ...n, isRead: true } : n))
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
    } else if (!notification.isRead) {
      try {
        await markAsRead(notification.id);
        setUnreadCount(prev => Math.max(0, prev - 1));
        setNotifications(prev =>
          prev.map(n => (n.id === notification.id ? { ...n, isRead: true } : n))
        );
      } catch {
        // Silently fail
      }
    }
    setIsOpen(false);
    const link = getNotificationLink(notification);
    if (link !== '#') {
      navigate(link);
    }
  };

  const handleDeleteFromBell = async (e: React.MouseEvent, notification: Notification) => {
    e.stopPropagation();

    if (notification.id.startsWith('mock-')) {
      saveMockDeletedId(notification.id);
    } else {
      try {
        await deleteNotification(notification.id);
      } catch {
        // Silently fail
      }
    }

    setNotifications(prev => prev.filter(n => n.id !== notification.id));
    setUnreadCount(prev => Math.max(0, prev - 1));
  };

  const handleMarkAllRead = async () => {
    try {
      const hasMock = notifications.some(n => n.id.startsWith('mock-'));
      if (hasMock) {
        setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
        setUnreadCount(0);
      } else {
        await Promise.all(filteredItems.filter(n => !n.isRead).map(n => markAsRead(n.id)));
        setUnreadCount(0);
        setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      }
    } catch {
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
    }
  };

  if (!currentUser) {
    return null;
  }

  const getFilteredNotifications = () => {
    return notifications.filter(n => !isChatNotification(n) && getNotificationRole(n) === activeRole);
  };

  const filteredItems = getFilteredNotifications();

  return (
    <div className="notification-bell-wrapper" ref={dropdownRef}>
      <button
        className="notification-bell-btn"
        onClick={handleToggle}
        title="Thông báo"
        aria-label={`Thông báo${unreadCount > 0 ? ` (${unreadCount} chưa đọc)` : ''}`}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        {unreadCount > 0 && (
          <span className="notification-badge">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="notification-dropdown">
          <header className="notification-dropdown-header">
            <div className="dropdown-title-wrapper">
              <div className="dropdown-bell-icon-bg">
                <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                  <path d="M13.73 21a2 2 0 0 1-3.46 0" />
                </svg>
              </div>
              <h3>Thông báo</h3>
            </div>
            <button className="dropdown-close-btn" onClick={() => setIsOpen(false)} aria-label="Đóng">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </header>

          <div className="notification-dropdown-tabs">
            <button 
              className={`tab-btn ${activeRole === 'tenant' ? 'active' : ''}`}
              onClick={() => setActiveRole('tenant')}
              style={{ flex: '1 1 calc(50% - 3px)' }}
            >
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
              <span>Người thuê</span>
            </button>
            <button 
              className={`tab-btn ${activeRole === 'landlord' ? 'active' : ''}`}
              onClick={() => setActiveRole('landlord')}
              style={{ flex: '1 1 calc(50% - 3px)' }}
            >
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
              <span>Chủ trọ</span>
            </button>
          </div>

          <div className="notification-dropdown-actions">
            <span className="summary-count">{filteredItems.length} thông báo</span>
            {unreadCount > 0 && (
              <button className="mark-all-read-btn" onClick={handleMarkAllRead}>
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
                <span>Đánh dấu tất cả đã đọc</span>
              </button>
            )}
          </div>

          <div className="notification-dropdown-list">
            {filteredItems.length === 0 ? (
              <div className="notification-dropdown-empty">
                Không có thông báo nào.
              </div>
            ) : (
              filteredItems.map((notification) => {
                const iconInfo = getNotificationIconInfo(notification);
                return (
                  <div
                    key={notification.id}
                    className={`notification-item-card ${!notification.isRead ? 'unread' : ''}`}
                    onClick={() => handleItemClick(notification)}
                  >
                    <div className={`notification-item-icon-box ${iconInfo.className}`}>
                      {iconInfo.icon}
                    </div>
                    <div className="notification-item-content">
                      <div className="notification-item-header">
                        <span className="notification-item-title">{notification.title}</span>
                        <span className="notification-item-time">{formatTime(notification.createdAt)}</span>
                      </div>
                      <div className="notification-item-subtitle">
                        {renderNotificationSubtitle(notification)}
                      </div>
                    </div>
                    <button
                      className="notification-item-delete-btn"
                      type="button"
                      onClick={(e) => handleDeleteFromBell(e, notification)}
                      title="Xóa thông báo"
                      aria-label="Xóa thông báo"
                    >
                      <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="3 6 5 6 21 6" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                        <line x1="10" y1="11" x2="10" y2="17" />
                        <line x1="14" y1="11" x2="14" y2="17" />
                      </svg>
                    </button>
                    {!notification.isRead && <div className="notification-item-unread-dot" />}
                  </div>
                );
              })
            )}
          </div>

          {notifications.length > 0 && (
            <div className="notification-dropdown-footer">
              <a 
                href={`${ROUTE_PATHS.ACCOUNT.NOTIFICATIONS}?role=${activeRole}`}
                onClick={(e) => { 
                  e.preventDefault(); 
                  navigate(`${ROUTE_PATHS.ACCOUNT.NOTIFICATIONS}?role=${activeRole}`); 
                  setIsOpen(false); 
                }}
              >
                <span>Xem tất cả thông báo</span>
                <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="9 18 15 12 9 6" />
                </svg>
              </a>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
