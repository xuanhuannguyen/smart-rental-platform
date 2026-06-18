import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../ui/Button';
import { Toast } from '../ui/Toast';
import { toAssetUrl } from '../../api/assets';
import { getMyRoomingHouseOnboarding } from '../../../features/rooming-houses/api';
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
        navigate(ROUTE_PATHS.LANDLORD.DASHBOARD);
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
        Smart Rental
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
              <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ADMIN.APPROVALS)}>
                Duyệt hồ sơ
              </Button>
            ) : isLandlord ? (
              <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
                Kênh chủ trọ
              </Button>
            ) : (
              <Button
                type="button"
                disabled={isCheckingLandlord}
                onClick={handleLandlordRegister}
              >
                {isCheckingLandlord ? 'Đang xử lý...' : 'Đăng ký làm chủ trọ'}
              </Button>
            )}
          </div>
        )}

        {currentUser ? (
          <div className="avatar-wrapper" ref={dropdownRef}>
            <button className="avatar-btn" onClick={() => setShowDropdown(!showDropdown)}>
              {currentUser.avatarUrl && currentUser.avatarUrl.trim() !== '' ? (
                <img src={toAssetUrl(currentUser.avatarUrl)} alt="Avatar" className="avatar-image" />
              ) : (
                <span className="avatar-initials">{avatarInitials}</span>
              )}
              <span className="avatar-name">{currentUser.displayName}</span>
            </button>
            {showDropdown && (
              <div className="avatar-dropdown">
                <div className="dropdown-info">
                  <strong>{currentUser.displayName}</strong>
                  <span>{currentUser.email}</span>
                </div>
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
          </div>
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
