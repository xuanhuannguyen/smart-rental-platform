import { env } from '../../config/env';
import { buildPrivateMediaViewUrl } from './media';

export type PublicPropertyImageSource = {
  imageUrl?: string | null;
};

export type AvatarImageSource = {
  avatarUrl?: string | null;
  avatarMediaAssetId?: string | null;
};

export function toAssetUrl(assetUrl: string): string {
  if (!assetUrl) return '';
  const trimmed = assetUrl.trim();
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') return '';

  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  if (trimmed.startsWith('/api/')) return `${env.apiBaseUrl}${trimmed}`;

  return '';
}

export function toPublicAssetUrl(imageUrl?: string | null): string {
  const safeImageUrl = normalizeOptionalValue(imageUrl);
  if (safeImageUrl) {
    return toAssetUrl(safeImageUrl);
  }

  return '';
}

// Public listing/card images only need one field today, but we keep a dedicated helper
// so future public-image routing changes do not leak back into shared UI code.
export function toPublicListingImageUrl(imageUrl?: string | null): string {
  return toPublicAssetUrl(imageUrl);
}

export function toPublicPropertyImageUrl(image: PublicPropertyImageSource | null | undefined): string {
  if (!image) {
    return '';
  }

  return toPublicAssetUrl(image.imageUrl);
}

export function toAvatarImageUrl(avatar?: string | null | AvatarImageSource): string {
  const safeAvatarUrl = typeof avatar === 'string'
    ? normalizeOptionalValue(avatar)
    : normalizeOptionalValue(avatar?.avatarUrl);

  return safeAvatarUrl ? toAssetUrl(safeAvatarUrl) : '';
}

export function toPrivateMediaAssetUrl(mediaAssetId?: string | null): string {
  const safeMediaAssetId = normalizeOptionalValue(mediaAssetId);
  return safeMediaAssetId ? buildPrivateMediaViewUrl(safeMediaAssetId) : '';
}

function normalizeOptionalValue(value?: string | null): string {
  const trimmed = value?.trim() ?? '';
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') {
    return '';
  }

  return trimmed;
}
