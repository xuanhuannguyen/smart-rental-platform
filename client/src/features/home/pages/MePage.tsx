import { useState, useEffect, useMemo, useRef, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { toAssetUrl } from '../../../shared/api/assets';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import { getPublicRoomingHouses, searchLocationAddress } from '../../rooming-houses/api';
import SearchSuggestionBox from '../../rooming-houses/components/SearchSuggestionBox';
import { saveRecentSearch } from '../../rooming-houses/searchRecentStorage';
import type { RoomingHouseDetail } from '../../rooming-houses/types';
import './MePage.css';

type HeaderLocationMode = 'area' | 'nearby' | null;

export function MePage() {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [publicHouses, setPublicHouses] = useState<RoomingHouseDetail[]>([]);
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
  const [nearbySearching, setNearbySearching] = useState(false);
  const [nearbyError, setNearbyError] = useState('');
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Chuyển hướng bắt buộc nếu user đã đăng nhập nhưng chưa xác thực email
  useEffect(() => {
    if (currentUser && !currentUser.emailConfirmed) {
      navigate(ROUTE_PATHS.AUTH.VERIFY_EMAIL, { replace: true });
    }
  }, [currentUser, navigate]);

  useEffect(() => {
    async function loadPublicHouses() {
      setLoadingHouses(true);
      try {
        setPublicHouses(await getPublicRoomingHouses());
      } catch {
        setError('Không thể tải danh sách khu trọ công khai.');
      } finally {
        setLoadingHouses(false);
      }
    }

    void loadPublicHouses();
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

  function handleLandlordRegister() {
    navigate(ROUTE_PATHS.LANDLORD.REGISTER);
  }

  function handleSearchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    const searchUrl = buildSearchUrl({ q: query });
    if (query) {
      saveRecentSearch(query, searchUrl);
    }
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

  function handleClearHeaderLocation() {
    setLocalProvinceCode('');
    setLocalWardCode('');
    setNearbyAddress('');
    setNearbyRadiusKm(3);
    setNearbyError('');
    setActiveLocationMode(null);
  }

  function handleApplyAreaSearch() {
    const searchUrl = buildSearchUrl({
      q: searchQuery.trim(),
      provinceCode: localProvinceCode || undefined,
      wardCode: localWardCode || undefined,
      page: 1,
    });
    navigate(searchUrl);
    setActiveLocationMode(null);
  }

  async function handleApplyNearbySearch() {
    const address = nearbyAddress.trim();
    if (!address) {
      setNearbyError('Vui lòng nhập vị trí muốn tìm.');
      return;
    }

    setNearbySearching(true);
    setNearbyError('');
    try {
      const locationResult = await searchLocationAddress(address);
      const searchUrl = buildSearchUrl({
        q: searchQuery.trim(),
        centerLat: locationResult.latitude,
        centerLng: locationResult.longitude,
        radiusKm: nearbyRadiusKm,
        nearbyLabel: locationResult.displayAddress || address,
        page: 1,
      });
      setNearbyAddress(locationResult.displayAddress || address);
      navigate(searchUrl);
      setActiveLocationMode(null);
    } catch {
      setNearbyError('Không tìm thấy vị trí phù hợp.');
    } finally {
      setNearbySearching(false);
    }
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
        <form className="home-header-search-form" onSubmit={handleSearchSubmit}>
          <SearchSuggestionBox
            placeholder="Tìm khu vực, trường, giá thuê..."
            value={searchQuery}
            onChange={setSearchQuery}
            onSearch={handleSuggestionSearch}
          />
          <Button type="submit">Tìm</Button>
        </form>
        <div className="home-header-location">
          <button
            type="button"
            className={`home-location-button ${activeLocationMode ? 'is-active' : ''}`}
            onClick={() => setActiveLocationMode((current) => (current ? null : 'area'))}
            aria-expanded={activeLocationMode != null}
          >
            <span>{locationButtonLabel}</span>
            <span>⌄</span>
          </button>

          {activeLocationMode && (
            <div className="home-location-panel" role="dialog" aria-label="Tùy chọn vị trí tìm kiếm">
              <button
                type="button"
                className="home-location-close"
                onClick={() => setActiveLocationMode(null)}
                aria-label="Đóng bộ lọc vị trí"
              >
                ×
              </button>

              {activeLocationMode === 'nearby' ? (
                <>
                  <div className="home-location-subheader">
                    <button type="button" onClick={() => setActiveLocationMode('area')}>‹</button>
                    <strong>Nhập vị trí quanh bạn</strong>
                  </div>
                  <div className="home-location-field">
                    <label htmlFor="home-nearby-address">Vị trí tìm kiếm</label>
                    <input
                      id="home-nearby-address"
                      value={nearbyAddress}
                      onChange={(event) => {
                        setNearbyAddress(event.target.value);
                        setNearbyError('');
                      }}
                      placeholder="Nhập địa điểm, tên trường, tên đường..."
                    />
                  </div>
                  <div className="home-location-field">
                    <label htmlFor="home-nearby-radius">Bán kính: {nearbyRadiusKm} km</label>
                    <input
                      id="home-nearby-radius"
                      type="range"
                      min="0.5"
                      max="30"
                      step="0.5"
                      value={nearbyRadiusKm}
                      onChange={(event) => setNearbyRadiusKm(Number(event.target.value))}
                    />
                  </div>
                  {nearbyError && <p className="home-location-error">{nearbyError}</p>}
                </>
              ) : (
                <>
                  <section className="home-location-section">
                    <button type="button" className="home-location-heading" onClick={() => setActiveLocationMode('nearby')}>
                      <span>⌖</span>
                      <strong>Tìm kiếm quanh bạn</strong>
                    </button>
                    <button type="button" className="home-nearby-trigger" onClick={() => setActiveLocationMode('nearby')}>
                      <span>{nearbyAddress || 'Nhập vị trí và khoảng cách tìm kiếm'}</span>
                      <span>›</span>
                    </button>
                  </section>

                  <section className="home-location-section">
                    <div className="home-location-heading">
                      <span>▦</span>
                      <strong>Tìm theo khu vực</strong>
                    </div>
                    <div className="home-location-chips">
                      {quickLocationProvinces.map((province) => (
                        <button
                          key={province.code}
                          type="button"
                          className={localProvinceCode === province.code ? 'is-selected' : ''}
                          onClick={() => {
                            setLocalProvinceCode(province.code);
                            setLocalWardCode('');
                          }}
                        >
                          {province.name}
                        </button>
                      ))}
                    </div>
                    <label className="home-location-select-card" htmlFor="home-location-province">
                      <span>
                        <strong>Chọn tỉnh thành</strong>
                        <small>{selectedProvinceName || 'Tất cả tỉnh/thành phố'}</small>
                      </span>
                      <span>›</span>
                      <select
                        id="home-location-province"
                        value={localProvinceCode}
                        onChange={(event) => {
                          setLocalProvinceCode(event.target.value);
                          setLocalWardCode('');
                        }}
                      >
                        <option value="">Tất cả tỉnh/thành phố</option>
                        {provinces.map((province) => (
                          <option key={province.code} value={province.code}>
                            {province.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="home-location-select-card" htmlFor="home-location-ward">
                      <span>
                        <strong>Chọn khu vực</strong>
                        <small>{selectedWardName || 'Tất cả khu vực trong tỉnh/thành'}</small>
                      </span>
                      <span>›</span>
                      <select
                        id="home-location-ward"
                        value={localWardCode}
                        disabled={!localProvinceCode}
                        onChange={(event) => setLocalWardCode(event.target.value)}
                      >
                        <option value="">Tất cả khu vực trong tỉnh/thành</option>
                        {wards.map((ward) => (
                          <option key={ward.code} value={ward.code}>
                            {ward.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  </section>
                </>
              )}

              <div className="home-location-actions">
                <Button type="button" variant="secondary" onClick={handleClearHeaderLocation}>Xóa lọc</Button>
                <Button
                  type="button"
                  disabled={nearbySearching}
                  onClick={activeLocationMode === 'nearby' ? handleApplyNearbySearch : handleApplyAreaSearch}
                >
                  {nearbySearching ? 'Đang tìm...' : 'Áp dụng'}
                </Button>
              </div>
            </div>
          )}
        </div>
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
                  onClick={() => void handleLandlordRegister()}
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

      <section className="home-listings-section">
        {error && <Alert type="error">{error}</Alert>}

        <div className="home-listings-header">
          <div>
            <p className="eyebrow">Khu trọ công khai</p>
            <h2>Khu trọ đang còn phòng</h2>
          </div>
        </div>

        {loadingHouses ? (
          <p className="feedback-state">Đang tải danh sách khu trọ...</p>
        ) : publicHouses.length === 0 ? (
          <p className="feedback-state">Chưa có khu trọ công khai đang còn phòng.</p>
        ) : (
          <div className="home-listings-grid">
            {publicHouses.map((house) => {
              const cover = house.images.find((image) => image.isCover) ?? house.images[0];
              return (
                <button
                  className="home-listing-card"
                  key={house.id}
                  type="button"
                  onClick={() => navigate(`/rooming-houses/${house.id}`)}
                >
                  {cover ? (
                    <img alt={house.name} src={toAssetUrl(cover.imageUrl || cover.objectKey)} />
                  ) : (
                    <div className="home-listing-card__placeholder">Chưa có ảnh</div>
                  )}
                  <div>
                    <strong>{house.name}</strong>
                    <span>{house.addressDisplay}</span>
                    <small>{house.availableRooms ?? 0} phòng còn trống</small>
                  </div>
                </button>
              );
            })}
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
