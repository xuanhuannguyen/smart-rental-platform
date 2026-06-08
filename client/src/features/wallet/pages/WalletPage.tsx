import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { walletApi } from '../api';
import type { Wallet } from '../types';
import './WalletLayout.css';

function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0
  }).format(value);
}

export function WalletPage() {
  const navigate = useNavigate();
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadWallet = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await walletApi.getWallet();
      setWallet(response.data);
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'Khong the tai thong tin vi.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadWallet();
  }, [loadWallet]);

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <header className="wallet-header">
          <div>
            <p className="wallet-muted">Wallet</p>
            <h1>Vi cua toi</h1>
            <p>Theo doi so du vi noi bo va tien dang duoc giu.</p>
          </div>
          <div className="wallet-actions">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
              Lịch sử giao dịch
            </Button>
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TOPUP)}>
              Nạp tiền
            </Button>
          </div>
        </header>

        <section className="wallet-panel">
          {isLoading ? <LoadingState message="Dang tai vi..." /> : null}
          {error ? <Alert type="error">{error}</Alert> : null}

          {!isLoading && wallet ? (
            <>
              {wallet.kycStatus && wallet.canTopUp === false ? (
                <Alert type="info">Tai khoan KYC hien tai: {wallet.kycStatus}. Hoan tat KYC de nap vi.</Alert>
              ) : null}
              <div className="wallet-grid">
                <div className="wallet-metric">
                  <span>So du</span>
                  <strong>{formatMoney(wallet.balance, wallet.currency)}</strong>
                </div>
                <div className="wallet-metric">
                  <span>Dang giu</span>
                  <strong>{formatMoney(wallet.reservedBalance, wallet.currency)}</strong>
                </div>
                <div className="wallet-metric">
                  <span>Co the dung</span>
                  <strong>{formatMoney(wallet.availableBalance, wallet.currency)}</strong>
                </div>
                <div className="wallet-metric">
                  <span>Trang thai</span>
                  <strong>{wallet.status}</strong>
                </div>
              </div>
            </>
          ) : null}
        </section>
      </div>
    </main>
  );
}
