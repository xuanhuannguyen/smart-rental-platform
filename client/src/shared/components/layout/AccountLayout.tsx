import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { NotificationBell } from '../../../features/notifications/components/NotificationBell';
import { toAvatarImageUrl } from '../../../shared/api/assets';
import './AccountLayout.css';

export function AccountLayout() {
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  if (!currentUser) {
    return null;
  }

  return (
    <div className="account-container">
      <aside className="account-sidebar">
        <div className="account-sidebar-header">
          <div className="account-sidebar-logo-group">
            <div className="account-sidebar-logo-box">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </div>
            <h2>Quản lý tài khoản</h2>
          </div>
          <NotificationBell navigateOnly />
        </div>

        <div className="sidebar-profile-card" onClick={() => navigate(ROUTE_PATHS.ACCOUNT.PROFILE)}>
        <div className="sidebar-profile-avatar-wrapper">
          {currentUser.avatarUrl && currentUser.avatarUrl.trim() !== '' ? (
              <img src={toAvatarImageUrl(currentUser)} alt="Avatar" className="sidebar-profile-avatar" />
            ) : (
              <div className="sidebar-profile-avatar-placeholder">
                {currentUser.displayName ? currentUser.displayName.charAt(0).toUpperCase() : 'H'}
              </div>
            )}
          </div>
          <div className="sidebar-profile-info">
            <span className="sidebar-profile-name">{currentUser.displayName || 'Người dùng'}</span>
          </div>
          <div className="sidebar-profile-arrow">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </div>
        </div>
        
        <NavLink
          to={ROUTE_PATHS.ACCOUNT.WALLET}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
            <polyline points="9 22 9 12 15 12 15 22" />
          </svg>
          <span className="sidebar-item-text">Ví của tôi</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <NavLink
          to={ROUTE_PATHS.ACCOUNT.PROFILE}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
            <circle cx="12" cy="7" r="4" />
          </svg>
          <span className="sidebar-item-text">Chỉnh sửa thông tin</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>
        
        <NavLink
          to={ROUTE_PATHS.ACCOUNT.SECURITY}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
          <span className="sidebar-item-text">Quản lý bảo mật</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <div className="sidebar-divider" />
        
        <NavLink
          to={ROUTE_PATHS.ACCOUNT.FAVORITES}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
          </svg>
          <span className="sidebar-item-text">Khu trọ yêu thích</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>
        
        <NavLink
          to={ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
          <span className="sidebar-item-text">Lịch hẹn xem phòng</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>
        
        <NavLink 
          to={ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <circle cx="10" cy="13" r="2" />
            <path d="m16 17-3.5-3.5" />
          </svg>
          <span className="sidebar-item-text">Yêu cầu thuê phòng</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <NavLink 
          to={ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
          <span className="sidebar-item-text">Lịch sử thuê phòng</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <NavLink
          to={ROUTE_PATHS.ACCOUNT.INVOICES}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="9" y1="15" x2="15" y2="15" />
            <line x1="9" y1="11" x2="15" y2="11" />
          </svg>
          <span className="sidebar-item-text">Hóa đơn của tôi</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <NavLink
          to={ROUTE_PATHS.ACCOUNT.TRANSACTIONS}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
          </svg>
          <span className="sidebar-item-text">Lịch sử giao dịch</span>
          <svg className="sidebar-item-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </NavLink>

        <div className="sidebar-divider" />

        <button
          type="button"
          className="sidebar-back-button"
          onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
        >
          <svg className="sidebar-item-icon back-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="19" y1="12" x2="5" y2="12" />
            <polyline points="12 19 5 12 12 5" />
          </svg>
          <span>Quay về trang chủ</span>
        </button>
      </aside>

      <main className="account-content">
        <Outlet />
      </main>
    </div>
  );
}
