import { useEffect, useState } from 'react';
import { Link, useParams, useNavigate, useLocation } from 'react-router-dom';
import { getPublicRoomingHouseDetail } from '../../rooming-houses/api';
import HouseImageGallery from '../../rooming-houses/components/HouseImageGallery';
import type { RoomingHouseDetail, RoomInHouseDetail } from '../../rooming-houses/types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { useAuth } from '../../../app/providers/AuthProvider';
import { Alert } from '../../../shared/components/ui/Alert';
import ViewingAppointmentModal from '../../viewing-appointments/components/ViewingAppointmentModal';
import SubmitRentalRequestModal from '../../rental-requests/components/SubmitRentalRequestModal';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { HomeHeader } from '../../../shared/components/layout/HomeHeader';
import './PublicRoomDetailPage.css';

function formatCurrency(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    currency: 'VND',
    maximumFractionDigits: 0,
    style: 'currency',
  }).format(value);
}

export default function PublicRoomDetailPage() {
  const { houseId, roomId } = useParams<{ houseId: string; roomId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser } = useAuth();

  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isRentalModalOpen, setIsRentalModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  const handleBookingClick = () => {
    if (!currentUser) {
      navigate('/login', { state: { from: location.pathname } });
      return;
    }
    setIsModalOpen(true);
  };

  useEffect(() => {
    async function loadHouseDetail() {
      if (!houseId) return;
      setLoading(true);
      setError('');
      try {
        const data = await getPublicRoomingHouseDetail(houseId);
        setHouse(data);
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'Không thể tải chi tiết phòng trọ.'));
      } finally {
        setLoading(false);
      }
    }

    void loadHouseDetail();
  }, [houseId]);

  if (loading) {
    return (
      <>
        <HomeHeader />
        <main className="public-room-detail public-room-detail--state">Đang tải chi tiết phòng trọ...</main>
      </>
    );
  }

  if (error || !house) {
    return (
      <>
        <HomeHeader />
        <main className="public-room-detail public-room-detail--state">
          <p className="public-room-detail__error-text">{error || 'Không tìm thấy thông tin khu trọ.'}</p>
          <Link to="/home" className="public-room-detail__home-link">Quay về trang chủ</Link>
        </main>
      </>
    );
  }

  const room = (house.rooms ?? []).find((r) => r.id === roomId);

  if (!room) {
    return (
      <>
        <HomeHeader />
        <main className="public-room-detail public-room-detail--state">
          <p className="public-room-detail__error-text">Không tìm thấy thông tin phòng trọ này.</p>
          <Link to={`/rooming-houses/${house.id}`} className="public-room-detail__home-link">Quay lại khu trọ</Link>
        </main>
      </>
    );
  }

  const roomImages = room.images ?? [];
  const roomAmenities = room.amenities ?? [];
  const activePriceTiers = (room.priceTiers ?? []).filter((tier) => tier.isActive);

  return (
    <>
      <HomeHeader />
      <main className="public-room-detail">
        <Link className="public-room-detail__back" to={`/rooming-houses/${house.id}`}>
        ← Quay về khu trọ: {house.name}
      </Link>

      {successMessage && (
        <div style={{ margin: '16px 0' }}>
          <Alert type="success">{successMessage}</Alert>
        </div>
      )}

      <div className="public-room-detail__container">
        {/* Left Column: Room Gallery */}
        <section className="public-room-detail__gallery-section">
          <HouseImageGallery images={roomImages} houseName={`Phòng ${room.roomNumber}`} />
        </section>

        {/* Right Column: Room Info Details */}
        <section className="public-room-detail__info-section">
          <header className="public-room-detail__header">
            <div className="public-room-detail__badge-row">
              <span className="public-room-detail__badge">Tầng {room.floor}</span>
              <span className="public-room-detail__badge public-room-detail__badge--accent">
                Còn trống
              </span>
            </div>
            <h1>Phòng {room.roomNumber}</h1>
            <p className="public-room-detail__house-name">Khu trọ: {house.name}</p>
            <p className="public-room-detail__address">{house.addressDisplay}</p>
            <div className="public-room-detail__actions" aria-label="Thao tác với phòng">
              <button
                className="public-room-detail__action public-room-detail__action--primary"
                type="button"
                onClick={() => {
                  if (!currentUser) {
                    navigate('/login', { state: { from: location.pathname } });
                    return;
                  }
                  setIsRentalModalOpen(true);
                }}
              >
                Gửi yêu cầu thuê
              </button>
              <button className="public-room-detail__action public-room-detail__action--secondary" type="button" onClick={handleBookingClick}>
                Đặt lịch xem phòng
              </button>
            </div>
          </header>

          <div className="public-room-detail__specs">
            <div className="public-room-detail__spec-item">
              <span className="spec-label">Diện tích</span>
              <strong className="spec-value">{room.areaM2 ? `${room.areaM2} m²` : 'Chưa cập nhật'}</strong>
            </div>
            <div className="public-room-detail__spec-item">
              <span className="spec-label">Sức chứa tối đa</span>
              <strong className="spec-value">{room.maxOccupants} người</strong>
            </div>
          </div>

          {/* Pricing Tiers Section */}
          <div className="public-room-detail__pricing">
            <h2>Bảng giá thuê theo người ở</h2>
            {activePriceTiers.length > 0 ? (
              <div className="public-room-detail__price-list">
                {activePriceTiers.map((tier) => (
                  <div className="public-room-detail__price-row" key={tier.id}>
                    <span className="price-occupants">
                      👤 {tier.occupantCount} {tier.occupantCount === room.maxOccupants ? 'người (Tối đa)' : 'người'}
                    </span>
                    <strong className="price-value-text">{formatCurrency(tier.monthlyRent)}/tháng</strong>
                  </div>
                ))}
              </div>
            ) : (
              <p className="public-room-detail__muted">Liên hệ chủ trọ để biết thêm chi tiết giá.</p>
            )}
          </div>

          {/* Amenities Section */}
          <div className="public-room-detail__amenities-container">
            <h2>Tiện ích phòng</h2>
            {roomAmenities.length > 0 ? (
              <div className="public-room-detail__amenities">
                {roomAmenities.map((amenity) => (
                  <span key={amenity.id} className="public-room-detail__amenity-badge">
                    {amenity.name}
                  </span>
                ))}
              </div>
            ) : (
              <p className="public-room-detail__muted">Phòng này chưa cập nhật tiện ích.</p>
            )}
          </div>

          {/* Description Section */}
          {room.description && (
            <div className="public-room-detail__description-container">
              <h2>Mô tả phòng</h2>
              <p className="public-room-detail__description">{room.description}</p>
            </div>
          )}
        </section>
      </div>

      {isModalOpen && (
        <ViewingAppointmentModal
          roomId={room.id}
          roomNumber={room.roomNumber}
          houseName={house.name}
          onClose={() => setIsModalOpen(false)}
          onSuccess={(msg) => {
            setSuccessMessage(msg);
            setTimeout(() => setSuccessMessage(''), 5000);
          }}
        />
      )}

      {isRentalModalOpen && (
        <SubmitRentalRequestModal
          roomId={room.id}
          roomNumber={room.roomNumber}
          houseName={house.name}
          maxOccupants={room.maxOccupants}
          onClose={() => setIsRentalModalOpen(false)}
          onSuccess={(msg) => {
            setSuccessMessage(msg);
            setTimeout(() => setSuccessMessage(''), 5000);
          }}
        />
      )}
    </main>
    </>
  );
}
