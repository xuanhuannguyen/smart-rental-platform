import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { NotificationBell } from '../../../features/notifications/components/NotificationBell';
import './LandlordLayout.css';

export function LandlordLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser } = useAuth();

  if (!currentUser) {
    return null;
  }

  // Active state conditions for sub-routes
  const isDashboardActive = location.pathname === ROUTE_PATHS.LANDLORD.DASHBOARD || location.pathname === '/landlord';
  const isRoomingHousesActive = location.pathname.startsWith('/landlord/rooming-houses') && !location.pathname.includes('/service-prices');
  const isRequestsActive = location.pathname.startsWith('/landlord/rental-requests');
  const isAppointmentsActive = location.pathname.startsWith('/landlord/viewing-appointments');
  const isMessagesActive = location.pathname.startsWith('/landlord/messages');
  const isInvoicesActive = location.pathname.includes('/billing') || location.pathname.includes('/meter-readings') || location.pathname.includes('/invoices') || location.pathname.includes('/service-prices');
  const isContractsActive = location.pathname.startsWith('/landlord/contracts');

  return (
    <div className="landlord-dashboard">
      <aside className="dashboard-sidebar">
        <div className="landlord-sidebar-header">
          <div className="sidebar-brand-wrapper">
            <div className="sidebar-brand-icon">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
            </div>
            <div className="sidebar-brand-text">
              <h1>Chủ trọ</h1>
              <p>Quản lý nhà trọ của bạn</p>
            </div>
          </div>
          <NotificationBell navigateOnly />
        </div>

        <div className="sidebar-items-list">
          <NavLink
            to={ROUTE_PATHS.LANDLORD.DASHBOARD}
            end
            className={() => `sidebar-item ${isDashboardActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="20" x2="18" y2="10" />
                <line x1="12" y1="20" x2="12" y2="4" />
                <line x1="6" y1="20" x2="6" y2="14" />
              </svg>
            </span>
            <span className="sidebar-item-text">Thống kê</span>
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.ROOMING_HOUSES}
            className={() => `sidebar-item ${isRoomingHousesActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
                <line x1="9" y1="22" x2="9" y2="16" />
                <line x1="15" y1="22" x2="15" y2="16" />
                <line x1="9" y1="16" x2="15" y2="16" />
                <path d="M9 6h.01" />
                <path d="M15 6h.01" />
                <path d="M9 10h.01" />
                <path d="M15 10h.01" />
              </svg>
            </span>
            <span className="sidebar-item-text">Quản lý khu trọ</span>
            {!isRoomingHousesActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS}
            className={() => `sidebar-item ${isRequestsActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </span>
            <span className="sidebar-item-text">Yêu cầu thuê</span>
            {!isRequestsActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS}
            className={() => `sidebar-item ${isAppointmentsActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
                <path d="M8 14h.01" />
                <path d="M12 14h.01" />
                <path d="M16 14h.01" />
                <path d="M8 18h.01" />
                <path d="M12 18h.01" />
                <path d="M16 18h.01" />
              </svg>
            </span>
            <span className="sidebar-item-text">Lịch hẹn xem phòng</span>
            {!isAppointmentsActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.INVOICES}
            className={() => `sidebar-item ${isInvoicesActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
                <polyline points="10 9 9 9 8 9" />
              </svg>
            </span>
            <span className="sidebar-item-text">Quản lý hóa đơn</span>
            {!isInvoicesActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.MESSAGES}
            className={() => `sidebar-item ${isMessagesActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
              </svg>
            </span>
            <span className="sidebar-item-text">Tin nhắn</span>
            {!isMessagesActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>

          <NavLink
            to={ROUTE_PATHS.LANDLORD.CONTRACTS}
            className={() => `sidebar-item ${isContractsActive ? 'active' : ''}`}
          >
            <span className="sidebar-item-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 20h9" />
                <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
              </svg>
            </span>
            <span className="sidebar-item-text">Hợp đồng</span>
            {!isContractsActive && (
              <svg className="sidebar-chevron" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            )}
          </NavLink>
        </div>

        <div className="sidebar-promo-card">
          <div className="promo-icon-wrapper">
            <svg viewBox="0 0 24 24" width="32" height="32" fill="none" stroke="currentColor">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" fill="#eff6ff" stroke="#246bfe" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              <polyline points="9 11 11 13 15 9" stroke="#246bfe" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <div className="promo-content-wrapper">
            <h4>Quản lý hiệu quả hơn</h4>
            <p>Cập nhật thông tin và theo dõi tình hình trọ mọi lúc, mọi nơi.</p>
          </div>
        </div>

        <button
          type="button"
          className="sidebar-back-btn"
          onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
        >
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <line x1="19" y1="12" x2="5" y2="12" />
            <polyline points="12 19 5 12 12 5" />
          </svg>
          Quay về trang chủ
        </button>
      </aside>

      <Outlet />
    </div>
  );
}
