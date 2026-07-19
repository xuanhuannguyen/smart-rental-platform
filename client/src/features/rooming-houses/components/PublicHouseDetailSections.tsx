import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { toAssetUrl, toPublicPropertyImageUrl } from '../../../shared/api/assets';
import { buildPublicMediaViewUrl } from '../../../shared/api/media';
import type {
  RoomInHouseDetail,
  RoomingHouseDetail,
  RoomingHouseServicePrice,
} from '../types';

function formatCurrency(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    currency: 'VND',
    maximumFractionDigits: 0,
    style: 'currency',
  }).format(value);
}

function getLowestActiveRent(priceTiers: RoomInHouseDetail['priceTiers'] = []) {
  const rents = priceTiers
    .filter((tier) => tier.isActive)
    .map((tier) => tier.monthlyRent)
    .sort((a, b) => a - b);
  return rents[0] ?? null;
}

function getServicePriceUnit(service: RoomingHouseServicePrice) {
  if (service.pricingUnit === 'PerMonth') return 'tháng';
  if (service.pricingUnit === 'PerPersonPerMonth') return 'người/tháng';
  if (service.pricingUnit === 'MeterReading') return service.meterUnitName || 'đơn vị';
  return service.pricingUnit;
}

export function AmenityIcon({ name }: { name: string }) {
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

export function SectionTitle({ children, icon }: { children: ReactNode; icon: ReactNode }) {
  return (
    <div className="section-title-with-icon">
      <div className="section-title-icon-wrapper circle-blue">{icon}</div>
      <h2>{children}</h2>
    </div>
  );
}

export function PublicHouseRulesSection({ houseRule }: Pick<RoomingHouseDetail, 'houseRule'>) {
  const ruleUrl = houseRule?.mediaAssetId
    ? buildPublicMediaViewUrl(houseRule.mediaAssetId)
    : houseRule?.pdfUrl
      ? toAssetUrl(houseRule.pdfUrl)
      : null;

  return (
    <section className="public-house-detail__section rules-section">
      <SectionTitle
        icon={(
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
          </svg>
        )}
      >
        Luật khu trọ
      </SectionTitle>

      <div className="rules-card-body">
        <div className="rules-card-info">
          {ruleUrl ? (
            <a className="public-house-detail__rule-link" href={ruleUrl} target="_blank" rel="noreferrer">
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
  );
}

export function PublicRentalPolicySection({ rentalPolicy }: Pick<RoomingHouseDetail, 'rentalPolicy'>) {
  if (!rentalPolicy) return null;

  const items = [
    { label: 'Thời gian thuê tối thiểu', value: `${rentalPolicy.minRentalMonths} tháng`, icon: 'calendar' },
    {
      label: 'Thời gian thuê tối đa',
      value: rentalPolicy.maxRentalMonths >= 120 ? 'Dài hạn (trên 10 năm)' : `${rentalPolicy.maxRentalMonths} tháng`,
      icon: 'calendar',
    },
    { label: 'Tiền cọc', value: `${rentalPolicy.depositMonths} tháng tiền thuê`, icon: 'money' },
    { label: 'Ngày thanh toán', value: `Ngày ${rentalPolicy.defaultPaymentDay} hàng tháng`, icon: 'payment-day' },
    { label: 'Báo trước khi gia hạn', value: `Trước ${rentalPolicy.renewalNoticeDays} ngày`, icon: 'bell' },
    { label: 'Gia hạn ngắn hạn', value: rentalPolicy.allowShortTermRenewal ? 'Được phép' : 'Không được phép', icon: 'renew' },
  ];

  return (
    <section className="public-house-detail__section policy-section">
      <SectionTitle
        icon={(
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
        )}
      >
        Chính sách thuê
      </SectionTitle>

      <div className="rental-policy-grid">
        {items.map((item) => (
          <div className="rental-policy-item" key={item.label}>
            <div className="policy-item-icon-wrapper"><PolicyIcon kind={item.icon} /></div>
            <div className="policy-item-text">
              <span className="rental-policy-label">{item.label}</span>
              <span className="rental-policy-value">{item.value}</span>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function PolicyIcon({ kind }: { kind: string }) {
  if (kind === 'money') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <line x1="12" y1="1" x2="12" y2="23" />
        <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
      </svg>
    );
  }
  if (kind === 'bell') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
      </svg>
    );
  }
  if (kind === 'renew') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={kind === 'payment-day' ? '2.5' : '2'} strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" />
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
      {kind === 'payment-day' && (
        <>
          <circle cx="12" cy="16" r="1" />
          <polyline points="12 12 12 16" />
        </>
      )}
    </svg>
  );
}

export function PublicServicePricesSection({ servicePrices }: Pick<RoomingHouseDetail, 'servicePrices'>) {
  if (!servicePrices?.length) return null;

  return (
    <section className="public-house-detail__section service-price-section">
      <SectionTitle
        icon={(
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
          </svg>
        )}
      >
        Bảng giá dịch vụ
      </SectionTitle>

      <div className="service-price-grid">
        {servicePrices.map((service) => (
          <div key={service.id} className="service-price-item">
            <div className="service-price-info">
              <span className="service-name">{service.serviceTypeName}</span>
              {service.note && <span className="service-note">{service.note}</span>}
            </div>
            <div className="service-price-value">
              <strong className="price-amount">{formatCurrency(service.unitPrice)}</strong>
              <span className="price-unit">/{getServicePriceUnit(service)}</span>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

export function PublicAvailableRoomsSection({
  houseId,
  rooms,
}: {
  houseId: string;
  rooms: RoomInHouseDetail[];
}) {
  return (
    <section className="public-house-detail__section rooms-section">
      <div className="public-house-detail__section-heading">
        <SectionTitle
          icon={(
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <path d="M9 3v18M15 3v18M3 9h18M3 15h18" />
            </svg>
          )}
        >
          Phòng còn trống
        </SectionTitle>
        <span className="available-rooms-count">{rooms.length} phòng</span>
      </div>

      {rooms.length > 0 ? (
        <div className="public-house-detail__rooms">
          {rooms.map((room) => (
            <PublicRoomCard houseId={houseId} room={room} key={room.id} />
          ))}
        </div>
      ) : (
        <p className="public-house-detail__muted">Hiện chưa có phòng trống.</p>
      )}
    </section>
  );
}

function PublicRoomCard({ houseId, room }: { houseId: string; room: RoomInHouseDetail }) {
  const roomImage = room.images.find((image) => image.isCover) ?? room.images[0];
  const lowestRent = getLowestActiveRent(room.priceTiers);

  return (
    <Link to={ROUTE_PATHS.ME.ROOM_DETAIL(houseId, room.id)} className="public-room-card">
      <div className="public-room-card__img-wrapper">
        {roomImage ? (
          <img alt={`Phòng ${room.roomNumber}`} src={toPublicPropertyImageUrl(roomImage)} />
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

        {room.amenities.length > 0 && (
          <div className="public-room-card__amenities">
            {room.amenities.slice(0, 4).map((amenity) => (
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
}

export function QuickLandlordMessageDialog({
  disabled,
  houseName,
  message,
  sending,
  onCancel,
  onChange,
  onSend,
}: {
  disabled: boolean;
  houseName: string;
  message: string;
  sending: boolean;
  onCancel: () => void;
  onChange: (message: string) => void;
  onSend: () => void;
}) {
  if (disabled) return null;

  return (
    <div className="public-house-detail__quick-message-overlay" role="presentation" onMouseDown={onCancel}>
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
            <h2 id="quick-landlord-message-title">{houseName}</h2>
          </div>
          <button
            type="button"
            className="public-house-detail__quick-message-close"
            onClick={onCancel}
            disabled={sending}
            aria-label="Đóng"
          >
            ×
          </button>
        </header>
        <label htmlFor="quick-landlord-message">Tin nhắn nhanh</label>
        <textarea
          id="quick-landlord-message"
          rows={4}
          value={message}
          onChange={(event) => onChange(event.target.value)}
          disabled={sending}
          autoFocus
        />
        <div className="public-house-detail__quick-message-actions">
          <button
            type="button"
            className="public-house-detail__quick-message-cancel"
            onClick={onCancel}
            disabled={sending}
          >
            Hủy
          </button>
          <button
            type="button"
            className="public-house-detail__quick-message-send"
            onClick={onSend}
            disabled={sending || !message.trim()}
          >
            {sending ? 'Đang gửi...' : 'Gửi'}
          </button>
        </div>
      </section>
    </div>
  );
}
