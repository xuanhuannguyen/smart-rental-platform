import { useEffect, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { getPublicAvailableRooms, getPublicRoomingHouseDetail } from './api';
import TenantMapPreview from './components/TenantMapPreview';
import HouseImageGallery from './components/HouseImageGallery';
import RentalAiChatbot from './components/RentalAiChatbot';
import FavoriteButton from './components/FavoriteButton';
import type { RoomingHouseDetail } from './types';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { HomeHeader } from '../../shared/components/layout/HomeHeader';
import { saveRoomingHouseView } from './rentalBehaviorStorage';
import { useAuth } from '../../app/providers/AuthProvider';
import { Toast } from '../../shared/components/ui/Toast';
import { contactLandlord } from '../chat/api';
import { HouseReviewsList } from './components/HouseReviewsList';
import { toPublicPropertyImageUrl } from '../../shared/api/assets';
import {
  AmenityIcon,
  PublicAvailableRoomsSection,
  PublicHouseRulesSection,
  PublicRentalPolicySection,
  PublicServicePricesSection,
  QuickLandlordMessageDialog,
} from './components/PublicHouseDetailSections';
import './PublicRoomingHouseDetailPage.css';

const DETAIL_CACHE_PREFIX = 'srp_public_house_detail_';
const DETAIL_CACHE_TTL = 5 * 60 * 1000;

type DetailCacheEntry = {
  item: RoomingHouseDetail;
  timestamp: number;
};

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
  const listingReturnUrl = getListingReturnUrl(location.state, new URLSearchParams(location.search));
  const listingReturnState = getListingReturnState(location.state);

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

  function handleBackToListing() {
    navigate(listingReturnUrl, {
      replace: true,
      preventScrollReset: true,
      state: listingReturnState,
    });
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

      const cached = readDetailCache(id);
      if (cached) {
        setHouse(cached);
        setLoading(false);
        setError('');
        saveRoomingHouseView(cached.id);
        return;
      }

      setLoading(true);
      setError('');
      try {
        const [data, rooms] = await Promise.all([
          getPublicRoomingHouseDetail(id),
          getPublicAvailableRooms(id)
        ]);
        const detail = { ...data, rooms };
        saveRoomingHouseView(data.id);
        setHouse(detail);
        writeDetailCache(data.id, detail);
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
          <button className="public-house-detail__back" type="button" onClick={handleBackToListing}>
            Quay về danh sách
          </button>
        </main>
      </>
    );
  }

  const houseImages = house.images ?? [];
  const houseAmenities = house.amenities ?? [];
  const availableRooms = house.rooms ?? [];

  return (
    <>
      <HomeHeader />
      <main className="public-house-detail">
        <button className="public-house-detail__back" type="button" onClick={handleBackToListing}>
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="15 18 9 12 15 6" />
          </svg>
          <span>Quay về danh sách</span>
        </button>

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
                      <AmenityIcon name={amenity.name} />
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

        <PublicHouseRulesSection houseRule={house.houseRule} />
        <PublicRentalPolicySection rentalPolicy={house.rentalPolicy} />
        <PublicServicePricesSection servicePrices={house.servicePrices} />
        <PublicAvailableRoomsSection houseId={house.id} rooms={availableRooms} listingReturnUrl={listingReturnUrl} />
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
              return img ? toPublicPropertyImageUrl(img) : undefined;
            })()}
          />
        </section>
      </main>
      <RentalAiChatbot context="detail" roomingHouseId={house.id} title={house.name} />
      <QuickLandlordMessageDialog
        disabled={!quickMessageOpen}
        houseName={house.name}
        message={quickMessage}
        sending={quickMessageSending}
        onCancel={handleCancelQuickMessage}
        onChange={setQuickMessage}
        onSend={() => void handleSendQuickMessage()}
      />
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </>
  );
}

function getListingReturnUrl(state: unknown, searchParams: URLSearchParams) {
  const stateFromSearch =
    state && typeof state === 'object' && 'fromSearch' in state
      ? (state as { fromSearch?: unknown }).fromSearch
      : undefined;

  if (typeof stateFromSearch === 'string' && stateFromSearch.startsWith('/search')) {
    return stateFromSearch;
  }

  const stateFromListing =
    state && typeof state === 'object' && 'fromListing' in state
      ? (state as { fromListing?: unknown }).fromListing
      : undefined;

  if (typeof stateFromListing === 'string' && (stateFromListing.startsWith('/home') || stateFromListing.startsWith('/search'))) {
    return stateFromListing;
  }

  const queryFromSearch = searchParams.get('from');
  if (queryFromSearch?.startsWith('/search')) {
    return queryFromSearch;
  }

  return '/home';
}

function getListingReturnState(state: unknown) {
  const homeScroll =
    state && typeof state === 'object' && 'homeScroll' in state
      ? (state as { homeScroll?: unknown }).homeScroll
      : undefined;

  return homeScroll ? { restoreHomeScroll: homeScroll } : undefined;
}

function readDetailCache(id: string): RoomingHouseDetail | null {
  try {
    const cached = sessionStorage.getItem(`${DETAIL_CACHE_PREFIX}${id}`);
    if (!cached) return null;

    const entry: DetailCacheEntry = JSON.parse(cached);
    if (!entry?.timestamp || Date.now() - entry.timestamp >= DETAIL_CACHE_TTL) {
      sessionStorage.removeItem(`${DETAIL_CACHE_PREFIX}${id}`);
      return null;
    }

    return entry.item;
  } catch {
    sessionStorage.removeItem(`${DETAIL_CACHE_PREFIX}${id}`);
    return null;
  }
}

function writeDetailCache(id: string, item: RoomingHouseDetail) {
  try {
    const entry: DetailCacheEntry = { item, timestamp: Date.now() };
    sessionStorage.setItem(`${DETAIL_CACHE_PREFIX}${id}`, JSON.stringify(entry));
  } catch {
    // sessionStorage full — silently skip cache
  }
}
