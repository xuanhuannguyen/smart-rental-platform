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
      case 'Succeeded': return 'completed';
      case 'Pending': return 'pending';
      case 'Failed':
      case 'Expired':
      case 'Cancelled':
        return 'failed';
      default:
        return status.toLowerCase();
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

  return (
    <div className="wallet-page-container">
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
        <h2>Lịch sử giao dịch</h2>
        <p className="text-secondary">Theo dõi dòng tiền ra vào ví của bạn</p>
        </div>
      </section>

      {error && <Alert type="error">{error}</Alert>}

      <div className="transaction-list-container">
        <div className="transaction-tabs">
          <button
            className={`transaction-tab ${activeTab === 'All' ? 'active' : ''}`}
            onClick={() => setActiveTab('All')}
          >
            Tất cả
          </button>
          <button
            className={`transaction-tab ${activeTab === 'Credit' ? 'active' : ''}`}
            onClick={() => setActiveTab('Credit')}
          >
            Tiền vào
          </button>
          <button
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
                    <td>{getTransactionReference(transaction).substring(0, 8)}</td>
                    <td>{getTransactionDescription(transaction)}</td>
                    <td className={transaction.direction === 'Credit' ? 'amount-in' : 'amount-out'}>
                      {transaction.direction === 'Credit' ? '+' : '-'}{formatMoneyString(transaction.amount)} đ
                    </td>
                    <td>
                      <span className={`status-badge status-${getStatusClass(transaction.status)}`}>
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
