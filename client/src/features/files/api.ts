import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';

export type FileUploadScope = 'RoomingHouse' | 'LegalDocument' | 'Room' | 'KycDocument' | 'Avatar';

export interface FileUploadResponse {
  objectKey: string;
  url: string;
}

export async function uploadImage(file: File, scope: FileUploadScope): Promise<FileUploadResponse> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('scope', scope);

  const data = await apiClient<ApiResponse<FileUploadResponse>>(
    '/api/files/images',
    { method: 'POST', auth: true, body: formData }
  );
  return data.data;
}
