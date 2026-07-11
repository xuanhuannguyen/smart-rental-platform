import { useState, useEffect, useMemo, useRef, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { toAvatarImageUrl, toPublicListingImageUrl } from '../../../shared/api/assets';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import {
  getGuestRoomingHouseRecommendations,
  getMyRoomingHouseOnboarding,
  getPublicRoomingHouseListing,
  searchLocationAddress,
} from '../../rooming-houses/api';
import type { GuestRoomingHouseRecommendationRequest, RoomingHouseListingItem, RoomingHouseSearchItem } from '../../rooming-houses/types';
import SearchSuggestionBox from '../../rooming-houses/components/SearchSuggestionBox';
import { LocationFilterPanel } from '../../rooming-houses/components/LocationFilterPanel';
import RentalAiChatbot from '../../rooming-houses/components/RentalAiChatbot';
import { saveRoomingHouseView, saveSearchBehavior, toGuestRecommendationRequest } from '../../rooming-houses/rentalBehaviorStorage';
import { saveRecentSearch } from '../../rooming-houses/searchRecentStorage';
import { NotificationBell } from '../../notifications/components/NotificationBell';
import './MePage.css';

type HeaderLocationMode = 'area' | 'nearby' | null;
type HomeListingItem = {
  id: string;
  name: string;
  addressDisplay: string;
  coverImageUrl?: string | null;
  availableRooms?: number | null;
  reason?: string | null;
  minMonthlyRent?: number | null;
  maxMonthlyRent?: number | null;
  minAreaM2?: number | null;
  maxAreaM2?: number | null;
  amenities?: string[];
  createdAt?: string;
};

type HomeListingCategory = {
  id: string;
  eyebrow: string;
  title: string;
  description?: string;
  icon?: React.ReactNode;
  items: HomeListingItem[];
  compact?: boolean;
};

/** sessionStorage cache key for home page listing. */
const LISTING_CACHE_KEY = 'srp_home_listing_cache';
const RECOMMENDATION_CACHE_KEY = 'srp_home_ai_recommendation_cache';
/** Cache TTL: 5 minutes. */
const LISTING_CACHE_TTL = 5 * 60 * 1000;

/** A rooming house is considered "new" if created within this many days. */
const NEW_LISTING_DAYS = 14;
/** A rooming house has "many available rooms" if available count >= this. */
const MANY_ROOMS_THRESHOLD = 5;
/** A rooming house is "affordable" if its min monthly rent <= this (VND). */
const AFFORDABLE_MAX_RENT = 5_000_000;

type ListingCacheEntry = {
  items: HomeListingItem[];
  timestamp: number;
};

export function MePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser, logout } = useAuth();
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  useEffect(() => {
    const state = location.state as { message?: string } | null;
    if (state?.message) {
      setToastMessage(state.message);
      window.history.replaceState({}, document.title);
    }
  }, [location]);

  const [error, setError] = useState('');
  const [homeListings, setHomeListings] = useState<HomeListingItem[]>([]);
  const [recommendedListings, setRecommendedListings] = useState<HomeListingItem[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [loadingHouses, setLoadingHouses] = useState(false);
  const [isCheckingLandlord, setIsCheckingLandlord] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [activeLocationMode, setActiveLocationMode] = useState<HeaderLocationMode>(null);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [localProvinceCode, setLocalProvinceCode] = useState('');
  const [localWardCode, setLocalWardCode] = useState('');
  const [nearbyAddress, setNearbyAddress] = useState('');
  const [nearbyRadiusKm, setNearbyRadiusKm] = useState(3);
  const [centerLat, setCenterLat] = useState<number | null>(null);
  const [centerLng, setCenterLng] = useState<number | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Chuyển hướng bắt buộc nếu user đã đăng nhập nhưng chưa xác thực email
  useEffect(() => {
    if (currentUser && !currentUser.emailConfirmed) {
      navigate(ROUTE_PATHS.AUTH.VERIFY_EMAIL, { replace: true });
    }
  }, [currentUser, navigate]);

  useEffect(() => {
    async function loadHomeListings() {
      setLoadingHouses(true);

      // 1. Try sessionStorage cache first — instant return
      try {
        const cached = sessionStorage.getItem(LISTING_CACHE_KEY);
        if (cached) {
          const entry: ListingCacheEntry = JSON.parse(cached);
          if (Date.now() - entry.timestamp < LISTING_CACHE_TTL) {
            setHomeListings(entry.items);
            setLoadingHouses(false);
            return;
          }
        }
      } catch {
        // Corrupted cache — ignore and re-fetch
      }

      // 2. Fetch from lightweight Select-projection endpoint
      try {
        const items = await getPublicRoomingHouseListing();
        const mapped = items.map(mapListingItemToHomeItem);
        setHomeListings(mapped);

        // 3. Write to cache for back-navigation
        try {
          const entry: ListingCacheEntry = { items: mapped, timestamp: Date.now() };
          sessionStorage.setItem(LISTING_CACHE_KEY, JSON.stringify(entry));
        } catch {
          // sessionStorage full — silently skip cache
        }
      } catch {
        setError('Không thể tải danh sách khu trọ công khai.');
      } finally {
        setLoadingHouses(false);
      }
    }

    void loadHomeListings();
  }, []);

  useEffect(() => {
    async function loadAiRecommendations() {
      try {
        const cached = sessionStorage.getItem(RECOMMENDATION_CACHE_KEY);
        if (cached) {
          const entry: ListingCacheEntry = JSON.parse(cached);
          if (Date.now() - entry.timestamp < LISTING_CACHE_TTL) {
            setRecommendedListings(entry.items);
            return;
          }
        }
      } catch {
        // Corrupted cache — ignore and re-fetch
      }

      try {
        const request = toGuestRecommendationRequest(8) ?? createDefaultRecommendationRequest();
        const recommendation = await getGuestRoomingHouseRecommendations(request);
        const mapped = recommendation.items.map((item) =>
          mapSearchItemToHomeItem(item, recommendation.reasons[item.id])
        );
        setRecommendedListings(mapped);

        try {
          const entry: ListingCacheEntry = { items: mapped, timestamp: Date.now() };
          sessionStorage.setItem(RECOMMENDATION_CACHE_KEY, JSON.stringify(entry));
        } catch {
          // sessionStorage full — silently skip cache
        }
      } catch {
        setRecommendedListings([]);
      }
    }

    void loadAiRecommendations();
  }, []);

  useEffect(() => {
    async function loadProvinces() {
      try {
        setProvinces(await getProvinces());
      } catch {
        setError('Không thể tải danh sách tỉnh/thành phố.');
      }
    }

    void loadProvinces();
  }, []);

  useEffect(() => {
    async function loadWards() {
      if (!localProvinceCode) {
        setWards([]);
        return;
      }

      try {
        setWards(await getWardsByProvince(localProvinceCode));
      } catch {
        setWards([]);
      }
    }

    void loadWards();
  }, [localProvinceCode]);

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
  const selectedProvinceName = useMemo(
    () => provinces.find((province) => province.code === localProvinceCode)?.name ?? '',
    [localProvinceCode, provinces]
  );
  const selectedWardName = useMemo(
    () => wards.find((ward) => ward.code === localWardCode)?.name ?? '',
    [localWardCode, wards]
  );
  const quickLocationProvinces = useMemo(() => {
    const preferredNames = ['Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng'];
    const preferred = preferredNames
      .map((name) => provinces.find((province) => province.name.includes(name)))
      .filter((province): province is Province => Boolean(province));
    return preferred.length > 0 ? preferred : provinces.slice(0, 3);
  }, [provinces]);
  const locationButtonLabel =
    nearbyAddress ||
    (selectedWardName && selectedProvinceName ? `${selectedWardName}, ${selectedProvinceName}` : selectedProvinceName) ||
    'Khu vực / Xung quanh';
  const listingCategories = useMemo(
    () => [
      ...buildListingCategories(recommendedListings, true),
      ...buildListingCategories(homeListings, false),
    ],
    [homeListings, recommendedListings]
  );

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

  function handleSearchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    const searchUrl = buildSearchUrl({ q: query });
    if (query) {
      saveRecentSearch(query, searchUrl);
    }
    saveSearchBehavior({ q: query || undefined });
    navigate(searchUrl);
  }

  function handleSuggestionSearch(query: string) {
    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      navigate('/search');
      return;
    }

    const searchUrl = `/search?q=${encodeURIComponent(trimmedQuery)}`;
    saveRecentSearch(trimmedQuery, searchUrl);
    saveSearchBehavior({ q: trimmedQuery });
    navigate(searchUrl);
  }

  function buildSearchUrl(params: Record<string, string | number | undefined>) {
    const searchParams = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== '') {
        searchParams.set(key, String(value));
      }
    });
    return `/search${searchParams.toString() ? `?${searchParams.toString()}` : ''}`;
  }

  function handleLocationApply(filters: {
    provinceCode: string;
    wardCode: string;
    centerLat: number | null;
    centerLng: number | null;
    radiusKm: number;
    address: string;
  }) {
    setLocalProvinceCode(filters.provinceCode);
    setLocalWardCode(filters.wardCode);
    setNearbyAddress(filters.address);
    setNearbyRadiusKm(filters.radiusKm);
    setCenterLat(filters.centerLat);
    setCenterLng(filters.centerLng);

    const searchUrl = buildSearchUrl({
      q: searchQuery.trim(),
      provinceCode: filters.provinceCode || undefined,
      wardCode: filters.wardCode || undefined,
      centerLat: filters.centerLat ?? undefined,
      centerLng: filters.centerLng ?? undefined,
      radiusKm: filters.radiusKm || undefined,
      nearbyLabel: filters.address || undefined,
      page: 1,
    });

    saveSearchBehavior({
      q: searchQuery.trim() || filters.address || undefined,
      provinceCode: filters.provinceCode || undefined,
      wardCode: filters.wardCode || undefined,
      centerLat: filters.centerLat ?? undefined,
      centerLng: filters.centerLng ?? undefined,
      radiusKm: filters.radiusKm || undefined,
    });

    navigate(searchUrl);
    setActiveLocationMode(null);
  }

  function handleLocationClear() {
    setLocalProvinceCode('');
    setLocalWardCode('');
    setNearbyAddress('');
    setNearbyRadiusKm(3);
    setCenterLat(null);
    setCenterLng(null);
    setActiveLocationMode(null);
  }

  // Tên viết tắt để hiển thị trên Avatar
  const avatarInitials = currentUser?.displayName
    ? currentUser.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
    : 'U';

  return (
    <div className="home-container">
      {toastMessage && <Toast message={toastMessage} onClose={() => setToastMessage(null)} />}

      {/* Header */}
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
        <form className="home-header-search-form" onSubmit={handleSearchSubmit}>
          <svg className="search-form-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <SearchSuggestionBox
            placeholder="Tìm khu vực, trường, giá thuê..."
            value={searchQuery}
            onChange={setSearchQuery}
            onSearch={handleSuggestionSearch}
          />
          <button type="submit" className="search-submit-btn">Tìm</button>
        </form>
        <div className="home-header-location">
          <button
            type="button"
            className={`home-location-button ${activeLocationMode ? 'is-active' : ''}`}
            onClick={() => setActiveLocationMode((current) => (current ? null : 'area'))}
            aria-expanded={activeLocationMode != null}
          >
            <svg className="location-pin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
              <circle cx="12" cy="10" r="3" />
            </svg>
            <span className="location-btn-label">{locationButtonLabel}</span>
            <svg className="location-chevron-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </button>

          {activeLocationMode && (
            <LocationFilterPanel
              initialProvinceCode={localProvinceCode}
              initialWardCode={localWardCode}
              initialRadiusKm={nearbyRadiusKm}
              initialAddress={nearbyAddress}
              initialLatitude={centerLat}
              initialLongitude={centerLng}
              initialTab={activeLocationMode === 'nearby' ? 'nearby' : 'area'}
              onClose={() => setActiveLocationMode(null)}
              onApply={handleLocationApply}
              onClear={handleLocationClear}
            />
          )}
        </div>
        <div className="header-auth" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {currentUser ? (
            <>
              <div className="header-role-action">
                {isAdmin ? (
                  <Button type="button" className="admin-channel-btn" onClick={() => navigate(ROUTE_PATHS.ADMIN.ROOT)}>
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                    </svg>
                    Duyệt hồ sơ
                  </Button>
                ) : isLandlord ? (
                  <Button type="button" className="landlord-channel-btn" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
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
              <NotificationBell />
              <div className="avatar-wrapper" ref={dropdownRef}>
                <button className="avatar-btn" onClick={() => setShowDropdown(!showDropdown)}>
                  {currentUser.avatarUrl && currentUser.avatarUrl.trim() !== '' ? (
                    <img src={toAvatarImageUrl(currentUser)} alt="Avatar" className="avatar-image" />
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
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.PROFILE); }}>
                      Chỉnh sửa thông tin
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.SECURITY); }}>
                      Bảo mật
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.WALLET); }}>
                      Nạp ví
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.INVOICES); }}>
                      Hóa đơn
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS); }}>
                      Yêu cầu thuê
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY); }}>
                      Lịch sử thuê
                    </button>
                    <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.VIEWING_APPOINTMENTS); }}>
                      Lịch xem phòng
                    </button>
                    <div className="dropdown-divider" />
                    {isAdmin && (
                      <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ADMIN.ROOT); }}>
                        Duyệt hồ sơ
                      </button>
                    )}
                    {isLandlord && (
                      <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.LANDLORD.DASHBOARD); }}>
                        Kênh chủ trọ
                      </button>
                    )}
                    <div className="dropdown-divider" />
                    <button className="dropdown-item dropdown-item--danger" onClick={() => { setShowDropdown(false); logout(); }}>
                      Đăng xuất
                    </button>
                  </div>
                )}
              </div>
            </>
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

      <section className="home-listings-section">
        {error && <Alert type="error">{error}</Alert>}

        {loadingHouses ? (
          <p className="feedback-state">Đang tải danh sách khu trọ...</p>
        ) : homeListings.length === 0 ? (
          <p className="feedback-state">Chưa có khu trọ công khai đang còn phòng.</p>
        ) : (
          <div className="home-listing-categories">
            {listingCategories.map((category) => (
              <section className="home-listing-category" key={category.id}>
                <div className="home-listings-header">
                  <div className="category-title-container">
                    <p className="eyebrow">
                      {category.icon}
                      <span>{category.eyebrow}</span>
                    </p>
                    <h2>{category.title}</h2>
                    {category.description && (
                      <p className="category-description">{category.description}</p>
                    )}
                  </div>
                  <button type="button" className="category-view-all-btn" onClick={() => navigate('/search')}>
                    <svg className="view-all-filter-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="4" y1="21" x2="4" y2="14" />
                      <line x1="4" y1="10" x2="4" y2="3" />
                      <line x1="12" y1="21" x2="12" y2="12" />
                      <line x1="12" y1="8" x2="12" y2="3" />
                      <line x1="20" y1="21" x2="20" y2="16" />
                      <line x1="20" y1="12" x2="20" y2="3" />
                      <line x1="2" y1="14" x2="6" y2="14" />
                      <line x1="10" y1="8" x2="14" y2="8" />
                      <line x1="18" y1="16" x2="22" y2="16" />
                    </svg>
                    <span>Xem tất cả</span>
                    <svg className="view-all-chevron-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="9 18 15 12 9 6" />
                    </svg>
                  </button>
                </div>

                <div className={`home-listings-grid ${category.compact ? 'home-listings-grid--compact' : ''}`}>
                  {category.items.map((house) => (
                    <HomeListingCard
                      key={`${category.id}-${house.id}`}
                      house={house}
                      onOpen={() => {
                        saveRoomingHouseView(house.id);
                        navigate(`/rooming-houses/${house.id}`);
                      }}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </section>

      {/* Footer */}
      <footer className="home-footer">
        <p>&copy; 2026 Smart Rental Platform. All rights reserved.</p>
      </footer>
      <RentalAiChatbot context="home" />
    </div>
  );
}

function HomeListingCard({ house, onOpen }: { house: HomeListingItem; onOpen: () => void }) {
  const roomsText = `${house.availableRooms ?? 0} phòng trống`;
  const areaText = house.minAreaM2 != null
    ? `Từ ${house.minAreaM2} m²`
    : 'Chưa có diện tích';
  const priceText = house.minMonthlyRent != null
    ? `Từ ${new Intl.NumberFormat('vi-VN').format(house.minMonthlyRent)} đ/tháng`
    : 'Liên hệ chủ';

  return (
    <button className="home-listing-card" type="button" onClick={onOpen}>
      <div className="card-image-wrapper">
        {house.coverImageUrl ? (
          <img alt={house.name} src={toPublicListingImageUrl(house.coverImageUrl)} className="card-image" />
        ) : (
          <div className="home-listing-card__placeholder">Chưa có ảnh</div>
        )}
      </div>
      <div className="card-content-wrapper">
        <h3 className="card-title">{house.name}</h3>
        <p className="card-address">
          <svg className="address-pin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
            <circle cx="12" cy="10" r="3" />
          </svg>
          <span>{house.addressDisplay}</span>
        </p>

        <div className="card-badges-grid">
          <span className="card-badge badge-blue">
            <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M13 4h3a2 2 0 0 1 2 2v14M2 20h20M5 4h8v16H5z" />
            </svg>
            <span>{roomsText}</span>
          </span>
          
          <span className="card-badge badge-orange">
            <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 2H2v10l9.29 9.29a1 1 0 0 0 1.41 0l7.29-7.29a1 1 0 0 0 0-1.41L12 2z" />
              <circle cx="5" cy="5" r="1.5" fill="currentColor" />
            </svg>
            <span>{priceText}</span>
          </span>

          <span className="card-badge badge-green">
            <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <path d="M9 3v18M15 3v18M3 9h18M3 15h18" />
            </svg>
            <span>{areaText}</span>
          </span>
        </div>

        {house.amenities && house.amenities.length > 0 && (
          <>
            <hr className="card-divider" />
            <div className="card-amenities-list">
              {house.amenities.slice(0, 3).map((amenity) => (
                <span key={amenity} className="card-amenity-item">
                  {getAmenityIcon(amenity)}
                  <span>{amenity}</span>
                </span>
              ))}
            </div>
          </>
        )}


      </div>
    </button>
  );
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

function mapListingItemToHomeItem(item: RoomingHouseListingItem): HomeListingItem {
  return {
    id: item.id,
    name: item.name,
    addressDisplay: item.addressDisplay,
    coverImageUrl: item.coverImageUrl,
    availableRooms: item.availableRooms,
    minMonthlyRent: item.minMonthlyRent,
    maxMonthlyRent: item.maxMonthlyRent,
    minAreaM2: item.minAreaM2,
    maxAreaM2: item.maxAreaM2,
    amenities: item.amenities.map((a) => a.name),
    createdAt: item.createdAt,
  };
}

function mapSearchItemToHomeItem(item: RoomingHouseSearchItem, reason?: string): HomeListingItem {
  return {
    id: item.id,
    name: item.name,
    addressDisplay: item.addressDisplay,
    coverImageUrl: item.coverImageUrl,
    availableRooms: item.availableRooms,
    minMonthlyRent: item.minMonthlyRent,
    maxMonthlyRent: item.maxMonthlyRent,
    minAreaM2: item.minAreaM2,
    maxAreaM2: item.maxAreaM2,
    amenities: item.amenities.map((a) => a.name),
    createdAt: item.createdAt,
    reason,
  };
}

function createDefaultRecommendationRequest(): GuestRoomingHouseRecommendationRequest {
  return {
    recentQueries: [],
    recentRoomingHouseIds: [],
    clickedRoomingHouseIds: [],
    preferredAmenityIds: [],
    preferredRoomAmenityIds: [],
    pageSize: 8,
  };
}

function buildListingCategories(items: HomeListingItem[], personalized: boolean): HomeListingCategory[] {
  // Lọc bỏ những khu trọ chưa có ảnh bìa
  const itemsWithImages = items.filter(item => item.coverImageUrl && item.coverImageUrl.trim() !== '');
  const categories: HomeListingCategory[] = [];
  const now = Date.now();

  // ── Category 1: Tất cả khu trọ ──────────────────────────────────
  const primary = itemsWithImages.slice(0, personalized ? 8 : 6);
  if (primary.length > 0) {
    categories.push({
      id: personalized ? 'personalized' : 'available',
      eyebrow: personalized ? 'GỢI Ý AI' : 'KHU TRỌ CÔNG KHAI',
      title: personalized ? 'Gợi ý phù hợp với bạn' : 'Khu trọ đang còn phòng',
      description: personalized
        ? 'Dựa trên thói quen tìm kiếm và sở thích của bạn, đề xuất bởi AI.'
        : 'Danh sách các khu trọ còn phòng, thông tin minh bạch, cập nhật liên tục.',
      icon: (
        <svg className="category-eyebrow-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
          <line x1="9" y1="22" x2="9" y2="16" />
          <line x1="15" y1="22" x2="15" y2="16" />
          <line x1="9" y1="16" x2="15" y2="16" />
          <path d="M8 6h2v2H8V6zm0 4h2v2H8v-2zm0 4h2v2H8v-2zm6-8h2v2h-2V6zm0 4h2v2h-2v-2zm0 4h2v2h-2v-2z" />
        </svg>
      ),
      items: primary,
    });
  }

  // ── Category 2: Khu trọ mới (created trong vòng 14 ngày) ────────
  const newest = itemsWithImages
    .filter((item) => {
      if (!item.createdAt) return false;
      const ageMs = now - new Date(item.createdAt).getTime();
      return ageMs >= 0 && ageMs <= NEW_LISTING_DAYS * 24 * 60 * 60 * 1000;
    })
    .sort((a, b) => new Date(b.createdAt ?? 0).getTime() - new Date(a.createdAt ?? 0).getTime())
    .slice(0, 4);
  if (newest.length > 0) {
    categories.push({
      id: 'newest',
      eyebrow: 'MỚI CẬP NHẬT',
      title: 'Khu trọ mới trên hệ thống',
      description: 'Khám phá các khu trọ mới đăng ký, phòng mới tinh, nhiều ưu đãi hấp dẫn.',
      icon: (
        <svg className="category-eyebrow-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275Z" />
        </svg>
      ),
      items: newest,
      compact: true,
    });
  }

  // ── Category 3: Còn nhiều phòng trống (availableRooms ≥ 5) ──────
  const manyRooms = itemsWithImages
    .filter((item) => (item.availableRooms ?? 0) >= MANY_ROOMS_THRESHOLD)
    .sort((a, b) => (b.availableRooms ?? 0) - (a.availableRooms ?? 0))
    .slice(0, 4);
  if (manyRooms.length > 0) {
    categories.push({
      id: 'many-rooms',
      eyebrow: 'DỄ ĐẶT LỊCH XEM',
      title: 'Còn nhiều phòng trống',
      description: 'Các khu trọ có số lượng phòng trống lớn, sẵn sàng đón tiếp bạn đến tham quan.',
      icon: (
        <svg className="category-eyebrow-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      ),
      items: manyRooms,
      compact: true,
    });
  }

  // ── Category 4: Giá thuê dễ tiếp cận (minMonthlyRent ≤ 5 triệu) ─
  const affordable = itemsWithImages
    .filter((item) => item.minMonthlyRent != null && item.minMonthlyRent <= AFFORDABLE_MAX_RENT)
    .sort((a, b) => (a.minMonthlyRent ?? Number.MAX_SAFE_INTEGER) - (b.minMonthlyRent ?? Number.MAX_SAFE_INTEGER))
    .slice(0, 4);
  if (affordable.length > 0) {
    categories.push({
      id: 'affordable',
      eyebrow: 'NGÂN SÁCH TỐT',
      title: 'Khu trọ giá tốt',
      description: 'Phòng trọ giá tốt với chi phí hợp lý, phù hợp cho học sinh, sinh viên và người đi làm.',
      icon: (
        <svg className="category-eyebrow-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 2H2v10l9.29 9.29a1 1 0 0 0 1.41 0l7.29-7.29a1 1 0 0 0 0-1.41L12 2z" />
          <circle cx="5" cy="5" r="1.5" fill="currentColor" />
        </svg>
      ),
      items: affordable,
      compact: true,
    });
  }

  return categories;
}

function formatListingSummary(house: HomeListingItem) {
  const rooms = `${house.availableRooms ?? 0} phòng còn trống`;
  const price = formatPriceRange(house.minMonthlyRent, house.maxMonthlyRent);
  return price ? `${rooms} · ${price}` : rooms;
}

function formatPriceRange(min?: number | null, max?: number | null) {
  if (min == null && max == null) return '';
  if (min != null && max != null && min !== max) {
    return `${formatCurrencyCompact(min)} - ${formatCurrencyCompact(max)}/tháng`;
  }
  return `Từ ${formatCurrencyCompact(min ?? max ?? 0)}/tháng`;
}

function formatCurrencyCompact(value: number) {
  if (value >= 1_000_000) {
    return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value / 1_000_000)} triệu`;
  }
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(value);
}
