import type { GuestRoomingHouseRecommendationRequest, RoomingHouseSearchParams } from './types';

const RENTAL_BEHAVIOR_KEY = 'srp_guest_rental_behavior';
export const GUEST_RECOMMENDATION_CACHE_KEY = 'srp_home_ai_recommendation_cache_v3';
const MAX_QUERIES = 10;
const MAX_IDS = 30;
const MAX_AMENITIES = 30;
const TTL_MS = 30 * 24 * 60 * 60 * 1000;

export type RentalBehaviorStorage = {
  recentQueries: string[];
  recentRoomingHouseIds: string[];
  clickedRoomingHouseIds: string[];
  preferredAmenityIds: number[];
  preferredRoomAmenityIds: number[];
  provinceCode?: string;
  wardCode?: string;
  minPrice?: number;
  maxPrice?: number;
  minAreaM2?: number;
  maxAreaM2?: number;
  updatedAt: string;
};

export function getRentalBehavior(): RentalBehaviorStorage | null {
  try {
    const rawValue = localStorage.getItem(RENTAL_BEHAVIOR_KEY);
    if (!rawValue) return null;

    const parsed = JSON.parse(rawValue);
    if (!isRentalBehaviorStorage(parsed)) {
      localStorage.removeItem(RENTAL_BEHAVIOR_KEY);
      return null;
    }

    if (Date.now() - new Date(parsed.updatedAt).getTime() > TTL_MS) {
      localStorage.removeItem(RENTAL_BEHAVIOR_KEY);
      return null;
    }

    return parsed;
  } catch {
    localStorage.removeItem(RENTAL_BEHAVIOR_KEY);
    return null;
  }
}

export function hasUsableRentalBehavior() {
  const behavior = getRentalBehavior();
  if (!behavior) return false;

  return behavior.recentQueries.length > 0 ||
    behavior.recentRoomingHouseIds.length > 0 ||
    behavior.clickedRoomingHouseIds.length > 0 ||
    behavior.preferredAmenityIds.length > 0 ||
    behavior.preferredRoomAmenityIds.length > 0 ||
    Boolean(
      behavior.provinceCode ||
      behavior.wardCode ||
      behavior.minPrice ||
      behavior.maxPrice ||
      behavior.minAreaM2 ||
      behavior.maxAreaM2
    );
}

export function saveSearchBehavior(params: RoomingHouseSearchParams) {
  updateBehavior((current) => ({
    ...current,
    recentQueries: params.q ? prependUnique(current.recentQueries, params.q.trim(), MAX_QUERIES) : current.recentQueries,
    preferredAmenityIds: mergeNumbers(current.preferredAmenityIds, params.amenityIds ?? [], MAX_AMENITIES),
    preferredRoomAmenityIds: mergeNumbers(current.preferredRoomAmenityIds, params.roomAmenityIds ?? [], MAX_AMENITIES),
    provinceCode: params.provinceCode ?? current.provinceCode,
    wardCode: params.wardCode ?? current.wardCode,
    minPrice: params.minPrice ?? current.minPrice,
    maxPrice: params.maxPrice ?? current.maxPrice,
    minAreaM2: params.minAreaM2 ?? current.minAreaM2,
    maxAreaM2: params.maxAreaM2 ?? current.maxAreaM2,
  }));
}

export function saveRoomingHouseView(id: string) {
  updateBehavior((current) => ({
    ...current,
    recentRoomingHouseIds: prependUnique(current.recentRoomingHouseIds, id, MAX_IDS),
    clickedRoomingHouseIds: prependUnique(current.clickedRoomingHouseIds, id, MAX_IDS),
  }));
}

export function toGuestRecommendationRequest(pageSize = 8): GuestRoomingHouseRecommendationRequest | null {
  const behavior = getRentalBehavior();
  if (!behavior || !hasUsableRentalBehavior()) return null;

  return {
    recentQueries: behavior.recentQueries,
    recentRoomingHouseIds: behavior.recentRoomingHouseIds,
    clickedRoomingHouseIds: behavior.clickedRoomingHouseIds,
    preferredAmenityIds: behavior.preferredAmenityIds,
    preferredRoomAmenityIds: behavior.preferredRoomAmenityIds,
    provinceCode: behavior.provinceCode,
    wardCode: behavior.wardCode,
    minPrice: behavior.minPrice,
    maxPrice: behavior.maxPrice,
    minAreaM2: behavior.minAreaM2,
    maxAreaM2: behavior.maxAreaM2,
    pageSize,
  };
}

function updateBehavior(updater: (current: RentalBehaviorStorage) => RentalBehaviorStorage) {
  const current = getRentalBehavior() ?? createEmptyBehavior();
  const next = updater(current);
  next.updatedAt = new Date().toISOString();
  localStorage.setItem(RENTAL_BEHAVIOR_KEY, JSON.stringify(next));
  invalidateGuestRecommendationCache();
}

export function invalidateGuestRecommendationCache() {
  try {
    sessionStorage.removeItem(GUEST_RECOMMENDATION_CACHE_KEY);
  } catch {
    // Storage can be unavailable in private mode or blocked browser contexts.
  }
}

function createEmptyBehavior(): RentalBehaviorStorage {
  return {
    recentQueries: [],
    recentRoomingHouseIds: [],
    clickedRoomingHouseIds: [],
    preferredAmenityIds: [],
    preferredRoomAmenityIds: [],
    updatedAt: new Date().toISOString(),
  };
}

function prependUnique(values: string[], value: string, maxItems: number) {
  const normalizedValue = value.trim();
  if (!normalizedValue) return values;

  return [
    normalizedValue,
    ...values.filter((item) => item.toLowerCase() !== normalizedValue.toLowerCase()),
  ].slice(0, maxItems);
}

function mergeNumbers(values: number[], nextValues: number[], maxItems: number) {
  return Array.from(new Set([...nextValues, ...values]))
    .filter((value) => Number.isFinite(value))
    .slice(0, maxItems);
}

function isRentalBehaviorStorage(value: unknown): value is RentalBehaviorStorage {
  if (!value || typeof value !== 'object') return false;
  const item = value as Partial<RentalBehaviorStorage>;

  return Array.isArray(item.recentQueries) &&
    Array.isArray(item.recentRoomingHouseIds) &&
    Array.isArray(item.clickedRoomingHouseIds) &&
    Array.isArray(item.preferredAmenityIds) &&
    Array.isArray(item.preferredRoomAmenityIds) &&
    typeof item.updatedAt === 'string';
}
