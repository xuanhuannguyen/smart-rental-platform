import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import './AccountLayout.css';

export function AccountLayout() {
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  if (!currentUser) {
    return null;
  }

  return (
    <div className="account-container">
      <div className="account-layout">
        {/* Sidebar */}
        <aside className="account-sidebar">
          <h2>Cài đặt tài khoản</h2>
          
          <NavLink
            to={ROUTE_PATHS.ACCOUNT.PROFILE}
            className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
          >
            Chỉnh sửa thông tin
          </NavLink>
          
          <NavLink
            to={ROUTE_PATHS.ACCOUNT.SECURITY}
            className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
          >
            Quản lý bảo mật
          </NavLink>
          
          {/* Lịch sử thuê can be added here later */}
          <button
            type="button"
            className="account-sidebar-item"
            onClick={() => alert('Tính năng Lịch sử thuê đang được phát triển.')}
          >
            Lịch sử thuê
          </button>
          
          <NavLink
            to={ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS}
            className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
          >
            Yêu cầu thuê
          </NavLink>

          <button
            type="button"
            className="account-sidebar-item sidebar-back-btn"
            onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
            style={{ marginTop: 'auto' }}
          >
            ← Quay lại trang chủ
          </button>
        </aside>

        {/* Content Area */}
        <main className="account-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
