import { useNavigate, useSearchParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../../../shared/components/ui/Button';
import './WalletLayout.css';
import { WalletNavigation } from './WalletNavigation';

export function PayOSTopUpReturnPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const code = searchParams.get('code') ?? '';
  const id = searchParams.get('id') ?? '';
  const cancel = searchParams.get('cancel') ?? '';
  const status = searchParams.get('status') ?? '';
  const orderCode = searchParams.get('orderCode') ?? '';
  const isCancelled = cancel.toLowerCase() === 'true' || status.toUpperCase() === 'CANCELLED';
  const isPaid = status.toUpperCase() === 'PAID' && cancel.toLowerCase() === 'false';

  const bannerClass = isCancelled
    ? 'wallet-banner wallet-banner-warning'
    : isPaid
      ? 'wallet-banner wallet-banner-success'
      : 'wallet-banner';

  const statusText = isCancelled ? 'Đã hủy' : isPaid ? 'Đã thanh toán' : status || 'Đang kiểm tra';
  const message = isCancelled
    ? 'Thanh toán đã bị hủy.'
    : isPaid
      ? 'Thanh toán đã hoàn tất. Hệ thống đang xác nhận để cộng tiền vào ví.'
      : 'PayOS đã chuyển bạn về hệ thống. Vui lòng kiểm tra ví sau khi webhook được xử lý.';

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <WalletNavigation />

        <header className="wallet-header">
          <p className="wallet-kicker">Kết quả PayOS</p>
          <h1>Kết quả thanh toán</h1>
          <p className="wallet-muted">
            Trang này chỉ hiển thị trạng thái PayOS trả về. Frontend không tự cộng tiền vào ví.
          </p>
        </header>

        <section className="wallet-panel">
          <div className={bannerClass}>
            <strong>{statusText}</strong>
            <br />
            {message}
          </div>
          <p className="wallet-helper">
            Nếu số dư chưa thay đổi ngay, vui lòng chờ vài giây rồi tải lại trang ví.
          </p>

          <div className="wallet-result wallet-result-flat">
            <div className="wallet-result-row">
              <span>Mã phản hồi</span>
              <strong>{code || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Trạng thái</span>
              <strong>
                <span className={`wallet-status-badge ${isCancelled ? 'wallet-status-cancelled' : isPaid ? 'wallet-status-succeeded' : 'wallet-status-pending'}`}>
                  {statusText}
                </span>
              </strong>
            </div>
            <div className="wallet-result-row">
              <span>Đã hủy</span>
              <strong>{cancel || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Mã PayOS</span>
              <strong className="wallet-code">{orderCode || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Mã định danh PayOS</span>
              <strong className="wallet-code">{id || 'Chưa có'}</strong>
            </div>
          </div>

          <div className="wallet-actions wallet-actions-spaced">
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
              Kiểm tra ví
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
              Xem lịch sử giao dịch
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TOPUP)}>
              Nạp thêm tiền
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Trang chủ
            </Button>
          </div>
        </section>
      </div>
    </main>
  );
}
