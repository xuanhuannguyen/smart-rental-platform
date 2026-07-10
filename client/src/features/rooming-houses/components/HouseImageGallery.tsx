import { useState } from 'react';
import type { PropertyImage } from '../types';
import { toPublicAssetUrl } from '../../../shared/api/assets';
import './HouseImageGallery.css';

type HouseImageGalleryProps = {
  images?: PropertyImage[];
  houseName: string;
};

export default function HouseImageGallery({ images = [], houseName }: HouseImageGalleryProps) {
  const [activeIndex, setActiveIndex] = useState(0);

  if (images.length === 0) {
    return (
      <div className="house-image-gallery-placeholder">
        <span>Chưa có ảnh</span>
      </div>
    );
  }

  const activeImage = images[activeIndex] ?? images[0];

  function handlePrev() {
    setActiveIndex((prev) => (prev === 0 ? images.length - 1 : prev - 1));
  }

  function handleNext() {
    setActiveIndex((prev) => (prev === images.length - 1 ? 0 : prev + 1));
  }

  return (
    <div className="house-image-gallery">
      {/* Main Large Image Container */}
      <div className="house-image-gallery__main">
        <img
          className="house-image-gallery__main-img"
          alt={`${houseName} - Ảnh số ${activeIndex + 1}`}
          src={toPublicAssetUrl(activeImage.imageUrl, activeImage.objectKey)}
          key={activeImage.id} // Changing key forces keyframe animation reload for smooth fade-in
        />

        {/* Index indicator */}
        {images.length > 1 && (
          <span className="house-image-gallery__badge">
            {activeIndex + 1} / {images.length}
          </span>
        )}

        {/* Navigation Arrows */}
        {images.length > 1 && (
          <>
            <button
              className="house-image-gallery__nav-btn house-image-gallery__nav-btn--prev"
              aria-label="Ảnh trước"
              type="button"
              onClick={handlePrev}
            >
              <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="15 18 9 12 15 6" />
              </svg>
            </button>
            <button
              className="house-image-gallery__nav-btn house-image-gallery__nav-btn--next"
              aria-label="Ảnh tiếp theo"
              type="button"
              onClick={handleNext}
            >
              <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </button>
          </>
        )}
      </div>

      {/* Thumbnails strip */}
      {images.length > 1 && (
        <div className="house-image-gallery__thumbnails">
          {images.map((image, index) => (
            <button
              className={`house-image-gallery__thumb-btn ${
                index === activeIndex ? 'house-image-gallery__thumb-btn--active' : ''
              }`}
              key={image.id}
              type="button"
              onClick={() => setActiveIndex(index)}
              aria-label={`Xem ảnh số ${index + 1}`}
            >
              <img alt={`Thumbnail ${index + 1}`} src={toPublicAssetUrl(image.imageUrl, image.objectKey)} />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
