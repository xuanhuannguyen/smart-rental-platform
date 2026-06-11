import { useCallback, useEffect, useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { walletApi } from '../api';
import type { WalletTransaction } from '../types';
import './WalletLayout.css';
import { WalletNavigation } from './WalletNavigation';

const PageSize = 10;

function formatMoney(value: number) {
  return `${new Intl.NumberFormat('vi-VN').format(value)}đ`;
}

function formatSignedMoney(value: number, direction: string) {
  const isCredit = direction.toLowerCase().includes('credit');
  return `${isCredit ? '+' : '-'}${formatMoney(value)}`;
}

function transactionTypeLabel(type: string) {
  const labels: Record<string, string> = {
    WalletTopUp: 'Nạp ví',
    DepositPayment: 'Thanh toán cọc',
    DepositReceive: 'Nhận tiền cọc',
    InvoicePayment: 'Thanh toán hóa đơn',
    InvoiceReceive: 'Nhận tiền hóa đơn',
    DepositRefundDebit: 'Hoàn cọc cho khách',
    DepositRefundCredit: 'Nhận hoàn cọc',
    DepositForfeitRelease: 'Giải phóng tiền cọc',
    ManualAdjustment: 'Điều chỉnh thủ công'
  };

  return labels[type] ?? type;
}

function directionLabel(direction: string) {
  if (direction.toLowerCase().includes('credit')) return 'Cộng tiền';
  if (direction.toLowerCase().includes('debit')) return 'Trừ tiền';
  return direction;
}

function ledgerStatusLabel(status: string) {
  const normalized = status.toLowerCase();
  if (normalized.includes('succeed') || normalized.includes('success')) return 'Thành công';
  if (normalized.includes('fail')) return 'Thất bại';
  if (normalized.includes('reverse')) return 'Đã hoàn tác';
  return status;
}

function statusClass(status: string) {
  const normalized = status.toLowerCase();
  if (normalized.includes('succeed') || normalized.includes('success')) return 'wallet-status-succeeded';
  if (normalized.includes('fail')) return 'wallet-status-failed';
  if (normalized.includes('reverse')) return 'wallet-status-expired';
  return 'wallet-status-default';
}

function directionClass(direction: string) {
  return direction.toLowerCase().includes('credit') ? 'wallet-direction-credit' : 'wallet-direction-debit';
}

function amountClassForDirection(direction: string) {
  return direction.toLowerCase().includes('credit') ? 'wallet-amount-credit' : 'wallet-amount-debit';
}

export function WalletTransactionsPage() {
  const [ledgerItems, setLedgerItems] = useState<WalletTransaction[]>([]);
  const [ledgerPage, setLedgerPage] = useState(1);
  const [ledgerTotalPages, setLedgerTotalPages] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadLedger = useCallback(async (page: number) => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await walletApi.getTransactions(page, PageSize);
      setLedgerItems(response.data.items ?? []);
      setLedgerTotalPages(response.data.totalPages ?? 0);
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'Không thể tải dữ liệu. Vui lòng thử lại.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadLedger(ledgerPage);
  }, [loadLedger, ledgerPage]);

  const hasNextPage = ledgerItems.length >= PageSize
    && (ledgerTotalPages === 0 || ledgerPage < ledgerTotalPages);

  return (
    <main className="wallet-page">
      <div className="wallet-shell">
        <WalletNavigation />

        <header className="wallet-header">
          <p className="wallet-kicker">Lịch sử ví</p>
          <h1>Lịch sử biến động số dư ví</h1>
          <p className="wallet-muted">
            Lịch sử này chỉ hiển thị các giao dịch đã làm thay đổi số dư ví. Các yêu cầu nạp đang chờ xác nhận sẽ được xử lý tại trang nạp tiền hoặc trang kết quả PayOS.
          </p>
        </header>

        <section className="wallet-panel">
          <div className="wallet-banner">
            Chỉ những giao dịch đã tác động đến số dư ví mới xuất hiện tại đây. Nếu bạn vừa thanh toán PayOS, vui lòng chờ webhook xác nhận rồi tải lại lịch sử.
          </div>

          <div className="wallet-actions wallet-actions-spaced">
            <Button type="button" onClick={() => void loadLedger(ledgerPage)} disabled={isLoading}>
              Tải lại dữ liệu
            </Button>
          </div>
        </section>

        <section className="wallet-panel">
          <h2 className="wallet-section-title">Lịch sử biến động số dư</h2>
          <p className="wallet-helper">
            Mỗi dòng bên dưới là một lần số dư ví hoặc số dư đang giữ được cập nhật thành công trong hệ thống.
          </p>

          {isLoading ? (
            <div className="wallet-transaction-list" aria-label="Đang tải dữ liệu">
              <div className="wallet-skeleton-card" />
              <div className="wallet-skeleton-card" />
            </div>
          ) : null}
          {error ? <Alert type="error">{error}</Alert> : null}

          {!isLoading && !error && ledgerItems.length === 0 ? (
            <div className="wallet-empty">
              <strong>Bạn chưa có biến động số dư nào.</strong>
              Sau khi nạp ví được xác nhận hoặc thanh toán thành công, dòng sổ cái sẽ xuất hiện tại đây.
            </div>
          ) : null}

          {!isLoading && !error && ledgerItems.length > 0 ? (
            <>
              <div className="wallet-transaction-list">
                {ledgerItems.map(item => (
                  <article className="wallet-transaction-card" key={item.id}>
                    <div className="wallet-transaction-main">
                      <div>
                        <h3 className="wallet-transaction-title">{transactionTypeLabel(item.transactionType)}</h3>
                        <div className="wallet-transaction-meta">
                          <span className={`wallet-status-badge ${directionClass(item.direction)}`}>
                            {directionLabel(item.direction)}
                          </span>
                          <span className={`wallet-status-badge ${statusClass(item.status)}`}>
                            {ledgerStatusLabel(item.status)}
                          </span>
                        </div>
                      </div>
                      <div className={`wallet-transaction-amount ${amountClassForDirection(item.direction)}`}>
                        {formatSignedMoney(item.amount, item.direction)}
                      </div>
                    </div>

                    <div className="wallet-transaction-details">
                      <div>
                        <span className="wallet-detail-label">Loại giao dịch</span>
                        <strong className="wallet-detail-value">{transactionTypeLabel(item.transactionType)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Cộng/Trừ</span>
                        <strong className="wallet-detail-value">{directionLabel(item.direction)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Số tiền</span>
                        <strong className="wallet-detail-value">{formatSignedMoney(item.amount, item.direction)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Trạng thái</span>
                        <strong className="wallet-detail-value">{ledgerStatusLabel(item.status)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Số dư trước</span>
                        <strong className="wallet-detail-value">{formatMoney(item.balanceBefore)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Số dư sau</span>
                        <strong className="wallet-detail-value">{formatMoney(item.balanceAfter)}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Thời gian</span>
                        <strong className="wallet-detail-value">{new Date(item.createdAt).toLocaleString('vi-VN')}</strong>
                      </div>
                      <div>
                        <span className="wallet-detail-label">Mô tả</span>
                        <strong className="wallet-detail-value">{item.description || 'Không có ghi chú'}</strong>
                      </div>
                    </div>
                  </article>
                ))}
              </div>

              <div className="wallet-pagination">
                <span>Trang {ledgerPage}</span>
                <div className="wallet-actions">
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={isLoading || ledgerPage <= 1}
                    onClick={() => setLedgerPage(page => Math.max(1, page - 1))}
                  >
                    Trang trước
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={isLoading || !hasNextPage}
                    onClick={() => setLedgerPage(page => page + 1)}
                  >
                    Trang sau
                  </Button>
                </div>
              </div>
            </>
          ) : null}
        </section>
      </div>
    </main>
  );
}
