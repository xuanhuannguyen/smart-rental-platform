import { useState, useRef, useEffect, MouseEvent as ReactMouseEvent, TouchEvent as ReactTouchEvent } from 'react';
import { Button } from './Button';

interface AvatarCropperProps {
  imageSrc: string;
  initialZoom?: number;
  initialPosition?: { x: number; y: number };
  onConfirm: (zoom: number, position: { x: number; y: number }) => void;
  onCancel: () => void;
}

export function cropAvatar(imageSrc: string, zoom: number, position: { x: number; y: number }): Promise<File> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    
    let finalSrc = imageSrc;
    // Apply crossOrigin and cache buster only for remote/external URLs, not blob/data URLs
    if (!imageSrc.startsWith('blob:') && !imageSrc.startsWith('data:')) {
      img.crossOrigin = 'anonymous';
      finalSrc = `${imageSrc}${imageSrc.includes('?') ? '&' : '?'}t=${Date.now()}`;
    }
    
    img.src = finalSrc;
    img.onload = () => {
      const canvas = document.createElement('canvas');
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        reject(new Error('Could not get canvas context'));
        return;
      }

      canvas.width = 300;
      canvas.height = 300;

      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, 300, 300);

      const naturalWidth = img.naturalWidth;
      const naturalHeight = img.naturalHeight;

      const aspect = naturalWidth / naturalHeight;
      const renderedWidth = aspect > 1 ? 320 * aspect : 320;

      const drawScale = (renderedWidth / naturalWidth) * zoom * 1.5;

      const dx = 150 + position.x * 1.5 - (naturalWidth / 2) * drawScale;
      const dy = 150 + position.y * 1.5 - (naturalHeight / 2) * drawScale;

      ctx.save();
      ctx.translate(dx, dy);
      ctx.scale(drawScale, drawScale);
      ctx.drawImage(img, 0, 0);
      ctx.restore();

      canvas.toBlob(
        (blob) => {
          if (blob) {
            const file = new File([blob], 'avatar.jpg', { type: 'image/jpeg' });
            resolve(file);
          } else {
            reject(new Error('Canvas toBlob failed'));
          }
        },
        'image/jpeg',
        0.9
      );
    };
    img.onerror = () => {
      reject(new Error('Failed to load image for cropping'));
    };
  });
}

export function AvatarCropper({ imageSrc, initialZoom, initialPosition, onConfirm, onCancel }: AvatarCropperProps) {
  const [zoom, setZoom] = useState(initialZoom ?? 1);
  const [position, setPosition] = useState(initialPosition ?? { x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const dragStart = useRef({ x: 0, y: 0 });
  const containerRef = useRef<HTMLDivElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);

  // Reset states when image source changes
  useEffect(() => {
    setZoom(initialZoom ?? 1);
    setPosition(initialPosition ?? { x: 0, y: 0 });
  }, [imageSrc, initialZoom, initialPosition]);

  const handleMouseDown = (e: ReactMouseEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(true);
    dragStart.current = {
      x: e.clientX - position.x,
      y: e.clientY - position.y
    };
  };

  const handleMouseMove = (e: MouseEvent) => {
    if (!isDragging) return;
    setPosition({
      x: e.clientX - dragStart.current.x,
      y: e.clientY - dragStart.current.y
    });
  };

  const handleMouseUp = () => {
    setIsDragging(false);
  };

  useEffect(() => {
    if (isDragging) {
      window.addEventListener('mousemove', handleMouseMove);
      window.addEventListener('mouseup', handleMouseUp);
    }
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isDragging, position]);

  // Touch support for mobile devices
  const handleTouchStart = (e: ReactTouchEvent<HTMLDivElement>) => {
    if (e.touches.length !== 1) return;
    setIsDragging(true);
    dragStart.current = {
      x: e.touches[0].clientX - position.x,
      y: e.touches[0].clientY - position.y
    };
  };

  const handleTouchMove = (e: ReactTouchEvent<HTMLDivElement>) => {
    if (!isDragging || e.touches.length !== 1) return;
    setPosition({
      x: e.touches[0].clientX - dragStart.current.x,
      y: e.touches[0].clientY - dragStart.current.y
    });
  };

  const handleTouchEnd = () => {
    setIsDragging(false);
  };

  const handleConfirm = () => {
    onConfirm(zoom, position);
  };

  return (
    <div className="cropper-modal-overlay">
      <div className="cropper-modal-content">
        <h3>Chỉnh sửa ảnh đại diện</h3>
        <p className="cropper-desc">Kéo để di chuyển ảnh, sử dụng thanh trượt để phóng to/thu nhỏ sao cho khớp với hình tròn.</p>
        
        <div 
          className="cropper-viewport-container" 
          ref={containerRef}
          onMouseDown={handleMouseDown}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
        >
          <img
            ref={imgRef}
            src={imageSrc}
            crossOrigin="anonymous"
            alt="To crop"
            className="cropper-source-img"
            style={{
              transform: `translate(${position.x}px, ${position.y}px) scale(${zoom})`,
              cursor: isDragging ? 'grabbing' : 'grab'
            }}
          />
          <div className="cropper-viewport-circle"></div>
        </div>

        <div className="cropper-controls">
          <span className="slider-label">Thu nhỏ</span>
          <input
            type="range"
            min="0.1"
            max="2"
            step="0.01"
            value={zoom}
            onChange={(e) => setZoom(parseFloat(e.target.value))}
            className="cropper-zoom-slider"
          />
          <span className="slider-label">Phóng to</span>
        </div>

        <div className="cropper-actions">
          <Button type="button" onClick={handleConfirm}>
            Xác nhận ảnh
          </Button>
          <Button type="button" variant="secondary" onClick={onCancel}>
            Hủy bỏ
          </Button>
        </div>
      </div>
    </div>
  );
}
