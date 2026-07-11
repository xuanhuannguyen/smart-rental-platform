export const SIGNATURE_IMAGE_ACCEPT = '.png,.jpg,.jpeg,image/png,image/jpeg';

const SUPPORTED_SIGNATURE_IMAGE_TYPES = new Set(['image/png', 'image/jpeg']);
const MAXIMUM_SIGNATURE_IMAGE_BYTES = 2_000_000;

export const validateSignatureImageFile = (file: File): string | null => {
  const extensionValid = /\.(png|jpe?g)$/i.test(file.name);
  const mediaTypeValid = SUPPORTED_SIGNATURE_IMAGE_TYPES.has(file.type.toLowerCase());

  if (!extensionValid || !mediaTypeValid) {
    return 'Ảnh chữ ký phải là tệp PNG, JPG hoặc JPEG.';
  }

  if (file.size < 32 || file.size > MAXIMUM_SIGNATURE_IMAGE_BYTES) {
    return 'Ảnh chữ ký phải có dung lượng từ 32 byte đến 2 MB.';
  }

  return null;
};
