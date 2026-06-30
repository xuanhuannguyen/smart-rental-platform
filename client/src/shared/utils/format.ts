export function formatDateVi(value?: string | null): string {
  if (!value) {
    return '--';
  }

  return new Date(value).toLocaleDateString('vi-VN');
}

export function formatDateTimeVi(value?: string | null): string {
  if (!value) {
    return '--';
  }

  const date = new Date(value);
  const time = date.toLocaleTimeString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });

  return `${date.toLocaleDateString('vi-VN')} lúc ${time}`;
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
