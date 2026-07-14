import { uploadFileViaMediaWorkflow } from '../../shared/api/media';
export type FileUploadScope = 'RoomingHouse' | 'LegalDocument' | 'KycDocument' | 'Room' | 'Avatar' | 'HouseRule' | 'MeterReading' | 'ChatAttachment';

export interface FileUploadResponse {
  url: string;
  mediaAssetId?: string | null;
}

export async function uploadImage(file: File, scope: FileUploadScope): Promise<FileUploadResponse> {
  const uploaded = await uploadFileViaMediaWorkflow(file, scope);
  return {
    url: uploaded.url,
    mediaAssetId: uploaded.mediaAssetId,
  };
}

export async function uploadPdf(file: File, scope: FileUploadScope): Promise<FileUploadResponse> {
  const uploaded = await uploadFileViaMediaWorkflow(file, scope);
  return {
    url: uploaded.url,
    mediaAssetId: uploaded.mediaAssetId,
  };
}
