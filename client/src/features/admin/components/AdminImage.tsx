import { useEffect, useState } from 'react';
import { tokenStorage } from '../../../shared/api/tokenStorage';

type AdminImageProps = {
  label: string;
  src: string;
  className?: string;
  placeholderClassName?: string;
};

export function AdminImage({
  label,
  src,
  className = 'admin-image-card admin-image-box',
  placeholderClassName = 'admin-image-card admin-image-box admin-image-placeholder',
}: AdminImageProps) {
  const [objectUrl, setObjectUrl] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (!src) {
      return;
    }

    let isMounted = true;
    let nextObjectUrl = '';

    async function loadImage() {
      setError('');
      setObjectUrl('');

      try {
        const accessToken = tokenStorage.getAccessToken();

        if (!accessToken) {
          throw new Error('Missing access token.');
        }

        const response = await fetch(src, {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        });

        if (!response.ok) {
          throw new Error(`Image request failed with ${response.status}.`);
        }

        const blob = await response.blob();
        nextObjectUrl = URL.createObjectURL(blob);

        if (isMounted) {
          setObjectUrl(nextObjectUrl);
        } else {
          URL.revokeObjectURL(nextObjectUrl);
        }
      } catch {
        if (isMounted) {
          setError('Không tải được ảnh.');
        }
      }
    }

    void loadImage();

    return () => {
      isMounted = false;

      if (nextObjectUrl) {
        URL.revokeObjectURL(nextObjectUrl);
      }
    };
  }, [src]);

  if (!src) {
    return null;
  }

  if (error) {
    return (
      <div className={placeholderClassName}>
        <span>{label}</span>
        <small>{error}</small>
      </div>
    );
  }

  if (!objectUrl) {
    return (
      <div className={placeholderClassName}>
        <span>{label}</span>
        <small>Đang tải ảnh...</small>
      </div>
    );
  }

  return (
    <a href={objectUrl} target="_blank" rel="noreferrer" className={className}>
      <img src={objectUrl} alt={label} />
      <span>{label}</span>
    </a>
  );
}
