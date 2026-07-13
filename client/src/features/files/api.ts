import { uploadFileViaMediaWorkflow } from '../../shared/api/media';
export type FileUploadScope = 'RoomingHouse' | 'LegalDocument' | 'Room' | 'Avatar' | 'HouseRule' | 'MeterReading' | 'ChatAttachment';

export interface FileUploadResponse {
  /**
   * @deprecated Compatibility-only storage key. New callers should persist mediaAssetId and use media URLs.
   */
  objectKey: string;
  url: string;
  mediaAssetId?: string | null;
  isCompatibilityResponse?: boolean;
  compatibilityWarning?: string | null;
}

export async function uploadImage(file: File, scope: FileUploadScope): Promise<FileUploadResponse> {
  const uploaded = await uploadFileViaMediaWorkflow(file, scope);
  return {
    objectKey: uploaded.objectKey,
    url: uploaded.url,
    mediaAssetId: uploaded.mediaAssetId,
  };
}

export async function uploadPdf(file: File, scope: FileUploadScope): Promise<FileUploadResponse> {
  const uploaded = await uploadFileViaMediaWorkflow(file, scope);
  return {
    objectKey: uploaded.objectKey,
    url: uploaded.url,
    mediaAssetId: uploaded.mediaAssetId,
  };
}
