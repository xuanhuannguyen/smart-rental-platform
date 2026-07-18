import { useEffect, useState, type ImgHTMLAttributes } from 'react';
import { toAssetUrl } from '../../api/assets';
import { extractPrivateMediaAssetId, getPrivateMediaBlob } from '../../api/media';

export function usePrivateMediaObjectUrl(mediaAssetId?: string | null) {
  const [objectUrl, setObjectUrl] = useState('');
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!mediaAssetId) {
      setObjectUrl('');
      setError(false);
      return;
    }

    let active = true;
    let nextObjectUrl = '';
    setObjectUrl('');
    setError(false);

    void getPrivateMediaBlob(mediaAssetId)
      .then((blob) => {
        nextObjectUrl = URL.createObjectURL(blob);
        if (active) {
          setObjectUrl(nextObjectUrl);
        } else {
          URL.revokeObjectURL(nextObjectUrl);
        }
      })
      .catch(() => {
        if (active) setError(true);
      });

    return () => {
      active = false;
      if (nextObjectUrl) URL.revokeObjectURL(nextObjectUrl);
    };
  }, [mediaAssetId]);

  return { objectUrl, error };
}

type PrivateMediaImageProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'src'> & {
  source?: string | null;
  mediaAssetId?: string | null;
  loadingLabel?: string;
  errorLabel?: string;
};

export function PrivateMediaImage({
  source,
  mediaAssetId,
  loadingLabel = 'Đang tải ảnh...',
  errorLabel = 'Không tải được ảnh.',
  ...imageProps
}: PrivateMediaImageProps) {
  const privateMediaAssetId = mediaAssetId ?? extractPrivateMediaAssetId(source);
  const { objectUrl, error } = usePrivateMediaObjectUrl(privateMediaAssetId);
  const directSource = privateMediaAssetId
    ? ''
    : source?.startsWith('blob:') || source?.startsWith('data:')
      ? source
      : toAssetUrl(source ?? '');
  const resolvedSource = objectUrl || directSource;

  if (error) return <span>{errorLabel}</span>;
  if (!resolvedSource) return <span>{loadingLabel}</span>;

  return <img {...imageProps} src={resolvedSource} />;
}
