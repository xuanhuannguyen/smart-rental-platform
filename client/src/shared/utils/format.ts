export function formatDateVi(value?: string | null): string {
  if (!value) {
    return '--';
  }

  return new Date(value).toLocaleDateString('vi-VN');
}

export function formatMoneyString(value: number): string {
  if (value === 0) {
    return '';
  }

  return value.toLocaleString('vi-VN');
}

export function parseMoneyString(value: string): number {
  const clean = value.replace(/[^0-9]/g, '');
  return clean === '' ? 0 : Number(clean);
}
