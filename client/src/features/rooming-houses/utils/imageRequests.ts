import type { PropertyImageRequest } from '../types';

type ExistingImage = {
  id: string;
  objectKey: string;
  imageUrl?: string;
  caption?: string | null;
  isCover: boolean;
  sortOrder: number;
};

export function toImageRequests(images: ExistingImage[]): PropertyImageRequest[] {
  return images.map((image) => ({
    id: image.id,
    objectKey: image.objectKey,
    imageUrl: image.imageUrl,
    caption: image.caption,
    isCover: image.isCover,
    sortOrder: image.sortOrder,
  }));
}

export function cleanImages(images: PropertyImageRequest[]) {
  return images
    .filter((image) => image.objectKey.trim().length > 0)
    .map((image, index) => ({
      id: image.id,
      objectKey: image.objectKey.trim(),
      caption: image.caption,
      isCover: image.isCover,
      sortOrder: image.sortOrder || index + 1,
    }));
}
