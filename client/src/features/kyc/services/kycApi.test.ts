import { beforeEach, describe, expect, it, vi } from 'vitest';
import { kycApi } from './kycApi';
import { apiClient } from '../../../shared/api/apiClient';
import { uploadImage } from '../../files/api';

vi.mock('../../../shared/api/apiClient', () => ({
  apiClient: vi.fn(),
}));

vi.mock('../../files/api', () => ({
  uploadImage: vi.fn(),
}));

describe('kycApi.submit', () => {
  const uploadImageMock = vi.mocked(uploadImage);
  const apiClientMock = vi.mocked(apiClient);

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('uploads all KYC images with KycDocument scope and submits media asset ids', async () => {
    const frontFile = new File(['front'], 'front.jpg', { type: 'image/jpeg' });
    const backFile = new File(['back'], 'back.jpg', { type: 'image/jpeg' });
    const selfieFile = new File(['selfie'], 'selfie.jpg', { type: 'image/jpeg' });

    uploadImageMock
      .mockResolvedValueOnce({ url: '/api/media/private/front', mediaAssetId: 'front-id' })
      .mockResolvedValueOnce({ url: '/api/media/private/back', mediaAssetId: 'back-id' })
      .mockResolvedValueOnce({ url: '/api/media/private/selfie', mediaAssetId: 'selfie-id' });

    apiClientMock.mockResolvedValue({
      success: true,
      message: 'ok',
      data: { kycId: 'kyc-id' }
    } as never);

    await kycApi.submit({
      documentType: 'CCCD',
      selfieCaptureMethod: 'Upload',
      frontImage: frontFile,
      backImage: backFile,
      selfieImage: selfieFile,
    });

    expect(uploadImageMock).toHaveBeenNthCalledWith(1, frontFile, 'KycDocument');
    expect(uploadImageMock).toHaveBeenNthCalledWith(2, backFile, 'KycDocument');
    expect(uploadImageMock).toHaveBeenNthCalledWith(3, selfieFile, 'KycDocument');
    expect(apiClientMock).toHaveBeenCalledWith(
      '/api/kyc/submissions',
      expect.objectContaining({
        method: 'POST',
        auth: true,
        body: {
          documentType: 'CCCD',
          selfieCaptureMethod: 'Upload',
          frontMediaAssetId: 'front-id',
          backMediaAssetId: 'back-id',
          selfieMediaAssetId: 'selfie-id',
          manualCitizenId: null,
          manualFullName: null,
          manualDateOfBirth: null,
          manualGender: null,
          manualAddress: null,
        }
      })
    );
  });
});
