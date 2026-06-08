import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { walletApi } from '../api';
import type { WalletTransaction } from '../types';
import './WalletLayout.css';

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN').format(value);
}

export function WalletTransactionsPage() {
  const navigate = useNavigate();
  const [items, setItems] = useState<WalletTransaction[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadTransactions = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await walletApi.getTransactions();
      setItems(response.data.items ?? []);
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'Khong the tai lich su giao dich.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadTransactions();
  }, [loadTransactions]);

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <header className="wallet-header">
          <div>
            <p className="wallet-muted">Ledger</p>
            <h1>Lich su vi</h1>
            <p>Cac dong bien dong balance va reserved balance.</p>
          </div>
          <div className="wallet-actions">
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
              Quay lại trang chủ
            </Button>
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.WALLET)}>
              Ve vi
            </Button>
            <Button type="button" onClick={() => void loadTransactions()}>
              Lam moi
            </Button>
          </div>
        </header>

        <section className="wallet-panel">
          {isLoading ? <LoadingState message="Dang tai lich su giao dich..." /> : null}
          {error ? <Alert type="error">{error}</Alert> : null}
          {!isLoading && !error && items.length === 0 ? (
            <Alert type="info">Chua co giao dich vi.</Alert>
          ) : null}

          {items.length > 0 ? (
            <div className="wallet-table-wrap">
              <table className="wallet-table">
                <thead>
                  <tr>
                    <th>Loai</th>
                    <th>Huong</th>
                    <th>So tien</th>
                    <th>Balance truoc</th>
                    <th>Balance sau</th>
                    <th>Thoi gian</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map(item => (
                    <tr key={item.id}>
                      <td>{item.transactionType}</td>
                      <td>{item.direction}</td>
                      <td>{formatMoney(item.amount)}</td>
                      <td>{formatMoney(item.balanceBefore)}</td>
                      <td>{formatMoney(item.balanceAfter)}</td>
                      <td>{new Date(item.createdAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </section>
      </div>
    </main>
  );
}
