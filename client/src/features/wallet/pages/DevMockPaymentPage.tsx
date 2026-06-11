import { FormEvent, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Input } from '../../../shared/components/ui/Input';
import { walletApi } from '../api';
import type { MockPaymentResponse } from '../types';
import './WalletLayout.css';
import { WalletNavigation } from './WalletNavigation';

type MockAction = 'success' | 'failed';

function statusClass(status?: string | null) {
  const normalized = (status ?? '').toLowerCase();
  if (normalized.includes('processed') || normalized.includes('success') || normalized.includes('succeed')) return 'wallet-status-succeeded';
  if (normalized.includes('pending')) return 'wallet-status-pending';
  if (normalized.includes('fail') || normalized.includes('invalid') || normalized.includes('error')) return 'wallet-status-failed';
  if (normalized.includes('cancel')) return 'wallet-status-cancelled';
  return 'wallet-status-default';
}

function statusLabel(status?: string | null) {
  const normalized = (status ?? '').toLowerCase();
  if (!status) return 'Chưa có';
  if (normalized.includes('processed')) return 'Đã xử lý';
  if (normalized.includes('success') || normalized.includes('succeed')) return 'Thành công';
  if (normalized.includes('pending')) return 'Đang chờ';
  if (normalized.includes('fail')) return 'Thất bại';
  if (normalized.includes('invalid')) return 'Không hợp lệ';
  return status;
}

export function DevMockPaymentPage() {
  const [searchParams] = useSearchParams();
  const [paymentTransactionId, setPaymentTransactionId] = useState(searchParams.get('paymentTransactionId') ?? '');
  const [amount, setAmount] = useState('');
  const [result, setResult] = useState<MockPaymentResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState<MockAction | null>(null);

  async function submit(action: MockAction, event?: FormEvent, amountOverride?: number) {
    event?.preventDefault();
    setError(null);
    setResult(null);
    setIsSubmitting(action);

    try {
      const parsedAmount = amountOverride ?? (amount.trim() ? Number(amount) : undefined);
      const response = action === 'success'
        ? await walletApi.mockSuccess(paymentTransactionId.trim(), parsedAmount)
        : await walletApi.mockFailed(paymentTransactionId.trim(), parsedAmount);

      setResult(response.data);
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Không thể gọi mock payment.'));
    } finally {
      setIsSubmitting(null);
    }
  }

  const walletCreditMessage = result?.paymentStatus === 'Succeeded' && result.processingStatus === 'Processed'
    ? 'Webhook thử nghiệm đã được xử lý thành công. Nếu số tiền khớp và giao dịch chưa xử lý trước đó, ví đã được cộng đúng một lần.'
    : 'Ví không được cộng trong trường hợp thất bại, sai số tiền, chữ ký không hợp lệ hoặc giao dịch đã xử lý trước đó.';

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <WalletNavigation />

        <header className="wallet-header">
          <p className="wallet-kicker">Chỉ dùng để test local</p>
          <h1>Thanh toán thử local</h1>
          <p className="wallet-muted">
            Giả lập webhook PayOS thành công, thất bại hoặc sai số tiền để kiểm tra luồng cộng tiền ví trong môi trường phát triển.
          </p>
        </header>

        <section className="wallet-panel">
          <div className="wallet-banner wallet-banner-warning">Trang này không dùng cho production.</div>

          <form className="wallet-form" onSubmit={(event) => void submit('success', event)}>
            <div className="wallet-form-grid">
              <div className="wallet-field">
                <label htmlFor="mock-payment-id">Mã giao dịch nạp ví</label>
                <Input
                  id="mock-payment-id"
                  value={paymentTransactionId}
                  onChange={(event) => setPaymentTransactionId(event.target.value)}
                  placeholder="00000000-0000-0000-0000-000000000000"
                  required
                />
              </div>

              <div className="wallet-field">
                <label htmlFor="mock-payment-amount">Số tiền ghi đè</label>
                <Input
                  id="mock-payment-amount"
                  type="number"
                  value={amount}
                  onChange={(event) => setAmount(event.target.value)}
                  placeholder="Dùng để test sai số tiền"
                />
                <p className="wallet-muted">Để trống khi muốn mock đúng số tiền của giao dịch.</p>
              </div>
            </div>

            {error ? <Alert type="error">{error}</Alert> : null}

            <div className="wallet-actions">
              <Button type="submit" disabled={isSubmitting !== null || !paymentTransactionId.trim()}>
                {isSubmitting === 'success' ? 'Đang xử lý...' : 'Giả lập thành công'}
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={isSubmitting !== null || !paymentTransactionId.trim()}
                onClick={() => void submit('failed')}
              >
                {isSubmitting === 'failed' ? 'Đang xử lý...' : 'Giả lập thất bại'}
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={isSubmitting !== null || !paymentTransactionId.trim()}
                onClick={() => void submit('success', undefined, amount.trim() ? Number(amount) : 999999)}
              >
                Kiểm thử sai số tiền
              </Button>
            </div>
          </form>

          {result ? (
            <div className="wallet-result">
              <h2 className="wallet-section-title">Kết quả mock</h2>
              <div className="wallet-banner">{walletCreditMessage}</div>
              <div className="wallet-result-row">
                <span>Trạng thái xử lý</span>
                <strong>
                  <span className={`wallet-status-badge ${statusClass(result.processingStatus)}`}>
                    {statusLabel(result.processingStatus)}
                  </span>
                </strong>
              </div>
              <div className="wallet-result-row">
                <span>Trạng thái chữ ký</span>
                <strong>
                  <span className={`wallet-status-badge ${statusClass(result.signatureStatus)}`}>
                    {statusLabel(result.signatureStatus)}
                  </span>
                </strong>
              </div>
              <div className="wallet-result-row">
                <span>Trạng thái giao dịch</span>
                <strong>
                  <span className={`wallet-status-badge ${statusClass(result.paymentStatus)}`}>
                    {statusLabel(result.paymentStatus)}
                  </span>
                </strong>
              </div>
              <div className="wallet-result-row">
                <span>Ví đã được cộng</span>
                <strong>{result.paymentStatus === 'Succeeded' && result.processingStatus === 'Processed' ? 'Có, nếu chưa xử lý trước đó' : 'Không'}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Mã webhook log</span>
                <strong className="wallet-code">{result.webhookLogId || 'Chưa có'}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Thông báo</span>
                <strong>{result.message || 'Hoàn tất'}</strong>
              </div>
            </div>
          ) : null}
        </section>
      </div>
    </main>
  );
}
