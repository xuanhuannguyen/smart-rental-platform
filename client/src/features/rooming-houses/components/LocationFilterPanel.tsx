import { useEffect, useState } from 'react';
import { getProvinces, getWardsByProvince } from '../../administrative/api';
import type { Province, Ward } from '../../administrative/types';
import { searchLocationAddress, suggestLocationAddresses } from '../api';
import type { LocationSuggestion } from '../types';
import { Button } from '../../../shared/components/ui/Button';
import './LocationFilterPanel.css';

const quickLocationProvinces = [
  { code: '79', name: 'TP. Hồ Chí Minh' },
  { code: '01', name: 'Hà Nội' },
  { code: '48', name: 'Đà Nẵng' },
];

interface LocationFilterPanelProps {
  onClose: () => void;
  onApply: (filters: {
    provinceCode: string;
    wardCode: string;
    centerLat: number | null;
    centerLng: number | null;
    radiusKm: number;
    address: string;
  }) => void;
  onClear: () => void;
  initialProvinceCode?: string;
  initialWardCode?: string;
  initialRadiusKm?: number;
  initialAddress?: string;
  initialLatitude?: number | null;
  initialLongitude?: number | null;
  initialTab?: 'area' | 'nearby';
}

export function LocationFilterPanel({
  onClose,
  onApply,
  onClear,
  initialProvinceCode = '',
  initialWardCode = '',
  initialRadiusKm = 5,
  initialAddress = '',
  initialLatitude = null,
  initialLongitude = null,
  initialTab = 'area',
}: LocationFilterPanelProps) {
  const [activeTab, setActiveTab] = useState<'area' | 'nearby'>(initialTab);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [localProvinceCode, setLocalProvinceCode] = useState(initialProvinceCode);
  const [localWardCode, setLocalWardCode] = useState(initialWardCode);
  const [localRadiusKm, setLocalRadiusKm] = useState(initialRadiusKm);
  const [nearbyAddress, setNearbyAddress] = useState(initialAddress);
  const [centerLat, setCenterLat] = useState<number | null>(initialLatitude);
  const [centerLng, setCenterLng] = useState<number | null>(initialLongitude);

  // Address search suggestions state
  const [nearbySearching, setNearbySearching] = useState(false);
  const [nearbyError, setNearbyError] = useState('');
  const [nearbySuggestions, setNearbySuggestions] = useState<LocationSuggestion[]>([]);
  const [nearbySuggesting, setNearbySuggesting] = useState(false);
  const [showNearbySuggestions, setShowNearbySuggestions] = useState(false);

  useEffect(() => {
    getProvinces()
      .then(setProvinces)
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!localProvinceCode) {
      setWards([]);
      return;
    }
    getWardsByProvince(localProvinceCode)
      .then(setWards)
      .catch(() => {});
  }, [localProvinceCode]);

  // Autocomplete debounced suggestions
  useEffect(() => {
    if (!nearbyAddress.trim()) {
      setNearbySuggestions([]);
      return;
    }
    const delayDebounce = setTimeout(() => {
      setNearbySuggesting(true);
      suggestLocationAddresses(nearbyAddress)
        .then((res) => {
          setNearbySuggestions(res);
          setShowNearbySuggestions(res.length > 0);
        })
        .catch(() => {
          setNearbySuggestions([]);
        })
        .finally(() => {
          setNearbySuggesting(false);
        });
    }, 400);

    return () => clearTimeout(delayDebounce);
  }, [nearbyAddress]);

  const selectedProvinceName = provinces.find((p) => p.code === localProvinceCode)?.name ?? '';
  const selectedWardName = wards.find((w) => w.code === localWardCode)?.name ?? '';

  async function handleFindNearbyAddress() {
    if (!nearbyAddress.trim()) {
      setNearbyError('Vui lòng nhập địa chỉ trước');
      return;
    }
    setNearbySearching(true);
    setNearbyError('');
    try {
      const locationResult = await searchLocationAddress(nearbyAddress);
      setCenterLat(locationResult.latitude);
      setCenterLng(locationResult.longitude);
      setNearbyAddress(locationResult.displayAddress || nearbyAddress);
    } catch {
      setNearbyError('Không tìm thấy tọa độ cho địa điểm này');
    } finally {
      setNearbySearching(false);
    }
  }

  function handleSelectNearbySuggestion(suggestion: LocationSuggestion) {
    setNearbyAddress(suggestion.displayAddress);
    setCenterLat(suggestion.latitude);
    setCenterLng(suggestion.longitude);
    setShowNearbySuggestions(false);
    setNearbyError('');
  }

  async function handleApplyClick() {
    if (activeTab === 'nearby') {
      if (!nearbyAddress.trim()) {
        setNearbyError('Vui lòng nhập vị trí hoặc lấy vị trí hiện tại');
        return;
      }

      let lat = centerLat;
      let lng = centerLng;
      let displayAddr = nearbyAddress;

      if (lat == null || lng == null) {
        setNearbySearching(true);
        try {
          const locationResult = await searchLocationAddress(nearbyAddress);
          lat = locationResult.latitude;
          lng = locationResult.longitude;
          displayAddr = locationResult.displayAddress || nearbyAddress;
          setCenterLat(lat);
          setCenterLng(lng);
          setNearbyAddress(displayAddr);
        } catch {
          setNearbyError('Không tìm thấy vị trí yêu cầu');
          setNearbySearching(false);
          return;
        } finally {
          setNearbySearching(false);
        }
      }

      onApply({
        provinceCode: '',
        wardCode: '',
        centerLat: lat,
        centerLng: lng,
        radiusKm: localRadiusKm,
        address: displayAddr,
      });
    } else {
      onApply({
        provinceCode: localProvinceCode,
        wardCode: localWardCode,
        centerLat: null,
        centerLng: null,
        radiusKm: localRadiusKm,
        address: '',
      });
    }
  }

  return (
    <div className="location-filter-panel" role="dialog" aria-label="Tùy chọn vị trí tìm kiếm">
      <button
        type="button"
        className="location-panel-close"
        onClick={onClose}
        aria-label="Đóng bộ lọc vị trí"
      >
        ×
      </button>

      {activeTab === 'nearby' ? (
        <>
          <div className="location-panel-subheader">
            <button type="button" className="location-back-button" onClick={() => setActiveTab('area')}>
              ‹
            </button>
            <strong>Nhập vị trí quanh bạn</strong>
          </div>
          <div className="nearby-search-details">
            <div className="location-panel-field">
              <label htmlFor="nearby-address-input">Vị trí tìm kiếm</label>
              <div className="nearby-address-row">
                <input
                  id="nearby-address-input"
                  value={nearbyAddress}
                  onChange={(e) => {
                    setNearbyAddress(e.target.value);
                    setNearbyError('');
                    setCenterLat(null);
                    setCenterLng(null);
                  }}
                  onFocus={() => {
                    if (nearbySuggestions.length > 0) {
                      setShowNearbySuggestions(true);
                    }
                  }}
                  onBlur={() => {
                    window.setTimeout(() => setShowNearbySuggestions(false), 120);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      void handleFindNearbyAddress();
                    }
                  }}
                  placeholder="Nhập địa điểm, tên đường, khu vực muốn tìm"
                />
                <Button type="button" variant="secondary" onClick={handleFindNearbyAddress} disabled={nearbySearching}>
                  {nearbySearching ? 'Đang tìm...' : 'Lấy vị trí'}
                </Button>
              </div>
              {(showNearbySuggestions || nearbySuggesting) && (
                <div className="nearby-suggestions" role="listbox" aria-label="Gợi ý vị trí">
                  {nearbySuggesting && nearbySuggestions.length === 0 ? (
                    <div className="nearby-suggestions__state">Đang tìm gợi ý...</div>
                  ) : (
                    nearbySuggestions.map((suggestion) => (
                      <button
                        key={suggestion.refId ?? suggestion.displayAddress}
                        type="button"
                        className="nearby-suggestion-item"
                        role="option"
                        aria-selected="false"
                        onMouseDown={(event) => event.preventDefault()}
                        onClick={() => handleSelectNearbySuggestion(suggestion)}
                      >
                        <strong>{suggestion.name || suggestion.displayAddress}</strong>
                        <small>{suggestion.address}</small>
                      </button>
                    ))
                  )}
                </div>
              )}
            </div>
            <div className="location-panel-field">
              <label htmlFor="nearby-radius-input">
                Bán kính xung quanh: {localRadiusKm} km
              </label>
              <input
                id="nearby-radius-input"
                type="range"
                min="0.5"
                max="30"
                step="0.5"
                value={localRadiusKm}
                onChange={(e) => setLocalRadiusKm(Number(e.target.value))}
              />
            </div>
            {nearbyError && <p className="nearby-error-text">{nearbyError}</p>}
          </div>
        </>
      ) : (
        <>
          <section className="location-panel-section">
            <button type="button" className="location-section-heading" onClick={() => setActiveTab('nearby')}>
              <span className="location-section-icon">◎</span>
              <strong>Tìm kiếm quanh bạn</strong>
            </button>
            <button type="button" className="nearby-search-trigger" onClick={() => setActiveTab('nearby')}>
              <span>
                {centerLat != null && centerLng != null
                  ? nearbyAddress || `Bán kính ${localRadiusKm}km`
                  : 'Nhập vị trí và khoảng cách tìm kiếm'}
              </span>
              <span>›</span>
            </button>
          </section>

          <section className="location-panel-section">
            <div className="location-section-heading">
              <span className="location-section-icon">▦</span>
              <strong>Tìm theo khu vực</strong>
            </div>
            <div className="area-quick-list">
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
            <div className="location-select-list">
              <label className={`location-select-card ${localProvinceCode ? 'has-value' : ''}`} htmlFor="location-province-select">
                <span>
                  <strong>Chọn tỉnh thành</strong>
                  <small>{selectedProvinceName || 'Tất cả tỉnh/thành phố'}</small>
                </span>
                <span>›</span>
                <select
                  id="location-province-select"
                  value={localProvinceCode}
                  onChange={(e) => {
                    setLocalProvinceCode(e.target.value);
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
              <label className={`location-select-card ${localWardCode ? 'has-value' : ''}`} htmlFor="location-ward-select">
                <span>
                  <strong>Chọn khu vực</strong>
                  <small>{selectedWardName || 'Tất cả khu vực trong tỉnh/thành'}</small>
                </span>
                <span>›</span>
                <select
                  id="location-ward-select"
                  value={localWardCode}
                  disabled={!localProvinceCode}
                  onChange={(e) => {
                    setLocalWardCode(e.target.value);
                  }}
                >
                  <option value="">Tất cả khu vực trong tỉnh/thành</option>
                  {wards.map((ward) => (
                    <option key={ward.code} value={ward.code}>
                      {ward.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </section>
        </>
      )}

      <div className="location-panel-actions">
        <Button type="button" variant="secondary" onClick={onClear}>Xóa Lọc</Button>
        <Button type="button" onClick={handleApplyClick} disabled={nearbySearching}>
          {nearbySearching ? 'Đang tìm...' : 'Áp dụng'}
        </Button>
      </div>
    </div>
  );
}
