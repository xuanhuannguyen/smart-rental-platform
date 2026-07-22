import type { CSSProperties } from 'react';
import { usePrivateMediaObjectUrl } from '../../../shared/components/media/PrivateMediaImage';

type PrivateChatImageProps = {
  mediaAssetId: string;
  alt: string;
  className?: string;
  style?: CSSProperties;
  openOnClick?: boolean;
};

export function usePrivateChatMediaObjectUrl(mediaAssetId?: string | null) {
  return usePrivateMediaObjectUrl(mediaAssetId);
}

export function PrivateChatImage({
  mediaAssetId,
  alt,
  className,
  style,
  openOnClick = false,
}: PrivateChatImageProps) {
  const { objectUrl, error } = usePrivateChatMediaObjectUrl(mediaAssetId);

  if (error) {
    return <span className="chat-media-error">Không tải được ảnh.</span>;
  }

  if (!objectUrl) {
    return <span className="chat-media-loading">Đang tải ảnh...</span>;
  }

  return (
    <img
      src={objectUrl}
      alt={alt}
      className={className}
      style={style}
      onClick={openOnClick ? () => window.open(objectUrl, '_blank', 'noopener,noreferrer') : undefined}
    />
  );
}
