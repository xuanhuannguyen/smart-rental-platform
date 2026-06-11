export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5294',
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID || '',
  vietMapApiKey: import.meta.env.VITE_VIETMAP_API_KEY || '',
  vietMapTileStyleUrl:
    import.meta.env.VITE_VIETMAP_TILE_STYLE_URL ||
    'https://maps.vietmap.vn/maps/styles/tm/style.json',
  leafletTileUrl:
    import.meta.env.VITE_LEAFLET_TILE_URL ||
    'https://maps.chotot.com/tile/{z}/{x}/{y}.png'
};
