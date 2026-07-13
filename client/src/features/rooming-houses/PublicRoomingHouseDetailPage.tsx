import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { getPublicRoomingHouseDetail } from './api';
import TenantMapPreview from './components/TenantMapPreview';
import HouseImageGallery from './components/HouseImageGallery';
import RentalAiChatbot from './components/RentalAiChatbot';
import FavoriteButton from './components/FavoriteButton';
import type { RoomingHouseDetail } from './types';
import { toAssetUrl } from '../../shared/api/assets';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { HomeHeader } from '../../shared/components/layout/HomeHeader';
import { ROUTE_PATHS } from '../../app/router/routePaths';
import { saveRoomingHouseView } from './rentalBehaviorStorage';
import { useAuth } from '../../app/providers/AuthProvider';
import { Toast } from '../../shared/components/ui/Toast';
import { contactLandlord } from '../chat/api';
import { HouseReviewsList } from './components/HouseReviewsList';
import './PublicRoomingHouseDetailPage.css';

function formatCurrency(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    currency: 'VND',
    maximumFractionDigits: 0,
    style: 'currency',
  }).format(value);
}

function getLowestActiveRent(priceTiers: RoomingHouseDetail['rooms'][number]['priceTiers'] = []) {
  const rents = priceTiers
    .filter((tier) => tier.isActive)
    .map((tier) => tier.monthlyRent)
    .sort((a, b) => a - b);
  return rents[0] ?? null;
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
  if (normalized.includes('camera') || normalized.includes('an ninh')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
        <circle cx="12" cy="13" r="4" />
      </svg>
    );
  }
  if (normalized.includes('xe') || normalized.includes('đỗ xe') || normalized.includes('gửi xe')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10" />
        <path d="M9 17V7h4a3 3 0 0 1 0 6H9" />
      </svg>
    );
  }
  if (normalized.includes('điều hòa') || normalized.includes('lạnh') || normalized.includes('ac')) {
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
  if (normalized.includes('không chung chủ') || normalized.includes('tự do')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="m21 2-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0 1.5 1.5m-1.5-1.5 1.5-1.5" />
      </svg>
    );
  }
  return (
    <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </svg>
  );
}

export default function PublicRoomingHouseDetailPage() {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [quickMessageOpen, setQuickMessageOpen] = useState(false);
  const [quickMessage, setQuickMessage] = useState('');
  const [quickMessageSending, setQuickMessageSending] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const fromSearch = getSearchReturnUrl(location.state, new URLSearchParams(location.search));

  const defaultQuickMessage = house
    ? `Xin chào, tôi muốn hỏi về thông tin khu trọ ${house.name}.`
    : 'Xin chào, tôi muốn hỏi về thông tin khu trọ này.';

  function handleOpenQuickMessage() {
    if (!house) return;
    if (!currentUser) {
      navigate('/login', { state: { from: location.pathname } });
      return;
    }
    if (currentUser.userId === house.landlordUserId) {
      setToast({ message: 'Bạn đang xem khu trọ của chính mình.', type: 'info' });
      return;
    }

    setQuickMessage(quickMessage.trim() || defaultQuickMessage);
    setQuickMessageOpen(true);
  }

  function handleCancelQuickMessage() {
    setQuickMessageOpen(false);
    setQuickMessage(defaultQuickMessage);
  }

  async function handleSendQuickMessage() {
    if (!house) return;
    const content = quickMessage.trim();
    if (!content) {
      setToast({ message: 'Vui lòng nhập nội dung tin nhắn.', type: 'info' });
      return;
    }

    setQuickMessageSending(true);
    try {
      const conversation = await contactLandlord(house.id, content);
      window.dispatchEvent(new CustomEvent('open-chat-bubble', {
        detail: { conversationId: conversation.id }
      }));
      window.dispatchEvent(new CustomEvent('refresh-chat-list'));
      setQuickMessageOpen(false);
      setQuickMessage(defaultQuickMessage);
      setToast({ message: 'Đã gửi tin nhắn cho chủ trọ.', type: 'success' });
    } catch (chatError) {
      setToast({ message: getApiErrorMessage(chatError, 'Không thể gửi tin nhắn cho chủ trọ.'), type: 'error' });
    } finally {
      setQuickMessageSending(false);
    }
  }

  useEffect(() => {
    async function loadDetail() {
      if (!id) return;
      setLoading(true);
      setError('');
      try {
        const data = await getPublicRoomingHouseDetail(id);
        saveRoomingHouseView(data.id);
        setHouse(data);
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'Không thể tải chi tiết khu trọ.'));
      } finally {
        setLoading(false);
      }
    }

    void loadDetail();
  }, [id]);

  if (loading) {
    return (
      <>
        <HomeHeader />
        <main className="public-house-detail public-house-detail--state">Đang tải khu trọ...</main>
      </>
    );
  }

  if (error || !house) {
    return (
      <>
        <HomeHeader />
        <main className="public-house-detail public-house-detail--state">
          <p>{error || 'Không tìm thấy khu trọ.'}</p>
          <Link to={fromSearch}>Quay về danh sách</Link>
        </main>
      </>
    );
  }

  const houseImages = house.images ?? [];
  const houseAmenities = house.amenities ?? [];
  const availableRooms = (house.rooms ?? []).filter((room) => room.status === 'Available');

  return (
    <>
      <HomeHeader />
      <main className="public-house-detail">
        <Link className="public-house-detail__back" to={fromSearch}>
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="15 18 9 12 15 6" />
          </svg>
          <span>Quay về danh sách</span>
        </Link>

        <section className="public-house-detail__hero">
          <HouseImageGallery images={houseImages} houseName={house.name} />
          <div className="hero-details-card" style={{ position: 'relative' }}>
            <div style={{ position: 'absolute', top: '24px', right: '24px', zIndex: 10 }}>
              <FavoriteButton roomingHouseId={house.id} />
            </div>
            <div className="house-status-badge">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5">
                <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
              <span>Khu trọ đang còn phòng</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '16px' }}>
              <h1 style={{ flex: 1, margin: 0, paddingRight: '48px' }}>{house.name}</h1>
            </div>
            <TenantMapPreview
              address={house.addressDisplay}
              googleMapUrl={house.googleMapUrl}
              latitude={house.latitude}
              longitude={house.longitude}
              title={house.name}
            />
            {house.description && <p className="public-house-detail__description">{house.description}</p>}

            <div className="public-house-detail__contact-actions">
              <button
                className="public-house-detail__message-button"
                type="button"
                onClick={handleOpenQuickMessage}
                disabled={currentUser?.userId === house.landlordUserId}
              >
                <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                </svg>
                <span>Nhắn tin chủ trọ</span>
              </button>
            </div>

            <div className="house-amenities-mini-section">
              <h3>Tiện ích</h3>
              {houseAmenities.length > 0 ? (
                <div className="public-house-detail__amenities">
                  {houseAmenities.map((amenity) => (
                    <span key={amenity.id} className="house-amenity-card">
                      {getAmenityIcon(amenity.name)}
                      <span>{amenity.name}</span>
                    </span>
                  ))}
                </div>
              ) : (
                <p className="public-house-detail__muted">Chủ trọ chưa cập nhật tiện ích.</p>
              )}
            </div>
          </div>
        </section>

        <section className="public-house-detail__section rules-section">
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
            <h2>Luật khu trọ</h2>
          </div>
          
          <div className="rules-card-body">
            <div className="rules-card-info">
              {house.houseRule?.pdfObjectKey ? (
                <a
                  className="public-house-detail__rule-link"
                  href={toAssetUrl(house.houseRule.pdfObjectKey)}
                  target="_blank"
                  rel="noreferrer"
                >
                  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.5">
                    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
                    <polyline points="15 3 21 3 21 9" />
                    <line x1="10" y1="14" x2="21" y2="3" />
                  </svg>
                  <span>Xem luật khu trọ</span>
                </a>
              ) : (
                <p className="public-house-detail__muted">Chủ trọ chưa cập nhật luật khu trọ.</p>
              )}
            </div>
            
            <div className="rules-card-illustration">
              <svg width="120" height="120" viewBox="0 0 120 120" fill="none" xmlns="http://www.w3.org/2000/svg">
                <rect x="35" y="25" width="60" height="80" rx="8" fill="#f1f5f9" />
                <rect x="30" y="20" width="60" height="80" rx="8" fill="#ffffff" stroke="#e2e8f0" strokeWidth="2" />
                <rect x="50" y="15" width="20" height="10" rx="2" fill="#cbd5e1" />
                <rect x="52" y="12" width="16" height="8" rx="4" fill="#94a3b8" />
                <rect x="42" y="38" width="6" height="6" rx="1.5" fill="#246bfe" />
                <line x1="54" y1="41" x2="80" y2="41" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" />
                <rect x="42" y="52" width="6" height="6" rx="1.5" fill="#246bfe" />
                <line x1="54" y1="55" x2="74" y2="55" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" />
                <rect x="42" y="66" width="6" height="6" rx="1.5" fill="#e2e8f0" />
                <line x1="54" y1="69" x2="80" y2="69" stroke="#e2e8f0" strokeWidth="2.5" strokeLinecap="round" />
                <g transform="rotate(15 90 60)">
                  <rect x="86" y="30" width="6" height="40" rx="3" fill="#38bdf8" />
                  <path d="M86 70l3 6 3-6z" fill="#0f172a" />
                  <rect x="85" y="30" width="8" height="10" rx="2" fill="#0284c7" />
                </g>
              </svg>
            </div>
          </div>
        </section>

        {house.rentalPolicy && (
          <section className="public-house-detail__section policy-section">
            <div className="section-title-with-icon">
              <div className="section-title-icon-wrapper circle-blue">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                </svg>
              </div>
              <h2>Chính sách thuê</h2>
            </div>
            
            <div className="rental-policy-grid">
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Thời gian thuê tối thiểu</span>
                  <span className="rental-policy-value">{house.rentalPolicy.minRentalMonths} tháng</span>
                </div>
              </div>
              
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Thời gian thuê tối đa</span>
                  <span className="rental-policy-value">
                    {house.rentalPolicy.maxRentalMonths >= 120
                      ? 'Dài hạn (trên 10 năm)'
                      : `${house.rentalPolicy.maxRentalMonths} tháng`}
                  </span>
                </div>
              </div>
              
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="12" y1="1" x2="12" y2="23" />
                    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Tiền cọc</span>
                  <span className="rental-policy-value">{house.rentalPolicy.depositMonths} tháng tiền thuê</span>
                </div>
              </div>
              
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                    <circle cx="12" cy="16" r="1" />
                    <polyline points="12 12 12 16" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Ngày thanh toán</span>
                  <span className="rental-policy-value">Ngày {house.rentalPolicy.defaultPaymentDay} hàng tháng</span>
                </div>
              </div>
              
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                    <path d="M13.73 21a2 2 0 0 1-3.46 0" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Báo trước khi gia hạn</span>
                  <span className="rental-policy-value">Trước {house.rentalPolicy.renewalNoticeDays} ngày</span>
                </div>
              </div>
              
              <div className="rental-policy-item">
                <div className="policy-item-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
                  </svg>
                </div>
                <div className="policy-item-text">
                  <span className="rental-policy-label">Gia hạn ngắn hạn</span>
                  <span className="rental-policy-value">
                    {house.rentalPolicy.allowShortTermRenewal ? 'Được phép' : 'Không được phép'}
                  </span>
                </div>
              </div>
            </div>
          </section>
        )}

        {house.servicePrices && house.servicePrices.length > 0 && (
          <section className="public-house-detail__section service-price-section">
            <div className="section-title-with-icon">
              <div className="section-title-icon-wrapper circle-blue">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                </svg>
              </div>
              <h2>Bảng giá dịch vụ</h2>
            </div>
            
            <div className="service-price-grid">
              {house.servicePrices.map((service) => (
                <div key={service.id} className="service-price-item">
                  <div className="service-price-info">
                    <span className="service-name">{service.serviceTypeName}</span>
                    {service.note && <span className="service-note">{service.note}</span>}
                  </div>
                  <div className="service-price-value">
                    <strong className="price-amount">{formatCurrency(service.unitPrice)}</strong>
                    <span className="price-unit">/{(() => {
                      if (service.pricingUnit === 'PerMonth') return 'tháng';
                      if (service.pricingUnit === 'PerPersonPerMonth') return 'người/tháng';
                      if (service.pricingUnit === 'MeterReading') {
                        return service.meterUnitName || 'đơn vị';
                      }
                      return service.pricingUnit;
                    })()}</span>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        <section className="public-house-detail__section rooms-section">
          <div className="public-house-detail__section-heading">
            <div className="section-title-with-icon">
              <div className="section-title-icon-wrapper circle-blue">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="18" height="18" rx="2" />
                  <path d="M9 3v18M15 3v18M3 9h18M3 15h18" />
                </svg>
              </div>
              <h2>Phòng còn trống</h2>
            </div>
            <span className="available-rooms-count">{availableRooms.length} phòng</span>
          </div>
          
          {availableRooms.length > 0 ? (
            <div className="public-house-detail__rooms">
              {availableRooms.map((room) => {
                const roomImages = room.images ?? [];
                const roomAmenities = room.amenities ?? [];
                const roomImage = roomImages.find((image) => image.isCover) ?? roomImages[0];
                const lowestRent = getLowestActiveRent(room.priceTiers);
                return (
                  <Link to={ROUTE_PATHS.ME.ROOM_DETAIL(house.id, room.id)} className="public-room-card" key={room.id}>
                    <div className="public-room-card__img-wrapper">
                      {roomImage ? (
                        <img alt={`Phòng ${room.roomNumber}`} src={toAssetUrl(roomImage.imageUrl || roomImage.objectKey)} />
                      ) : (
                        <div className="public-room-card__placeholder">Chưa có ảnh</div>
                      )}
                    </div>
                    
                    <div className="public-room-card__body">
                      <div className="room-card-main-info">
                        <h3>Phòng {room.roomNumber}</h3>
                        <p className="room-card-spec">
                          Tầng {room.floor} · {room.areaM2 ? `${room.areaM2} m²` : 'Chưa cập nhật diện tích'} · Tối đa {room.maxOccupants} người
                        </p>
                      </div>
                      
                      <strong className="room-card-price">
                        {lowestRent != null ? `Từ ${formatCurrency(lowestRent)}` : 'Liên hệ chủ trọ'}
                      </strong>
                      
                      {roomAmenities.length > 0 && (
                        <div className="public-room-card__amenities">
                          {roomAmenities.slice(0, 4).map((amenity) => (
                            <span key={amenity.id} className="room-amenity-badge">
                              {amenity.name}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                    
                    <div className="public-room-card__chevron">
                      <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2.5">
                        <polyline points="9 18 15 12 9 6" />
                      </svg>
                    </div>
                  </Link>
                );
              })}
            </div>
          ) : (
            <p className="public-house-detail__muted">Hiện chưa có phòng trống.</p>
          )}
        </section>

        <section className="public-house-detail__section reviews-section">
          <div className="section-title-with-icon">
            <div className="section-title-icon-wrapper circle-blue">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"/>
              </svg>
            </div>
            <h2>Đánh giá từ người thuê</h2>
          </div>
          
          <HouseReviewsList 
            roomingHouseId={house.id} 
            landlordUserId={house.landlordUserId} 
            roomingHouseName={house.name}
            roomingHouseAvatarUrl={(() => {
              const img = houseImages.find(i => i.isCover) || houseImages[0];
              return img ? (img.imageUrl || img.objectKey) : undefined;
            })()}
          />
        </section>
      </main>
      <RentalAiChatbot context="detail" roomingHouseId={house.id} title={house.name} />
      {quickMessageOpen && (
        <div className="public-house-detail__quick-message-overlay" role="presentation" onMouseDown={handleCancelQuickMessage}>
          <section
            className="public-house-detail__quick-message"
            role="dialog"
            aria-modal="true"
            aria-labelledby="quick-landlord-message-title"
            onMouseDown={(event) => event.stopPropagation()}
          >
            <header className="public-house-detail__quick-message-header">
              <div>
                <p>Nhắn tin chủ trọ</p>
                <h2 id="quick-landlord-message-title">{house.name}</h2>
              </div>
              <button
                type="button"
                className="public-house-detail__quick-message-close"
                onClick={handleCancelQuickMessage}
                disabled={quickMessageSending}
                aria-label="Đóng"
              >
                ×
              </button>
            </header>
            <label htmlFor="quick-landlord-message">Tin nhắn nhanh</label>
            <textarea
              id="quick-landlord-message"
              rows={4}
              value={quickMessage}
              onChange={(event) => setQuickMessage(event.target.value)}
              disabled={quickMessageSending}
              autoFocus
            />
            <div className="public-house-detail__quick-message-actions">
              <button
                type="button"
                className="public-house-detail__quick-message-cancel"
                onClick={handleCancelQuickMessage}
                disabled={quickMessageSending}
              >
                Hủy
              </button>
              <button
                type="button"
                className="public-house-detail__quick-message-send"
                onClick={() => void handleSendQuickMessage()}
                disabled={quickMessageSending || !quickMessage.trim()}
              >
                {quickMessageSending ? 'Đang gửi...' : 'Gửi'}
              </button>
            </div>
          </section>
        </div>
      )}
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </>
  );
}

function getSearchReturnUrl(state: unknown, searchParams: URLSearchParams) {
  const stateFromSearch =
    state && typeof state === 'object' && 'fromSearch' in state
      ? (state as { fromSearch?: unknown }).fromSearch
      : undefined;

  if (typeof stateFromSearch === 'string' && stateFromSearch.startsWith('/search')) {
    return stateFromSearch;
  }

  const queryFromSearch = searchParams.get('from');
  if (queryFromSearch?.startsWith('/search')) {
    return queryFromSearch;
  }

  return '/search';
}
