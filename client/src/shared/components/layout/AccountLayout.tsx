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
      <aside className="account-sidebar">
        <h2>Quản lý tài khoản</h2>
        
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
        
        <NavLink
          to={ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          Lịch hẹn xem phòng
        </NavLink>
        
        <NavLink 
          to={ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          Yêu cầu thuê phòng
        </NavLink>

        <NavLink 
          to={ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY}
          className={({ isActive }) => `account-sidebar-item ${isActive ? 'active' : ''}`}
        >
          Lịch sử thuê phòng
        </NavLink>

        <button
          type="button"
          className="account-sidebar-item sidebar-back-btn"
          onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
        >
          Quay lại trang chủ
        </button>
      </aside>

      <main className="account-content">
        <Outlet />
      </main>
    </div>
  );
}
