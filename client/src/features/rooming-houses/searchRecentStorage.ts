const RECENT_SEARCH_KEY = 'srp_recent_searches';
const MAX_RECENT_SEARCHES = 10;

export interface RecentSearchItem {
  query: string;
  searchUrl: string;
  createdAt: string;
}

export function getRecentSearches(): RecentSearchItem[] {
  try {
    const rawValue = localStorage.getItem(RECENT_SEARCH_KEY);
    if (!rawValue) return [];

    const parsed = JSON.parse(rawValue);
    if (!Array.isArray(parsed)) return [];

    return parsed
      .filter(isRecentSearchItem)
      .slice(0, MAX_RECENT_SEARCHES);
  } catch {
    return [];
  }
}

export function saveRecentSearch(query: string, searchUrl: string) {
  const normalizedQuery = query.trim();
  if (!normalizedQuery) return;

  const nextSearches = [
    {
      query: normalizedQuery,
      searchUrl,
      createdAt: new Date().toISOString(),
    },
    ...getRecentSearches().filter(
      (item) => normalizeText(item.query) !== normalizeText(normalizedQuery)
    ),
  ].slice(0, MAX_RECENT_SEARCHES);

  localStorage.setItem(RECENT_SEARCH_KEY, JSON.stringify(nextSearches));
}

export function removeRecentSearch(query: string) {
  const normalizedQuery = normalizeText(query);
  const nextSearches = getRecentSearches().filter(
    (item) => normalizeText(item.query) !== normalizedQuery
  );
  localStorage.setItem(RECENT_SEARCH_KEY, JSON.stringify(nextSearches));
}

export function clearRecentSearches() {
  localStorage.removeItem(RECENT_SEARCH_KEY);
}

export function normalizeText(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

function isRecentSearchItem(value: unknown): value is RecentSearchItem {
  if (!value || typeof value !== 'object') return false;
  const item = value as Partial<RecentSearchItem>;
  return typeof item.query === 'string' &&
    typeof item.searchUrl === 'string' &&
    typeof item.createdAt === 'string';
}
