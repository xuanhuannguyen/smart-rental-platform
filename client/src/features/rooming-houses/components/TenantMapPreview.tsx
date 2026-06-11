import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { env } from '../../../config/env';
import './TenantMapPreview.css';

type TenantMapPreviewProps = {
  address: string;
  googleMapUrl?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  title: string;
};

const vietnamBounds = L.latLngBounds(L.latLng(4.5, 100), L.latLng(24.5, 119.5));
const defaultZoom = 16;

function buildGoogleMapsUrl(latitude: number, longitude: number, googleMapUrl?: string | null) {
  if (googleMapUrl?.trim()) return googleMapUrl;
  const query = encodeURIComponent(`${latitude},${longitude}`);
  return `https://www.google.com/maps/search/?api=1&query=${query}`;
}

export default function TenantMapPreview({
  address,
  googleMapUrl,
  latitude,
  longitude,
  title,
}: TenantMapPreviewProps) {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const hasCoordinates = latitude != null && longitude != null;

  const miniMapContainerRef = useRef<HTMLDivElement | null>(null);
  const miniMapRef = useRef<L.Map | null>(null);
  const miniMarkerRef = useRef<L.Marker | null>(null);

  const largeMapContainerRef = useRef<HTMLDivElement | null>(null);
  const largeMapRef = useRef<L.Map | null>(null);
  const largeMarkerRef = useRef<L.Marker | null>(null);

  // Div icon marker representing the housing location
  const markerIcon = useMemo(
    () =>
      L.divIcon({
        className: 'tenant-map-preview__marker',
        html: '<span></span>',
        iconSize: [48, 48],
        iconAnchor: [24, 44],
        popupAnchor: [0, -42],
      }),
    []
  );
  // Initialize mini preview map
  useEffect(() => {
    if (!miniMapContainerRef.current || miniMapRef.current || !hasCoordinates) return;

    const center: [number, number] = [latitude, longitude];
    try {
      const map = L.map(miniMapContainerRef.current, {
        attributionControl: false,
        center,
        maxBounds: vietnamBounds,
        maxBoundsViscosity: 1,
        maxZoom: 19,
        minZoom: 5,
        zoom: 15,
        zoomControl: false,
        dragging: false,
        scrollWheelZoom: false,
        doubleClickZoom: false,
        boxZoom: false,
        touchZoom: false,
        keyboard: false,
      });

      L.tileLayer(env.leafletTileUrl, {
        bounds: vietnamBounds,
        keepBuffer: 1,
        maxZoom: 19,
        noWrap: true,
      }).addTo(map);

      miniMarkerRef.current = L.marker(center, { icon: markerIcon }).addTo(map);

      map.whenReady(() => {
        map.invalidateSize({ animate: false });
      });

      miniMapRef.current = map;
    } catch (err) {
      console.error("Error initializing mini map:", err);
    }

    return () => {
      miniMarkerRef.current?.remove();
      miniMarkerRef.current = null;
      miniMapRef.current?.remove();
      miniMapRef.current = null;
    };
  }, [hasCoordinates, latitude, longitude, markerIcon]);

  // Initialize large modal map when opened
  useEffect(() => {
    if (!isModalOpen || !largeMapContainerRef.current || largeMapRef.current || !hasCoordinates) return;

    const center: [number, number] = [latitude, longitude];
    const map = L.map(largeMapContainerRef.current, {
      attributionControl: false,
      center,
      maxBounds: vietnamBounds,
      maxBoundsViscosity: 1,
      maxZoom: 19,
      minZoom: 5,
      zoom: defaultZoom,
      zoomControl: false,
    });

    L.tileLayer(env.leafletTileUrl, {
      bounds: vietnamBounds,
      keepBuffer: 1,
      maxZoom: 19,
      noWrap: true,
    }).addTo(map);

    largeMarkerRef.current = L.marker(center, { icon: markerIcon })
      .addTo(map)
      .bindPopup(`<strong>${title}</strong><br />${address}`)
      .openPopup();

    map.whenReady(() => {
      map.invalidateSize({ animate: false });
    });

    // Use ResizeObserver to handle map resize dynamically as the modal scale animation finishes
    const container = largeMapContainerRef.current;
    const resizeObserver = new ResizeObserver(() => {
      map.invalidateSize({ animate: false });
    });
    resizeObserver.observe(container);

    largeMapRef.current = map;

    return () => {
      resizeObserver.disconnect();
      largeMarkerRef.current?.remove();
      largeMarkerRef.current = null;
      map.remove();
      largeMapRef.current = null;
    };
  }, [isModalOpen, address, hasCoordinates, latitude, longitude, markerIcon, title]);

  // Handle escape key to close modal
  useEffect(() => {
    if (!isModalOpen) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        setIsModalOpen(false);
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isModalOpen]);

  if (!hasCoordinates) {
    return (
      <div className="tenant-map-preview tenant-map-preview--empty">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
          <circle cx="12" cy="10" r="3" />
        </svg>
        <span>Chủ trọ chưa cập nhật tọa độ bản đồ cho khu trọ này.</span>
      </div>
    );
  }

  const mapsUrl = buildGoogleMapsUrl(latitude, longitude, googleMapUrl);

  return (
    <>
      {/* Small Map Preview Box */}
      <div className="tenant-map-preview-card">
        <div className="tenant-map-preview-card__info">
          <div className="tenant-map-preview-card__address">
            <svg
              className="tenant-map-preview-card__icon"
              viewBox="0 0 24 24"
              aria-hidden="true"
              width="18"
              height="18"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
              <circle cx="12" cy="10" r="3" />
            </svg>
            <span className="tenant-map-preview-card__address-text">{address}</span>
          </div>
          <button
            className="tenant-map-preview-card__link"
            type="button"
            onClick={() => setIsModalOpen(true)}
          >
            Xem bản đồ
          </button>
        </div>

        <button
          className="tenant-map-preview-card__map-button"
          aria-label="Xem bản đồ chi tiết"
          type="button"
          onClick={() => setIsModalOpen(true)}
        >
          <div ref={miniMapContainerRef} className="tenant-map-preview-card__canvas" />
          <div className="tenant-map-preview-card__overlay" />
        </button>
      </div>

      {/* Fullscreen Map Modal */}
      {isModalOpen && (
        <div className="tenant-map-modal-overlay" onClick={() => setIsModalOpen(false)}>
          <div className="tenant-map-modal" onClick={(e) => e.stopPropagation()}>
            <div className="tenant-map-modal__header">
              <div className="tenant-map-modal__title">
                <svg
                  className="tenant-map-modal__icon"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                  width="20"
                  height="20"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                >
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span>{address}</span>
              </div>
              <button
                className="tenant-map-modal__close-btn"
                aria-label="Đóng bản đồ"
                type="button"
                onClick={() => setIsModalOpen(false)}
              >
                <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
            </div>

            <div className="tenant-map-modal__body">
              <div ref={largeMapContainerRef} className="tenant-map-modal__canvas" />

              <div className="tenant-map-modal__controls" role="group" aria-label="Điều khiển bản đồ">
                <button
                  aria-label="Về vị trí tin đăng"
                  type="button"
                  onClick={() =>
                    largeMapRef.current?.flyTo([latitude, longitude], defaultZoom, {
                      animate: true,
                      duration: 0.6,
                    })
                  }
                >
                  <svg viewBox="0 0 24 24" aria-hidden="true">
                    <path
                      d="M12 4.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5Zm1 6.82A4 4 0 1 0 11 11.32V18a1 1 0 1 0 2 0v-6.68ZM4 18c0-.9.62-1.64 1.6-2.2.96-.55 2.28-.96 3.82-1.17a1 1 0 0 0-.28-1.98c-1.72.24-3.29.7-4.54 1.42C3.37 14.78 2 16.04 2 18c0 1.32.78 2.36 1.84 3.04C5.9 22.37 9.1 23 12 23s6.1-.63 8.16-1.96C21.22 20.36 22 19.32 22 18c0-1.96-1.37-3.22-2.6-3.93-1.25-.72-2.82-1.18-4.54-1.42a1 1 0 1 0-.28 1.98c1.54.21 2.86.62 3.82 1.17.98.56 1.6 1.3 1.6 2.2 0 .4-.25.88-.92 1.31C17.45 20.36 14.65 21 12 21s-5.45-.64-7.08-1.69C4.25 18.88 4 18.4 4 18Z"
                      fill="currentColor"
                    />
                  </svg>
                </button>
                <button
                  aria-label="Phóng to"
                  type="button"
                  onClick={() => largeMapRef.current?.zoomIn()}
                >
                  +
                </button>
                <button
                  aria-label="Thu nhỏ"
                  type="button"
                  onClick={() => largeMapRef.current?.zoomOut()}
                >
                  -
                </button>
              </div>
            </div>

            <div className="tenant-map-modal__footer">
              <a className="tenant-map-modal__directions-btn" href={mapsUrl} target="_blank" rel="noreferrer">
                Chỉ đường (Mở Google Maps)
              </a>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
