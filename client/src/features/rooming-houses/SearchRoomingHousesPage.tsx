import { useEffect, useMemo, useState, useRef, type CSSProperties, type FormEvent } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { ROUTE_PATHS } from '../../app/router/routePaths';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { toPublicListingImageUrl } from '../../shared/api/assets';
import { Button } from '../../shared/components/ui/Button';
import { HomeHeader } from '../../shared/components/layout/HomeHeader';
import { searchPublicRoomingHouses, getAmenities, searchLocationAddress, suggestLocationAddresses } from './api';
import { getProvinces, getWardsByProvince } from '../administrative/api';
import type { Province, Ward } from '../administrative/types';
import type { PagedResult, RoomingHouseSearchItem, RoomingHouseSearchParams, Amenity, LocationSuggestion } from './types';
import { env } from '../../config/env';
import SearchSuggestionBox from './components/SearchSuggestionBox';
import { LocationFilterPanel } from './components/LocationFilterPanel';
import RentalAiChatbot from './components/RentalAiChatbot';
import { saveRecentSearch } from './searchRecentStorage';
import './SearchRoomingHousesPage.css';

const DEFAULT_PAGE_SIZE = 12;
const PRICE_RANGE_MAX = 30_000_000;
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
  const [searchParams, setSearchParams] = useSearchParams();
  const currentSearchPath = `${location.pathname}${location.search}`;
  const query = useMemo(() => buildSearchParams(searchParams), [searchParams]);
  const searchCacheKey = useMemo(() => paramsToUrl(query).toString(), [query]);
  const nearbyLabelParam = searchParams.get('nearbyLabel')?.trim() ?? '';
  
  const [result, setResult] = useState<PagedResult<RoomingHouseSearchItem> | null>(
    () => searchResultCache.get(searchCacheKey) ?? null
  );
  const [loading, setLoading] = useState(() => !searchResultCache.has(searchCacheKey));
  const [error, setError] = useState('');
  const restoredSearchPathRef = useRef<string | null>(null);

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

  // Handle clicking outside location panel
  const locationPanelRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        activeLocationPanel != null &&
        locationPanelRef.current &&
        !locationPanelRef.current.contains(event.target as Node)
      ) {
        setActiveLocationPanel(null);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [activeLocationPanel]);

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
  }, [query]);

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

  function handleClearLocationFilters() {
    setLocalProvinceCode('');
    setLocalWardCode('');
    setLocalCenterLat(null);
    setLocalCenterLng(null);
    setLocalRadiusKm(3);
    setShowRadiusSearch(false);

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
    setSearchParams(newSearchParams);
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

  function handleLocationApply(filters: {
    provinceCode: string;
    wardCode: string;
    centerLat: number | null;
    centerLng: number | null;
    radiusKm: number;
    address: string;
  }) {
    const params = new URLSearchParams(location.search);
    if (filters.centerLat != null && filters.centerLng != null) {
      params.set('centerLat', filters.centerLat.toString());
      params.set('centerLng', filters.centerLng.toString());
      params.set('radiusKm', filters.radiusKm.toString());
      params.set('nearbyLabel', filters.address);
      params.delete('provinceCode');
      params.delete('wardCode');
    } else {
      if (filters.provinceCode) {
        params.set('provinceCode', filters.provinceCode);
      } else {
        params.delete('provinceCode');
      }
      if (filters.wardCode) {
        params.set('wardCode', filters.wardCode);
      } else {
        params.delete('wardCode');
      }
      params.delete('centerLat');
      params.delete('centerLng');
      params.delete('radiusKm');
      params.delete('nearbyLabel');
    }
    params.set('page', '1');
    setSearchParams(params);
    setActiveLocationPanel(null);
  }

  function handleLocationClear() {
    const params = new URLSearchParams(location.search);
    params.delete('provinceCode');
    params.delete('wardCode');
    params.delete('centerLat');
    params.delete('centerLng');
    params.delete('radiusKm');
    params.delete('nearbyLabel');
    params.set('page', '1');
    setSearchParams(params);
    setActiveLocationPanel(null);
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
      <HomeHeader
        centerContent={
          <>
            <form className="search-header__search-form" onSubmit={handleApplyFilters}>
              <svg className="search-form-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="11" cy="11" r="8"></circle>
                <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              </svg>
              <SearchSuggestionBox
                placeholder="VD: gần đại học FPT dưới 3tr có máy lạnh"
                value={searchQuery}
                onChange={setSearchQuery}
                onSearch={handleSuggestionSearch}
              />
              <button type="submit" className="search-submit-btn">Tìm</button>
            </form>
            <div className="search-location-actions" ref={locationPanelRef}>
              <button
                type="button"
                className={`search-location-action ${ (hasAppliedLocationSearch || activeLocationPanel) ? 'is-active' : ''}`}
                onClick={handleOpenLocationPanel}
                aria-expanded={activeLocationPanel != null}
              >
                <svg className="location-pin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
                <span className="location-btn-label">{locationButtonLabel}</span>
                <svg className="location-chevron-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ transform: activeLocationPanel ? 'rotate(180deg)' : 'none', transition: 'transform 0.2s' }}>
                  <polyline points="6 9 12 15 18 9" />
                </svg>
              </button>

              {activeLocationPanel && (
                <LocationFilterPanel
                  initialProvinceCode={query.provinceCode ?? ''}
                  initialWardCode={query.wardCode ?? ''}
                  initialRadiusKm={query.radiusKm ?? 5}
                  initialAddress={nearbyLabelParam}
                  initialLatitude={query.centerLat ?? null}
                  initialLongitude={query.centerLng ?? null}
                  initialTab={activeLocationPanel === 'nearby' ? 'nearby' : 'area'}
                  onClose={() => setActiveLocationPanel(null)}
                  onApply={handleLocationApply}
                  onClear={handleLocationClear}
                />
              )}
            </div>
          </>
        }
      />

      <div className="search-page-container">
        {/* Left column: Filters Sidebar */}
        <aside className="search-filters-sidebar" ref={filtersSidebarRef}>
          <div className="search-filters-header">
            <h2>Bộ lọc nâng cao</h2>
            <button type="button" className="search-clear-btn" onClick={handleClearFilters}>
              <span>Xóa tất cả</span>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="3 6 5 6 21 6" />
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
              </svg>
            </button>
          </div>

          <form className="search-filters-form" onSubmit={handleApplyFilters}>
            {/* Price Range */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.price = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'price' ? 'is-open' : ''} ${(localMinPrice || localMaxPrice) ? 'has-value' : ''}`}
                onClick={() => handleToggleSidebarFilter('price')}
                aria-expanded={activeSidebarFilter === 'price'}
              >
                <span className="filter-toggle-content">
                  <div className="filter-toggle-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="12" y1="1" x2="12" y2="23"></line>
                      <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
                    </svg>
                  </div>
                  <span className="filter-toggle-text">
                    <strong>Giá thuê</strong>
                    <small>{formatRangeSummary(localMinPrice, localMaxPrice, 'price')}</small>
                  </span>
                </span>
                <svg className="filter-toggle__chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="6 9 12 15 18 9" />
                </svg>
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
                    <span>30 triệu</span>
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
                    <Button type="button" variant="secondary" onClick={handleClearPriceFilter} className="btn-clear-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <polyline points="3 6 5 6 21 6" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                      </svg>
                      <span>Xóa lọc</span>
                    </Button>
                    <Button type="button" onClick={handleSavePriceFilter} className="btn-save-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                        <polyline points="17 21 17 13 7 13 7 21" />
                        <polyline points="7 3 7 8 15 8" />
                      </svg>
                      <span>Lưu</span>
                    </Button>
                  </div>
                </div>
              )}
            </div>

            {/* Area Range */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.area = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'area' ? 'is-open' : ''} ${(localMinArea || localMaxArea) ? 'has-value' : ''}`}
                onClick={() => handleToggleSidebarFilter('area')}
                aria-expanded={activeSidebarFilter === 'area'}
              >
                <span className="filter-toggle-content">
                  <div className="filter-toggle-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="4 8 4 4 8 4" />
                      <polyline points="16 4 20 4 20 8" />
                      <polyline points="20 16 20 20 16 20" />
                      <polyline points="8 20 4 20 4 16" />
                    </svg>
                  </div>
                  <span className="filter-toggle-text">
                    <strong>Diện tích</strong>
                    <small>{formatRangeSummary(localMinArea, localMaxArea, 'area')}</small>
                  </span>
                </span>
                <svg className="filter-toggle__chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="6 9 12 15 18 9" />
                </svg>
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
                    <Button type="button" variant="secondary" onClick={handleClearAreaFilter} className="btn-clear-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <polyline points="3 6 5 6 21 6" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                      </svg>
                      <span>Xóa lọc</span>
                    </Button>
                    <Button type="button" onClick={handleSaveAreaFilter} className="btn-save-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                        <polyline points="17 21 17 13 7 13 7 21" />
                        <polyline points="7 3 7 8 15 8" />
                      </svg>
                      <span>Lưu</span>
                    </Button>
                  </div>
                </div>
              )}
            </div>

            {/* Max Occupants capacity */}
            <div className="filter-group">
              <button
                type="button"
                ref={(node) => { filterButtonRefs.current.occupants = node; }}
                className={`filter-toggle ${activeSidebarFilter === 'occupants' ? 'is-open' : ''} ${localMinOccupants ? 'has-value' : ''}`}
                onClick={() => handleToggleSidebarFilter('occupants')}
                aria-expanded={activeSidebarFilter === 'occupants'}
              >
                <span className="filter-toggle-content">
                  <div className="filter-toggle-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                      <circle cx="9" cy="7" r="4" />
                      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                    </svg>
                  </div>
                  <span className="filter-toggle-text">
                    <strong>Số người ở</strong>
                    <small>{formatOccupantsSummary(localMinOccupants)}</small>
                  </span>
                </span>
                <svg className="filter-toggle__chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="6 9 12 15 18 9" />
                </svg>
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
                    <Button type="button" variant="secondary" onClick={handleClearOccupantsFilter} className="btn-clear-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <polyline points="3 6 5 6 21 6" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                      </svg>
                      <span>Xóa lọc</span>
                    </Button>
                    <Button type="button" onClick={handleSaveOccupantsFilter} className="btn-save-filter">
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                        <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                        <polyline points="17 21 17 13 7 13 7 21" />
                        <polyline points="7 3 7 8 15 8" />
                      </svg>
                      <span>Lưu</span>
                    </Button>
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
                  className={`filter-toggle ${activeSidebarFilter === 'houseAmenities' ? 'is-open' : ''} ${localAmenityIds.length > 0 ? 'has-value' : ''}`}
                  onClick={() => handleToggleSidebarFilter('houseAmenities')}
                  aria-expanded={activeSidebarFilter === 'houseAmenities'}
                >
                  <span className="filter-toggle-content">
                    <div className="filter-toggle-icon">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
                        <line x1="9" y1="22" x2="9" y2="16" />
                        <line x1="15" y1="22" x2="15" y2="16" />
                        <line x1="9" y1="16" x2="15" y2="16" />
                        <path d="M8 6h2M14 6h2M8 10h2M14 10h2" />
                      </svg>
                    </div>
                    <span className="filter-toggle-text">
                      <strong>Tiện ích khu trọ</strong>
                      <small>{formatAmenitySummary(localAmenityIds, houseAmenitiesList)}</small>
                    </span>
                  </span>
                  <svg className="filter-toggle__chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
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
                      <Button type="button" variant="secondary" onClick={() => handleClearAmenityFilter('house')} className="btn-clear-filter">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                          <polyline points="3 6 5 6 21 6" />
                          <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                        </svg>
                        <span>Xóa lọc</span>
                      </Button>
                      <Button type="button" onClick={handleSaveAmenityFilter} className="btn-save-filter">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                          <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                          <polyline points="17 21 17 13 7 13 7 21" />
                          <polyline points="7 3 7 8 15 8" />
                        </svg>
                        <span>Lưu</span>
                      </Button>
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
                  className={`filter-toggle ${activeSidebarFilter === 'roomAmenities' ? 'is-open' : ''} ${localRoomAmenityIds.length > 0 ? 'has-value' : ''}`}
                  onClick={() => handleToggleSidebarFilter('roomAmenities')}
                  aria-expanded={activeSidebarFilter === 'roomAmenities'}
                >
                  <span className="filter-toggle-content">
                    <div className="filter-toggle-icon">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M2 4v16" />
                        <path d="M2 11h20" />
                        <path d="M22 4v16" />
                        <path d="M2 17h20" />
                        <path d="M4 8h14a2 2 0 0 1 2 2v7H4V10a2 2 0 0 1 2-2z" />
                      </svg>
                    </div>
                    <span className="filter-toggle-text">
                      <strong>Tiện ích phòng trọ</strong>
                      <small>{formatAmenitySummary(localRoomAmenityIds, roomAmenitiesList)}</small>
                    </span>
                  </span>
                  <svg className="filter-toggle__chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="6 9 12 15 18 9" />
                  </svg>
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
                      <Button type="button" variant="secondary" onClick={() => handleClearAmenityFilter('room')} className="btn-clear-filter">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                          <polyline points="3 6 5 6 21 6" />
                          <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                        </svg>
                        <span>Xóa lọc</span>
                      </Button>
                      <Button type="button" onClick={handleSaveAmenityFilter} className="btn-save-filter">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="btn-icon">
                          <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                          <polyline points="17 21 17 13 7 13 7 21" />
                          <polyline points="7 3 7 8 15 8" />
                        </svg>
                        <span>Lưu</span>
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            )}

            <Button type="submit" className="apply-filters-btn">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="apply-filters-icon">
                <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
              </svg>
              <span>Áp dụng bộ lọc</span>
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
                        <img src={toPublicListingImageUrl(item.coverImageUrl)} alt={item.name} />
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
                      <h3 className="search-result-card__title">{item.name}</h3>
                      <p className="search-result-card__address">
                        <svg className="address-pin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                          <circle cx="12" cy="10" r="3" />
                        </svg>
                        <span>{item.addressDisplay}</span>
                      </p>

                      <div className="card-badges-grid">
                        <span className="card-badge badge-blue">
                          <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M13 4h3a2 2 0 0 1 2 2v14M2 20h20M5 4h8v16H5z" />
                          </svg>
                          <span>{item.availableRooms} phòng trống</span>
                        </span>
                        
                        <span className="card-badge badge-orange">
                          <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M12 2H2v10l9.29 9.29a1 1 0 0 0 1.41 0l7.29-7.29a1 1 0 0 0 0-1.41L12 2z" />
                            <circle cx="5" cy="5" r="1.5" fill="currentColor" />
                          </svg>
                          <span>{formatPriceRange(item.minMonthlyRent, item.maxMonthlyRent)}</span>
                        </span>

                        <span className="card-badge badge-green">
                          <svg className="badge-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <rect x="3" y="3" width="18" height="18" rx="2" />
                            <path d="M9 3v18M15 3v18M3 9h18M3 15h18" />
                          </svg>
                          <span>{formatAreaRange(item.minAreaM2, item.maxAreaM2)}</span>
                        </span>
                      </div>

                      {item.amenities && item.amenities.length > 0 && (
                        <>
                          <hr className="search-card-divider" />
                          <div className="search-card-amenities-list">
                            {item.amenities.slice(0, 3).map((a) => (
                              <span key={a.id} className="search-card-amenity-item">
                                {getAmenityIcon(a.name)}
                                <span>{a.name}</span>
                              </span>
                            ))}
                          </div>
                        </>
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
      <RentalAiChatbot context="search" />
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
        <polyline points="22 17 13.5 8.5 8.5 13.5 2 7" />
        <polyline points="16 17 22 17 22 11" />
      </svg>
    );
  }
  if (normalized.includes('máy giặt') || normalized.includes('giặt')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M3 6V3a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v3M3 6v15a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1V6M3 6h18" />
        <circle cx="12" cy="14" r="4" />
        <path d="M12 12a2.5 2.5 0 0 0 0 4" />
      </svg>
    );
  }
  if (normalized.includes('tủ lạnh')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <rect x="5" y="2" width="14" height="20" rx="2" ry="2" />
        <line x1="5" y1="10" x2="19" y2="10" />
        <line x1="9" y1="6" x2="9" y2="8" />
        <line x1="9" y1="14" x2="9" y2="18" />
      </svg>
    );
  }
  if (normalized.includes('bếp') || normalized.includes('nấu ăn')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6 18h12M6 6h12M6 10h12M6 14h12" />
      </svg>
    );
  }
  if (normalized.includes('wc riêng') || normalized.includes('khép kín') || normalized.includes('vệ sinh riêng') || normalized.includes('phòng tắm')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M4 4h16v16H4zM10 8h4v4h-4zM6 16h12" />
      </svg>
    );
  }
  if (normalized.includes('ban công') || normalized.includes('cửa sổ')) {
    return (
      <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M3 3h18v18H3zM9 9h6v12H9z" />
      </svg>
    );
  }
  // Default fallback icon
  return (
    <svg className="amenity-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="16" />
      <line x1="8" y1="12" x2="12" y2="12" />
    </svg>
  );
}
