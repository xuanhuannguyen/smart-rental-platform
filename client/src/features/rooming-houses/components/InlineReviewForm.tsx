import React, { useState, useRef, useEffect } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toPublicPropertyImageUrl } from '../../../shared/api/assets';
import { Toast } from '../../../shared/components/ui/Toast';
import { createRoomingHouseReview, updateRoomingHouseReview } from '../api';
import type { RoomingHouseReviewResponse, PropertyImageRequest } from '../types';
import './InlineReviewForm.css';

const MAX_REVIEW_IMAGES = 4;

interface InlineReviewFormProps {
  mode: 'create' | 'edit';
  contractId?: string; // required for create
  review?: RoomingHouseReviewResponse; // required for edit
  onSuccess: (review: RoomingHouseReviewResponse) => void;
  onCancel?: () => void;
  disabled?: boolean;
  disabledReason?: string;
  avatarUrl?: string; // current user avatar
  displayName?: string;
  hideAvatar?: boolean;
}

export const InlineReviewForm: React.FC<InlineReviewFormProps> = ({
  mode,
  contractId,
  review,
  onSuccess,
  onCancel,
  disabled = false,
  disabledReason,
  avatarUrl,
  displayName,
  hideAvatar = false,
}) => {
  const [rating, setRating] = useState<number>(review?.rating || 0);
  const [comment, setComment] = useState<string>(review?.comment || '');
  const [isFocused, setIsFocused] = useState(false);

  // For edit mode: track which old images are kept
  const [retainedImages, setRetainedImages] = useState<PropertyImageRequest[]>(
    mode === 'edit' && review ? review.images : []
  );

  // For new images
  const [newImages, setNewImages] = useState<File[]>([]);
  const [newImagePreviews, setNewImagePreviews] = useState<string[]>([]);


  const [isSubmitting, setIsSubmitting] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    // Generate previews for new images
    const previews = newImages.map((file) => URL.createObjectURL(file));
    setNewImagePreviews(previews);

    // Cleanup URLs when component unmounts or images change
    return () => {
      previews.forEach((url) => URL.revokeObjectURL(url));
    };
  }, [newImages]);

  // Auto-resize textarea
  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
      textareaRef.current.style.height = `${Math.max(36, textareaRef.current.scrollHeight)}px`;
    }
  }, [comment]);

  const handleRatingClick = (selectedRating: number) => {
    if (disabled) return;
    setRating(selectedRating);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (disabled) return;
    if (e.target.files && e.target.files.length > 0) {
      const filesArray = Array.from(e.target.files);
      const availableSlots = MAX_REVIEW_IMAGES - retainedImages.length - newImages.length;
      if (availableSlots <= 0) {
        setToast({ message: `Mỗi đánh giá chỉ được tải tối đa ${MAX_REVIEW_IMAGES} ảnh.`, type: 'info' });
      } else {
        const acceptedFiles = filesArray.slice(0, availableSlots);
        setNewImages((prev) => [...prev, ...acceptedFiles]);
        if (filesArray.length > acceptedFiles.length) {
          setToast({ message: `Chỉ nhận thêm ${acceptedFiles.length} ảnh. Mỗi đánh giá tối đa ${MAX_REVIEW_IMAGES} ảnh.`, type: 'info' });
        }
      }
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const removeRetainedImage = (idToRemove?: string) => {
    if (disabled || !idToRemove) return;
    setRetainedImages((prev) => prev.filter((img) => img.id !== idToRemove));
  };

  const removeNewImage = (indexToRemove: number) => {
    if (disabled) return;
    setNewImages((prev) => prev.filter((_, index) => index !== indexToRemove));
  };

  const handleCancel = () => {
    setRating(review?.rating || 0);
    setComment(review?.comment || '');
    setRetainedImages(mode === 'edit' && review ? review.images : []);
    setNewImages([]);
    setToast(null);
    setIsFocused(false);
    if (onCancel) onCancel();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (disabled) return;

    if (rating === 0) {
      setToast({ message: 'Vui lòng chọn số sao đánh giá.', type: 'info' });
      return;
    }

    try {
      setIsSubmitting(true);
      setToast(null);

      let result: RoomingHouseReviewResponse;
      if (mode === 'create') {
        if (!contractId) throw new Error('Thiếu ID hợp đồng.');
        result = await createRoomingHouseReview(contractId, {
          rating,
          comment,
          images: newImages.length > 0 ? newImages : undefined,
        });

        // Reset form after successful create
        setRating(0);
        setComment('');
        setNewImages([]);
        setIsFocused(false);
      } else {
        if (!review) throw new Error('Thiếu thông tin đánh giá để sửa.');
        const retainedImageIds = retainedImages.map(i => i.id).filter(Boolean) as string[];
        result = await updateRoomingHouseReview(review.id, {
          rating,
          comment,
          retainedImageIds: retainedImageIds.length > 0 ? retainedImageIds : undefined,
          newImages: newImages.length > 0 ? newImages : undefined,
        });
      }

      onSuccess(result);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu đánh giá. Vui lòng thử lại.'), type: 'error' });
    } finally {
      setIsSubmitting(false);
    }
  };

  const showActions = !disabled && (mode === 'create' || isFocused || comment.length > 0 || rating > 0 || newImages.length > 0 || retainedImages.length > 0 || mode === 'edit');
  const selectedImageCount = retainedImages.length + newImages.length;
  const canAddImages = selectedImageCount < MAX_REVIEW_IMAGES;

  return (
    <div className={`inline-review-form ${disabled ? 'disabled' : ''} ${hideAvatar ? 'no-avatar' : ''} mode-${mode}`}>
      {!hideAvatar && (
        <div className="inline-review-form-avatar">
          {avatarUrl ? (
            <img src={avatarUrl} alt="Avatar" />
          ) : (
            <div className="avatar-placeholder">
              {displayName ? displayName.charAt(0).toUpperCase() : 'U'}
            </div>
          )}
        </div>
      )}

      <div className="inline-review-form-body">
        <form onSubmit={handleSubmit}>
          {(!disabled || mode === 'edit') && (
            <div className="inline-star-rating">
              {[1, 2, 3, 4, 5].map((star) => (
                <button
                  key={star}
                  type="button"
                  className={star <= rating ? 'active' : ''}
                  onClick={() => handleRatingClick(star)}
                  disabled={disabled}
                >
                  ★
                </button>
              ))}
            </div>
          )}

          <div className="inline-textarea-container">
            <textarea
              ref={textareaRef}
              className="inline-review-textarea"
              placeholder={disabled ? disabledReason : "Viết bình luận công khai..."}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              onFocus={() => !disabled && setIsFocused(true)}
              disabled={disabled}
              rows={1}
            />
          </div>

          {(retainedImages.length > 0 || newImagePreviews.length > 0) && (
            <div className="inline-image-preview-grid">
              {retainedImages.map((img) => (
                <div key={img.id} className="image-preview-item">
                  <img src={toPublicPropertyImageUrl(img)} alt="Đã lưu" />
                  {!disabled && (
                    <button type="button" className="image-remove-btn" onClick={() => removeRetainedImage(img.id)}>
                      &times;
                    </button>
                  )}
                </div>
              ))}
              {newImagePreviews.map((previewUrl, index) => (
                <div key={previewUrl} className="image-preview-item">
                  <img src={previewUrl} alt="Mới" />
                  {!disabled && (
                    <button type="button" className="image-remove-btn" onClick={() => removeNewImage(index)}>
                      &times;
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          {showActions && !disabled && (
            <div className="inline-form-actions">
              <div className="inline-form-tools">
                <button
                  type="button"
                  className="icon-btn tool-btn"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={!canAddImages}
                  title="Đính kèm ảnh"
                >
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"></path>
                    <circle cx="12" cy="13" r="4"></circle>
                  </svg>
                  <span>Thêm ảnh ({selectedImageCount}/{MAX_REVIEW_IMAGES})</span>
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  multiple
                  accept="image/jpeg,image/png,image/webp"
                  style={{ display: 'none' }}
                  onChange={handleFileChange}
                />
              </div>
              <div className="inline-form-buttons">
                <Button type="button" variant="outline" onClick={handleCancel} disabled={isSubmitting}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting}>
                  {mode === 'create' ? 'Đánh giá' : 'Lưu'}
                </Button>
              </div>
            </div>
          )}
        </form>
      </div>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
};
