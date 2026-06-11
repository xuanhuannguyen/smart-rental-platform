import { useEffect, useMemo, useState, useRef, type CSSProperties, type FormEvent } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useAuth } from '../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../app/router/routePaths';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { toAssetUrl } from '../../shared/api/assets';
import { Button } from '../../shared/components/ui/Button';
import { searchPublicRoomingHouses, getAmenities, searchLocationAddress, suggestLocationAddresses } from './api';
import { getProvinces, getWardsByProvince } from '../administrative/api';
import type { Province, Ward } from '../administrative/types';
import type { PagedResult, RoomingHouseSearchItem, RoomingHouseSearchParams, Amenity, LocationSuggestion } from './types';
import { env } from '../../config/env';
import SearchSuggestionBox from './components/SearchSuggestionBox';
import { saveRecentSearch } from './searchRecentStorage';
import './SearchRoomingHousesPage.css';

const DEFAULT_PAGE_SIZE = 12;
const PRICE_RANGE_MAX = 10_000_000;
const PRICE_RANGE_STEP = 100_000;
const SORT_OPTIONS = new Set(['relevance', 'newest', 'priceAsc', 'priceDesc', 'areaAsc', 'areaDesc', 'distanceAsc']);
type LocationPanel = 'area' | 'nearby' | null;
type SidebarFilterPanel = 'price' | 'area' | 'occupants' | 'houseAmenities' | 'roomAmenities' | null;

const searchResultCache = new Map<string, PagedResult<RoomingHouseSearchItem>>();
const wardsCache = new Map<string, Ward[]>();
const scrollPositionCache = new Map<string, number>();
let metadataCache: { provinces: Province[]; amenities: Amenity[] } | null = null;

export default function SearchRoomingHousesPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const currentSearchPath = `${location.pathname}${location.search}`;
  const query = useMemo(() => buildSearchParams(searchParams), [searchParams]);
  const searchCacheKey = useMemo(() => paramsToUrl(query).toString(), [query]);
  const nearbyLabelParam = searchParams.get('nearbyLabel')?.trim() ?? '';
  
  const { currentUser, logout } = useAuth();
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  
  const [result, setResult] = useState<PagedResult<RoomingHouseSearchItem> | null>(
    () => searchResultCache.get(searchCacheKey) ?? null
  );
  const [loading, setLoading] = useState(() => !searchResultCache.has(searchCacheKey));
  const [error, setError] = useState('');
  const restoredSearchPathRef = useRef<string | null>(null);

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
  const avatarInitials = currentUser?.displayName
    ? currentUser.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
    : 'U';

  // Dropdown & Checkbox source states
  const [provinces, setProvinces] = useState<Province[]>([]);
  const [wards, setWards] = useState<Ward[]>([]);
  const [amenities, setAmenities] = useState<Amenity[]>([]);

  const hasAppliedRadiusSearch = query.centerLat != null && query.centerLng != null;

  // Search input state (binds search query)
  const [searchQuery, setSearchQuery] = useState(query.q ?? '');

  // Local filter states to avoid immediate URL updates while typing
  const [localProvinceCode, setLocalProvinceCode] = useState(query.provinceCode ?? '');
  const [localWardCode, setLocalWardCode] = useState(query.wardCode ?? '');
  const [localMinPrice, setLocalMinPrice] = useState(query.minPrice?.toString() ?? '');
  const [localMaxPrice, setLocalMaxPrice] = useState(query.maxPrice?.toString() ?? '');
  const [localMinArea, setLocalMinArea] = useState(query.minAreaM2?.toString() ?? '');
  const [localMaxArea, setLocalMaxArea] = useState(query.maxAreaM2?.toString() ?? '');
  const [localMinOccupants, setLocalMinOccupants] = useState(query.minOccupants?.toString() ?? '');
  const [localAmenityIds, setLocalAmenityIds] = useState<number[]>(query.amenityIds ?? []);
  const [localRoomAmenityIds, setLocalRoomAmenityIds] = useState<number[]>(query.roomAmenityIds ?? []);

  // Radius Search states
  const [showRadiusSearch, setShowRadiusSearch] = useState(query.centerLat != null && query.centerLng != null);
  const [localCenterLat, setLocalCenterLat] = useState<number | null>(query.centerLat ?? null);
  const [localCenterLng, setLocalCenterLng] = useState<number | null>(query.centerLng ?? null);
  const [localRadiusKm, setLocalRadiusKm] = useState<number>(query.radiusKm ?? 3);
  const [activeLocationPanel, setActiveLocationPanel] = useState<LocationPanel>(null);
  const [nearbyAddress, setNearbyAddress] = useState('');
  const [nearbyError, setNearbyError] = useState('');
  const [nearbySearching, setNearbySearching] = useState(false);
  const [nearbySuggestions, setNearbySuggestions] = useState<LocationSuggestion[]>([]);
  const [nearbySuggesting, setNearbySuggesting] = useState(false);
  const [showNearbySuggestions, setShowNearbySuggestions] = useState(false);
  const [activeSidebarFilter, setActiveSidebarFilter] = useState<SidebarFilterPanel>(null);
  const [sidebarPanelTop, setSidebarPanelTop] = useState<number | null>(null);
  const filtersSidebarRef = useRef<HTMLElement | null>(null);
  const filterButtonRefs = useRef<Record<Exclude<SidebarFilterPanel, null>, HTMLButtonElement | null>>({
    price: null,
    area: null,
    occupants: null,
    houseAmenities: null,
    roomAmenities: null,
  });

  // Map Refs
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<L.Map | null>(null);
  const circleRef = useRef<L.Circle | null>(null);
  const centerMarkerRef = useRef<L.Marker | null>(null);
  const resultMarkersRef = useRef<L.Marker[]>([]);

  // Grouped amenities
  const houseAmenitiesList = useMemo(() => amenities.filter((a) => a.scope === 'House' || a.scope === 'Both'), [amenities]);
  const roomAmenitiesList = useMemo(() => amenities.filter((a) => a.scope === 'Room' || a.scope === 'Both'), [amenities]);
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
  const activeFilters = useMemo(
    () => buildActiveFilterLabels(query, provinces, wards, amenities),
    [query, provinces, wards, amenities]
  );
  const locationButtonLabel = useMemo(
    () => buildLocationButtonLabel(query, searchParams, provinces, wards),
    [query, searchParams, provinces, wards]
  );
  const hasAppliedLocationSearch =
    query.provinceCode != null ||
    query.wardCode != null ||
    (query.centerLat != null && query.centerLng != null);

  // Load provinces and active amenities on mount
  useEffect(() => {
    if (metadataCache) {
      setProvinces(metadataCache.provinces);
      setAmenities(metadataCache.amenities);
      return;
    }

    let cancelled = false;
    async function loadMetadata() {
      try {
        const [provincesData, amenitiesData] = await Promise.all([
          getProvinces(),
          getAmenities(),
        ]);
        metadataCache = { provinces: provincesData, amenities: amenitiesData };
        if (!cancelled) {
          setProvinces(provincesData);
          setAmenities(amenitiesData);
        }
      } catch (err) {
        console.error('Lỗi khi tải danh mục tìm kiếm:', err);
      }
    }
    void loadMetadata();

    return () => {
      cancelled = true;
    };
  }, []);

  // Fetch wards when provinceCode changes
  useEffect(() => {
    if (!localProvinceCode) {
      setWards([]);
      return;
    }

    const cachedWards = wardsCache.get(localProvinceCode);
    if (cachedWards) {
      setWards(cachedWards);
      return;
    }

    let cancelled = false;
    async function loadWards() {
      try {
        const wardsData = await getWardsByProvince(localProvinceCode);
        wardsCache.set(localProvinceCode, wardsData);
        if (cancelled) return;
        setWards(wardsData);
      } catch (err) {
        console.error('Lỗi khi tải danh sách phường/xã:', err);
      }
    }
    void loadWards();

    return () => {
      cancelled = true;
    };
  }, [localProvinceCode]);

  // Sync inputs with URL params (handles back/forward navigation)
  useEffect(() => {
    setSearchQuery(query.q ?? '');
    setLocalProvinceCode(query.provinceCode ?? '');
    setLocalWardCode(query.wardCode ?? '');
    setLocalMinPrice(query.minPrice?.toString() ?? '');
    setLocalMaxPrice(query.maxPrice?.toString() ?? '');
    setLocalMinArea(query.minAreaM2?.toString() ?? '');
    setLocalMaxArea(query.maxAreaM2?.toString() ?? '');
    setLocalMinOccupants(query.minOccupants?.toString() ?? '');
    setLocalAmenityIds(query.amenityIds ?? []);
    setLocalRoomAmenityIds(query.roomAmenityIds ?? []);
    setLocalCenterLat(query.centerLat ?? null);
    setLocalCenterLng(query.centerLng ?? null);
    setLocalRadiusKm(query.radiusKm ?? 3);
    setShowRadiusSearch(query.centerLat != null && query.centerLng != null);
    if (query.centerLat != null && query.centerLng != null && nearbyLabelParam) {
      setNearbyAddress(nearbyLabelParam);
    }
  }, [nearbyLabelParam, query]);

  useEffect(() => {
    if (activeLocationPanel !== 'nearby') {
      setNearbySuggestions([]);
      setShowNearbySuggestions(false);
      setNearbySuggesting(false);
      return;
    }

    const text = nearbyAddress.trim();
    if (text.length < 2 || text === 'Vị trí hiện tại của tôi') {
      setNearbySuggestions([]);
      setShowNearbySuggestions(false);
      setNearbySuggesting(false);
      return;
    }

    let cancelled = false;
    setNearbySuggesting(true);
    const timeoutId = window.setTimeout(() => {
      suggestLocationAddresses(text, 5)
        .then((items) => {
          if (!cancelled) {
            setNearbySuggestions(items);
            setShowNearbySuggestions(items.length > 0);
          }
        })
        .catch(() => {
          if (!cancelled) {
            setNearbySuggestions([]);
            setShowNearbySuggestions(false);
          }
        })
        .finally(() => {
          if (!cancelled) {
            setNearbySuggesting(false);
          }
        });
    }, 350);

    return () => {
      cancelled = true;
      window.clearTimeout(timeoutId);
    };
  }, [activeLocationPanel, nearbyAddress]);

  useEffect(() => {
    if (!activeSidebarFilter) {
      setSidebarPanelTop(null);
      return;
    }

    const updatePanelTop = () => {
      const button = filterButtonRefs.current[activeSidebarFilter];
      if (!button) return;

      const top = button.getBoundingClientRect().top;
      setSidebarPanelTop(Math.max(88, top));
    };

    updatePanelTop();
    window.addEventListener('resize', updatePanelTop);
    window.addEventListener('scroll', updatePanelTop, true);

    return () => {
      window.removeEventListener('resize', updatePanelTop);
      window.removeEventListener('scroll', updatePanelTop, true);
    };
  }, [activeSidebarFilter]);

  // Fetch search results from API
  useEffect(() => {
    const cachedResult = searchResultCache.get(searchCacheKey);
    if (cachedResult) {
      setResult(cachedResult);
      setError('');
      setLoading(false);
      return;
    }

    let cancelled = false;

    async function loadSearchResults() {
      setLoading(true);
      setError('');
      try {
        const data = await searchPublicRoomingHouses(query);
        searchResultCache.set(searchCacheKey, data);
        if (!cancelled) {
          setResult(data);
        }
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err, 'Không thể tìm kiếm khu trọ.'));
          setResult(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadSearchResults();

    return () => {
      cancelled = true;
    };
  }, [query, searchCacheKey]);

  useEffect(() => {
    return () => {
      scrollPositionCache.set(currentSearchPath, window.scrollY);
    };
  }, [currentSearchPath]);

  useEffect(() => {
    if (loading || restoredSearchPathRef.current === currentSearchPath) return;

    restoredSearchPathRef.current = currentSearchPath;
    const scrollY = scrollPositionCache.get(currentSearchPath);
    if (scrollY == null) return;

    const frameId = window.requestAnimationFrame(() => {
      window.scrollTo({ top: scrollY });
    });
    return () => window.cancelAnimationFrame(frameId);
  }, [currentSearchPath, loading, result]);

  // Leaflet Map Initialization
  useEffect(() => {
    if (!hasAppliedRadiusSearch || !mapContainerRef.current) {
      if (mapRef.current) {
        mapRef.current.remove();
        mapRef.current = null;
        circleRef.current = null;
        centerMarkerRef.current = null;
        resultMarkersRef.current = [];
      }
      return;
    }

    if (mapRef.current) return;

    const initialLat = query.centerLat ?? 16.0471;
    const initialLng = query.centerLng ?? 108.2062;
    const initialZoom = hasAppliedRadiusSearch ? 13 : 12;

    const map = L.map(mapContainerRef.current, {
      attributionControl: false,
      center: [initialLat, initialLng],
      zoom: initialZoom,
      maxZoom: 19,
      minZoom: 5,
    });

    L.tileLayer(env.leafletTileUrl, {
      maxZoom: 19,
    }).addTo(map);

    map.on('click', (e) => {
      setLocalCenterLat(e.latlng.lat);
      setLocalCenterLng(e.latlng.lng);
    });

    const handleMapPopupLinkClick = (event: MouseEvent) => {
      const target = event.target instanceof HTMLElement ? event.target : null;
      const link = target?.closest<HTMLAnchorElement>('.map-popup__link');
      if (!link) return;

      event.preventDefault();
      navigate(`${link.pathname}${link.search}`, { state: { fromSearch: currentSearchPath } });
    };
    map.getContainer().addEventListener('click', handleMapPopupLinkClick);

    mapRef.current = map;

    map.whenReady(() => {
      map.invalidateSize();
    });

    return () => {
      if (mapRef.current) {
        mapRef.current.getContainer().removeEventListener('click', handleMapPopupLinkClick);
        mapRef.current.remove();
        mapRef.current = null;
        circleRef.current = null;
        centerMarkerRef.current = null;
        resultMarkersRef.current = [];
      }
    };
  }, [currentSearchPath, hasAppliedRadiusSearch, navigate, query.centerLat, query.centerLng]);

  // Draw Center Marker & Radius Circle on Map
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    if (query.centerLat != null && query.centerLng != null) {
      const position = L.latLng(query.centerLat, query.centerLng);

      // Draw Center Marker
      if (!centerMarkerRef.current) {
        centerMarkerRef.current = L.marker(position, {
          icon: L.divIcon({
            className: 'search-map__center-marker',
            html: '📍',
            iconSize: [32, 32],
            iconAnchor: [16, 32],
          }),
        }).addTo(map);
      } else {
        centerMarkerRef.current.setLatLng(position);
      }

      // Draw Radius Circle
      const radiusMeters = (query.radiusKm ?? 3) * 1000;
      if (!circleRef.current) {
        circleRef.current = L.circle(position, {
          radius: radiusMeters,
          color: '#246bfe',
          fillColor: '#246bfe',
          fillOpacity: 0.15,
          weight: 1.5,
        }).addTo(map);
      } else {
        circleRef.current.setLatLng(position);
        circleRef.current.setRadius(radiusMeters);
      }

      map.panTo(position);
    } else {
      if (centerMarkerRef.current) {
        centerMarkerRef.current.remove();
        centerMarkerRef.current = null;
      }
      if (circleRef.current) {
        circleRef.current.remove();
        circleRef.current = null;
      }
    }
  }, [query.centerLat, query.centerLng, query.radiusKm]);

  // Draw search results on Map
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    // Clear old result markers
    resultMarkersRef.current.forEach((marker) => marker.remove());
    resultMarkersRef.current = [];

    if (!result) return;

    const newMarkers: L.Marker[] = [];
    result.items.forEach((item) => {
      if (item.latitude != null && item.longitude != null) {
        const position = L.latLng(item.latitude, item.longitude);
        const marker = L.marker(position, {
          icon: L.divIcon({
            className: 'search-map__result-marker',
            html: '<span></span>',
            iconSize: [24, 24],
            iconAnchor: [12, 12],
          }),
        }).addTo(map);

        marker.bindPopup(`
          <div class="map-popup">
            <strong>${escapeHtml(item.name)}</strong>
            <p>${escapeHtml(item.addressDisplay)}</p>
            <span>${formatPriceRange(item.minMonthlyRent, item.maxMonthlyRent)}</span>
            <a href="/rooming-houses/${item.id}?from=${encodeURIComponent(currentSearchPath)}" class="map-popup__link">Xem chi tiết</a>
          </div>
        `);
        newMarkers.push(marker);
      }
    });

    resultMarkersRef.current = newMarkers;
    if (newMarkers.length > 0) {
      const bounds = L.latLngBounds(newMarkers.map((marker) => marker.getLatLng()));
      if (centerMarkerRef.current) {
        bounds.extend(centerMarkerRef.current.getLatLng());
      }
      map.fitBounds(bounds, { maxZoom: 14, padding: [28, 28] });
    }
  }, [currentSearchPath, result]);

  // Browser Geolocation integration
  function handleUseCurrentLocation() {
    if (!navigator.geolocation) {
      alert('Trình duyệt của bạn không hỗ trợ định vị.');
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const lat = position.coords.latitude;
        const lng = position.coords.longitude;
        setLocalCenterLat(lat);
        setLocalCenterLng(lng);
        setShowRadiusSearch(true);
        setLocalProvinceCode('');
        setLocalWardCode('');
        setNearbyAddress('Vị trí hiện tại của tôi');
      },
      (err) => {
        setError(`Không thể lấy vị trí hiện tại: ${err.message}`);
      }
    );
  }

  // Handle checkboxes change
  const handleAmenityChange = (id: number) => {
    setLocalAmenityIds((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
    );
  };

  const handleRoomAmenityChange = (id: number) => {
    setLocalRoomAmenityIds((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
    );
  };

  // Submit filters to URL parameters
  function buildCurrentFilterParams(nextQuery: string): RoomingHouseSearchParams {
    const hasRadiusCenter = hasAppliedRadiusSearch && localCenterLat != null && localCenterLng != null;
    return {
      q: nextQuery.trim() || undefined,
      provinceCode: hasRadiusCenter ? undefined : localProvinceCode || undefined,
      wardCode: hasRadiusCenter ? undefined : localWardCode || undefined,
      minPrice: localMinPrice ? Number(localMinPrice) : undefined,
      maxPrice: localMaxPrice ? Number(localMaxPrice) : undefined,
      minAreaM2: localMinArea ? Number(localMinArea) : undefined,
      maxAreaM2: localMaxArea ? Number(localMaxArea) : undefined,
      minOccupants: localMinOccupants ? Number(localMinOccupants) : undefined,
      amenityIds: localAmenityIds.length > 0 ? localAmenityIds : undefined,
      roomAmenityIds: localRoomAmenityIds.length > 0 ? localRoomAmenityIds : undefined,
      centerLat: hasRadiusCenter ? localCenterLat : undefined,
      centerLng: hasRadiusCenter ? localCenterLng : undefined,
      radiusKm: hasRadiusCenter ? localRadiusKm : undefined,
      sortBy: query.sortBy === 'distanceAsc' && !hasRadiusCenter ? 'relevance' : query.sortBy,
      page: 1, // Reset page to 1 on new filter apply
      pageSize: query.pageSize,
    };
  }

  function handleApplyFilters(e?: FormEvent) {
    if (e) e.preventDefault();

    const updatedParams = buildCurrentFilterParams(searchQuery);
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    const nextUrl = `/search${newSearchParams.toString() ? `?${newSearchParams.toString()}` : ''}`;
    if (updatedParams.q) {
      saveRecentSearch(updatedParams.q, nextUrl);
    }

    navigate({ search: newSearchParams.toString() });
  }

  async function handleFindNearbyAddress() {
    const text = nearbyAddress.trim();
    if (!text) {
      setNearbyError('Vui lòng nhập vị trí muốn tìm.');
      return;
    }

    setNearbySearching(true);
    setNearbyError('');
    try {
      const locationResult = await searchLocationAddress(text);
      setLocalCenterLat(locationResult.latitude);
      setLocalCenterLng(locationResult.longitude);
      setNearbyAddress(locationResult.displayAddress || text);
      setNearbySuggestions([]);
      setShowNearbySuggestions(false);
      setShowRadiusSearch(true);
      setLocalProvinceCode('');
      setLocalWardCode('');
    } catch (err) {
      setNearbyError(getApiErrorMessage(err, 'Không tìm thấy vị trí phù hợp.'));
    } finally {
      setNearbySearching(false);
    }
  }

  function handleSelectNearbySuggestion(suggestion: LocationSuggestion) {
    setNearbyAddress(suggestion.displayAddress);
    setLocalCenterLat(suggestion.latitude);
    setLocalCenterLng(suggestion.longitude);
    setShowRadiusSearch(true);
    setLocalProvinceCode('');
    setLocalWardCode('');
    setNearbyError('');
    setNearbySuggestions([]);
    setShowNearbySuggestions(false);
  }

  function handleApplyAreaSearch() {
    const updatedParams: RoomingHouseSearchParams = {
      ...buildCurrentFilterParams(searchQuery),
      provinceCode: localProvinceCode || undefined,
      wardCode: localWardCode || undefined,
      centerLat: undefined,
      centerLng: undefined,
      radiusKm: undefined,
      sortBy: query.sortBy === 'distanceAsc' ? 'relevance' : query.sortBy,
      page: 1,
    };
    setShowRadiusSearch(false);
    setLocalCenterLat(null);
    setLocalCenterLng(null);
    const newSearchParams = paramsToUrl(updatedParams);
    newSearchParams.delete('nearbyLabel');
    navigate({ search: newSearchParams.toString() });
    setActiveLocationPanel(null);
  }

  function handleApplyNearbySearch() {
    if (localCenterLat == null || localCenterLng == null) {
      setNearbyError('Vui lòng nhập vị trí hoặc dùng vị trí hiện tại trước khi áp dụng.');
      return;
    }

    const updatedParams: RoomingHouseSearchParams = {
      ...buildCurrentFilterParams(searchQuery),
      provinceCode: undefined,
      wardCode: undefined,
      centerLat: localCenterLat,
      centerLng: localCenterLng,
      radiusKm: localRadiusKm,
      page: 1,
    };
    setShowRadiusSearch(true);
    setLocalProvinceCode('');
    setLocalWardCode('');
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyAddress.trim() || searchQuery.trim());
    navigate({ search: newSearchParams.toString() });
    setActiveLocationPanel(null);
  }

  function handleSuggestionSearch(nextQuery: string) {
    const updatedParams = buildCurrentFilterParams(nextQuery);
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    const nextUrl = `/search${newSearchParams.toString() ? `?${newSearchParams.toString()}` : ''}`;
    saveRecentSearch(nextQuery, nextUrl);
    navigate({ search: newSearchParams.toString() });
  }

  function handleClearFilters() {
    setSearchQuery('');
    setLocalProvinceCode('');
    setLocalWardCode('');
    setLocalMinPrice('');
    setLocalMaxPrice('');
    setLocalMinArea('');
    setLocalMaxArea('');
    setLocalMinOccupants('');
    setLocalAmenityIds([]);
    setLocalRoomAmenityIds([]);
    setLocalCenterLat(null);
    setLocalCenterLng(null);
    setLocalRadiusKm(3);
    setShowRadiusSearch(false);

    navigate({ search: '' });
  }

  function handleClearAreaSearch() {
    setLocalProvinceCode('');
    setLocalWardCode('');
  }

  function handleClearNearbySearch() {
    setNearbyAddress('');
    setNearbyError('');
    setLocalCenterLat(null);
    setLocalCenterLng(null);
    setLocalRadiusKm(3);
    setShowRadiusSearch(false);
  }

  function handleClearLocationFilters() {
    handleClearAreaSearch();
    handleClearNearbySearch();

    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      provinceCode: undefined,
      wardCode: undefined,
      centerLat: undefined,
      centerLng: undefined,
      radiusKm: undefined,
      sortBy: query.sortBy === 'distanceAsc' ? 'relevance' : query.sortBy,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    newSearchParams.delete('nearbyLabel');
    navigate({ search: newSearchParams.toString() });
    setActiveLocationPanel(null);
  }

  function handleClearPriceFilter() {
    setLocalMinPrice('');
    setLocalMaxPrice('');
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      minPrice: undefined,
      maxPrice: undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleSavePriceFilter() {
    const updatedParams: RoomingHouseSearchParams = {
      ...buildCurrentFilterParams(searchQuery),
      minPrice: normalizePriceInputString(localMinPrice) ? Number(normalizePriceInputString(localMinPrice)) : undefined,
      maxPrice: normalizePriceInputString(localMaxPrice) ? Number(normalizePriceInputString(localMaxPrice)) : undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleMinPriceChange(nextValue: string, normalizeValue = false) {
    const nextInputValue = normalizeValue ? normalizePriceInputString(nextValue) : sanitizeNumericInput(nextValue);
    const normalizedNext = normalizePriceInputString(nextInputValue);
    const maxPrice = normalizePriceInputString(localMaxPrice);
    if (maxPrice && normalizedNext && Number(normalizedNext) > Number(maxPrice)) {
      setLocalMinPrice(maxPrice);
      return;
    }

    setLocalMinPrice(nextInputValue);
  }

  function handleMaxPriceChange(nextValue: string, normalizeValue = false) {
    const nextInputValue = normalizeValue ? normalizePriceInputString(nextValue) : sanitizeNumericInput(nextValue);
    const normalizedNext = normalizePriceInputString(nextInputValue);
    const minPrice = normalizePriceInputString(localMinPrice);
    if (minPrice && normalizedNext && Number(normalizedNext) < Number(minPrice)) {
      setLocalMaxPrice(minPrice);
      return;
    }

    setLocalMaxPrice(nextInputValue);
  }

  function handleClearAreaFilter() {
    setLocalMinArea('');
    setLocalMaxArea('');
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      minAreaM2: undefined,
      maxAreaM2: undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleSaveAreaFilter() {
    const updatedParams: RoomingHouseSearchParams = {
      ...buildCurrentFilterParams(searchQuery),
      minAreaM2: localMinArea ? Number(localMinArea) : undefined,
      maxAreaM2: localMaxArea ? Number(localMaxArea) : undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleClearOccupantsFilter() {
    setLocalMinOccupants('');
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      minOccupants: undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleSaveOccupantsFilter() {
    const updatedParams: RoomingHouseSearchParams = {
      ...buildCurrentFilterParams(searchQuery),
      minOccupants: localMinOccupants ? Number(localMinOccupants) : undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleClearAmenityFilter(type: 'house' | 'room') {
    if (type === 'house') {
      setLocalAmenityIds([]);
    } else {
      setLocalRoomAmenityIds([]);
    }
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      amenityIds: type === 'house' ? undefined : query.amenityIds,
      roomAmenityIds: type === 'room' ? undefined : query.roomAmenityIds,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleSaveAmenityFilter() {
    const updatedParams = buildCurrentFilterParams(searchQuery);
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
    setActiveSidebarFilter(null);
  }

  function handleSortChange(sortVal: string) {
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      sortBy: sortVal || undefined,
      page: 1,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
  }

  function handlePageChange(newPage: number) {
    const updatedParams: RoomingHouseSearchParams = {
      ...query,
      page: newPage,
    };
    const newSearchParams = paramsToUrl(updatedParams);
    preserveNearbyLabel(newSearchParams, updatedParams, nearbyLabelParam);
    navigate({ search: newSearchParams.toString() });
  }

  function handleOpenLocationPanel() {
    setActiveLocationPanel((current) => {
      if (current) return null;
      if (query.centerLat != null && query.centerLng != null) return 'nearby';
      return 'area';
    });
  }

  function handleApplyLocationPanel() {
    if (activeLocationPanel === 'nearby') {
      handleApplyNearbySearch();
      return;
    }

    handleApplyAreaSearch();
  }

  function handleToggleSidebarFilter(panel: Exclude<SidebarFilterPanel, null>) {
    setActiveSidebarFilter((current) => (current === panel ? null : panel));
  }

  const sidebarPanelStyle =
    sidebarPanelTop == null
      ? undefined
      : ({ '--filter-panel-top': `${sidebarPanelTop}px` } as CSSProperties);

  return (
    <div className="search-page-wrapper">
      <header className="search-header">
        <div className="search-header__logo" onClick={() => navigate('/home')}>
          Smart Rental
        </div>
        <form className="search-header__search-form" onSubmit={handleApplyFilters}>
          <SearchSuggestionBox
            placeholder="VD: gần đại học FPT dưới 3tr có máy lạnh"
            value={searchQuery}
            onChange={setSearchQuery}
            onSearch={handleSuggestionSearch}
          />
          <Button type="submit">Tìm</Button>
        </form>
        <div className="search-location-actions">
          <button
            type="button"
            className={`search-location-action ${hasAppliedLocationSearch ? 'is-active' : ''}`}
            onClick={handleOpenLocationPanel}
            aria-expanded={activeLocationPanel != null}
          >
            <span>{locationButtonLabel}</span>
            <span className="search-location-action__chevron">⌄</span>
          </button>

          {activeLocationPanel && (
            <div className="search-location-panel" role="dialog" aria-label="Tùy chọn vị trí tìm kiếm">
            <button
              type="button"
              className="location-panel-close"
              onClick={() => setActiveLocationPanel(null)}
              aria-label="Đóng bộ lọc vị trí"
            >
              ×
            </button>

            {activeLocationPanel === 'nearby' ? (
              <>
                <div className="location-panel-subheader">
                  <button type="button" className="location-back-button" onClick={() => setActiveLocationPanel('area')}>
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
                              onMouseDown={(event) => event.preventDefault()}
                              onClick={() => handleSelectNearbySuggestion(suggestion)}
                            >
                              <span>{suggestion.name || suggestion.displayAddress}</span>
                              {suggestion.address && <small>{suggestion.address}</small>}
                            </button>
                          ))
                        )}
                      </div>
                    )}
                    {nearbyError && <p className="location-panel-error">{nearbyError}</p>}
                  </div>
                  <Button type="button" variant="secondary" className="current-loc-btn" onClick={handleUseCurrentLocation}>
                    Dùng vị trí hiện tại
                  </Button>
                  <div className="location-panel-field">
                    <label htmlFor="nearby-radius-range">Bán kính tìm kiếm</label>
                    <div className="radius-slider-row">
                      <span className="radius-value">{formatRadius(localRadiusKm)}</span>
                      <span>0.5</span>
                      <input
                        id="nearby-radius-range"
                        type="range"
                        min="0.5"
                        max="30"
                        step="0.5"
                        value={localRadiusKm}
                        onChange={(e) => setLocalRadiusKm(Number(e.target.value))}
                      />
                      <span>30 km</span>
                    </div>
                  </div>
                  {localCenterLat != null && localCenterLng != null && (
                    <p className="location-panel-summary">
                      Tâm: {localCenterLat.toFixed(5)}, {localCenterLng.toFixed(5)}
                    </p>
                  )}
                </div>
              </>
            ) : (
              <>
                <section className="location-panel-section">
                  <button type="button" className="location-section-heading" onClick={() => setActiveLocationPanel('nearby')}>
                    <span className="location-section-icon" aria-hidden="true">⌖</span>
                    <span>Tìm kiếm quanh bạn</span>
                  </button>
                  <button type="button" className="nearby-search-trigger" onClick={() => setActiveLocationPanel('nearby')}>
                    <span>{nearbyAddress || 'Nhập vị trí và khoảng cách tìm kiếm'}</span>
                    <span>›</span>
                  </button>
                </section>

                <section className="location-panel-section">
                  <button type="button" className="location-section-heading" onClick={() => setActiveLocationPanel('area')}>
                    <span className="location-section-icon" aria-hidden="true">▦</span>
                    <span>Tìm theo khu vực</span>
                  </button>
                  <div className="area-quick-list">
                    {quickLocationProvinces.map((province) => (
                      <button
                        key={province.code}
                        type="button"
                        className={localProvinceCode === province.code ? 'is-selected' : ''}
                        onClick={() => {
                          setActiveLocationPanel('area');
                          setLocalProvinceCode(province.code);
                          setLocalWardCode('');
                        }}
                      >
                        {province.name}
                      </button>
                    ))}
                  </div>
                  <div className="location-select-list">
                    <label className="location-select-card" htmlFor="location-province-select">
                      <span>
                        <strong>Chọn tỉnh thành</strong>
                        <small>{selectedProvinceName || 'Tất cả tỉnh/thành phố'}</small>
                      </span>
                      <span>›</span>
                      <select
                        id="location-province-select"
                        value={localProvinceCode}
                        onChange={(e) => {
                          setActiveLocationPanel('area');
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
                    <label className="location-select-card" htmlFor="location-ward-select">
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
                          setActiveLocationPanel('area');
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
              <Button type="button" variant="secondary" onClick={handleClearLocationFilters}>Xóa Lọc</Button>
              <Button type="button" onClick={handleApplyLocationPanel}>Áp dụng</Button>
            </div>
          </div>
          )}
        </div>

        <div className="search-header__auth">
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

      <div className="search-page-container">
        {/* Left column: Filters Sidebar */}
        <aside className="search-filters-sidebar" ref={filtersSidebarRef}>
          <div className="search-filters-header">
            <h2>Bộ lọc nâng cao</h2>
            <button className="search-clear-btn" onClick={handleClearFilters}>
              Xóa tất cả
            </button>
          </div>

          <form className="search-filters-form" onSubmit={handleApplyFilters}>
            {/* Price Range */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.price = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'price' ? 'is-open' : ''}`}
                onClick={() => handleToggleSidebarFilter('price')}
                aria-expanded={activeSidebarFilter === 'price'}
              >
                <span>
                  <strong>Giá thuê</strong>
                  <small>{formatRangeSummary(localMinPrice, localMaxPrice, 'price')}</small>
                </span>
                <span className="filter-toggle__chevron">⌄</span>
              </button>
              {activeSidebarFilter === 'price' && (
                <div className="filter-panel price-filter-panel" style={sidebarPanelStyle}>
                  <div className="price-range-visual">
                    <span>0</span>
                    <div
                      className="price-range-track"
                      style={getPriceRangeTrackStyle(localMinPrice, localMaxPrice)}
                    >
                      <span className="price-range-fill" />
                      <input
                        type="range"
                        min="0"
                        max={PRICE_RANGE_MAX}
                        step={PRICE_RANGE_STEP}
                        value={getPriceRangeValue(localMinPrice, 0)}
                        onChange={(e) => handleMinPriceChange(e.target.value, true)}
                        aria-label="Kéo giá tối thiểu"
                      />
                      <input
                        type="range"
                        min="0"
                        max={PRICE_RANGE_MAX}
                        step={PRICE_RANGE_STEP}
                        value={getPriceRangeValue(localMaxPrice, PRICE_RANGE_MAX)}
                        onChange={(e) => handleMaxPriceChange(e.target.value, true)}
                        aria-label="Kéo giá tối đa"
                      />
                    </div>
                    <span>10 triệu</span>
                  </div>

                  <div className="price-input-row">
                    <label className="price-input-card">
                      <span>Giá tối thiểu</span>
                      <div>
                        <input
                          type="text"
                          inputMode="numeric"
                          aria-label="Giá tối thiểu"
                          placeholder="100000"
                          value={formatInputNumber(localMinPrice)}
                          onChange={(e) => handleMinPriceChange(e.target.value)}
                        />
                        <strong>đ</strong>
                      </div>
                    </label>
                    <span className="price-input-separator">-</span>
                    <label className="price-input-card">
                      <span>Giá tối đa</span>
                      <div>
                        <input
                          type="text"
                          inputMode="numeric"
                          aria-label="Giá tối đa"
                          placeholder="2500000"
                          value={formatInputNumber(localMaxPrice)}
                          onChange={(e) => handleMaxPriceChange(e.target.value)}
                        />
                        <strong>đ</strong>
                      </div>
                    </label>
                  </div>

                  {getPriceSuggestions(localMinPrice, localMaxPrice).length > 0 && (
                    <div className="price-suggestion-row" aria-label="Gợi ý giá tiền">
                      {getPriceSuggestions(localMinPrice, localMaxPrice).map((suggestion) => (
                        <button
                          key={suggestion.value}
                          type="button"
                          onClick={() => {
                            if (suggestion.target === 'min') {
                              handleMinPriceChange(suggestion.rawValue, true);
                            } else {
                              handleMaxPriceChange(suggestion.rawValue, true);
                            }
                          }}
                        >
                          {suggestion.value}
                        </button>
                      ))}
                    </div>
                  )}

                  <div className="price-filter-actions">
                    <Button type="button" variant="secondary" onClick={handleClearPriceFilter}>Xóa lọc</Button>
                    <Button type="button" onClick={handleSavePriceFilter}>Lưu</Button>
                  </div>
                </div>
              )}
            </div>

            {/* Area Range */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.area = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'area' ? 'is-open' : ''}`}
                onClick={() => handleToggleSidebarFilter('area')}
                aria-expanded={activeSidebarFilter === 'area'}
              >
                <span>
                  <strong>Diện tích</strong>
                  <small>{formatRangeSummary(localMinArea, localMaxArea, 'area')}</small>
                </span>
                <span className="filter-toggle__chevron">⌄</span>
              </button>
              {activeSidebarFilter === 'area' && (
                <div className="filter-panel" style={sidebarPanelStyle}>
                  <div className="filter-range-inputs">
                    <input
                      type="number"
                      aria-label="Diện tích tối thiểu"
                      placeholder="Từ (m²)"
                      value={localMinArea}
                      onChange={(e) => setLocalMinArea(e.target.value)}
                    />
                    <span className="range-separator">đến</span>
                    <input
                      type="number"
                      aria-label="Diện tích tối đa"
                      placeholder="Đến (m²)"
                      value={localMaxArea}
                      onChange={(e) => setLocalMaxArea(e.target.value)}
                    />
                  </div>
                  <div className="filter-panel-actions">
                    <Button type="button" variant="secondary" onClick={handleClearAreaFilter}>Xóa lọc</Button>
                    <Button type="button" onClick={handleSaveAreaFilter}>Lưu</Button>
                  </div>
                </div>
              )}
            </div>

            {/* Max Occupants capacity */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.occupants = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'occupants' ? 'is-open' : ''}`}
                onClick={() => handleToggleSidebarFilter('occupants')}
                aria-expanded={activeSidebarFilter === 'occupants'}
              >
                <span>
                  <strong>Số người ở</strong>
                  <small>{formatOccupantsSummary(localMinOccupants)}</small>
                </span>
                <span className="filter-toggle__chevron">⌄</span>
              </button>
              {activeSidebarFilter === 'occupants' && (
                <div className="filter-panel" style={sidebarPanelStyle}>
                  <select
                    id="occupants-select"
                    value={localMinOccupants}
                    onChange={(e) => setLocalMinOccupants(e.target.value)}
                    aria-label="Số người ở tối thiểu"
                  >
                    <option value="">-- Bất kỳ --</option>
                    <option value="1">1 người</option>
                    <option value="2">2 người</option>
                    <option value="3">3 người</option>
                    <option value="4">4 người trở lên</option>
                  </select>
                  <div className="filter-panel-actions">
                    <Button type="button" variant="secondary" onClick={handleClearOccupantsFilter}>Xóa lọc</Button>
                    <Button type="button" onClick={handleSaveOccupantsFilter}>Lưu</Button>
                  </div>
                </div>
              )}
            </div>

            {/* House Amenities */}
            {houseAmenitiesList.length > 0 && (
              <div className="filter-group">
                <button
                  type="button"
                  ref={(node) => { filterButtonRefs.current.houseAmenities = node; }}
                  className={`filter-toggle ${activeSidebarFilter === 'houseAmenities' ? 'is-open' : ''}`}
                  onClick={() => handleToggleSidebarFilter('houseAmenities')}
                  aria-expanded={activeSidebarFilter === 'houseAmenities'}
                >
                  <span>
                    <strong>Tiện ích khu trọ</strong>
                    <small>{formatAmenitySummary(localAmenityIds, houseAmenitiesList)}</small>
                  </span>
                  <span className="filter-toggle__chevron">⌄</span>
                </button>
                {activeSidebarFilter === 'houseAmenities' && (
                  <div className="filter-panel" style={sidebarPanelStyle}>
                    <div className="checkbox-list">
                      {houseAmenitiesList.map((a) => (
                        <label key={a.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={localAmenityIds.includes(a.id)}
                            onChange={() => handleAmenityChange(a.id)}
                          />
                          <span>{a.name}</span>
                        </label>
                      ))}
                    </div>
                    <div className="filter-panel-actions">
                      <Button type="button" variant="secondary" onClick={() => handleClearAmenityFilter('house')}>Xóa lọc</Button>
                      <Button type="button" onClick={handleSaveAmenityFilter}>Lưu</Button>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Room Amenities */}
            {roomAmenitiesList.length > 0 && (
              <div className="filter-group">
                <button
                  type="button"
                  ref={(node) => { filterButtonRefs.current.roomAmenities = node; }}
                  className={`filter-toggle ${activeSidebarFilter === 'roomAmenities' ? 'is-open' : ''}`}
                  onClick={() => handleToggleSidebarFilter('roomAmenities')}
                  aria-expanded={activeSidebarFilter === 'roomAmenities'}
                >
                  <span>
                    <strong>Tiện ích phòng trọ</strong>
                    <small>{formatAmenitySummary(localRoomAmenityIds, roomAmenitiesList)}</small>
                  </span>
                  <span className="filter-toggle__chevron">⌄</span>
                </button>
                {activeSidebarFilter === 'roomAmenities' && (
                  <div className="filter-panel" style={sidebarPanelStyle}>
                    <div className="checkbox-list">
                      {roomAmenitiesList.map((a) => (
                        <label key={a.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={localRoomAmenityIds.includes(a.id)}
                            onChange={() => handleRoomAmenityChange(a.id)}
                          />
                          <span>{a.name}</span>
                        </label>
                      ))}
                    </div>
                    <div className="filter-panel-actions">
                      <Button type="button" variant="secondary" onClick={() => handleClearAmenityFilter('room')}>Xóa lọc</Button>
                      <Button type="button" onClick={handleSaveAmenityFilter}>Lưu</Button>
                    </div>
                  </div>
                )}
              </div>
            )}

            <Button type="submit" className="apply-filters-btn">
              Áp dụng bộ lọc
            </Button>
          </form>
        </aside>

        {/* Right column: Results & Map */}
        <main className="search-results-section">
          {/* Collapse Map view for Radius Search */}
          {hasAppliedRadiusSearch && (
            <div className="search-map-container">
              <div ref={mapContainerRef} className="search-leaflet-map" />
            </div>
          )}

          <div className="search-results-header">
            <div>
              {loading ? (
                <div className="results-count">Đang tìm kiếm...</div>
              ) : (
                <div className="results-count">
                  Có <strong>{result?.totalItems ?? 0}</strong> khu trọ phù hợp
                </div>
              )}
              {activeFilters.length > 0 && (
                <div className="active-filter-row">
                  {activeFilters.map((label) => (
                    <span key={label}>{label}</span>
                  ))}
                </div>
              )}
            </div>

            <div className="sort-dropdown-container">
              <label htmlFor="sort-select">Sắp xếp</label>
              <select
                id="sort-select"
                value={query.sortBy ?? 'relevance'}
                onChange={(e) => handleSortChange(e.target.value)}
              >
                <option value="relevance">Mặc định (Độ liên quan)</option>
                <option value="newest">Mới nhất</option>
                <option value="priceAsc">Giá: Thấp đến Cao</option>
                <option value="priceDesc">Giá: Cao đến Thấp</option>
                <option value="areaAsc">Diện tích: Nhỏ đến Lớn</option>
                <option value="areaDesc">Diện tích: Lớn đến Nhỏ</option>
                {hasAppliedRadiusSearch && (
                  <option value="distanceAsc">Khoảng cách gần nhất</option>
                )}
              </select>
            </div>
          </div>

          {loading ? (
            <div className="search-page__state">Đang tải kết quả...</div>
          ) : error ? (
            <div className="search-page__state search-page__state--error">{error}</div>
          ) : !result || result.items.length === 0 ? (
            <div className="search-page__state">Không tìm thấy khu trọ nào phù hợp với bộ lọc của bạn.</div>
          ) : (
            <>
              <div className="search-results__grid">
                {result.items.map((item) => (
                  <button
                    key={item.id}
                    className="search-result-card"
                    type="button"
                    onClick={() =>
                      navigate(`/rooming-houses/${item.id}`, {
                        state: { fromSearch: currentSearchPath },
                      })
                    }
                  >
                    <div className="search-result-card__image-container">
                      {item.coverImageUrl ? (
                        <img src={toAssetUrl(item.coverImageUrl)} alt={item.name} />
                      ) : (
                        <div className="search-result-card__placeholder">Chưa có ảnh</div>
                      )}
                      {item.distanceKm != null && (
                        <span className="distance-badge">
                          Cách {(item.distanceKm).toFixed(1)} km
                        </span>
                      )}
                    </div>

                    <div className="search-result-card__body">
                      <div>
                        <h3>{item.name}</h3>
                        <span className="address-text">{item.addressDisplay}</span>
                      </div>

                      <div className="search-result-card__meta">
                        <span>{item.availableRooms} phòng trống</span>
                        <span className="price-tag">
                          {formatPriceRange(item.minMonthlyRent, item.maxMonthlyRent)}
                        </span>
                        <span>{formatAreaRange(item.minAreaM2, item.maxAreaM2)}</span>
                      </div>

                      {item.amenities.length > 0 && (
                        <div className="search-result-card__amenities">
                          {item.amenities.slice(0, 4).map((a) => (
                            <span key={a.id} className="amenity-tag">
                              {a.name}
                            </span>
                          ))}
                          {item.amenities.length > 4 && (
                            <span className="amenity-tag amenity-tag--more">
                              +{item.amenities.length - 4}
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  </button>
                ))}
              </div>

              {/* Pagination controls */}
              {result.totalPages > 1 && (
                <nav className="search-pagination" aria-label="Pagination">
                  <button
                    className="pagination-btn"
                    disabled={result.page <= 1}
                    onClick={() => handlePageChange(result.page - 1)}
                  >
                    &larr; Trước
                  </button>

                  <div className="pagination-pages">
                    {getVisiblePages(result.page, result.totalPages).map((pageNum) => {
                      return (
                        <button
                          key={pageNum}
                          className={`pagination-page-number ${
                            result.page === pageNum ? 'active' : ''
                          }`}
                          onClick={() => handlePageChange(pageNum)}
                        >
                          {pageNum}
                        </button>
                      );
                    })}
                  </div>

                  <button
                    className="pagination-btn"
                    disabled={result.page >= result.totalPages}
                    onClick={() => handlePageChange(result.page + 1)}
                  >
                    Sau &rarr;
                  </button>
                </nav>
              )}
            </>
          )}
        </main>
      </div>
    </div>
  );
}

function formatRadius(value: number) {
  return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value)} km`;
}

function buildSearchParams(params: URLSearchParams): RoomingHouseSearchParams {
  const centerLat = getNumberParam(params, 'centerLat');
  const centerLng = getNumberParam(params, 'centerLng');
  const hasRadiusMode = centerLat != null && centerLng != null;
  const sortBy = getStringParam(params, 'sortBy');
  const normalizedSort = sortBy && SORT_OPTIONS.has(sortBy) ? sortBy : undefined;
  return {
    q: getStringParam(params, 'q'),
    provinceCode: hasRadiusMode ? undefined : getStringParam(params, 'provinceCode'),
    wardCode: hasRadiusMode ? undefined : getStringParam(params, 'wardCode'),
    minPrice: getNumberParam(params, 'minPrice'),
    maxPrice: getNumberParam(params, 'maxPrice'),
    minAreaM2: getNumberParam(params, 'minAreaM2'),
    maxAreaM2: getNumberParam(params, 'maxAreaM2'),
    minOccupants: getNumberParam(params, 'minOccupants'),
    amenityIds: getNumberArrayParam(params, 'amenityIds'),
    roomAmenityIds: getNumberArrayParam(params, 'roomAmenityIds'),
    centerLat,
    centerLng,
    radiusKm: clampRadiusKm(getNumberParam(params, 'radiusKm') ?? 3),
    sortBy: normalizedSort === 'distanceAsc' && (centerLat == null || centerLng == null) ? 'relevance' : normalizedSort,
    page: Math.max(1, Math.floor(getNumberParam(params, 'page') ?? 1)),
    pageSize: clampPageSize(getNumberParam(params, 'pageSize') ?? DEFAULT_PAGE_SIZE),
  };
}

function paramsToUrl(params: RoomingHouseSearchParams): URLSearchParams {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    if (Array.isArray(value)) {
      value.forEach((v) => {
        search.append(key, String(v));
      });
    } else {
      search.set(key, String(value));
    }
  });
  return search;
}

function preserveNearbyLabel(
  search: URLSearchParams,
  params: RoomingHouseSearchParams,
  label: string
) {
  if (params.centerLat == null || params.centerLng == null) {
    search.delete('nearbyLabel');
    return;
  }

  const normalizedLabel = label.trim();
  if (normalizedLabel) {
    search.set('nearbyLabel', normalizedLabel);
  }
}

function buildLocationButtonLabel(
  query: RoomingHouseSearchParams,
  searchParams: URLSearchParams,
  provinces: Province[],
  wards: Ward[]
) {
  if (query.centerLat != null && query.centerLng != null) {
    const nearbyLabel = searchParams.get('nearbyLabel')?.trim() || query.q?.trim();
    return nearbyLabel ? `Trọ xung quanh ${nearbyLabel}` : 'Trọ xung quanh vị trí đã chọn';
  }

  if (query.provinceCode || query.wardCode) {
    const provinceName = provinces.find((province) => province.code === query.provinceCode)?.name;
    const wardName = wards.find((ward) => ward.code === query.wardCode)?.name;
    const locationParts = [wardName, provinceName].filter(Boolean);
    return locationParts.length > 0 ? locationParts.join(', ') : 'Khu vực đã chọn';
  }

  return 'Khu vực / Xung quanh';
}

function getStringParam(params: URLSearchParams, key: string) {
  const value = params.get(key)?.trim();
  return value ? value : undefined;
}

function getNumberParam(params: URLSearchParams, key: string) {
  const value = params.get(key);
  if (!value) return undefined;
  const number = Number(value);
  return Number.isFinite(number) ? number : undefined;
}

function getNumberArrayParam(params: URLSearchParams, key: string) {
  const values = params.getAll(key).map(Number).filter(Number.isFinite);
  return values.length > 0 ? values : undefined;
}

function clampPageSize(value: number) {
  if (!Number.isFinite(value)) return DEFAULT_PAGE_SIZE;
  return Math.min(48, Math.max(1, Math.floor(value)));
}

function clampRadiusKm(value: number) {
  if (!Number.isFinite(value)) return 3;
  return Math.min(30, Math.max(0.5, value));
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
  }).format(value);
}

function formatPriceRange(min?: number | null, max?: number | null) {
  if (min == null && max == null) return 'Liên hệ giá';
  if (min != null && max != null && min !== max) {
    return `${formatCurrency(min)} - ${formatCurrency(max)}/tháng`;
  }
  return `Từ ${formatCurrency(min ?? max ?? 0)}/tháng`;
}

function formatAreaRange(min?: number | null, max?: number | null) {
  if (min == null && max == null) return 'Chưa cập nhật diện tích';
  if (min != null && max != null && min !== max) {
    return `${formatArea(min)} - ${formatArea(max)}`;
  }
  return `Từ ${formatArea(min ?? max ?? 0)}`;
}

function formatArea(value: number) {
  return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value)} m²`;
}

function formatRangeSummary(minValue: string, maxValue: string, type: 'price' | 'area') {
  const min = parseOptionalNumber(minValue);
  const max = parseOptionalNumber(maxValue);

  if (min == null && max == null) return 'Bất kỳ';

  const formatter = type === 'price' ? formatRentalPriceInput : formatArea;
  if (min != null && max != null) {
    return `Từ ${formatter(min)} đến ${formatter(max)}`;
  }

  if (min != null) return `Từ ${formatter(min)}`;
  return `Đến ${formatter(max ?? 0)}`;
}

function formatOccupantsSummary(value: string) {
  if (!value) return 'Bất kỳ';
  return value === '4' ? 'Từ 4 người trở lên' : `Từ ${value} người`;
}

function formatAmenitySummary(selectedIds: number[], amenities: Amenity[]) {
  if (selectedIds.length === 0) return 'Chưa chọn';

  const selectedNames = selectedIds
    .map((id) => amenities.find((amenity) => amenity.id === id)?.name)
    .filter(Boolean);

  if (selectedNames.length === 0) return `${selectedIds.length} tiện ích`;
  if (selectedNames.length <= 2) return selectedNames.join(', ');
  return `${selectedNames.slice(0, 2).join(', ')} +${selectedNames.length - 2}`;
}

function parseOptionalNumber(value: string) {
  const numericValue = sanitizeNumericInput(value);
  const parsed = Number(numericValue);
  return numericValue && Number.isFinite(parsed) ? parsed : null;
}

function sanitizeNumericInput(value: string) {
  return value.replace(/\D/g, '');
}

function formatInputNumber(value: string) {
  const numericValue = sanitizeNumericInput(value);
  if (!numericValue) return '';
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(Number(numericValue));
}

function formatRentalPriceInput(value: number) {
  const normalizedValue = normalizeRentalPriceInput(value);
  return formatCurrency(normalizedValue);
}

function normalizeRentalPriceInput(value: number) {
  if (!Number.isFinite(value) || value <= 0) return value;
  if (value < 100) return value * 1_000_000;
  if (value < 10_000) return value * 1_000;
  return value;
}

function getPriceRangeValue(value: string, fallback: number) {
  const parsed = parseOptionalNumber(value);
  if (parsed == null) return fallback;
  return Math.min(PRICE_RANGE_MAX, Math.max(0, normalizeRentalPriceInput(parsed)));
}

function normalizePriceInputString(value: string) {
  const parsed = parseOptionalNumber(value);
  if (parsed == null) return '';
  return String(Math.min(PRICE_RANGE_MAX, Math.max(0, normalizeRentalPriceInput(parsed))));
}

function getPriceRangeTrackStyle(minValue: string, maxValue: string) {
  const min = getPriceRangeValue(minValue, 0);
  const max = getPriceRangeValue(maxValue, PRICE_RANGE_MAX);
  const safeMin = Math.min(min, max);
  const safeMax = Math.max(min, max);
  return {
    '--price-range-min': `${(safeMin / PRICE_RANGE_MAX) * 100}%`,
    '--price-range-max': `${(safeMax / PRICE_RANGE_MAX) * 100}%`,
  } as CSSProperties;
}

function getPriceSuggestions(minValue: string, maxValue: string) {
  const suggestions: { target: 'min' | 'max'; rawValue: string; value: string }[] = [];
  const minSuggestion = buildPriceSuggestion(minValue, 'min');
  const maxSuggestion = buildPriceSuggestion(maxValue, 'max');
  if (minSuggestion) suggestions.push(minSuggestion);
  if (maxSuggestion && maxSuggestion.value !== minSuggestion?.value) suggestions.push(maxSuggestion);
  return suggestions;
}

function buildPriceSuggestion(value: string, target: 'min' | 'max') {
  const parsed = parseOptionalNumber(value);
  if (parsed == null || parsed <= 0) return null;

  const normalizedValue = normalizeRentalPriceInput(parsed);
  if (normalizedValue > PRICE_RANGE_MAX) return null;

  return {
    target,
    rawValue: String(normalizedValue),
    value: new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(normalizedValue),
  };
}

function getVisiblePages(currentPage: number, totalPages: number) {
  const start = Math.max(1, currentPage - 2);
  const end = Math.min(totalPages, currentPage + 2);
  return Array.from({ length: end - start + 1 }, (_, index) => start + index);
}

function buildActiveFilterLabels(
  query: RoomingHouseSearchParams,
  provinces: Province[],
  wards: Ward[],
  amenities: Amenity[]
) {
  const labels: string[] = [];
  if (query.q) labels.push(`Từ khóa: ${query.q}`);
  if (query.provinceCode) {
    labels.push(provinces.find((province) => province.code === query.provinceCode)?.name ?? `Tỉnh ${query.provinceCode}`);
  }
  if (query.wardCode) {
    labels.push(wards.find((ward) => ward.code === query.wardCode)?.name ?? `Phường ${query.wardCode}`);
  }
  if (query.minPrice != null || query.maxPrice != null) {
    labels.push(`Giá: ${formatPriceBoundary(query.minPrice, 'từ')} ${formatPriceBoundary(query.maxPrice, 'đến')}`.trim());
  }
  if (query.minAreaM2 != null || query.maxAreaM2 != null) {
    labels.push(`Diện tích: ${query.minAreaM2 ?? '...'}-${query.maxAreaM2 ?? '...'} m²`);
  }
  if (query.minOccupants != null) labels.push(`Từ ${query.minOccupants} người`);
  if (query.radiusKm != null && query.centerLat != null && query.centerLng != null) labels.push(`Bán kính ${query.radiusKm} km`);

  const amenityIds = new Set([...(query.amenityIds ?? []), ...(query.roomAmenityIds ?? [])]);
  amenityIds.forEach((id) => {
    const amenity = amenities.find((item) => item.id === id);
    if (amenity) labels.push(amenity.name);
  });

  return labels.slice(0, 8);
}

function formatPriceBoundary(value: number | undefined, prefix: string) {
  return value == null ? '' : `${prefix} ${formatCurrency(value)}`;
}

function escapeHtml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
