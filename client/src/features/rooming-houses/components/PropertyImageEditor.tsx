import { useState, useRef } from 'react';
import type { DragEvent } from 'react';
import { uploadImage, type FileUploadScope } from '../../files/api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toPublicAssetUrl } from '../../../shared/api/assets';
import type { PropertyImageRequest } from '../types';
import './RoomingHouseEditor.css';

type PropertyImageEditorProps = {
  images: PropertyImageRequest[];
  scope: FileUploadScope;
  onChange: (images: PropertyImageRequest[]) => void;
  onSave: () => void;
  onSubmit?: () => void;
  saving?: boolean;
};

export default function PropertyImageEditor({
  images,
  scope,
  onChange,
  onSave,
  onSubmit,
  saving,
}: PropertyImageEditorProps) {
  const [uploading, setUploading] = useState(false);
  const [uploadMessage, setUploadMessage] = useState('');
  const [isDragActive, setIsDragActive] = useState(false);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  function removeImage(index: number) {
    const nextImages = images.filter((_, itemIndex) => itemIndex !== index);

    // Automatically set the first image as cover if none is cover
    if (nextImages.length > 0 && !nextImages.some((image) => image.isCover)) {
      nextImages[0] = { ...nextImages[0], isCover: true };
    }

    onChange(nextImages.map((image, itemIndex) => ({ ...image, sortOrder: itemIndex + 1 })));
  }

  function setCover(index: number) {
    onChange(
      images.map((image, itemIndex) => ({
        ...image,
        isCover: itemIndex === index,
      }))
    );
  }

  async function uploadSelectedImages(fileList: FileList | null) {
    const selectedFiles = Array.from(fileList ?? []);
    if (selectedFiles.length === 0) return;

    const remainingSlots = 10 - images.length;
    if (remainingSlots <= 0) {
      setUploadMessage('Bạn đã tải lên tối đa 10 ảnh. Vui lòng xóa bớt ảnh trước khi tải thêm.');
      return;
    }

    let filesToUpload = selectedFiles;
    let limitMessage = '';
    if (selectedFiles.length > remainingSlots) {
      filesToUpload = selectedFiles.slice(0, remainingSlots);
      limitMessage = `Chỉ có thể tải lên thêm ${remainingSlots} ảnh. Đã bỏ qua các ảnh vượt quá giới hạn.`;
    }

    setUploading(true);
    setUploadMessage('');

    try {
      const uploadResults = await Promise.allSettled(
        filesToUpload.map((file) => uploadImage(file, scope))
      );

      const uploadedImages: PropertyImageRequest[] = [];
      uploadResults.forEach((result, index) => {
        if (result.status !== 'fulfilled') return;

        const uploadedMediaAssetId = result.value.mediaAssetId ?? null;
        if (!uploadedMediaAssetId) return;

        uploadedImages.push({
          mediaAssetId: uploadedMediaAssetId,
          imageUrl: result.value.url,
          caption: '',
          isCover: images.length === 0 && index === 0,
          sortOrder: images.length + index + 1,
        });
      });

      if (uploadedImages.length > 0) {
        const nextImages = [...images, ...uploadedImages];
        // Ensure first image is cover
        if (nextImages.length > 0 && !nextImages.some((img) => img.isCover)) {
          nextImages[0].isCover = true;
        }
        onChange(nextImages.map((img, idx) => ({ ...img, sortOrder: idx + 1 })));
      }

      const failedCount = filesToUpload.length - uploadedImages.length;
      if (failedCount > 0) {
        const firstFailure = uploadResults.find((result) => result.status === 'rejected');
        const failureMessage = firstFailure?.status === 'rejected'
          ? getApiErrorMessage(firstFailure.reason, 'Không thể tải ảnh lên.')
          : 'Không thể hoàn tất media asset cho ảnh đã chọn.';
        setUploadMessage(
          `${failedCount} ảnh chưa tải lên được. ${failureMessage}`
        );
      } else if (limitMessage) {
        setUploadMessage(limitMessage);
      }
    } catch (error) {
      setUploadMessage(getApiErrorMessage(error, 'Không thể tải ảnh lên.'));
    } finally {
      setUploading(false);
    }
  }

  function handleDragOver(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragActive(true);
  }

  function handleDragLeave(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragActive(false);
  }

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragActive(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      void uploadSelectedImages(e.dataTransfer.files);
    }
  }

  function triggerFileInput() {
    fileInputRef.current?.click();
  }

  // Card drag and drop handlers
  function handleCardDragStart(e: React.DragEvent<HTMLElement>, index: number) {
    if (uploading) {
      e.preventDefault();
      return;
    }
    e.dataTransfer.setData('text/plain', index.toString());
    setDraggedIndex(index);
  }

  function handleCardDragOver(e: React.DragEvent<HTMLElement>) {
    e.preventDefault();
  }

  function handleCardDrop(e: React.DragEvent<HTMLElement>, targetIndex: number) {
    e.preventDefault();
    
    // Support file dropping on the cards
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      void uploadSelectedImages(e.dataTransfer.files);
      return;
    }

    const sourceIndexStr = e.dataTransfer.getData('text/plain');
    if (sourceIndexStr === '') return;
    const sourceIndex = parseInt(sourceIndexStr, 10);
    
    if (isNaN(sourceIndex) || sourceIndex === targetIndex) return;

    const reorderedImages = [...images];
    const [draggedItem] = reorderedImages.splice(sourceIndex, 1);
    reorderedImages.splice(targetIndex, 0, draggedItem);

    // Reassign cover to the first image in the new order
    const updatedImages = reorderedImages.map((image, idx) => ({
      ...image,
      isCover: idx === 0,
      sortOrder: idx + 1,
    }));

    onChange(updatedImages);
    setDraggedIndex(null);
  }

  return (
    <div className="property-image-editor">
      <header className="property-image-editor__header">
        <h3>Tải ảnh khu trọ</h3>
        <p>Thêm ảnh rõ nét để tăng mức độ tin cậy và giúp người thuê dễ hình dung hơn.</p>
      </header>

      {/* Drag and Drop Zone */}
      <div
        className={`property-image-editor__drag-drop ${isDragActive ? 'drag-active' : ''}`}
        onDragLeave={handleDragLeave}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        onClick={triggerFileInput}
      >
        <div className="drag-drop-icon-wrapper">
          {uploading ? (
            <span className="spinner-loader"></span>
          ) : (
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 16.58A5 5 0 0 0 18 7h-1.26A8 8 0 1 0 4 15.25" />
              <polyline points="8 16 12 12 16 16" />
              <line x1="12" y1="12" x2="12" y2="21" />
            </svg>
          )}
        </div>
        <p className="drag-drop-text">
          Kéo & thả ảnh vào đây hoặc <span className="click-highlight">bấm để tải lên</span>
        </p>
        <p className="drag-drop-subtext">PNG, JPG • Tối đa 10 ảnh</p>
        <button className="drag-drop-btn" type="button" disabled={uploading}>
          <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
            <circle cx="8.5" cy="8.5" r="1.5" />
            <polyline points="21 15 16 10 5 21" />
          </svg>
          Chọn ảnh
        </button>
        <input
          accept="image/jpeg,image/png,image/webp"
          disabled={uploading}
          multiple
          ref={fileInputRef}
          style={{ display: 'none' }}
          type="file"
          onChange={(event) => {
            void uploadSelectedImages(event.target.files);
            event.target.value = '';
          }}
        />
      </div>

      {uploadMessage && <p className="rooming-house-editor__message">{uploadMessage}</p>}

      {/* Grid of Images */}
      <div className="property-image-editor__grid">
        {images.map((image, index) => (
          <article
            className={`property-image-card ${draggedIndex === index ? 'dragging' : ''}`}
            key={(image.id ?? image.mediaAssetId) || `uploaded-image-${index}`}
            draggable
            onDragStart={(e) => handleCardDragStart(e, index)}
            onDragOver={handleCardDragOver}
            onDrop={(e) => handleCardDrop(e, index)}
          >
            <img
              alt={image.caption || 'Ảnh khu trọ'}
              src={toPublicAssetUrl(image.imageUrl)}
            />
            {image.isCover && <span className="property-image-cover-badge">Ảnh bìa</span>}
            <div className="property-image-card-overlay">
              <button
                className={`image-action-btn cover-btn ${image.isCover ? 'active' : ''}`}
                title={image.isCover ? 'Đang là ảnh bìa' : 'Đặt làm ảnh bìa'}
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setCover(index);
                }}
              >
                <svg
                  viewBox="0 0 24 24"
                  width="14"
                  height="14"
                  fill={image.isCover ? 'currentColor' : 'none'}
                  stroke="currentColor"
                  strokeWidth="2.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
                </svg>
              </button>

              <button
                className="image-action-btn delete-btn"
                title="Xóa ảnh"
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  removeImage(index);
                }}
              >
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>

              <button
                className="image-action-btn drag-btn"
                title="Kéo để đổi thứ tự"
                type="button"
                onClick={(e) => e.stopPropagation()}
                style={{ cursor: 'grab' }}
              >
                <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="9" cy="5" r="1.2" />
                  <circle cx="9" cy="12" r="1.2" />
                  <circle cx="9" cy="19" r="1.2" />
                  <circle cx="15" cy="5" r="1.2" />
                  <circle cx="15" cy="12" r="1.2" />
                  <circle cx="15" cy="19" r="1.2" />
                </svg>
              </button>
            </div>
          </article>
        ))}

        {/* Plus Placeholder Card */}
        {images.length < 10 && (
          <div
            className="property-image-card add-placeholder"
            onClick={triggerFileInput}
            onDragOver={handleCardDragOver}
            onDrop={(e) => {
              e.preventDefault();
              if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                void uploadSelectedImages(e.dataTransfer.files);
              }
            }}
          >
            <span className="add-plus-icon">+</span>
            <span>Thêm ảnh</span>
          </div>
        )}
      </div>

      {/* Actions Row */}
      <div className="property-image-actions">
        <button
          className="rooming-house-editor__primary"
          disabled={uploading || saving}
          type="button"
          onClick={onSave}
          style={{ minHeight: '38px', borderRadius: '8px', padding: '8px 24px' }}
        >
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '8px' }}>
            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
            <polyline points="17 21 17 13 7 13 7 21" />
            <polyline points="7 3 7 8 15 8" />
          </svg>
          Lưu
        </button>
      </div>
    </div>
  );
}

