import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../ui/Button';
import { Toast } from '../ui/Toast';
import { toAvatarImageUrl } from '../../api/assets';
import { getMyRoomingHouseOnboarding } from '../../../features/rooming-houses/api';
import { NotificationBell } from '../../../features/notifications/components/NotificationBell';
import './HomeHeader.css';

interface HomeHeaderProps {
  centerContent?: React.ReactNode;
}

export function HomeHeader({ centerContent }: HomeHeaderProps) {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [isCheckingLandlord, setIsCheckingLandlord] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  const isAdmin = currentUser?.roles.includes('Admin') || false;
  const isLandlord = currentUser?.roles.includes('Landlord') || false;

  async function handleLandlordRegister() {
    setIsCheckingLandlord(true);
    try {
      const onboarding = await getMyRoomingHouseOnboarding();

      if (onboarding.canEnterLandlordDashboard) {
        navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES);
        return;
      }

      if ((onboarding.status === 'Draft' || onboarding.status === 'Rejected') && onboarding.roomingHouseId) {
        navigate(`${ROUTE_PATHS.LANDLORD.REGISTER}?id=${onboarding.roomingHouseId}`);
        return;
      }

      if (onboarding.status === 'Pending') {
        setToastMessage('Hồ sơ chủ trọ của bạn đang chờ duyệt.');
        return;
      }

      navigate(ROUTE_PATHS.LANDLORD.REGISTER);
    } catch {
      setToastMessage('Không thể kiểm tra trạng thái đăng ký chủ trọ.');
    } finally {
      setIsCheckingLandlord(false);
    }
  }

  const avatarInitials = currentUser?.displayName
    ? currentUser.displayName.split(' ').map((n: string) => n[0]).join('').substring(0, 2).toUpperCase()
    : 'U';

  return (
    <header className="home-header">
      <div className="header-logo" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
        <div className="logo-icon-container">
          <svg viewBox="0 0 24 24" fill="none" stroke="#ffffff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="logo-svg-icon">
            <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
            <polyline points="9 22 9 12 15 12 15 22" />
          </svg>
        </div>
        <span className="logo-text">Smart Rental</span>
      </div>

      {centerContent && (
        <div className="header-center">
          {centerContent}
        </div>
      )}

      <div className="header-auth" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
        {currentUser && (
          <div className="header-role-action">
            {isAdmin ? (
              <Button type="button" className="admin-channel-btn" onClick={() => navigate(ROUTE_PATHS.ADMIN.APPROVALS)}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                </svg>
                Duyệt hồ sơ
              </Button>
            ) : isLandlord ? (
              <Button type="button" className="landlord-channel-btn" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                  <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                  <polyline points="9 22 9 12 15 12 15 22" />
                </svg>
                Kênh chủ trọ
              </Button>
            ) : (
              <Button
                type="button"
                className="landlord-register-btn"
                disabled={isCheckingLandlord}
                onClick={handleLandlordRegister}
              >
                {isCheckingLandlord ? 'Đang xử lý...' : 'Đăng ký làm chủ trọ'}
              </Button>
            )}
          </div>
        )}

        {currentUser ? (
          <>
            <NotificationBell />
            <div className="avatar-wrapper" ref={dropdownRef}>
            <button className="avatar-btn" onClick={() => setShowDropdown(!showDropdown)}>
              {currentUser.avatarUrl && currentUser.avatarUrl.trim() !== '' ? (
                <img src={toAvatarImageUrl(currentUser.avatarUrl)} alt="Avatar" className="avatar-image" />
              ) : (
                <span className="avatar-initials">{avatarInitials}</span>
              )}
              <span className="avatar-name">{currentUser.displayName}</span>
              <svg className="avatar-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </button>
            {showDropdown && (
              <div className="avatar-dropdown">
                <div className="dropdown-info">
                  <strong>{currentUser.displayName}</strong>
                  <span>{currentUser.email}</span>
                </div>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.WALLET); }}>
                  Ví của tôi
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.PROFILE); }}>
                  Chỉnh sửa thông tin
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.SECURITY); }}>
                  Quản lý bảo mật
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS); }}>
                  Lịch hẹn xem phòng
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS); }}>
                  Yêu cầu thuê phòng
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY); }}>
                  Lịch sử thuê phòng
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.INVOICES); }}>
                  Hóa đơn của tôi
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.TRANSACTIONS); }}>
                  Lịch sử giao dịch
                </button>
                {isAdmin && (
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ADMIN.APPROVALS); }}>
                    Duyệt hồ sơ
                  </button>
                )}

                <button className="dropdown-item dropdown-item--danger" onClick={() => { setShowDropdown(false); logout(); }}>
                  Đăng xuất
                </button>
              </div>
            )}
          </div></>
        ) : (
          <div className="auth-buttons">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.AUTH.LOGIN)}>
              Đăng nhập
            </Button>
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.AUTH.REGISTER)}>
              Đăng ký
            </Button>
          </div>
        )}
      </div>
      {toastMessage && <Toast message={toastMessage} onClose={() => setToastMessage(null)} />}
    </header>
  );
}
