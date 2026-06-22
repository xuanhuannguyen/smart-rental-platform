import { NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { NotificationBell } from '../../../features/notifications/components/NotificationBell';

export function LandlordLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser } = useAuth();

  if (!currentUser) {
    return null;
  }

  return (
    <div className="landlord-dashboard">
      <aside className="dashboard-sidebar">
        <div className="landlord-sidebar-header">
          <h1>Chủ trọ</h1>
          <NotificationBell />
        </div>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.DASHBOARD}
          end
          className={({ isActive }) => `sidebar-item ${isActive ? 'active' : ''}`}
        >
          Thống kê
        </NavLink>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.ROOMING_HOUSES}
          className={({ isActive }) => `sidebar-item ${isActive ? 'active' : ''}`}
        >
          Quản lý khu trọ
        </NavLink>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS}
          className={({ isActive }) => `sidebar-item ${isActive ? 'active' : ''}`}
        >
          Yêu cầu thuê
        </NavLink>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS}
          className={({ isActive }) => `sidebar-item ${isActive ? 'active' : ''}`}
        >
          Lịch hẹn xem phòng
        </NavLink>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.INVOICES}
          className={({ isActive }) => `sidebar-item ${isActive || location.pathname.includes('/billing') || location.pathname.includes('/meter-readings') || location.pathname.includes('/invoices') || location.pathname.includes('/service-prices') ? 'active' : ''}`}
        >
          Quản lý hóa đơn
        </NavLink>
        <NavLink
          to={ROUTE_PATHS.LANDLORD.CONTRACTS}
          className={({ isActive }) => `sidebar-item ${isActive ? 'active' : ''}`}
        >
          Hợp đồng
        </NavLink>
        <button
          type="button"
          className="sidebar-item sidebar-back-btn"
          onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
        >
          ← Quay lại trang chủ
        </button>
      </aside>

      <Outlet />
    </div>
  );
}
