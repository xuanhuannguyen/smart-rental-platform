import React, { useEffect, useState } from 'react';
import { walletApi } from '../api';
import type { WalletResponse, WalletTopUpResponse, WalletTopUpStatus } from '../types';
import './Wallet.css';
import { formatMoneyString } from '../../../shared/utils/format';
import { Button } from '../../../shared/components/ui/Button';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import { ROUTE_PATHS } from '../../../app/router/routePaths';

const TOP_UP_PAGE_SIZE = 10;

export const MyWalletPage: React.FC = () => {
  const [wallet, setWallet] = useState<WalletResponse | null>(null);
  const [topUps, setTopUps] = useState<WalletTopUpResponse[]>([]);
  const [topUpPage, setTopUpPage] = useState(1);
  const [hasMoreTopUps, setHasMoreTopUps] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isTopUpsLoading, setIsTopUpsLoading] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [showTopUp, setShowTopUp] = useState(false);
  const [topUpAmount, setTopUpAmount] = useState<number | ''>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    fetchInitialData();
  }, []);

  const fetchInitialData = async () => {
    setIsLoading(true);
    setPageError(null);

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
        setPageError(res.message || 'Không thể tải thông tin ví.');
      }
    } catch (err: any) {
      setPageError(err.message || 'Đã xảy ra lỗi khi tải thông tin ví.');
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
        setPageError(res.message || 'Không thể tải lịch sử yêu cầu nạp ví.');
      }
    } catch (err: any) {
      setPageError(err.message || 'Đã xảy ra lỗi khi tải lịch sử yêu cầu nạp ví.');
    } finally {
      setIsTopUpsLoading(false);
    }
  };

  const handleTopUpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!topUpAmount || topUpAmount < 10000 || !Number.isInteger(Number(topUpAmount))) {
      setValidationError('Số tiền nạp tối thiểu là 10.000đ và phải là số nguyên.');
      return;
    }

    try {
      setIsSubmitting(true);
      setValidationError(null);
      const returnUrl = `${window.location.origin}${ROUTE_PATHS.ACCOUNT.TOPUP_RESULT}`;
      const res = await walletApi.createPayOSTopUp({
        amount: Number(topUpAmount),
        returnUrl,
        cancelUrl: returnUrl
      });

      if (res.success && res.data?.paymentUrl) {
        window.location.href = res.data.paymentUrl;
      } else {
        setToast({ message: res.message || 'Không thể tạo yêu cầu nạp tiền.', type: 'error' });
        setIsSubmitting(false);
      }
    } catch (err: any) {
      setToast({ message: err.message || 'Đã xảy ra lỗi khi tạo yêu cầu nạp tiền.', type: 'error' });
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
      case 'Succeeded': return 'success';
      case 'Pending': return 'pending';
      case 'Expired': return 'expired';
      case 'Failed':
      case 'Cancelled':
      default:
        return 'failed';
    }
  };

  const getTopUpStatusIcon = (status: WalletTopUpStatus) => {
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
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="5" width="20" height="14" rx="2" ry="2" />
              <line x1="2" y1="10" x2="22" y2="10" />
            </svg>
          </div>
        }
        eyebrow="QUẢN LÝ"
        title="Ví của tôi"
        description="Quản lý số dư và nạp tiền vào ví của bạn"
      />

      {pageError && <Alert type="error">{pageError}</Alert>}

      {wallet && (
        <>
        <div className="wallet-card">
          <div className="wallet-summary-grid">
            <div className="wallet-balance-item">
              <div className="balance-item-icon-box available">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="2" y="5" width="20" height="14" rx="2" ry="2" />
                  <line x1="2" y1="10" x2="22" y2="10" />
                </svg>
              </div>
              <div className="balance-item-details">
                <span className="balance-item-label">Số dư khả dụng</span>
                <strong className="balance-item-value available-blue">{formatCurrency(wallet.availableBalance)}</strong>
              </div>
            </div>

            <div className="wallet-balance-item">
              <div className="balance-item-icon-box total">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <ellipse cx="12" cy="5" rx="9" ry="3" />
                  <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
                  <path d="M3 12c0 1.66 4 3 9 3s9-1.34 9-3" />
                </svg>
              </div>
              <div className="balance-item-details">
                <span className="balance-item-label">Tổng số dư</span>
                <strong className="balance-item-value">{formatCurrency(wallet.balance)}</strong>
              </div>
            </div>

            <div className="wallet-balance-item">
              <div className="balance-item-icon-box reserved">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
              </div>
              <div className="balance-item-details">
                <span className="balance-item-label">Số tiền đang giữ</span>
                <strong className="balance-item-value">{formatCurrency(wallet.reservedBalance)}</strong>
              </div>
            </div>
          </div>

          <div className="wallet-card-footer">
            <div className="wallet-card-footer-left">
              <div className="wallet-info-banner">
                <svg className="wallet-info-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="16" x2="12" y2="12" />
                  <line x1="12" y1="8" x2="12.01" y2="8" />
                </svg>
                <span className="wallet-info-text">
                  Số tiền đang giữ là khoản đã được khóa cho các giao dịch như tiền cọc, chưa thể sử dụng cho thanh toán khác.
                </span>
              </div>
            </div>

            <div className="wallet-card-footer-right">
              <div className={`wallet-status-tag ${wallet.status === 'Active' ? 'active' : 'locked'}`}>
                {wallet.status === 'Active' ? (
                  <>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
                      <polyline points="20 6 9 17 4 12" />
                    </svg>
                    Trạng thái: Đang hoạt động
                  </>
                ) : (
                  <>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
                      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                    </svg>
                    Trạng thái: Bị khóa
                  </>
                )}
              </div>

              <Button
                variant="primary"
                className="wallet-topup-btn"
                onClick={() => setShowTopUp(true)}
                disabled={wallet.status !== 'Active'}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                  <path d="M20 12V8H6a2 2 0 0 1-2-2c0-1.1.9-2 2-2h12v4" />
                  <path d="M4 6v12c0 1.1.9 2 2 2h14v-4" />
                  <path d="M18 12a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h4v-6Z" />
                  <line x1="12" y1="11" x2="12" y2="17" />
                  <line x1="9" y1="14" x2="15" y2="14" />
                </svg>
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
                setValidationError(null);
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
                  onClick={() => { setShowTopUp(false); setValidationError(null); }}
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
                  {validationError && (
                    <div style={{ color: '#dc2626', marginTop: '8px', fontSize: '13px' }}>
                      {validationError}
                    </div>
                  )}
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

      <div className="transaction-list-card">
        <div className="topup-history-header">
          <div className="topup-history-header-left">
            <div className="topup-history-icon-box">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10" />
                <polyline points="12 6 12 12 16 14" />
              </svg>
            </div>
            <div className="topup-history-header-text">
              <h3>Lịch sử yêu cầu nạp ví</h3>
              <p className="text-secondary">Theo dõi các yêu cầu nạp tiền đang chờ, đã hết hạn hoặc đã hoàn tất</p>
            </div>
          </div>
          <button
            type="button"
            className="wallet-refresh-btn"
            onClick={() => fetchTopUps(1, true)}
            disabled={isTopUpsLoading}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }} className={isTopUpsLoading ? 'spin' : ''}>
              <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
            </svg>
            Làm mới
          </button>
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
                      <span className={`wallet-status-badge ${getTopUpStatusClass(topUp.status)}`}>
                        {getTopUpStatusIcon(topUp.status)}
                        {getTopUpStatusText(topUp.status)}
                      </span>
                    </td>
                    <td>{formatDateTime(topUp.expiresAt)}</td>
                    <td>
                      {topUp.status === 'Pending' && topUp.providerCheckoutUrl ? (
                        <button
                          type="button"
                          className="wallet-refresh-btn"
                          onClick={() => continueTopUpPayment(topUp)}
                          style={{ padding: '6px 12px', fontSize: '12px' }}
                        >
                          Tiếp tục thanh toán
                        </button>
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
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
};
