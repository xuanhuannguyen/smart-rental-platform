import { env } from '../../config/env';

// Converts an objectKey to a full asset URL for displaying images
export function toAssetUrl(objectKeyOrUrl: string): string {
  if (!objectKeyOrUrl) return '';
  const trimmed = objectKeyOrUrl.trim();
  if (trimmed === '' || trimmed === 'null' || trimmed === 'undefined') return '';
  
  // Already a full URL (http/https)
  if (/^https?:\/\//i.test(trimmed)) return trimmed;
  
  // If it already starts with /api/ (e.g. private media endpoints)
  if (trimmed.startsWith('/api/')) return `${env.apiBaseUrl}${trimmed}`;
  
  // If it already starts with /uploads/
  if (trimmed.startsWith('/uploads/')) return `${env.apiBaseUrl}${trimmed}`;
  
  // If it starts with uploads/ (without leading slash)
  if (trimmed.startsWith('uploads/')) return `${env.apiBaseUrl}/${trimmed}`;
  
  // If it's just an objectKey (e.g. legal-documents/xxx.png)
  return `${env.apiBaseUrl}/uploads/${trimmed.replace(/^\/+/, '')}`;
}
