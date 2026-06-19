import { useState } from 'react';
import { uploadImage, type FileUploadScope } from '../../files/api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import type { PropertyImageRequest } from '../types';
import './RoomingHouseEditor.css';

type PropertyImageEditorProps = {
  images: PropertyImageRequest[];
  scope: FileUploadScope;
  onChange: (images: PropertyImageRequest[]) => void;
  onSave: () => void;
};

export default function PropertyImageEditor({
  images,
  scope,
  onChange,
  onSave,
}: PropertyImageEditorProps) {
  const [uploading, setUploading] = useState(false);
  const [uploadMessage, setUploadMessage] = useState('');

  function updateImage(index: number, image: PropertyImageRequest) {
    onChange(images.map((item, itemIndex) => (itemIndex === index ? image : item)));
  }

  function removeImage(index: number) {
    const nextImages = images.filter((_, itemIndex) => itemIndex !== index);

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

    setUploading(true);
    setUploadMessage('');

    try {
      const uploadResults = await Promise.allSettled(
        selectedFiles.map((file) => uploadImage(file, scope))
      );

      const uploadedImages: PropertyImageRequest[] = [];
      uploadResults.forEach((result, index) => {
        if (result.status !== 'fulfilled') return;

        uploadedImages.push({
          objectKey: result.value.objectKey,
          imageUrl: result.value.url,
          caption: '',
          isCover: images.length === 0 && index === 0,
          sortOrder: images.length + index + 1,
        });
      });

      if (uploadedImages.length > 0) {
        onChange([...images, ...uploadedImages]);
      }

      const failedCount = uploadResults.length - uploadedImages.length;
      if (failedCount > 0) {
        setUploadMessage(
          `${failedCount} ảnh chưa tải lên được. Vui lòng kiểm tra định dạng và dung lượng ảnh.`
        );
      }
    } catch (error) {
      setUploadMessage(getApiErrorMessage(error, 'Không thể tải ảnh lên.'));
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="property-image-editor">
      <div className="property-image-editor__toolbar">
        <label className="property-image-editor__upload">
          <span>{uploading ? 'Đang tải ảnh...' : 'Tải ảnh lên'}</span>
          <input
            accept="image/jpeg,image/png,image/webp"
            disabled={uploading}
            multiple
            type="file"
            onChange={(event) => {
              void uploadSelectedImages(event.target.files);
              event.target.value = '';
            }}
          />
        </label>
        <button className="rooming-house-editor__primary" onClick={onSave}>
          Lưu
        </button>
      </div>

      {uploadMessage && <p className="rooming-house-editor__message">{uploadMessage}</p>}

      {images.length === 0 ? (
        <p className="property-image-editor__empty">Chưa có ảnh nào.</p>
      ) : (
        <div className="property-image-editor__grid">
          {images.map((image, index) => (
            <article
              className="property-image-editor__item"
              key={image.id ?? image.objectKey}
            >
              <img
                alt={image.caption || 'Ảnh'}
                src={toAssetUrl(image.imageUrl || image.objectKey)}
              />
              <label className="rooming-house-editor__field">
                <span>Chú thích</span>
                <input
                  value={image.caption ?? ''}
                  onChange={(event) =>
                    updateImage(index, { ...image, caption: event.target.value })
                  }
                />
              </label>
              <label className="rooming-house-editor__field">
                <span>Thứ tự</span>
                <input
                  min={1}
                  type="number"
                  value={image.sortOrder}
                  onChange={(event) =>
                    updateImage(index, {
                      ...image,
                      sortOrder: event.target.value === '' ? 0 : Number(event.target.value),
                    })
                  }
                />
              </label>
              <div className="property-image-editor__actions">
                <label className="rooming-house-editor__checkbox">
                  <input
                    checked={image.isCover}
                    type="radio"
                    name={`cover-${scope}`}
                    onChange={() => setCover(index)}
                  />
                  Ảnh đại diện
                </label>
                <button
                  className="property-image-editor__delete"
                  onClick={() => removeImage(index)}
                >
                  Xóa ảnh
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
