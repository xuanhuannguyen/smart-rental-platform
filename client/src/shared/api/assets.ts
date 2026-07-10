import { env } from '../../config/env';

// Generic asset helper for legacy/object-key-based flows and explicit API paths.
export function toAssetUrl(objectKeyOrUrl: string): string {
  if (!objectKeyOrUrl) return '';
  const trimmed = objectKeyOrUrl.trim();
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') return '';

  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  if (trimmed.startsWith('/api/')) return `${env.apiBaseUrl}${trimmed}`;
  if (trimmed.startsWith('/uploads/')) return `${env.apiBaseUrl}${trimmed}`;
  if (trimmed.startsWith('uploads/')) return `${env.apiBaseUrl}/${trimmed}`;

  return `${env.apiBaseUrl}/uploads/${trimmed.replace(/^\/+/, '')}`;
}

// Public property image helper for the media-core migration path.
export function toPublicAssetUrl(imageUrl?: string | null, objectKey?: string | null): string {
  const safeImageUrl = normalizeOptionalValue(imageUrl);
  if (safeImageUrl) {
    return toAssetUrl(safeImageUrl);
  }

  const safeObjectKey = normalizeOptionalValue(objectKey);
  if (!safeObjectKey) {
    return '';
  }

  const normalizedObjectKey = safeObjectKey.replace(/\\/g, '/').replace(/^\/+/, '');
  if (normalizedObjectKey.startsWith('public/')) {
    return `${env.apiBaseUrl}/api/media/public/${normalizedObjectKey}`;
  }

  return toAssetUrl(normalizedObjectKey);
}

function normalizeOptionalValue(value?: string | null): string {
  const trimmed = value?.trim() ?? '';
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') {
    return '';
  }

  return trimmed;
}
