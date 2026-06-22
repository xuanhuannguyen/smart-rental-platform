import React, { useEffect, useState } from 'react';
import { walletApi } from '../api';
import type { WalletTransactionDirection, WalletTransactionResponse } from '../types';
import './Wallet.css';
import { formatMoneyString } from '../../../shared/utils/format';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';

type FilterTab = 'All' | WalletTransactionDirection;

export const TransactionHistoryPage: React.FC = () => {
  const [transactions, setTransactions] = useState<WalletTransactionResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [activeTab, setActiveTab] = useState<FilterTab>('All');
  const [copiedId, setCopiedId] = useState<string | null>(null);

  useEffect(() => {
    fetchTransactions(1, true);
  }, []);

  const fetchTransactions = async (pageNumber: number, reset: boolean = false) => {
    try {
      setIsLoading(true);
      const res = await walletApi.getTransactions(pageNumber, 20);
      if (res.success && res.data) {
        setTransactions(prev => reset ? res.data!.items : [...prev, ...res.data!.items]);
        setHasMore(res.data.page < res.data.totalPages);
        setPage(pageNumber);
      } else {
        setError(res.message || 'Không thể tải lịch sử giao dịch.');
      }
    } catch (err: any) {
      setError(err.message || 'Lỗi kết nối.');
    } finally {
      setIsLoading(false);
    }
  };

  const filteredTransactions = transactions.filter(transaction => {
    if (activeTab === 'All') return true;
    return transaction.direction === activeTab;
  });

  const getStatusText = (status: string) => {
    switch (status) {
      case 'Succeeded': return 'Thành công';
      case 'Pending': return 'Đang xử lý';
      case 'Failed': return 'Thất bại';
      case 'Expired': return 'Đã hết hạn';
      case 'Cancelled': return 'Đã hủy';
      default: return status;
    }
  };

  const getStatusClass = (status: string) => {
    switch (status) {
      case 'Succeeded': return 'success';
      case 'Pending': return 'pending';
      case 'Expired': return 'expired';
      case 'Failed':
      case 'Cancelled':
      default:
        return 'failed';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Succeeded':
        return (
          <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
            <polyline points="20 6 9 17 4 12" />
          </svg>
        );
      case 'Pending':
        return (
          <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
            <circle cx="12" cy="12" r="10" />
            <polyline points="12 6 12 12 16 14" />
          </svg>
        );
      case 'Failed':
      case 'Expired':
      case 'Cancelled':
      default:
        return (
          <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        );
    }
  };

  const getTransactionTypeText = (transactionType: string) => {
    switch (transactionType) {
      case 'WalletTopUp': return 'Nạp ví';
      case 'DepositPayment': return 'Thanh toán tiền cọc';
      case 'DepositReceive': return 'Nhận tiền cọc';
      case 'InvoicePayment': return 'Thanh toán hóa đơn';
      case 'InvoiceReceive': return 'Nhận tiền hóa đơn';
      case 'DepositRefundDebit': return 'Hoàn tiền cọc';
      case 'DepositRefundCredit': return 'Nhận hoàn tiền cọc';
      case 'DepositForfeitRelease': return 'Tất toán/tịch thu tiền cọc';
      case 'ManualAdjustment': return 'Điều chỉnh thủ công';
      default: return transactionType;
    }
  };

  const getTransactionReference = (transaction: WalletTransactionResponse) => {
    return transaction.transferGroupId
      || transaction.relatedEntityId
      || transaction.id;
  };

  const getTransactionDescription = (transaction: WalletTransactionResponse) => {
    return transaction.description || getTransactionTypeText(transaction.transactionType);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('vi-VN');
  };

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(text);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const formatCurrency = (value: number) => `${formatMoneyString(value) || '0'} đ`;

  // Stats calculations
  const totalTransactions = transactions.length;
  const creditCount = transactions.filter(t => t.direction === 'Credit').length;
  const debitCount = transactions.filter(t => t.direction === 'Debit').length;
  const totalCredit = transactions
    .filter(t => t.direction === 'Credit' && t.status === 'Succeeded')
    .reduce((sum, t) => sum + t.amount, 0);
  const totalDebit = transactions
    .filter(t => t.direction === 'Debit' && t.status === 'Succeeded')
    .reduce((sum, t) => sum + t.amount, 0);

  if (isLoading && page === 1) return <div>Đang tải lịch sử giao dịch...</div>;

  return (
    <div className="wallet-page-container">
      <div className="wallet-header-band">
        <div className="wallet-header-title-row">
          <div className="wallet-header-icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="5" width="20" height="14" rx="2" ry="2" />
              <line x1="2" y1="10" x2="22" y2="10" />
            </svg>
          </div>
          <div>
            <p className="wallet-modal-eyebrow" style={{ margin: '0 0 4px 0', fontSize: '11px', color: '#246bfe', fontWeight: 'bold' }}>QUẢN LÝ</p>
            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: 'bold', color: '#0f172a' }}>Lịch sử giao dịch</h2>
            <p className="text-secondary" style={{ margin: '4px 0 0 0', fontSize: '13px', color: '#64748b' }}>Theo dõi dòng tiền ra vào ví của bạn</p>
          </div>
        </div>
        <div className="wallet-header-illustration">
          <svg width="120" height="80" viewBox="0 0 120 80" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect x="15" y="20" width="90" height="55" rx="8" fill="#eff6ff" stroke="#cbd5e1" strokeWidth="1.5" />
            <rect x="25" y="10" width="70" height="15" rx="4" fill="#3b82f6" opacity="0.15" />
            <path d="M85 35H105C107.761 35 110 37.2386 110 40V50C110 52.7614 107.761 55 105 55H85V35Z" fill="#eff6ff" stroke="#cbd5e1" strokeWidth="1.5" />
            <circle cx="95" cy="45" r="3" fill="#3b82f6" />
            <circle cx="50" cy="45" r="10" fill="#fbcfe8" opacity="0.25" />
            <circle cx="50" cy="45" r="6" fill="#ec4899" opacity="0.15" />
          </svg>
        </div>
      </div>

      {error && <Alert type="error">{error}</Alert>}

      <div className="wallet-card" style={{ marginBottom: '24px' }}>
        <div className="wallet-summary-grid">
          <div className="wallet-balance-item">
            <div className="balance-item-icon-box total">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
                <polyline points="10 9 9 9 8 9" />
              </svg>
            </div>
            <div className="balance-item-details" style={{ flexGrow: 1 }}>
              <span className="balance-item-label">Tổng giao dịch</span>
              <strong className="balance-item-value">{totalTransactions}</strong>
              <span style={{ fontSize: '12px', color: '#64748b' }}>Giao dịch</span>
            </div>
            <div className="trend-indicator blue">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="23 6 13.5 15.5 8.5 10.5 1 18" />
                <polyline points="17 6 23 6 23 12" />
              </svg>
            </div>
          </div>

          <div className="wallet-balance-item">
            <div className="balance-item-icon-box credit">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="5" x2="12" y2="19" />
                <polyline points="19 12 12 19 5 12" />
              </svg>
            </div>
            <div className="balance-item-details" style={{ flexGrow: 1 }}>
              <span className="balance-item-label">Tổng tiền vào</span>
              <strong className="balance-item-value credit-blue">+{formatCurrency(totalCredit)}</strong>
              <span style={{ fontSize: '12px', color: '#64748b' }}>{creditCount} giao dịch</span>
            </div>
            <div className="trend-indicator green">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="23 6 13.5 15.5 8.5 10.5 1 18" />
                <polyline points="17 6 23 6 23 12" />
              </svg>
            </div>
          </div>

          <div className="wallet-balance-item">
            <div className="balance-item-icon-box debit">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="19" x2="12" y2="5" />
                <polyline points="5 12 12 5 19 12" />
              </svg>
            </div>
            <div className="balance-item-details" style={{ flexGrow: 1 }}>
              <span className="balance-item-label">Tổng tiền ra</span>
              <strong className="balance-item-value">{formatCurrency(totalDebit)}</strong>
              <span style={{ fontSize: '12px', color: '#64748b' }}>{debitCount} giao dịch</span>
            </div>
            <div className="trend-indicator red">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="23 18 13.5 8.5 8.5 13.5 1 6" />
                <polyline points="17 18 23 18 23 12" />
              </svg>
            </div>
          </div>
        </div>
      </div>

      <div className="transaction-list-card">
        <div className="transaction-tabs" style={{ justifyContent: 'flex-start' }}>
          <button
            type="button"
            className={`transaction-tab ${activeTab === 'All' ? 'active' : ''}`}
            onClick={() => setActiveTab('All')}
          >
            Tất cả
          </button>
          <button
            type="button"
            className={`transaction-tab ${activeTab === 'Credit' ? 'active' : ''}`}
            onClick={() => setActiveTab('Credit')}
          >
            Tiền vào
          </button>
          <button
            type="button"
            className={`transaction-tab ${activeTab === 'Debit' ? 'active' : ''}`}
            onClick={() => setActiveTab('Debit')}
          >
            Tiền ra
          </button>
        </div>

        <div className="table-responsive">
          <table className="transaction-table">
            <thead>
              <tr>
                <th>Ngày/Giờ</th>
                <th>Mã GD</th>
                <th>Nội dung</th>
                <th>Số tiền</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {filteredTransactions.length === 0 ? (
                <tr>
                  <td colSpan={5} className="text-center py-4">Không có giao dịch nào</td>
                </tr>
              ) : (
                filteredTransactions.map(transaction => (
                  <tr key={transaction.id}>
                    <td>{formatDate(transaction.createdAt)}</td>
                    <td>
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                        <span>{getTransactionReference(transaction).substring(0, 8)}</span>
                        <button
                          type="button"
                          onClick={() => handleCopy(getTransactionReference(transaction))}
                          style={{ background: 'none', border: 'none', padding: 0, color: '#246bfe', cursor: 'pointer', display: 'inline-flex' }}
                          title="Sao chép mã giao dịch"
                        >
                          {copiedId === getTransactionReference(transaction) ? (
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#10b981" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                              <polyline points="20 6 9 17 4 12" />
                            </svg>
                          ) : (
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                              <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
                            </svg>
                          )}
                        </button>
                      </span>
                    </td>
                    <td>{getTransactionDescription(transaction)}</td>
                    <td className={transaction.direction === 'Credit' ? 'amount-in' : 'amount-out'}>
                      {transaction.direction === 'Credit' ? '+' : '-'}{formatMoneyString(transaction.amount)} đ
                    </td>
                    <td>
                      <span className={`wallet-status-badge ${getStatusClass(transaction.status)}`}>
                        {getStatusIcon(transaction.status)}
                        {getStatusText(transaction.status)}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {hasMore && (
          <div className="text-center mt-4">
            <Button
              variant="outline"
              type="button"
              onClick={() => fetchTransactions(page + 1)}
              disabled={isLoading}
            >
              {isLoading ? 'Đang tải...' : 'Xem thêm'}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};
