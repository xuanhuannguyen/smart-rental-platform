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

export function WalletTopUpPage() {
  const navigate = useNavigate();
  const [amount, setAmount] = useState('10000');
  const [idempotencyKey, setIdempotencyKey] = useState('');
  const [result, setResult] = useState<CreatePayOSTopUpResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setResult(null);
    setIsSubmitting(true);

    try {
      const response = await walletApi.createPayOSTopUp({
        amount: Number(amount),
        idempotencyKey: idempotencyKey.trim() || undefined
      });
      setResult(response.data);
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Khong the tao giao dich nap vi.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <header className="wallet-header">
          <div>
            <p className="wallet-muted">PayOS</p>
            <h1>Nạp ví</h1>
            <p>Tạo giao dịch PayOS đang chờ thanh toán. Ví chỉ được cộng sau webhook thành công.</p>
          </div>
          <div className="wallet-actions">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
              Về ví
            </Button>
          </div>
        </header>

        <section className="wallet-panel">
          <form className="wallet-form" onSubmit={handleSubmit}>
            <div className="wallet-field">
              <label htmlFor="wallet-topup-amount">Số tiền</label>
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
              <p className="wallet-muted">Tối thiểu 10,000 VND, tối đa 50,000,000 VND.</p>
            </div>

            <div className="wallet-field">
              <label htmlFor="wallet-topup-idempotency">Idempotency key</label>
              <Input
                id="wallet-topup-idempotency"
                value={idempotencyKey}
                onChange={(event) => setIdempotencyKey(event.target.value)}
                placeholder="test-topup-001"
              />
            </div>

            {error ? <Alert type="error">{error}</Alert> : null}

            <div className="wallet-actions">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Đang tạo...' : 'Tạo giao dịch'}
              </Button>
            </div>
          </form>

          {result ? (
            <div className="wallet-result">
              <div className="wallet-result-row">
                <span>Payment transaction</span>
                <strong className="wallet-code">{result.paymentTransactionId}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Provider order</span>
                <strong className="wallet-code">{result.providerOrderCode}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Status</span>
                <strong>{result.status}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Payment URL</span>
                {result.paymentUrl ? <a href={result.paymentUrl} target="_blank" rel="noreferrer">{result.paymentUrl}</a> : <strong>Chưa có</strong>}
              </div>
              <div className="wallet-result-row">
                <span>QR</span>
                <strong className="wallet-code">{result.qrCode || 'Chưa có'}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Hết hạn</span>
                <strong>{new Date(result.expiredAt).toLocaleString()}</strong>
              </div>
              <div className="wallet-actions">
                {result.paymentUrl ? (
                  <Button
                    type="button"
                    onClick={() => window.open(result.paymentUrl ?? '', '_blank', 'noopener,noreferrer')}
                  >
                    Mở trang thanh toán PayOS
                  </Button>
                ) : null}
                <Button
                  type="button"
                  onClick={() => navigate(`${ROUTE_PATHS.DEV.MOCK_PAYMENT}?paymentTransactionId=${result.paymentTransactionId}`)}
                >
                  Thanh toán thử bằng Mock (Dev only)
                </Button>
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
                  Về ví của tôi
                </Button>
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
                  Quay lại trang chủ
                </Button>
              </div>
            </div>
          ) : null}
        </section>
      </div>
    </main>
  );
}
