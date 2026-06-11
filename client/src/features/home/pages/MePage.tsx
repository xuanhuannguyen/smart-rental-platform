import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { toAssetUrl } from '../../../shared/api/assets';
import './MePage.css';

export function MePage() {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [isCheckingLandlord, setIsCheckingLandlord] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Chuyển hướng bắt buộc nếu user đã đăng nhập nhưng chưa xác thực email
  useEffect(() => {
    if (currentUser && !currentUser.emailConfirmed) {
      navigate(ROUTE_PATHS.AUTH.VERIFY_EMAIL, { replace: true });
    }
  }, [currentUser, navigate]);

  // Click bên ngoài để đóng dropdown Avatar
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

  function handleLandlordRegister() {
    navigate(ROUTE_PATHS.LANDLORD.REGISTER);
  }

  // Tên viết tắt để hiển thị trên Avatar
  const avatarInitials = currentUser?.displayName
    ? currentUser.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
    : 'U';

  return (
    <div className="home-container">
      {/* Header */}
      <header className="home-header">
        <div className="header-logo" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
          Smart Rental
        </div>
        <div className="header-auth" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {currentUser && (
            <div className="header-role-action">
              {isAdmin ? (
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ADMIN.APPROVALS)}>
                  Duyệt hồ sơ
                </Button>
              ) : isLandlord ? (
                <>
                  <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
                    Kênh chủ trọ
                  </Button>
                </>
              ) : (
                <>
                  <Button
                    type="button"
                    disabled={isCheckingLandlord}
                    onClick={() => void handleLandlordRegister()}
                  >
                    {isCheckingLandlord ? 'Đang xử lý...' : 'Đăng ký làm chủ trọ'}
                  </Button>
                </>
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
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ME.PROFILE); }}>
                    Chỉnh sửa thông tin
                  </button>
                  {isAdmin && (
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ADMIN.APPROVALS); }}>
                      Duyệt hồ sơ
                    </button>
                  )}
                  {isLandlord && (
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.LANDLORD.DASHBOARD); }}>
                      Kênh chủ trọ
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
      </header>

      {/* Hero Section */}
      <section className="hero-section">
        <div className="hero-content">
          <p className="eyebrow">Smart Rental Platform</p>
          <h1>Tìm kiếm và quản lý phòng trọ thông minh</h1>
          <p className="hero-description">
            Nền tảng kết nối người thuê trọ và chủ trọ uy tín. Hỗ trợ xác thực danh tính eKYC an toàn,
            ký hợp đồng online và quản lý phòng trọ chuyên nghiệp, tự động.
          </p>



          {error && (
            <div style={{ marginTop: '16px', maxWidth: '520px', width: '100%' }}>
              <Alert type="error">{error}</Alert>
            </div>
          )}
        </div>
      </section>



      {/* Footer */}
      <footer className="home-footer">
        <p>&copy; 2026 Smart Rental Platform. All rights reserved.</p>
      </footer>
    </div>
  );
}
