const vietMapApiKey = import.meta.env.VITE_VIETMAP_API_KEY || '';

/**
 * Build the best available raster tile URL.
 *
 * Priority:
 *   1. Explicit VITE_LEAFLET_TILE_URL from .env (always wins)
 *   2. VietMap raster tiles when API key is present
 *   3. Fallback to Chotot tiles (no key required)
 *
 * VietMap raster tile endpoint: /tm/{z}/{x}/{y}.png
 * Reference: https://maps.vietmap.vn/docs/tileserver
 */
function defaultTileUrl(): string {
  if (vietMapApiKey) {
    return `https://maps.vietmap.vn/tm/{z}/{x}/{y}.png?apikey=${vietMapApiKey}`;
  }
  return 'https://maps.chotot.com/tile/{z}/{x}/{y}.png';
}

export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5294',
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID || '',
  vietMapApiKey,
  vietMapTileStyleUrl:
    import.meta.env.VITE_VIETMAP_TILE_STYLE_URL ||
    'https://maps.vietmap.vn/maps/styles/tm/style.json',
  leafletTileUrl:
    import.meta.env.VITE_LEAFLET_TILE_URL || defaultTileUrl()
};
