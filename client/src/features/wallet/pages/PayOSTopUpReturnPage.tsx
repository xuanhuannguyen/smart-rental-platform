import { useNavigate, useSearchParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../../../shared/components/ui/Button';
import './WalletLayout.css';

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

  const message = isCancelled
    ? 'Thanh toán đã bị hủy.'
    : isPaid
      ? 'Thanh toán PayOS đã hoàn tất. Hệ thống đang chờ webhook xác nhận để cộng tiền vào ví.'
      : 'PayOS đã chuyển bạn về hệ thống. Vui lòng kiểm tra ví hoặc lịch sử giao dịch sau khi webhook được xử lý.';

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <header className="wallet-header">
          <div>
            <p className="wallet-muted">PayOS</p>
            <h1>Kết quả thanh toán</h1>
            <p>{message}</p>
          </div>
          <div className="wallet-actions">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
          </div>
        </header>

        <section className="wallet-panel">
          <div className="wallet-result" style={{ marginTop: 0, paddingTop: 0, borderTop: 'none' }}>
            <div className="wallet-result-row">
              <span>Code</span>
              <strong>{code || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Status</span>
              <strong>{status || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Cancel</span>
              <strong>{cancel || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>Order code</span>
              <strong className="wallet-code">{orderCode || 'Chưa có'}</strong>
            </div>
            <div className="wallet-result-row">
              <span>PayOS id</span>
              <strong className="wallet-code">{id || 'Chưa có'}</strong>
            </div>
          </div>

          <div className="wallet-actions" style={{ marginTop: 20 }}>
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
              Về ví của tôi
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
              Xem lịch sử giao dịch
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TOPUP)}>
              Nạp tiếp
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
          </div>
        </section>
      </div>
    </main>
  );
}
