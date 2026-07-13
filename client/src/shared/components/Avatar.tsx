import { toAssetUrl } from '../api/assets';

interface AvatarProps {
  name: string;
  url?: string | null;
  className?: string;
}

export function Avatar({ name, url, className = 'chat-avatar' }: AvatarProps) {
  if (url) {
    return <img className={className} src={toAssetUrl(url)} alt={name} />;
  }

  const placeholderClass = className === 'chat-avatar' 
    ? 'chat-avatar chat-avatar--placeholder' 
    : `${className} avatar--placeholder`;

  return (
    <div className={placeholderClass}>
      {name.trim().charAt(0).toUpperCase() || 'U'}
    </div>
  );
}
