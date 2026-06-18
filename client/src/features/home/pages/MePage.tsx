import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { toAssetUrl } from '../../../shared/api/assets';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import {
  getPublicRoomingHouses,
  searchLocationAddress,
} from '../../rooming-houses/api';
import { saveRecentSearch } from '../../rooming-houses/searchRecentStorage';
import type { RoomingHouseDetail } from '../../rooming-houses/types';
import { HomeHeader } from '../../../shared/components/layout/HomeHeader';
import './MePage.css';

type HeaderLocationMode = 'area' | 'nearby' | null;

export function MePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser, logout } = useAuth();

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [publicHouses, setPublicHouses] = useState<RoomingHouseDetail[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [loadingHouses, setLoadingHouses] = useState(false);
  const [activeLocationMode, setActiveLocationMode] = useState<HeaderLocationMode>(null);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [localProvinceCode, setLocalProvinceCode] = useState('');
  const [localWardCode, setLocalWardCode] = useState('');
  const [nearbyAddress, setNearbyAddress] = useState('');
  const [nearbyRadiusKm, setNearbyRadiusKm] = useState(3);
  const [nearbySearching, setNearbySearching] = useState(false);
  const [nearbyError, setNearbyError] = useState('');
  const locationPanelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        activeLocationMode != null &&
        locationPanelRef.current &&
        !locationPanelRef.current.contains(event.target as Node)
      ) {
        setActiveLocationMode(null);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [activeLocationMode]);
  useEffect(() => {
    const state = location.state as { message?: string } | null;
    if (state?.message) {
      setToastMessage(state.message);
      window.history.replaceState({}, document.title);
    }
  }, [location]);

  useEffect(() => {
    if (currentUser && !currentUser.emailConfirmed) {
      navigate(ROUTE_PATHS.AUTH.VERIFY_EMAIL, { replace: true });
    }
  }, [currentUser, navigate]);

  useEffect(() => {
    async function loadPublicHouses() {
      setLoadingHouses(true);
      setError('');
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
    'Khu vực / xung quanh';



  function buildSearchUrl(params: Record<string, string | number | undefined>) {
    const searchParams = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== '') {
        searchParams.set(key, String(value));
      }
    });

    const query = searchParams.toString();
    return `/search${query ? `?${query}` : ''}`;
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

  function handleClearHeaderLocation() {
    setLocalProvinceCode('');
    setLocalWardCode('');
    setNearbyAddress('');
    setNearbyRadiusKm(3);
    setNearbyError('');
    setActiveLocationMode(null);
  }

  function handleApplyAreaSearch() {
    navigate(buildSearchUrl({
      q: searchQuery.trim(),
      provinceCode: localProvinceCode || undefined,
      wardCode: localWardCode || undefined,
      page: 1,
    }));
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
      navigate(buildSearchUrl({
        q: searchQuery.trim(),
        centerLat: locationResult.latitude,
        centerLng: locationResult.longitude,
        radiusKm: nearbyRadiusKm,
        nearbyLabel: locationResult.displayAddress || address,
        page: 1,
      }));
      setNearbyAddress(locationResult.displayAddress || address);
      setActiveLocationMode(null);
    } catch {
      setNearbyError('Không tìm thấy vị trí phù hợp.');
    } finally {
      setNearbySearching(false);
    }
  }

  return (
    <div className="home-container">
      {toastMessage && <Toast message={toastMessage} onClose={() => setToastMessage(null)} />}

      <HomeHeader
        centerContent={
          <>
            <form className="home-header-search-form" onSubmit={handleSearchSubmit}>
              <input
                aria-label="Tìm kiếm khu trọ"
                placeholder="Tìm khu vực, trường, giá thuê..."
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
              />
              <Button type="submit">Tìm</Button>
            </form>

            <div className="home-header-location" ref={locationPanelRef}>
          <button
            type="button"
            className={`home-location-button ${activeLocationMode ? 'is-active' : ''}`}
            onClick={() => setActiveLocationMode((current) => (current ? null : 'area'))}
            aria-expanded={activeLocationMode != null}
          >
            <span>{locationButtonLabel}</span>
            <span>{activeLocationMode ? '▲' : '▼'}</span>
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
                      placeholder="Nhập địa chỉ, tên trường, tên đường..."
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
                      <span>◎</span>
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
          </>
        }
      />

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

      <footer className="home-footer">
        <p>&copy; 2026 Smart Rental Platform. All rights reserved.</p>
      </footer>
    </div>
  );
}
