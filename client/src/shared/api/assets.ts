import { env } from '../../config/env';

export type PublicPropertyImageSource = {
  imageUrl?: string | null;
  objectKey?: string | null;
};

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

// Public listing/card images only need one field today, but we keep a dedicated helper
// so future public-image routing changes do not leak back into shared UI code.
export function toPublicListingImageUrl(imageUrl?: string | null): string {
  return toPublicAssetUrl(imageUrl, imageUrl);
}

export function toPublicPropertyImageUrl(image: PublicPropertyImageSource | null | undefined): string {
  if (!image) {
    return '';
  }

  return toPublicAssetUrl(image.imageUrl, image.objectKey);
}

export function toAvatarImageUrl(avatarUrl?: string | null): string {
  const safeAvatarUrl = normalizeOptionalValue(avatarUrl);
  return safeAvatarUrl ? toAssetUrl(safeAvatarUrl) : '';
}

function normalizeOptionalValue(value?: string | null): string {
  const trimmed = value?.trim() ?? '';
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') {
    return '';
  }

  return trimmed;
}
