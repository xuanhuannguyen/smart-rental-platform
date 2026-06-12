import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { getPublicRoomingHouseDetail } from './api';
import TenantMapPreview from './components/TenantMapPreview';
import HouseImageGallery from './components/HouseImageGallery';
import type { RoomingHouseDetail } from './types';
import { toAssetUrl } from '../../shared/api/assets';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { ROUTE_PATHS } from '../../app/router/routePaths';
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

export default function PublicRoomingHouseDetailPage() {
  const { id } = useParams();
  const location = useLocation();
  const [house, setHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const fromSearch = getSearchReturnUrl(location.state, new URLSearchParams(location.search));

  useEffect(() => {
    async function loadDetail() {
      if (!id) return;
      setLoading(true);
      setError('');
      try {
        setHouse(await getPublicRoomingHouseDetail(id));
      } catch (loadError) {
        setError(getApiErrorMessage(loadError, 'Không thể tải chi tiết khu trọ.'));
      } finally {
        setLoading(false);
      }
    }

    void loadDetail();
  }, [id]);

  if (loading) {
    return <main className="public-house-detail public-house-detail--state">Đang tải khu trọ...</main>;
  }

  if (error || !house) {
    return (
      <main className="public-house-detail public-house-detail--state">
        <p>{error || 'Không tìm thấy khu trọ.'}</p>
        <Link to={fromSearch}>Quay về danh sách</Link>
      </main>
    );
  }

  const houseImages = house.images ?? [];
  const houseAmenities = house.amenities ?? [];
  const availableRooms = (house.rooms ?? []).filter((room) => room.status === 'Available');
  const coverImage = houseImages.find((image) => image.isCover) ?? houseImages[0];

  return (
    <main className="public-house-detail">
      <Link className="public-house-detail__back" to={fromSearch}>
        Quay về danh sách
      </Link>

      <section className="public-house-detail__hero">
        <HouseImageGallery images={houseImages} houseName={house.name} />
        <div>
          <p className="eyebrow">Khu trọ đang còn phòng</p>
          <h1>{house.name}</h1>
          <TenantMapPreview
            address={house.addressDisplay}
            googleMapUrl={house.googleMapUrl}
            latitude={house.latitude}
            longitude={house.longitude}
            title={house.name}
          />
          {house.description && <p className="public-house-detail__description">{house.description}</p>}
        </div>
      </section>

      <section className="public-house-detail__section">
        <h2>Tiện ích</h2>
        {houseAmenities.length > 0 ? (
          <div className="public-house-detail__amenities">
            {houseAmenities.map((amenity) => (
              <span key={amenity.id}>{amenity.name}</span>
            ))}
          </div>
        ) : (
          <p className="public-house-detail__muted">Chủ trọ chưa cập nhật tiện ích.</p>
        )}
      </section>

      <section className="public-house-detail__section">
        <h2>Luật khu trọ</h2>
        {house.houseRule?.pdfObjectKey ? (
          <a
            className="public-house-detail__rule-link"
            href={toAssetUrl(house.houseRule.pdfObjectKey)}
            target="_blank"
            rel="noreferrer"
          >
            Xem luật khu trọ
          </a>
        ) : (
          <p className="public-house-detail__muted">Chủ trọ chưa cập nhật luật khu trọ.</p>
        )}
      </section>

      <section className="public-house-detail__section">
        <div className="public-house-detail__section-heading">
          <h2>Phòng còn trống</h2>
          <span>{availableRooms.length} phòng</span>
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
                  {roomImage ? (
                    <img alt={`Phòng ${room.roomNumber}`} src={toAssetUrl(roomImage.imageUrl || roomImage.objectKey)} />
                  ) : (
                    <div className="public-room-card__placeholder">Chưa có ảnh</div>
                  )}
                  <div className="public-room-card__body">
                    <div>
                      <h3>Phòng {room.roomNumber}</h3>
                      <p>
                        Tầng {room.floor} · {room.areaM2 ? `${room.areaM2} m²` : 'Chưa cập nhật diện tích'} · Tối đa{' '}
                        {room.maxOccupants} người
                      </p>
                    </div>
                    <strong>
                      {lowestRent != null ? `Từ ${formatCurrency(lowestRent)}/tháng` : 'Liên hệ chủ trọ'}
                    </strong>
                    {roomAmenities.length > 0 && (
                      <div className="public-room-card__amenities">
                        {roomAmenities.slice(0, 4).map((amenity) => (
                          <span key={amenity.id}>{amenity.name}</span>
                        ))}
                      </div>
                    )}
                  </div>
                </Link>
              );
            })}
          </div>
        ) : (
          <p className="public-house-detail__muted">Hiện chưa có phòng trống.</p>
        )}
      </section>
    </main>
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
