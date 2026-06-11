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
          <div className="search-suggestion-box__tabs">
            <span>Gần đây</span>
            {recentSearches.length > 0 && (
              <button type="button" onClick={clearRecent}>
                Xóa lịch sử
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
                    <span className="search-suggestion-box__icon">↺</span>
                    <span>
                      <strong>{item.query}</strong>
                      <small> Tìm kiếm gần đây</small>
                    </span>
                  </button>
                  <button
                    aria-label={`Xóa ${item.query}`}
                    className="search-suggestion-box__remove"
                    type="button"
                    onClick={(event) => removeRecent(event, item.query)}
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          )}

          {!hasQuery && recentSearches.length === 0 && (
            <p className="search-suggestion-box__empty">Chưa có tìm kiếm gần đây.</p>
          )}

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
                  <span className="search-suggestion-box__icon">
                    {suggestion.kind === 'location' ? '⌖' : '⌕'}
                  </span>
                  <span>
                    <strong>{suggestion.query}</strong>
                    {suggestion.description && <small> {suggestion.description}</small>}
                  </span>
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
