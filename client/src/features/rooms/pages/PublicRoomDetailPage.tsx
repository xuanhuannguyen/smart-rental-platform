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

function getAmenityIcon(name: string) {
  const normalized = name.toLowerCase();
  if (normalized.includes('wifi') || normalized.includes('internet')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 20h.01" />
        <path d="M8.5 16.5a5 5 0 0 1 7 0" />
        <path d="M5 13a10 10 0 0 1 14 0" />
        <path d="M1.5 9.5a15 15 0 0 1 21 0" />
      </svg>
    );
  }
  if (normalized.includes('điều hòa') || normalized.includes('lạnh') || normalized.includes('ac') || normalized.includes('máy lạnh')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M2 12h20M12 2v20M20 7l-3.5 3.5M4 17l3.5-3.5M17 17l-3.5-3.5M7 7l3.5 3.5" />
      </svg>
    );
  }
  if (normalized.includes('gác') || normalized.includes('lửng')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6 3v18M18 3v18M6 7h12M6 12h12M6 17h12" />
      </svg>
    );
  }
  if (
    normalized.includes('vệ sinh') ||
    normalized.includes('khép kín') ||
    normalized.includes('wc') ||
    normalized.includes('toilet') ||
    normalized.includes('phòng tắm')
  ) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M4 19h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z" />
        <circle cx="12" cy="8" r="3" />
        <path d="M6 19v2M18 19v2" />
      </svg>
    );
  }
  return (
    <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  );
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
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="15 18 9 12 15 6" />
          </svg>
          <span>Quay về khu trọ: {house.name}</span>
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
              <h1>Phòng {room.roomNumber}</h1>
              <p className="public-room-detail__house-name">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                  <polyline points="9 22 9 12 15 12 15 22" />
                </svg>
                <span>Khu trọ: {house.name}</span>
              </p>
              <p className="public-room-detail__address">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span>{house.addressDisplay}</span>
              </p>
              
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
                  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="22" y1="2" x2="11" y2="13" />
                    <polygon points="22 2 15 22 11 13 2 9 22 2" />
                  </svg>
                  <span>Gửi yêu cầu thuê</span>
                </button>
                <button className="public-room-detail__action public-room-detail__action--secondary" type="button" onClick={handleBookingClick}>
                  <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                  <span>Đặt lịch xem phòng</span>
                </button>
              </div>
            </header>

            <div className="public-room-detail__specs">
              <div className="public-room-detail__spec-item">
                <div className="spec-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7" />
                  </svg>
                </div>
                <div className="spec-text">
                  <span className="spec-label">Diện tích</span>
                  <strong className="spec-value">{room.areaM2 ? `${room.areaM2} m²` : 'Chưa cập nhật'}</strong>
                </div>
              </div>
              <div className="public-room-detail__spec-divider" />
              <div className="public-room-detail__spec-item">
                <div className="spec-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                    <circle cx="9" cy="7" r="4" />
                    <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                    <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                  </svg>
                </div>
                <div className="spec-text">
                  <span className="spec-label">Sức chứa tối đa</span>
                  <strong className="spec-value">{room.maxOccupants} người</strong>
                </div>
              </div>
            </div>

            {/* Pricing Tiers Section */}
            <div className="public-room-detail__pricing public-room-detail__section">
              <div className="section-title-with-icon">
                <div className="section-title-icon-wrapper circle-blue">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
                    <line x1="7" y1="7" x2="7.01" y2="7" />
                  </svg>
                </div>
                <h2>Bảng giá thuê theo người ở</h2>
              </div>
              
              {activePriceTiers.length > 0 ? (
                <div className="public-room-detail__price-list">
                  {activePriceTiers.map((tier) => (
                    <div className="public-room-detail__price-row" key={tier.id}>
                      <span className="price-occupants">
                        <div className="price-occupant-icon-wrapper">
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                            <circle cx="12" cy="7" r="4" />
                          </svg>
                        </div>
                        <span>{tier.occupantCount} {tier.occupantCount === room.maxOccupants ? 'người (Tối đa)' : 'người'}</span>
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
            <div className="public-room-detail__amenities-container public-room-detail__section">
              <div className="section-title-with-icon">
                <div className="section-title-icon-wrapper circle-blue">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="3" width="7" height="7" />
                    <rect x="14" y="3" width="7" height="7" />
                    <rect x="14" y="14" width="7" height="7" />
                    <rect x="3" y="14" width="7" height="7" />
                  </svg>
                </div>
                <h2>Tiện ích phòng</h2>
              </div>
              
              {roomAmenities.length > 0 ? (
                <div className="public-room-detail__amenities">
                  {roomAmenities.map((amenity) => (
                    <span key={amenity.id} className="public-room-detail__amenity-badge">
                      {getAmenityIcon(amenity.name)}
                      <span>{amenity.name}</span>
                    </span>
                  ))}
                </div>
              ) : (
                <p className="public-room-detail__muted">Phòng này chưa cập nhật tiện ích.</p>
              )}
            </div>

            {/* Description Section */}
            {room.description && (
              <div className="public-room-detail__description-container public-room-detail__section">
                <div className="section-title-with-icon">
                  <div className="section-title-icon-wrapper circle-blue">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                      <polyline points="14 2 14 8 20 8" />
                      <line x1="16" y1="13" x2="8" y2="13" />
                      <line x1="16" y1="17" x2="8" y2="17" />
                      <polyline points="10 9 9 9 8 9" />
                    </svg>
                  </div>
                  <h2>Mô tả phòng</h2>
                </div>
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
