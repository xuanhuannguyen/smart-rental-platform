import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { env } from '../../../config/env';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { searchLocationAddress } from '../api';
import './RoomingHouseEditor.css';

type LeafletLocationPickerProps = {
  addressLine: string;
  provinceName?: string;
  wardName?: string;
  latitude?: number | null;
  longitude?: number | null;
  googleMapUrl?: string | null;
  onAddressChange: (addressLine: string) => void;
  onLocationChange: (latitude: number, longitude: number) => void;
  onGoogleMapUrlChange: (googleMapUrl: string) => void;
};

const defaultCenter: [number, number] = [16.0471, 108.2062];
const defaultZoom = 16;
const defaultBrowseZoom = 12;
const vietnamBounds = L.latLngBounds(L.latLng(4.5, 100), L.latLng(24.5, 119.5));

export default function LeafletLocationPicker({
  addressLine,
  provinceName,
  wardName,
  latitude,
  longitude,
  googleMapUrl,
  onAddressChange,
  onLocationChange,
  onGoogleMapUrlChange,
}: LeafletLocationPickerProps) {
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<L.Map | null>(null);
  const markerRef = useRef<L.Marker | null>(null);
  const isEditingMarkerRef = useRef(false);
  const onLocationChangeRef = useRef(onLocationChange);
  const [searching, setSearching] = useState(false);
  const [isEditingMarker, setIsEditingMarker] = useState(false);
  const [error, setError] = useState('');

  const hasCoordinates = latitude != null && longitude != null;
  const markerIcon = useMemo(
    () =>
      L.divIcon({
        className: 'leaflet-location-picker__marker',
        html: '<span></span>',
        iconSize: [48, 48],
        iconAnchor: [24, 44],
        popupAnchor: [0, -42],
      }),
    []
  );

  useEffect(() => {
    isEditingMarkerRef.current = isEditingMarker;
  }, [isEditingMarker]);

  useEffect(() => {
    onLocationChangeRef.current = onLocationChange;
  }, [onLocationChange]);

  useEffect(() => {
    if (!mapContainerRef.current || mapRef.current) return;

    const map = L.map(mapContainerRef.current, {
      attributionControl: false,
      center: hasCoordinates ? [latitude, longitude] : defaultCenter,
      maxBounds: vietnamBounds,
      maxBoundsViscosity: 1,
      maxZoom: 19,
      minZoom: 5,
      zoom: hasCoordinates ? defaultZoom : defaultBrowseZoom,
    });

    L.tileLayer(env.leafletTileUrl, {
      bounds: vietnamBounds,
      keepBuffer: 1,
      maxZoom: 19,
      noWrap: true,
    }).addTo(map);

    map.on('click', (event) => {
      if (!isEditingMarkerRef.current) return;
      onLocationChangeRef.current(event.latlng.lat, event.latlng.lng);
    });

    map.whenReady(() => {
      map.invalidateSize({ animate: false });
      window.setTimeout(() => map.invalidateSize({ animate: false }), 150);
    });

    mapRef.current = map;

    return () => {
      markerRef.current?.remove();
      markerRef.current = null;
      map.remove();
      mapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !hasCoordinates) return;

    const coordinates: [number, number] = [latitude, longitude];
    map.flyTo(coordinates, defaultZoom, { animate: true, duration: 0.8 });
    window.setTimeout(() => map.invalidateSize({ animate: false }), 100);

    if (!markerRef.current) {
      markerRef.current = L.marker(coordinates, {
        draggable: isEditingMarker,
        icon: markerIcon,
      }).addTo(map);

      markerRef.current.on('dragend', () => {
        const position = markerRef.current?.getLatLng();
        if (!position) return;
        onLocationChangeRef.current(position.lat, position.lng);
      });
      return;
    }

    markerRef.current.setLatLng(coordinates);
    if (isEditingMarker) {
      markerRef.current.dragging?.enable();
    } else {
      markerRef.current.dragging?.disable();
    }
  }, [hasCoordinates, isEditingMarker, latitude, longitude, markerIcon]);

  const fullAddress = [addressLine, wardName, provinceName, 'Việt Nam']
    .filter((part) => Boolean(part?.trim()))
    .join(', ');

  async function searchAddressOnMap() {
    if (!fullAddress.trim()) {
      setError('Vui lòng nhập địa chỉ trước khi tìm vị trí.');
      return;
    }

    setSearching(true);
    setError('');
    try {
      const result = await searchLocationAddress(fullAddress);
      // Không ghi đè addressLine bằng displayAddress từ VietMap vì nó đã chứa ward+province
      // -> backend BuildAddressDisplayAsync sẽ append ward+province vào addressLine, gây trùng
      onLocationChange(result.latitude, result.longitude);
      setIsEditingMarker(false);
    } catch (searchError) {
      setError(getApiErrorMessage(searchError, 'Không thể tìm vị trí bằng VietMap.'));
    } finally {
      setSearching(false);
    }
  }

  return (
    <section className="leaflet-location-picker">
      <label className="leaflet-location-picker__field">
        <span>Tìm địa chỉ</span>
        <div className="leaflet-location-picker__search-row">
          <div className="field-input-wrapper search-input-wrapper">
            <span className="field-icon">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
            </span>
            <input
              className="leaflet-location-picker__input has-icon"
              value={fullAddress}
              readOnly
              placeholder="Nhập đầy đủ Tỉnh/thành phố, Phường/xã, Địa chỉ phía trên..."
            />
          </div>
          <button
            className="leaflet-location-picker__search-button primary-btn-with-icon"
            disabled={searching}
            type="button"
            onClick={searchAddressOnMap}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
              <circle cx="12" cy="12" r="10" />
              <circle cx="12" cy="12" r="3" />
              <line x1="12" y1="1" x2="12" y2="3" />
              <line x1="12" y1="21" x2="12" y2="23" />
              <line x1="1" y1="12" x2="3" y2="12" />
              <line x1="21" y1="12" x2="23" y2="12" />
            </svg>
            <span>{searching ? 'Đang tìm' : 'Tìm vị trí'}</span>
          </button>
        </div>
      </label>

      <label className="leaflet-location-picker__field">
        <span>Link Google Maps cho người thuê</span>
        <div className="field-input-wrapper">
          <span className="field-icon">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
              <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
            </svg>
          </span>
          <input
            className="leaflet-location-picker__input has-icon"
            value={googleMapUrl ?? ''}
            onChange={(event) => onGoogleMapUrlChange(event.target.value)}
            placeholder="Dán link Google Maps để người thuê mở chỉ đường"
          />
        </div>
      </label>

      <div className="leaflet-map-card">
        <div className="leaflet-map-card-header">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="#246bfe" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="map-pin-header-icon">
            <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
            <circle cx="12" cy="10" r="3" />
          </svg>
          <h3>Vị trí trên bản đồ</h3>
        </div>

        <div ref={mapContainerRef} className="leaflet-location-picker__map" />

        <div className="leaflet-location-picker__actions">
          <button
            className="leaflet-location-picker__secondary-button outline-btn-with-icon"
            disabled={!hasCoordinates}
            type="button"
            onClick={() => setIsEditingMarker((current) => !current)}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
              <circle cx="12" cy="12" r="10" />
              <circle cx="12" cy="12" r="3" />
              <line x1="12" y1="1" x2="12" y2="3" />
              <line x1="12" y1="21" x2="12" y2="23" />
              <line x1="1" y1="12" x2="3" y2="12" />
              <line x1="21" y1="12" x2="23" y2="12" />
            </svg>
            <span>{isEditingMarker ? 'Khóa vị trí' : 'Chỉnh vị trí'}</span>
          </button>
        </div>

        {hasCoordinates ? (
          <p className="leaflet-location-picker__coords">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: '6px', color: '#64748b' }}>
              <circle cx="12" cy="12" r="10" />
              <path d="M12 16v-4" />
              <path d="M12 8h.01" />
            </svg>
            Vĩ độ: {latitude.toFixed(7)} - Kinh độ: {longitude.toFixed(7)}
          </p>
        ) : (
          <p className="leaflet-location-picker__hint">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: '6px', color: '#64748b' }}>
              <circle cx="12" cy="12" r="10" />
              <path d="M12 16v-4" />
              <path d="M12 8h.01" />
            </svg>
            Bấm Tìm vị trí để lấy tọa độ. Nếu marker lệch, bấm Chỉnh vị trí rồi kéo marker hoặc click lên bản đồ.
          </p>
        )}
        {error && <p className="leaflet-location-picker__error">{error}</p>}
      </div>
    </section>
  );
}
