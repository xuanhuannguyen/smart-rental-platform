import { useState, useEffect, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { toAssetUrl } from '../../../shared/api/assets';
import { apiClient } from '../../../shared/api/apiClient';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import { HomeHeader } from '../../../shared/components/layout/HomeHeader';
import './MePage.css';

interface PublicRoomingHouse {
  id: string;
  name: string;
  addressDisplay: string;
  avatarUrl?: string | null;
  landlordName: string;
}

interface PublicRoom {
  id: string;
  roomNumber: string;
  floor: number;
  area: number;
  maxOccupants: number;
  status: string;
  images: { id: string; url: string; order: number }[];
  priceTiers: { monthlyRent: number }[];
}

export function MePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser, logout } = useAuth();
  
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  useEffect(() => {
    if (location.state?.message) {
      setToastMessage(location.state.message);
      window.history.replaceState({}, document.title);
    }
  }, [location]);

  const [error, setError] = useState('');
  const [isCheckingLandlord, setIsCheckingLandlord] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [publicHouses, setPublicHouses] = useState<PublicRoomingHouse[]>([]);
  const [loadingPublic, setLoadingPublic] = useState(true);

  const [selectedHouseId, setSelectedHouseId] = useState<string | null>(null);
  const [publicRooms, setPublicRooms] = useState<PublicRoom[]>([]);
  const [loadingRooms, setLoadingRooms] = useState(false);

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

  useEffect(() => {
    async function loadPublicHouses() {
      try {
        const res = await apiClient<{ data: PublicRoomingHouse[] }>(ENDPOINTS.PUBLIC.ROOMING_HOUSES, { method: 'GET' });
        setPublicHouses(res.data || []);
      } catch (err) {
        console.error('Failed to load public houses', err);
      } finally {
        setLoadingPublic(false);
      }
    }
    void loadPublicHouses();
  }, []);

  const handleHouseClick = async (houseId: string) => {
    setSelectedHouseId(houseId);
    setLoadingRooms(true);
    try {
      const res = await apiClient<{ data: PublicRoom[] }>(ENDPOINTS.PUBLIC.ROOMS(houseId), { method: 'GET' });
      setPublicRooms(res.data || []);
    } catch (err) {
      console.error('Failed to load public rooms', err);
    } finally {
      setLoadingRooms(false);
    }
  };

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
      {toastMessage && <Toast message={toastMessage} onClose={() => setToastMessage(null)} />}
      
      {/* Header */}
      <HomeHeader />

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

      <section className="home-public-houses" style={{ padding: '40px 24px', maxWidth: '1200px', margin: '0 auto' }}>
        {selectedHouseId ? (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', marginBottom: '24px', gap: '16px' }}>
              <Button type="button" variant="secondary" onClick={() => setSelectedHouseId(null)}>← Quay lại</Button>
              <h2 style={{ fontSize: '1.5rem', margin: 0 }}>Danh sách phòng trống</h2>
            </div>
            {loadingRooms ? (
              <p>Đang tải danh sách phòng...</p>
            ) : publicRooms.length === 0 ? (
              <p className="subtle">Khu trọ này hiện chưa có phòng trống.</p>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: '24px' }}>
                {publicRooms.map(room => (
                  <div key={room.id} style={{ border: '1px solid #e2e8f0', borderRadius: '12px', padding: '16px', background: '#fff', display: 'flex', flexDirection: 'column' }}>
                    {room.images && room.images.length > 0 ? (
                      <img src={toAssetUrl(room.images[0].url)} alt={room.roomNumber} style={{ width: '100%', height: '160px', objectFit: 'cover', borderRadius: '8px', marginBottom: '16px' }} />
                    ) : (
                      <div style={{ width: '100%', height: '160px', background: '#f1f5f9', borderRadius: '8px', marginBottom: '16px', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#94a3b8' }}>Không có ảnh</div>
                    )}
                    <h3 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Phòng {room.roomNumber}</h3>
                    <p style={{ color: '#64748b', fontSize: '0.9rem', marginBottom: '8px' }}>Tầng {room.floor} • Diện tích: {room.area}m²</p>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 'auto', paddingTop: '16px', borderTop: '1px solid #f1f5f9' }}>
                      <span style={{ fontSize: '1rem', fontWeight: 600, color: '#2563eb' }}>
                        {room.priceTiers && room.priceTiers.length > 0 && room.priceTiers[0].monthlyRent != null ? room.priceTiers[0].monthlyRent.toLocaleString() + 'đ' : 'Liên hệ'}
                      </span>
                      <Button type="button" onClick={() => navigate(`${ROUTE_PATHS.RENTAL_REQUESTS.SUBMIT}?roomId=${room.id}`)}>
                        Thuê ngay
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        ) : (
          <div>
            <h2 style={{ fontSize: '1.5rem', marginBottom: '24px' }}>Khu trọ nổi bật (Test luồng thuê)</h2>
            {loadingPublic ? (
              <p>Đang tải danh sách khu trọ...</p>
            ) : publicHouses.length === 0 ? (
              <p className="subtle">Chưa có khu trọ nào được duyệt hiển thị.</p>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: '24px' }}>
                {publicHouses.map(house => (
                  <div key={house.id} onClick={() => void handleHouseClick(house.id)} style={{ cursor: 'pointer', border: '1px solid #e2e8f0', borderRadius: '12px', padding: '16px', background: '#fff', display: 'flex', flexDirection: 'column', transition: 'all 0.2s ease' }}>
                    {house.avatarUrl ? (
                      <img src={toAssetUrl(house.avatarUrl)} alt={house.name} style={{ width: '100%', height: '200px', objectFit: 'cover', borderRadius: '8px', marginBottom: '16px' }} />
                    ) : (
                      <div style={{ width: '100%', height: '200px', background: '#f1f5f9', borderRadius: '8px', marginBottom: '16px', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#94a3b8' }}>
                        Không có ảnh
                      </div>
                    )}
                    <h3 style={{ fontSize: '1.2rem', marginBottom: '8px', flex: 1 }}>{house.name}</h3>
                    <p style={{ color: '#64748b', fontSize: '0.9rem', marginBottom: '16px' }}>{house.addressDisplay}</p>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 'auto' }}>
                      <span style={{ fontSize: '0.85rem', color: '#475569' }}>Chủ trọ: <strong>{house.landlordName}</strong></span>
                      <span style={{ fontSize: '0.9rem', color: '#2563eb', fontWeight: 500 }}>Xem phòng →</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </section>

      {/* Footer */}
      <footer className="home-footer">
        <p>&copy; 2026 Smart Rental Platform. All rights reserved.</p>
      </footer>
    </div>
  );
}
