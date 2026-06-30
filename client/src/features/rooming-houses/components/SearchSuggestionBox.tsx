import { useEffect, useMemo, useRef, useState, type MouseEvent as ReactMouseEvent } from 'react';
import { getProvinces } from '../../administrative/api';
import type { Province } from '../../administrative/types';
import {
  clearRecentSearches,
  getRecentSearches,
  normalizeText,
  removeRecentSearch,
  saveRecentSearch,
  type RecentSearchItem,
} from '../searchRecentStorage';
import './SearchSuggestionBox.css';

type SearchSuggestionBoxProps = {
  value: string;
  placeholder: string;
  onChange: (value: string) => void;
  onSearch: (query: string) => void;
};

type SuggestionItem = {
  query: string;
  description?: string;
  kind: 'location' | 'template';
};

const DEFAULT_SUGGESTIONS = [
  'Phòng trọ Huế',
  'Phòng trọ Đà Nẵng',
  'Phòng trọ dưới 3 triệu',
  'Trọ có máy lạnh',
  'Trọ gần đại học FPT',
];

const TEMPLATE_SUGGESTIONS = [
  ...DEFAULT_SUGGESTIONS,
  'Phòng trọ Hồ Chí Minh',
  'Phòng trọ Hà Nội',
  'Phòng trọ 2 người',
  'Trọ có ban công',
  'Trọ có chỗ để xe',
  'Trọ có wifi',
];

export default function SearchSuggestionBox({
  value,
  placeholder,
  onChange,
  onSearch,
}: SearchSuggestionBoxProps) {
  const [open, setOpen] = useState(false);
  const [recentSearches, setRecentSearches] = useState<RecentSearchItem[]>([]);
  const [provinces, setProvinces] = useState<Province[]>([]);
  const rootRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    setRecentSearches(getRecentSearches());
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function loadProvinces() {
      try {
        const provinceList = await getProvinces();
        if (!cancelled) setProvinces(provinceList);
      } catch {
        if (!cancelled) setProvinces([]);
      }
    }

    void loadProvinces();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const typedSuggestions = useMemo(
    () => buildTypedSuggestions(value, provinces),
    [value, provinces]
  );
  const defaultSuggestions = DEFAULT_SUGGESTIONS.map<SuggestionItem>((query) => ({
    query,
    description: 'Gợi ý phổ biến',
    kind: 'template',
  }));
  const hasQuery = value.trim().length > 0;
  const visibleSuggestions = hasQuery ? typedSuggestions : defaultSuggestions;

  function refreshRecentSearches() {
    setRecentSearches(getRecentSearches());
  }

  function runSearch(query: string) {
    const trimmed = query.trim();
    if (!trimmed) return;

    const searchUrl = `/search?q=${encodeURIComponent(trimmed)}`;
    saveRecentSearch(trimmed, searchUrl);
    refreshRecentSearches();
    onChange(trimmed);
    setOpen(false);
    onSearch(trimmed);
  }

  function removeRecent(event: ReactMouseEvent<HTMLButtonElement>, query: string) {
    event.stopPropagation();
    removeRecentSearch(query);
    refreshRecentSearches();
  }

  function clearRecent(event: ReactMouseEvent<HTMLButtonElement>) {
    event.stopPropagation();
    clearRecentSearches();
    refreshRecentSearches();
  }

  return (
    <div className="search-suggestion-box" ref={rootRef}>
      <input
        aria-label="Tìm kiếm khu trọ"
        className="search-suggestion-box__input"
        placeholder={placeholder}
        value={value}
        onChange={(event) => {
          onChange(event.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
      />

      {open && (
        <div className="search-suggestion-box__dropdown">
          {/* Recent Section */}
          <div className="search-suggestion-box__tabs">
            <span>Gần đây</span>
            {recentSearches.length > 0 && (
              <button type="button" className="search-suggestion-box__clear-btn" onClick={clearRecent}>
                <span>Xóa lịch sử</span>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="icon-trash">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>
            )}
          </div>

          {!hasQuery && recentSearches.length > 0 && (
            <div className="search-suggestion-box__section">
              {recentSearches.map((item) => (
                <div
                  className="search-suggestion-box__row"
                  key={`${item.query}-${item.createdAt}`}
                >
                  <button
                    className="search-suggestion-box__row-main"
                    type="button"
                    onClick={() => runSearch(item.query)}
                  >
                    <span className="search-suggestion-box__icon">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="icon-clock">
                        <circle cx="12" cy="12" r="10" />
                        <polyline points="12 6 12 12 16 14" />
                      </svg>
                    </span>
                    <span className="search-suggestion-box__text-group">
                      <strong className="search-suggestion-box__query">{item.query}</strong>
                      <span className="search-suggestion-box__row-desc">Tìm kiếm gần đây</span>
                    </span>
                  </button>
                  <button
                    aria-label={`Xóa ${item.query}`}
                    className="search-suggestion-box__remove"
                    type="button"
                    onClick={(event) => removeRecent(event, item.query)}
                  >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="icon-close">
                      <line x1="18" y1="6" x2="6" y2="18" />
                      <line x1="6" y1="6" x2="18" y2="18" />
                    </svg>
                  </button>
                </div>
              ))}
            </div>
          )}

          {!hasQuery && recentSearches.length === 0 && (
            <p className="search-suggestion-box__empty">Chưa có tìm kiếm gần đây.</p>
          )}

          {/* Suggestions Section */}
          <div className="search-suggestion-box__suggestions">
            <h3>{hasQuery ? 'Gợi ý phù hợp' : 'Gợi ý cho bạn'}</h3>
            {visibleSuggestions.length > 0 ? (
              visibleSuggestions.map((suggestion) => (
                <button
                  className="search-suggestion-box__row"
                  key={`${suggestion.kind}-${suggestion.query}`}
                  type="button"
                  onClick={() => runSearch(suggestion.query)}
                >
                  <div className="search-suggestion-box__row-main">
                    <span className="search-suggestion-box__icon">
                      {suggestion.kind === 'location' ? (
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="icon-pin">
                          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                          <circle cx="12" cy="10" r="3" />
                        </svg>
                      ) : (
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="icon-search">
                          <circle cx="11" cy="11" r="8" />
                          <line x1="21" y1="21" x2="16.65" y2="16.65" />
                        </svg>
                      )}
                    </span>
                    <strong className="search-suggestion-box__query">{suggestion.query}</strong>
                  </div>
                  {suggestion.description && (
                    <span className="search-suggestion-box__badge">
                      {suggestion.description}
                    </span>
                  )}
                </button>
              ))
            ) : (
              <p className="search-suggestion-box__empty">Không có gợi ý phù hợp.</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function buildTypedSuggestions(value: string, provinces: Province[]) {
  const normalizedValue = normalizeText(value);
  if (!normalizedValue) return [];

  const locationSuggestions = provinces
    .filter((province) => {
      const normalizedName = normalizeText(province.name);
      const shortName = stripLocationPrefix(normalizedName);
      return normalizedName.includes(normalizedValue) || shortName.includes(normalizedValue);
    })
    .slice(0, 4)
    .map<SuggestionItem>((province) => ({
      query: `Trọ ở ${province.name}`,
      description: 'Theo tỉnh/thành',
      kind: 'location',
    }));

  const templateSuggestions = TEMPLATE_SUGGESTIONS
    .filter((query) => normalizeText(query).includes(normalizedValue))
    .slice(0, 5)
    .map<SuggestionItem>((query) => ({
      query,
      description: 'Mẫu tìm kiếm',
      kind: 'template',
    }));

  const seen = new Set<string>();
  return [...locationSuggestions, ...templateSuggestions]
    .filter((item) => {
      const key = normalizeText(item.query);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice(0, 8);
}

function stripLocationPrefix(value: string) {
  return value
    .replace(/^(thanh pho|tp|tinh)\s+/, '')
    .trim();
}
