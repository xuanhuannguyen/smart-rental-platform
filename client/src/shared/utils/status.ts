const STATUS_LABELS: Record<string, string> = {
  Approved: 'Đã duyệt',
  Pending: 'Chờ duyệt',
  Rejected: 'Bị từ chối',
  Draft: 'Bản nháp',
  Submitted: 'Đã gửi duyệt',
  Available: 'Còn trống',
  Occupied: 'Đang thuê',
  Hidden: 'Đang ẩn',
  Visible: 'Hiển thị',
  Maintenance: 'Bảo trì',
  Reserved: 'Đã đặt chỗ',
  Active: 'Đang hiệu lực',
  Expired: 'Kết thúc',
  Cancelled: 'Đã hủy',
};

export function formatStatus(status: string): string {
  return STATUS_LABELS[status] ?? status;
}

export function getStatusToneClass(status: string): string {
  switch (status) {
    case 'Approved':
    case 'Available':
    case 'Visible':
    case 'Active':
      return 'status-pill--success';
    case 'Pending':
      return 'status-pill--info';
    case 'Rejected':
    case 'Cancelled':
      return 'status-pill--danger';
    case 'Draft':
      return 'status-pill--warning';
    case 'Hidden':
    case 'Maintenance':
    case 'Expired':
      return 'status-pill--neutral';
    case 'Occupied':
    case 'Reserved':
      return 'status-pill--reserved';
    default:
      return '';
  }
}

export function getCreateHouseBlockedMessage(status: string): string {
  if (status === 'Draft') {
    return 'Bạn đang có khu trọ ở trạng thái bản nháp. Vui lòng hoàn tất và gửi duyệt trước khi tạo khu trọ mới.';
  }

  if (status === 'Pending') {
    return 'Bạn đang có khu trọ đang chờ duyệt. Vui lòng chờ kết quả trước khi tạo khu trọ mới.';
  }

  if (status === 'Rejected') {
    return 'Bạn đang có khu trọ bị từ chối. Vui lòng cập nhật và gửi duyệt lại trước khi tạo khu trọ mới.';
  }

  return 'Bạn không thể tạo khu trọ mới lúc này.';
}
