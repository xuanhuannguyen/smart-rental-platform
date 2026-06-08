import { FormEvent, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Input } from '../../../shared/components/ui/Input';
import { walletApi } from '../api';
import type { MockPaymentResponse } from '../types';
import './WalletLayout.css';

type MockAction = 'success' | 'failed';

export function DevMockPaymentPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [paymentTransactionId, setPaymentTransactionId] = useState(searchParams.get('paymentTransactionId') ?? '');
  const [amount, setAmount] = useState('');
  const [result, setResult] = useState<MockPaymentResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState<MockAction | null>(null);

  async function submit(action: MockAction, event?: FormEvent) {
    event?.preventDefault();
    setError(null);
    setResult(null);
    setIsSubmitting(action);

    try {
      const parsedAmount = amount.trim() ? Number(amount) : undefined;
      const response = action === 'success'
        ? await walletApi.mockSuccess(paymentTransactionId.trim(), parsedAmount)
        : await walletApi.mockFailed(paymentTransactionId.trim(), parsedAmount);

      setResult(response.data);
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Khong the goi mock payment.'));
    } finally {
      setIsSubmitting(null);
    }
  }

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <header className="wallet-header">
          <div>
            <p className="wallet-muted">Development</p>
            <h1>Mock payment</h1>
            <p>Gia lap webhook PayOS thanh cong, that bai, hoac sai amount.</p>
          </div>
          <div className="wallet-actions">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TOPUP)}>
              Ve nap vi
            </Button>
          </div>
        </header>

        <section className="wallet-panel">
          <form className="wallet-form" onSubmit={(event) => void submit('success', event)}>
            <div className="wallet-field">
              <label htmlFor="mock-payment-id">Payment transaction ID</label>
              <Input
                id="mock-payment-id"
                value={paymentTransactionId}
                onChange={(event) => setPaymentTransactionId(event.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
                required
              />
            </div>

            <div className="wallet-field">
              <label htmlFor="mock-payment-amount">Amount override</label>
              <Input
                id="mock-payment-amount"
                type="number"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                placeholder="Dung de test wrong amount"
              />
            </div>

            {error ? <Alert type="error">{error}</Alert> : null}

            <div className="wallet-actions">
              <Button type="submit" disabled={isSubmitting !== null}>
                {isSubmitting === 'success' ? 'Dang xu ly...' : 'Mock success'}
              </Button>
              <Button
                type="button"
                variant="secondary"
                disabled={isSubmitting !== null || !paymentTransactionId.trim()}
                onClick={() => void submit('failed')}
              >
                {isSubmitting === 'failed' ? 'Dang xu ly...' : 'Mock failed'}
              </Button>
            </div>
          </form>

          {result ? (
            <div className="wallet-result">
              <div className="wallet-result-row">
                <span>Processing</span>
                <strong>{result.processingStatus}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Signature</span>
                <strong>{result.signatureStatus}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Payment status</span>
                <strong>{result.paymentStatus || 'Chua co'}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Webhook log</span>
                <strong className="wallet-code">{result.webhookLogId || 'Chua co'}</strong>
              </div>
              <div className="wallet-result-row">
                <span>Message</span>
                <strong>{result.message || 'Done'}</strong>
              </div>
              <div className="wallet-actions">
                <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
                  Xem ví
                </Button>
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
                  Xem lịch sử giao dịch
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
