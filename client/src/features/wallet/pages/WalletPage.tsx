import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { walletApi } from '../api';
import type { Wallet } from '../types';
import './WalletLayout.css';
import { WalletNavigation } from './WalletNavigation';

function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0
  }).format(value);
}

function getStatusLabel(status: string) {
  if (status === 'Active') return 'Đang hoạt động';
  if (status === 'Suspended') return 'Tạm khóa';
  if (status === 'Closed') return 'Đã đóng';
  return status;
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
      setError(getApiErrorMessage(loadError, 'Không thể tải thông tin ví.'));
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
        <WalletNavigation />

        <header className="wallet-header">
          <p className="wallet-kicker">Tổng quan ví</p>
          <h1>Ví của tôi</h1>
          <p className="wallet-muted">
            Theo dõi số dư ví nội bộ, số tiền đang giữ và số tiền có thể dùng để thanh toán trên SmartRentalPlatform.
          </p>
        </header>

        <section className="wallet-panel">
          <h2 className="wallet-section-title">Thông tin số dư</h2>
          <p className="wallet-helper">
            Số dư đang giữ thường dùng cho tiền cọc và chưa thể sử dụng ngay. Số dư khả dụng là số tiền có thể dùng để thanh toán.
          </p>

          {isLoading ? (
            <div className="wallet-skeleton-grid" aria-label="Đang tải thông tin ví">
              <div className="wallet-skeleton-card" />
              <div className="wallet-skeleton-card" />
              <div className="wallet-skeleton-card" />
              <div className="wallet-skeleton-card" />
            </div>
          ) : null}
          {error ? <Alert type="error">{error}</Alert> : null}

          {!isLoading && wallet ? (
            <>
              {wallet.status !== 'Active' ? (
                <div className="wallet-banner wallet-banner-warning">
                  Ví hiện không ở trạng thái hoạt động. Bạn chưa thể nạp tiền hoặc thanh toán bằng ví này.
                </div>
              ) : null}

              {wallet.kycStatus && wallet.canTopUp === false ? (
                <div className="wallet-banner">
                  Trạng thái xác thực eKYC hiện tại: <strong>{wallet.kycStatus}</strong>. Hoàn tất xác thực được duyệt để nạp ví.
                </div>
              ) : null}

              <div className="wallet-grid">
                <article className="wallet-metric">
                  <span className="wallet-metric-label">Tổng số dư</span>
                  <strong className="wallet-metric-value">{formatMoney(wallet.balance, wallet.currency)}</strong>
                  <span className="wallet-metric-help">Tổng số tiền hiện có trong ví.</span>
                </article>
                <article className="wallet-metric">
                  <span className="wallet-metric-label">Số dư khả dụng</span>
                  <strong className="wallet-metric-value">{formatMoney(wallet.availableBalance, wallet.currency)}</strong>
                  <span className="wallet-metric-help">Số tiền có thể dùng để thanh toán.</span>
                </article>
                <article className="wallet-metric">
                  <span className="wallet-metric-label">Số dư đang giữ</span>
                  <strong className="wallet-metric-value">{formatMoney(wallet.reservedBalance, wallet.currency)}</strong>
                  <span className="wallet-metric-help">Tiền tạm giữ, chủ yếu cho đặt cọc.</span>
                </article>
                <article className="wallet-metric">
                  <span className="wallet-metric-label">Trạng thái ví</span>
                  <strong className="wallet-metric-value">
                    <span className={`wallet-status-badge ${wallet.status === 'Active' ? 'wallet-status-active' : 'wallet-status-default'}`}>
                      {getStatusLabel(wallet.status)}
                    </span>
                  </strong>
                  <span className="wallet-metric-help">Tiền tệ: {wallet.currency}</span>
                </article>
              </div>

              <div className="wallet-divider" />

              <div className="wallet-actions">
                <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TOPUP)}>
                  Nạp tiền
                </Button>
                <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET_TRANSACTIONS)}>
                  Xem lịch sử giao dịch
                </Button>
              </div>
            </>
          ) : null}
        </section>
      </div>
    </main>
  );
}
