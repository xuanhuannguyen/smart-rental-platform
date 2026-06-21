import React, { useEffect, useState } from 'react';
import { walletApi } from '../api';
import type { WalletResponse, WalletTopUpResponse, WalletTopUpStatus } from '../types';
import './Wallet.css';
import { formatMoneyString } from '../../../shared/utils/format';
import { Button } from '../../../shared/components/ui/Button';
import { Alert } from '../../../shared/components/ui/Alert';
import { ROUTE_PATHS } from '../../../app/router/routePaths';

const TOP_UP_PAGE_SIZE = 10;

export const MyWalletPage: React.FC = () => {
  const [wallet, setWallet] = useState<WalletResponse | null>(null);
  const [topUps, setTopUps] = useState<WalletTopUpResponse[]>([]);
  const [topUpPage, setTopUpPage] = useState(1);
  const [hasMoreTopUps, setHasMoreTopUps] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isTopUpsLoading, setIsTopUpsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showTopUp, setShowTopUp] = useState(false);
  const [topUpAmount, setTopUpAmount] = useState<number | ''>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    fetchInitialData();
  }, []);

  const fetchInitialData = async () => {
    setIsLoading(true);
    setError(null);

    await Promise.all([
      fetchWallet(),
      fetchTopUps(1, true)
    ]);

    setIsLoading(false);
  };

  const fetchWallet = async () => {
    try {
      const res = await walletApi.getMyWallet();
      if (res.success && res.data) {
        setWallet(res.data);
      } else {
        setError(res.message || 'Không thể tải thông tin ví.');
      }
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi khi tải thông tin ví.');
    }
  };

  const fetchTopUps = async (pageNumber: number, reset: boolean = false) => {
    try {
      setIsTopUpsLoading(true);
      const res = await walletApi.getTopUps(pageNumber, TOP_UP_PAGE_SIZE);

      if (res.success && res.data) {
        setTopUps(prev => reset ? res.data!.items : [...prev, ...res.data!.items]);
        setTopUpPage(pageNumber);
        setHasMoreTopUps(res.data.page < res.data.totalPages);
      } else {
        setError(res.message || 'Không thể tải lịch sử yêu cầu nạp ví.');
      }
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi khi tải lịch sử yêu cầu nạp ví.');
    } finally {
      setIsTopUpsLoading(false);
    }
  };

  const handleTopUpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!topUpAmount || topUpAmount < 10000 || !Number.isInteger(Number(topUpAmount))) {
      setError('Số tiền nạp tối thiểu là 10.000đ và phải là số nguyên.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);
      const returnUrl = `${window.location.origin}${ROUTE_PATHS.ACCOUNT.TOPUP_RESULT}`;
      const res = await walletApi.createPayOSTopUp({
        amount: Number(topUpAmount),
        returnUrl,
        cancelUrl: returnUrl
      });

      if (res.success && res.data?.paymentUrl) {
        window.location.href = res.data.paymentUrl;
      } else {
        setError(res.message || 'Không thể tạo yêu cầu nạp tiền.');
        setIsSubmitting(false);
      }
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi khi tạo yêu cầu nạp tiền.');
      setIsSubmitting(false);
    }
  };

  const getTopUpStatusText = (status: WalletTopUpStatus) => {
    switch (status) {
      case 'Pending': return 'Đang chờ thanh toán';
      case 'Succeeded': return 'Thành công';
      case 'Failed': return 'Thất bại';
      case 'Expired': return 'Đã hết hạn';
      case 'Cancelled': return 'Đã hủy';
      default: return status;
    }
  };

  const getTopUpStatusClass = (status: WalletTopUpStatus) => {
    switch (status) {
      case 'Succeeded': return 'completed';
      case 'Pending': return 'pending';
      case 'Failed':
      case 'Expired':
      case 'Cancelled':
        return 'failed';
      default:
        return 'failed';
    }
  };

  const formatDateTime = (dateString?: string | null) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('vi-VN');
  };

  const continueTopUpPayment = (topUp: WalletTopUpResponse) => {
    if (topUp.providerCheckoutUrl) {
      window.location.href = topUp.providerCheckoutUrl;
    }
  };

  const formatCurrency = (value: number) => `${formatMoneyString(value) || '0'} đ`;

  if (isLoading) return <div>Đang tải thông tin ví...</div>;

  return (
    <div className="wallet-page-container">
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
        <h2>Ví của tôi</h2>
        <p className="text-secondary">Quản lý số dư và nạp tiền vào ví của bạn</p>
        </div>
      </section>

      {error && <Alert type="error">{error}</Alert>}

      {wallet && (
        <>
        <div className="wallet-card">
          <div className="wallet-summary-grid">
            <div className="wallet-balance-item wallet-balance-primary">
              <span>Số dư khả dụng</span>
              <strong>{formatCurrency(wallet.availableBalance)}</strong>
            </div>
            <div className="wallet-balance-item">
              <span>Tổng số dư</span>
              <strong>{formatCurrency(wallet.balance)}</strong>
            </div>
            <div className="wallet-balance-item">
              <span>Số tiền đang giữ</span>
              <strong>{formatCurrency(wallet.reservedBalance)}</strong>
            </div>
          </div>

          <div className="wallet-card-footer">
            <p className="wallet-balance-note">
              Số tiền đang giữ là khoản đã được khóa cho các giao dịch như tiền cọc, chưa thể sử dụng cho thanh toán khác.
            </p>

            <div className="wallet-card-actions">
              <div className={`wallet-status ${wallet.status.toLowerCase()}`}>
                Trạng thái: {wallet.status === 'Active' ? 'Đang hoạt động' : 'Bị khóa'}
              </div>

              <Button
                variant="primary"
                onClick={() => setShowTopUp(true)}
                disabled={wallet.status !== 'Active'}
              >
                Nạp tiền vào ví
              </Button>
            </div>
          </div>
        </div>

        {showTopUp && (
          <div
            className="wallet-modal-backdrop"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget && !isSubmitting) {
                setShowTopUp(false);
              }
            }}
          >
            <div className="wallet-topup-modal" role="dialog" aria-modal="true" aria-labelledby="topup-modal-title">
              <div className="wallet-topup-modal-header">
                <div>
                  <p className="wallet-modal-eyebrow">Nạp ví</p>
                  <h3 id="topup-modal-title">Chọn số tiền muốn nạp</h3>
                </div>
                <button
                  type="button"
                  className="wallet-modal-close"
                  onClick={() => setShowTopUp(false)}
                  disabled={isSubmitting}
                  aria-label="Đóng"
                >
                  ×
                </button>
              </div>

              <div className="quick-amount-buttons">
                {[50000, 100000, 200000, 500000, 1000000].map(amount => (
                  <Button
                    key={amount}
                    variant="outline"
                    type="button"
                    onClick={() => setTopUpAmount(amount)}
                  >
                    {formatCurrency(amount)}
                  </Button>
                ))}
              </div>

              <form onSubmit={handleTopUpSubmit} className="wallet-topup-form">
                <div className="form-group mb-3">
                  <label htmlFor="topUpAmount">Hoặc nhập số tiền khác (VNĐ)</label>
                  <input
                    id="topUpAmount"
                    type="number"
                    className="form-control"
                    value={topUpAmount}
                    onChange={(e) => setTopUpAmount(e.target.value ? Number(e.target.value) : '')}
                    placeholder="Ví dụ: 100000"
                    min={10000}
                    step={1000}
                  />
                </div>
                <Button
                  type="submit"
                  variant="primary"
                  disabled={isSubmitting}
                  className="w-100"
                >
                  {isSubmitting ? 'Đang xử lý...' : 'Xác nhận và Thanh toán qua PayOS'}
                </Button>
              </form>
            </div>
          </div>
        )}
        </>
      )}

      <div className="transaction-list-container">
        <div className="topup-history-header">
          <div>
            <h3>Lịch sử yêu cầu nạp ví</h3>
            <p className="text-secondary">Theo dõi các yêu cầu nạp tiền đang chờ, đã hết hạn hoặc đã hoàn tất</p>
          </div>
          <Button
            variant="outline"
            type="button"
            onClick={() => fetchTopUps(1, true)}
            disabled={isTopUpsLoading}
          >
            Làm mới
          </Button>
        </div>

        <div className="table-responsive">
          <table className="transaction-table">
            <thead>
              <tr>
                <th>Ngày tạo</th>
                <th>Mã giao dịch</th>
                <th>Số tiền</th>
                <th>Trạng thái</th>
                <th>Hết hạn</th>
                <th>Hành động</th>
              </tr>
            </thead>
            <tbody>
              {topUps.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-4">Chưa có yêu cầu nạp ví nào</td>
                </tr>
              ) : (
                topUps.map(topUp => (
                  <tr key={topUp.id}>
                    <td>{formatDateTime(topUp.createdAt)}</td>
                    <td>{topUp.providerOrderCode || topUp.id.substring(0, 8)}</td>
                    <td>{formatCurrency(topUp.amount)}</td>
                    <td>
                      <span className={`status-badge status-${getTopUpStatusClass(topUp.status)}`}>
                        {getTopUpStatusText(topUp.status)}
                      </span>
                    </td>
                    <td>{formatDateTime(topUp.expiresAt)}</td>
                    <td>
                      {topUp.status === 'Pending' && topUp.providerCheckoutUrl ? (
                        <Button
                          variant="outline"
                          type="button"
                          onClick={() => continueTopUpPayment(topUp)}
                        >
                          Tiếp tục thanh toán
                        </Button>
                      ) : (
                        <span className="text-secondary">-</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {hasMoreTopUps && (
          <div className="text-center mt-4">
            <Button
              variant="outline"
              type="button"
              onClick={() => fetchTopUps(topUpPage + 1)}
              disabled={isTopUpsLoading}
            >
              {isTopUpsLoading ? 'Đang tải...' : 'Xem thêm'}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
};
