import { useEffect, useState, useCallback } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { getNotifications, markAsRead, markAllAsRead, deleteNotification } from '../api';
import { getNotificationCategory, getNotificationLink, getNotificationRole } from '../notificationUtils';
import type { Notification } from '../types';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import './NotificationsPage.css';

function formatDateTimeString(isoString: string): string {
  const date = new Date(isoString);
  let hours = date.getHours();
  const minutes = String(date.getMinutes()).padStart(2, '0');
  const ampm = hours >= 12 ? 'PM' : 'AM';
  hours = hours % 12;
  hours = hours ? hours : 12;
  const timeStr = `${String(hours).padStart(2, '0')}:${minutes} ${ampm}`;
  
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const year = date.getFullYear();
  
  return `${timeStr}  •  ${day}/${month}/${year}`;
}

function getNotificationCategoryInfo(notification: Notification) {
  const category = getNotificationCategory(notification);

  if (category === 'rental') {
    return {
      theme: 'orange',
      label: 'Yêu cầu thuê trọ',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
          <circle cx="12" cy="7" r="4" />
        </svg>
      )
    };
  }

  if (category === 'appointment') {
    return {
      theme: 'sky',
      label: 'Lịch hẹn',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      )
    };
  }

  if (category === 'billing') {
    return {
      theme: 'green',
      label: 'Thanh toán',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="5" width="20" height="14" rx="2" ry="2" />
          <line x1="2" y1="10" x2="22" y2="10" />
        </svg>
      )
    };
  }

  return {
    theme: 'slate',
    label: 'Thông báo',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
      </svg>
    )
  };
}

function groupNotificationsByDate(items: Notification[]): { [dateKey: string]: Notification[] } {
  const groups: { [dateKey: string]: Notification[] } = {};
  
  items.forEach(item => {
    const date = new Date(item.createdAt);
    const today = new Date();
    const yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);
    
    let dateKey = '';
    
    const isToday = date.getDate() === today.getDate() && 
                    date.getMonth() === today.getMonth() && 
                    date.getFullYear() === today.getFullYear();
                    
    const isYesterday = date.getDate() === yesterday.getDate() && 
                        date.getMonth() === yesterday.getMonth() && 
                        date.getFullYear() === yesterday.getFullYear();
                        
    if (isToday) {
      dateKey = 'Hôm nay';
    } else if (isYesterday) {
      dateKey = 'Hôm qua';
    } else {
      const day = String(date.getDate()).padStart(2, '0');
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const year = date.getFullYear();
      dateKey = `${day}/${month}/${year}`;
    }
    
    if (!groups[dateKey]) {
      groups[dateKey] = [];
    }
    groups[dateKey].push(item);
  });
  
  return groups;
}

export default function NotificationsPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryRole = searchParams.get('role');
  const initialRole = queryRole === 'landlord' ? 'landlord' : 'tenant';

  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeRole, setActiveRole] = useState<'tenant' | 'landlord'>(initialRole);
  const [activeTab, setActiveTab] = useState<'all' | 'unread' | 'rental' | 'appointment' | 'billing'>('all');
  const [limit, setLimit] = useState(15);

  // Sync state if URL query parameter changes
  useEffect(() => {
    const roleParam = searchParams.get('role');
    if (roleParam === 'landlord' || roleParam === 'tenant') {
      setActiveRole(roleParam);
    }
  }, [searchParams]);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const items = await getNotifications(100);
      setNotifications(items);
    } catch {
      setNotifications([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const handleMarkRead = async (notification: Notification) => {
    if (!notification.isRead) {
      try {
        await markAsRead(notification.id);
        setNotifications(prev =>
          prev.map(n => (n.id === notification.id ? { ...n, isRead: true } : n))
        );
      } catch {
        // Ignore
      }
    }

    navigate(getNotificationLink(notification));
  };

  const handleMarkAllRead = async () => {
    try {
      await markAllAsRead();
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
    } catch {
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
    }
  };

  const handleDeleteNotification = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();

    // Always remove from local state first
    setNotifications(prev => prev.filter(n => n.id !== id));

    // Real notification: call API
    try {
      await deleteNotification(id);
    } catch {
      console.error('Xoá thông báo trên server thất bại');
    }
  };

  // Filter notifications belonging to active role
  const roleNotifications = notifications.filter(n => getNotificationRole(n) === activeRole);

  // Tab calculations
  const totalCount = roleNotifications.length;
  const unreadCount = roleNotifications.filter(n => !n.isRead).length;
  
  const rentalCount = roleNotifications.filter(n => 
    getNotificationCategory(n) === 'rental'
  ).length;

  const appointmentCount = roleNotifications.filter(n => 
    getNotificationCategory(n) === 'appointment'
  ).length;

  const billingCount = roleNotifications.filter(n => 
    getNotificationCategory(n) === 'billing'
  ).length;

  const getFilteredItems = () => {
    let filtered = roleNotifications;
    
    if (activeTab === 'unread') {
      filtered = filtered.filter(n => !n.isRead);
    } else if (activeTab === 'rental') {
      filtered = filtered.filter(n => 
        getNotificationCategory(n) === 'rental'
      );
    } else if (activeTab === 'appointment') {
      filtered = filtered.filter(n => 
        getNotificationCategory(n) === 'appointment'
      );
    } else if (activeTab === 'billing') {
      filtered = filtered.filter(n => 
        getNotificationCategory(n) === 'billing'
      );
    }
    
    return filtered;
  };

  const filteredItems = getFilteredItems().slice(0, limit);
  const groupedNotifications = groupNotificationsByDate(filteredItems);
  const dateKeys = Object.keys(groupedNotifications);

  return (
    <main className="auth-page" style={{ padding: '40px 24px', background: '#f4f7fb', minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'flex-start', width: '100%' }}>
      <section className="auth-panel kyc-panel notifications-page-container" style={{ width: '100%', maxWidth: '960px', background: 'transparent', boxShadow: 'none' }}>
        {/* Back Button */}
        <div style={{ marginBottom: '20px', display: 'flex', justifyContent: 'flex-start' }}>
          <button
            type="button"
            className="wallet-refresh-btn"
            onClick={() => navigate(-1)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '8px 16px', color: '#246bfe' }}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            <span style={{ color: '#246bfe', fontWeight: '600' }}>Quay lại</span>
          </button>
        </div>

        {/* Header */}
        <header className="notifications-header-wrapper" style={{ marginBottom: '24px' }}>
          <div className="notifications-header-left">
            <div className="notifications-bell-icon-container">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
            </div>
            <div className="notifications-header-info">
              <h1>Thông báo</h1>
              <p className="subtle">Cập nhật các thông tin mới nhất từ hệ thống.</p>
            </div>
          </div>
          <div className="notifications-header-actions">
            {unreadCount > 0 && (
              <button className="notifications-mark-read-btn" type="button" onClick={handleMarkAllRead}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 8 12 12 16 14" />
                </svg>
                <span>Đọc tất cả</span>
              </button>
            )}
          </div>
        </header>

        {/* Role Switcher Tabs */}
        <div className="role-switcher-container">
          <button 
            className={`role-switch-btn ${activeRole === 'tenant' ? 'active' : ''}`} 
            onClick={() => {
              setActiveRole('tenant');
              setActiveTab('all');
            }}
          >
            Người thuê
          </button>
          <button 
            className={`role-switch-btn ${activeRole === 'landlord' ? 'active' : ''}`} 
            onClick={() => {
              setActiveRole('landlord');
              setActiveTab('all');
            }}
          >
            Chủ trọ
          </button>
        </div>

        {/* Categories Tab Bar */}
        <nav className="notifications-tabs-bar" style={{ marginBottom: '24px' }}>
          <button className={`notifications-tab-item ${activeTab === 'all' ? 'active' : ''}`} onClick={() => setActiveTab('all')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <rect x="3" y="3" width="7" height="7" rx="1.5" />
              <rect x="14" y="3" width="7" height="7" rx="1.5" />
              <rect x="14" y="14" width="7" height="7" rx="1.5" />
              <rect x="3" y="14" width="7" height="7" rx="1.5" />
            </svg>
            <span>Tất cả</span>
            <span className="tab-badge-count blue">{totalCount}</span>
          </button>
          
          <button className={`notifications-tab-item ${activeTab === 'unread' ? 'active' : ''}`} onClick={() => setActiveTab('unread')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
              <polyline points="22,6 12,13 2,6" />
            </svg>
            <span>Chưa đọc</span>
            <span className="tab-badge-count blue">{unreadCount}</span>
          </button>

          <button className={`notifications-tab-item ${activeTab === 'rental' ? 'active' : ''}`} onClick={() => setActiveTab('rental')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
              <polyline points="9 22 9 12 15 12 15 22" />
            </svg>
            <span>Yêu cầu thuê</span>
            <span className="tab-badge-count orange">{rentalCount}</span>
          </button>

          <button className={`notifications-tab-item ${activeTab === 'appointment' ? 'active' : ''}`} onClick={() => setActiveTab('appointment')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
              <line x1="16" y1="2" x2="16" y2="6" />
              <line x1="8" y1="2" x2="8" y2="6" />
              <line x1="3" y1="10" x2="21" y2="10" />
            </svg>
            <span>Lịch hẹn</span>
            <span className="tab-badge-count sky">{appointmentCount}</span>
          </button>

          <button className={`notifications-tab-item ${activeTab === 'billing' ? 'active' : ''}`} onClick={() => setActiveTab('billing')}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="5" width="20" height="14" rx="2" ry="2" />
              <line x1="2" y1="10" x2="22" y2="10" />
            </svg>
            <span>Thanh toán</span>
            <span className="tab-badge-count green">{billingCount}</span>
          </button>
        </nav>

        {/* Content list */}
        {loading ? (
          <LoadingState message="Đang tải danh sách thông báo..." />
        ) : filteredItems.length === 0 ? (
          <div style={{ background: '#ffffff', borderRadius: '16px', padding: '48px', textAlign: 'center', color: '#64748b', border: '1px solid #eef2f6' }}>
            Không có thông báo nào phù hợp với bộ lọc hiện tại.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '28px' }}>
            {dateKeys.map(dateKey => {
              const dateItems = groupedNotifications[dateKey];
              return (
                <div key={dateKey} className="notifications-date-group">
                  <header className="notifications-list-section-header">
                    <span className="notifications-date-group-title">{dateKey}</span>
                  </header>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    {dateItems.map(item => {
                      const info = getNotificationCategoryInfo(item);
                      return (
                        <div
                          key={item.id}
                          className={`notification-card-item ${!item.isRead ? 'unread' : ''}`}
                          onClick={() => handleMarkRead(item)}
                        >
                          <div className={`notification-card-icon-wrapper icon-theme--${info.theme}`}>
                            {info.icon}
                          </div>
                          <div className="notification-card-content">
                            <h3 className="notification-card-title">{item.title}</h3>
                            <p className="notification-card-description">{item.body}</p>
                            <span className="notification-card-time">{formatDateTimeString(item.createdAt)}</span>
                          </div>
                          <span className={`notification-category-tag ${info.theme}`}>
                            {info.label}
                          </span>
                          <button
                            className="notification-delete-btn"
                            type="button"
                            onClick={(e) => handleDeleteNotification(e, item.id)}
                            title="Xóa thông báo"
                            aria-label="Xóa thông báo"
                          >
                            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                              <polyline points="3 6 5 6 21 6" />
                              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                              <line x1="10" y1="11" x2="10" y2="17" />
                              <line x1="14" y1="11" x2="14" y2="17" />
                            </svg>
                          </button>
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}

            {totalCount > limit && (
              <div className="notifications-view-more-container">
                <button 
                  className="notifications-view-more-btn" 
                  onClick={() => setLimit(prev => prev + 15)}
                  type="button"
                >
                  <span>Xem thêm</span>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
                </button>
              </div>
            )}
          </div>
        )}
      </section>
    </main>
  );
}
