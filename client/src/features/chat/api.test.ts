import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from '../../shared/api/apiClient';
import { downloadChatFile, getChatImage, updateConversation, uploadChatAvatar, uploadChatFile, uploadChatImage } from './api';

vi.mock('../../shared/api/apiClient', () => ({
  apiClient: vi.fn(),
}));

describe('chat media api', () => {
  const apiClientMock = vi.mocked(apiClient);

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('preserves mediaAssetId returned by image and file uploads', async () => {
    apiClientMock
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'image-asset-id',
          url: '/api/media/private/image-asset-id',
        }
      } as never)
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'file-asset-id',
          url: '/api/media/private/file-asset-id',
          fileName: 'lease.pdf',
          contentType: 'application/pdf',
          size: 128,
        }
      } as never);

    const image = await uploadChatImage(new File(['image'], 'chat.jpg', { type: 'image/jpeg' }));
    const file = await uploadChatFile(new File(['pdf'], 'lease.pdf', { type: 'application/pdf' }));

    expect(image).toEqual({
      mediaAssetId: 'image-asset-id',
      url: '/api/media/private/image-asset-id',
    });
    expect(file).toEqual({
      mediaAssetId: 'file-asset-id',
      url: '/api/media/private/file-asset-id',
      fileName: 'lease.pdf',
      contentType: 'application/pdf',
      size: 128,
    });
  });

  it('loads private image and file content through authenticated media routes', async () => {
    const imageBlob = new Blob(['image'], { type: 'image/jpeg' });
    const fileBlob = new Blob(['file'], { type: 'application/pdf' });
    apiClientMock
      .mockResolvedValueOnce(imageBlob as never)
      .mockResolvedValueOnce(fileBlob as never);

    await expect(getChatImage('image-asset-id')).resolves.toBe(imageBlob);
    await expect(downloadChatFile('file-asset-id')).resolves.toBe(fileBlob);

    expect(apiClientMock).toHaveBeenNthCalledWith(
      1,
      '/api/media/private/image-asset-id',
      { auth: true, responseType: 'blob' }
    );
    expect(apiClientMock).toHaveBeenNthCalledWith(
      2,
      '/api/media/private/file-asset-id/download',
      { auth: true, responseType: 'blob' }
    );
  });

  it('uploads group avatars through the dedicated public avatar endpoint', async () => {
    apiClientMock.mockResolvedValue({
      data: {
        mediaAssetId: 'group-avatar-id',
        url: '/api/media/public/group-avatar-id',
      }
    } as never);

    const result = await uploadChatAvatar(new File(['avatar'], 'group.jpg', { type: 'image/jpeg' }));

    expect(result.mediaAssetId).toBe('group-avatar-id');
    expect(apiClientMock).toHaveBeenCalledWith(
      '/api/chat/avatars',
      expect.objectContaining({ method: 'POST', auth: true })
    );
  });

  it('uses explicit clear semantics when removing a group avatar', async () => {
    apiClientMock.mockResolvedValue({ data: { id: 'conversation-id' } } as never);

    await updateConversation('conversation-id', undefined, undefined, true);

    expect(apiClientMock).toHaveBeenCalledWith(
      '/api/chat/conversations/conversation-id',
      {
        method: 'PATCH',
        auth: true,
        body: {
          title: undefined,
          avatarMediaAssetId: undefined,
          clearAvatar: true,
        }
      }
    );
  });
});
