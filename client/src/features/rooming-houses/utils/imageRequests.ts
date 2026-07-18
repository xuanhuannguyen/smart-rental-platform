import type { PropertyImageRequest } from '../types';

type ExistingImage = {
  id: string;
  mediaAssetId?: string | null;
  imageUrl?: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
};

export function toImageRequests(images: ExistingImage[]): PropertyImageRequest[] {
  return images.map((image) => ({
    id: image.id,
    mediaAssetId: image.mediaAssetId,
    imageUrl: image.imageUrl,
    caption: image.caption,
    isCover: image.isCover,
    sortOrder: image.sortOrder,
  }));
}

export function cleanImages(images: PropertyImageRequest[]) {
  return images
    .filter((image) => Boolean(image.mediaAssetId))
    .map((image, index) => ({
      id: image.id,
      mediaAssetId: image.mediaAssetId ?? null,
      caption: image.caption,
      isCover: image.isCover,
      sortOrder: image.sortOrder || index + 1,
    }));
}
