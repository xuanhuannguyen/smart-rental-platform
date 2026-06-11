import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Input } from '../../../shared/components/ui/Input';
import { walletApi } from '../api';
import type { CreatePayOSTopUpResponse } from '../types';
import './WalletLayout.css';
import { WalletNavigation } from './WalletNavigation';

const quickAmounts = [10000, 50000, 100000, 500000];

function formatMoney(value: number) {
  return `${new Intl.NumberFormat('vi-VN').format(value)}đ`;
}

function statusLabel(status: string) {
  const normalized = status.toLowerCase();
  if (normalized.includes('pending')) return 'Đang chờ thanh toán';
  if (normalized.includes('success') || normalized.includes('paid') || normalized.includes('succeeded')) return 'Thành công';
  if (normalized.includes('fail')) return 'Thất bại';
  if (normalized.includes('cancel')) return 'Đã hủy';
  if (normalized.includes('expire')) return 'Hết hạn';
  return status;
}

function statusClass(status: string) {
  const normalized = status.toLowerCase();
  if (normalized.includes('success') || normalized.includes('paid') || normalized.includes('succeeded')) return 'wallet-status-succeeded';
  if (normalized.includes('pending')) return 'wallet-status-pending';
  if (normalized.includes('fail')) return 'wallet-status-failed';
  if (normalized.includes('cancel')) return 'wallet-status-cancelled';
  if (normalized.includes('expire')) return 'wallet-status-expired';
  return 'wallet-status-default';
}

function isLocalTestPaymentUrl(paymentUrl?: string | null) {
  return Boolean(paymentUrl?.includes('/dev/mock-payment'));
}

export function WalletTopUpPage() {
  const navigate = useNavigate();
  const [amount, setAmount] = useState('10000');
  const [idempotencyKey, setIdempotencyKey] = useState('');
  const [result, setResult] = useState<CreatePayOSTopUpResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [popupBlocked, setPopupBlocked] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function openPaymentPage(paymentUrl?: string | null) {
    if (!paymentUrl) {
      return false;
    }

    const openedWindow = window.open(paymentUrl, '_blank', 'noopener,noreferrer');
    const wasBlocked = openedWindow === null;
    setPopupBlocked(wasBlocked);
    return !wasBlocked;
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setResult(null);
    setPopupBlocked(false);
    setIsSubmitting(true);

    try {
      const response = await walletApi.createPayOSTopUp({
        amount: Number(amount),
        idempotencyKey: idempotencyKey.trim() || undefined
      });

      setResult(response.data);
      openPaymentPage(response.data.paymentUrl);
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Không thể tạo giao dịch nạp ví.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  const isLocalTestMode = isLocalTestPaymentUrl(result?.paymentUrl);

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <WalletNavigation />

        <header className="wallet-header">
          <p className="wallet-kicker">Nạp tiền</p>
          <h1>Nạp ví bằng PayOS</h1>
          <p className="wallet-muted">
            Tạo giao dịch nạp tiền và mở trang thanh toán PayOS để bạn quét QR hoặc hoàn tất thanh toán.
          </p>
        </header>

        <section className="wallet-panel">
          <h2 className="wallet-section-title">Thông tin nạp tiền</h2>
          <p className="wallet-helper">
            Tối thiểu 10.000 VNĐ, tối đa 50.000.000 VNĐ cho mỗi giao dịch nạp ví.
          </p>

          <form className="wallet-form" onSubmit={handleSubmit}>
            <div className="wallet-form-grid">
              <div className="wallet-field">
                <label htmlFor="wallet-topup-amount">Số tiền muốn nạp</label>
                <Input
                  id="wallet-topup-amount"
                  type="number"
                  min="10000"
                  max="50000000"
                  step="1000"
                  value={amount}
                  onChange={(event) => setAmount(event.target.value)}
                  required
                />
                <div className="wallet-quick-amounts">
                  {quickAmounts.map(value => (
                    <Button key={value} type="button" variant="secondary" onClick={() => setAmount(String(value))}>
                      {formatMoney(value)}
                    </Button>
                  ))}
                </div>
              </div>

              <div className="wallet-field">
                <label htmlFor="wallet-topup-idempotency">Khóa chống tạo trùng</label>
                <Input
                  id="wallet-topup-idempotency"
                  value={idempotencyKey}
                  onChange={(event) => setIdempotencyKey(event.target.value)}
                  placeholder="test-topup-001"
                />
                <p className="wallet-muted">Tùy chọn cho kiểm thử retry. Để trống nếu không cần.</p>
              </div>
            </div>

            <div className="wallet-banner">
              Sau khi thanh toán thành công, PayOS sẽ gửi webhook về hệ thống. Số dư ví chỉ được cập nhật khi webhook xác nhận thành công.
            </div>

            {error ? <Alert type="error">{error}</Alert> : null}

            <div className="wallet-actions">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Đang tạo giao dịch...' : 'Tạo giao dịch và mở PayOS'}
              </Button>
            </div>
          </form>

          {result ? (
            <div className="wallet-result">
              <h2 className="wallet-section-title">Giao dịch đã được tạo</h2>

              {isLocalTestMode ? (
                <div className="wallet-banner wallet-banner-warning">
                  Chế độ test local: trang thanh toán đang trỏ tới mock payment để kiểm thử webhook nội bộ.
                </div>
              ) : null}

              {popupBlocked ? (
                <div className="wallet-banner wallet-banner-warning">
                  Trình duyệt có thể đã chặn cửa sổ thanh toán. Vui lòng bấm nút mở lại bên dưới để tiếp tục thanh toán.
                </div>
              ) : (
                <div className="wallet-banner">
                  Trang thanh toán đã được mở trong tab mới. Nếu bạn chưa thấy tab thanh toán, hãy bấm nút mở lại bên dưới.
                </div>
              )}

              <div className="wallet-result-row">
                <span>Mã giao dịch</span>
                <strong className="wallet-code">{result.paymentTransactionId}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Mã PayOS</span>
                <strong className="wallet-code">{result.providerOrderCode}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Trạng thái</span>
                <strong>
                  <span className={`wallet-status-badge ${statusClass(result.status)}`}>{statusLabel(result.status)}</span>
                </strong>
              </div>
              <div className="wallet-result-row">
                <span>Thời gian hết hạn</span>
                <strong>{new Date(result.expiredAt).toLocaleString('vi-VN')}</strong>
              </div>
              {result.qrCode ? (
                <div className="wallet-result-row">
                  <span>Mã QR</span>
                  <strong className="wallet-code">{result.qrCode}</strong>
                </div>
              ) : null}

              <p className="wallet-helper">
                Nếu số dư chưa thay đổi ngay sau khi thanh toán, vui lòng chờ vài giây để webhook PayOS xác nhận rồi kiểm tra lại ví.
              </p>

              <div className="wallet-actions">
                {result.paymentUrl ? (
                  <Button type="button" onClick={() => openPaymentPage(result.paymentUrl)}>
                    {isLocalTestMode ? 'Mở lại trang thanh toán thử' : 'Mở lại trang thanh toán PayOS'}
                  </Button>
                ) : null}
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
                  Kiểm tra ví
                </Button>
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
                  Xem lịch sử giao dịch
                </Button>
              </div>
            </div>
          ) : null}
        </section>
      </div>
    </main>
  );
}
