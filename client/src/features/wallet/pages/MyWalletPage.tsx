import React, { useEffect, useState } from 'react';
import { walletApi } from '../api';
import type { WalletResponse, WalletTopUpResponse, WalletTopUpStatus, WithdrawalRequestResponse } from '../types';
import './Wallet.css';
import { formatMoneyString } from '../../../shared/utils/format';
import { Button } from '../../../shared/components/ui/Button';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { profileApi } from '../../profile/services/profileApi';

const normalizeName = (name: string) => {
  return name
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9 ]/g, "")
    .trim()
    .replace(/\s+/g, " ");
};

const TOP_UP_PAGE_SIZE = 10;

export const MyWalletPage: React.FC = () => {
  const [wallet, setWallet] = useState<WalletResponse | null>(null);
  const [topUps, setTopUps] = useState<WalletTopUpResponse[]>([]);
  const [withdrawals, setWithdrawals] = useState<WithdrawalRequestResponse[]>([]);
  const [topUpPage, setTopUpPage] = useState(1);
  const [hasMoreTopUps, setHasMoreTopUps] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isTopUpsLoading, setIsTopUpsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showTopUp, setShowTopUp] = useState(false);
  const [topUpAmount, setTopUpAmount] = useState<number | ''>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Withdraw state
  const [showWithdraw, setShowWithdraw] = useState(false);
  const [withdrawAmount, setWithdrawAmount] = useState<number | ''>('');
  const [withdrawBankBin, setWithdrawBankBin] = useState('');
  const [customBankBin, setCustomBankBin] = useState('');
  const [withdrawAccountNumber, setWithdrawAccountNumber] = useState('');
  const [withdrawAccountName, setWithdrawAccountName] = useState('');
  const [isWithdrawing, setIsWithdrawing] = useState(false);
  const [withdrawError, setWithdrawError] = useState<string | null>(null);

  // Toast state
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [toastType, setToastType] = useState<'success' | 'error' | 'info'>('success');
  const [verifiedKycName, setVerifiedKycName] = useState('');

  useEffect(() => {
    fetchInitialData();
  }, []);

  const fetchInitialData = async () => {
    setIsLoading(true);
    setError(null);

    await Promise.all([
      fetchWallet(),
      fetchTransactions(1, true),
      fetchProfile()
    ]);

    setIsLoading(false);
  };

  const fetchProfile = async () => {
    try {
      const res = await profileApi.getProfile();
      if (res.success && res.data) {
        setVerifiedKycName(res.data.fullName || res.data.displayName || '');
      }
    } catch (err) {
      console.error('Failed to fetch profile', err);
    }
  };

  const fetchTransactions = async (pageNumber: number, reset: boolean = false) => {
    try {
      setIsTopUpsLoading(true);
      const [topUpRes, withdrawRes] = await Promise.all([
        walletApi.getTopUps(pageNumber, TOP_UP_PAGE_SIZE),
        walletApi.getMyWithdrawals(pageNumber, TOP_UP_PAGE_SIZE)
      ]);

      if (topUpRes.success && topUpRes.data) {
        setTopUps(prev => reset ? topUpRes.data!.items : [...prev, ...topUpRes.data!.items]);
        setTopUpPage(pageNumber);
        setHasMoreTopUps(topUpRes.data.page < topUpRes.data.totalPages);
      }
      
      if (withdrawRes.success && withdrawRes.data) {
        setWithdrawals(prev => reset ? withdrawRes.data!.items : [...prev, ...withdrawRes.data!.items]);
      }
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi khi tải lịch sử giao dịch.');
    } finally {
      setIsTopUpsLoading(false);
    }
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

  const handleWithdrawSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!withdrawAmount || withdrawAmount < 10000 || !Number.isInteger(Number(withdrawAmount))) {
      setWithdrawError('Số tiền rút tối thiểu là 10.000đ và phải là số nguyên.');
      return;
    }
    const finalBankBin = withdrawBankBin === 'other' ? customBankBin : withdrawBankBin;
    if (!finalBankBin.trim() || !withdrawAccountNumber.trim() || !withdrawAccountName.trim()) {
      setWithdrawError('Vui lòng nhập đầy đủ thông tin tài khoản ngân hàng.');
      return;
    }

    const normalizedInputName = normalizeName(withdrawAccountName);
    const normalizedKycName = normalizeName(verifiedKycName);

    if (normalizedInputName !== normalizedKycName) {
      setWithdrawError('Tên chủ tài khoản không khớp với tên xác thực KYC của bạn.');
      return;
    }

    const payloadName = normalizedInputName.toUpperCase();

    try {
      setIsWithdrawing(true);
      setWithdrawError(null);
      const res = await walletApi.requestWithdrawal({
        amount: Number(withdrawAmount),
        bankBin: finalBankBin,
        accountNumber: withdrawAccountNumber,
        accountName: payloadName
      });

      if (res.success) {
        setShowWithdraw(false);
        setWithdrawAmount('');
        setWithdrawBankBin('');
        setCustomBankBin('');
        setWithdrawAccountNumber('');
        setWithdrawAccountName('');
        setToastMessage('Yêu cầu rút tiền đã được gửi thành công.');
        setToastType('success');
        fetchInitialData();
      } else {
        setWithdrawError(res.message || 'Không thể tạo yêu cầu rút tiền.');
      }
    } catch (err: any) {
      console.error('Lỗi khi rút tiền:', err);
      const apiMessage = err.response?.data?.message || err.message;
      setWithdrawError(apiMessage || 'Đã xảy ra lỗi khi tạo yêu cầu rút tiền.');
    } finally {
      setIsWithdrawing(false);
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

  const continueTopUpPayment = (url?: string | null) => {
    if (url) {
      window.location.href = url;
    }
  };

  const getCombinedTransactions = () => {
    const combined = [
      ...topUps.map(t => ({ ...t, _type: 'TopUp' as const })),
      ...withdrawals.map(w => ({ ...w, _type: 'Withdrawal' as const }))
    ];
    return combined.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  };

  const formatCurrency = (value: number) => `${formatMoneyString(value) || '0'} đ`;

  if (isLoading) return <div>Đang tải thông tin ví...</div>;

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
            <h2 style={{ margin: 0, fontSize: '24px', fontWeight: 'bold', color: '#0f172a' }}>Ví của tôi</h2>
            <p className="text-secondary" style={{ margin: '4px 0 0 0', fontSize: '13px', color: '#64748b' }}>Quản lý số dư và nạp tiền vào ví của bạn</p>
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
                variant="outline"
                className="wallet-topup-btn"
                onClick={() => setShowWithdraw(true)}
                disabled={wallet.status !== 'Active'}
                style={{ marginRight: '10px' }}
              >
                Rút tiền
              </Button>

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

        {showWithdraw && (
          <div
            className="wallet-modal-backdrop"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget && !isWithdrawing) {
                setShowWithdraw(false);
              }
            }}
          >
            <div className="wallet-topup-modal" role="dialog" aria-modal="true" aria-labelledby="withdraw-modal-title">
              <div className="wallet-topup-modal-header">
                <div>
                  <p className="wallet-modal-eyebrow">Rút tiền</p>
                  <h3 id="withdraw-modal-title">Thông tin rút tiền</h3>
                </div>
                <button
                  type="button"
                  className="wallet-modal-close"
                  onClick={() => setShowWithdraw(false)}
                  disabled={isWithdrawing}
                  aria-label="Đóng"
                >
                  ×
                </button>
              </div>

              {withdrawError && <Alert type="error">{withdrawError}</Alert>}

              <form onSubmit={handleWithdrawSubmit} className="wallet-topup-form" style={{ marginTop: withdrawError ? '16px' : '0' }}>
                <div className="form-group mb-3">
                  <label htmlFor="withdrawAmount">Số tiền muốn rút (VNĐ)</label>
                  <input
                    id="withdrawAmount"
                    type="number"
                    className="form-control"
                    value={withdrawAmount}
                    onChange={(e) => setWithdrawAmount(e.target.value ? Number(e.target.value) : '')}
                    placeholder="Ví dụ: 100000"
                    min={10000}
                    step={1000}
                    required
                    disabled={isWithdrawing}
                  />
                </div>
                <div className="form-group mb-3">
                  <label htmlFor="withdrawBankBin">Ngân hàng</label>
                  <select
                    id="withdrawBankBin"
                    className="form-control"
                    value={withdrawBankBin}
                    onChange={(e) => {
                      setWithdrawBankBin(e.target.value);
                      if (e.target.value !== 'other') {
                        setCustomBankBin('');
                      }
                    }}
                    required
                    disabled={isWithdrawing}
                  >
                    <option value="" disabled>Chọn ngân hàng</option>
                    <option value="970436">Vietcombank</option>
                    <option value="970415">VietinBank</option>
                    <option value="970418">BIDV</option>
                    <option value="970405">Agribank</option>
                    <option value="970407">Techcombank</option>
                    <option value="970422">MBBank</option>
                    <option value="970416">ACB</option>
                    <option value="970423">TPBank</option>
                    <option value="970432">VPBank</option>
                    <option value="970403">Sacombank</option>
                    <option value="970441">VIB</option>
                    <option value="970437">HDBank</option>
                    <option value="970443">SHB</option>
                    <option value="970440">SeABank</option>
                    <option value="970426">MSB</option>
                    <option value="970448">OCB</option>
                    <option value="970431">Eximbank</option>
                    <option value="970449">LienVietPostBank</option>
                    <option value="other">Khác</option>
                  </select>
                </div>
                
                <div
                  className="form-group overflow-hidden"
                  style={{
                    transition: 'all 0.3s ease-in-out',
                    maxHeight: withdrawBankBin === 'other' ? '120px' : '0',
                    opacity: withdrawBankBin === 'other' ? 1 : 0,
                    marginBottom: withdrawBankBin === 'other' ? '1rem' : '0',
                    visibility: withdrawBankBin === 'other' ? 'visible' : 'hidden'
                  }}
                >
                  <label htmlFor="customBankBin">Nhập mã BIN ngân hàng</label>
                  <input
                    id="customBankBin"
                    type="text"
                    className="form-control"
                    value={customBankBin}
                    onChange={(e) => setCustomBankBin(e.target.value)}
                    placeholder="Ví dụ: 970436"
                    required={withdrawBankBin === 'other'}
                    disabled={isWithdrawing || withdrawBankBin !== 'other'}
                  />
                  <small className="text-muted mt-1 d-block" style={{ fontStyle: 'italic', fontSize: '0.85em' }}>
                    Vui lòng nhập mã BIN của ngân hàng để thực hiện rút tiền.
                  </small>
                </div>
                <div className="form-group mb-3">
                  <label htmlFor="withdrawAccountNumber">Số tài khoản</label>
                  <input
                    id="withdrawAccountNumber"
                    type="text"
                    className="form-control"
                    value={withdrawAccountNumber}
                    onChange={(e) => setWithdrawAccountNumber(e.target.value)}
                    placeholder="Nhập số tài khoản"
                    required
                    disabled={isWithdrawing}
                  />
                </div>
                <div className="form-group mb-3">
                  <label htmlFor="withdrawAccountName">Tên chủ tài khoản</label>
                  <input
                    id="withdrawAccountName"
                    type="text"
                    className="form-control"
                    value={withdrawAccountName}
                    onChange={(e) => setWithdrawAccountName(e.target.value)}
                    placeholder="Ví dụ: Nguyễn Văn A"
                    required
                    disabled={isWithdrawing}
                  />
                </div>
                <Button
                  type="submit"
                  variant="primary"
                  disabled={isWithdrawing}
                  className="w-100"
                >
                  {isWithdrawing ? 'Đang xử lý...' : 'Xác nhận rút tiền'}
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
              <h3>Lịch sử giao dịch</h3>
              <p className="text-secondary">Theo dõi các giao dịch Nạp tiền và Rút tiền gần đây</p>
            </div>
          </div>
          <button
            type="button"
            className="wallet-refresh-btn"
            onClick={() => fetchTransactions(1, true)}
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
              {getCombinedTransactions().length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-4">Chưa có giao dịch nào</td>
                </tr>
              ) : (
                getCombinedTransactions().slice(0, 3).map(tx => (
                  <tr key={`${tx._type}-${tx.id}`}>
                    <td>{formatDateTime(tx.createdAt)}</td>
                    <td>
                      <div>{tx.providerOrderCode || tx.id.substring(0, 8)}</div>
                      <div style={{ fontSize: '11px', color: tx._type === 'TopUp' ? '#3b82f6' : '#f97316', fontWeight: 'bold' }}>
                        {tx._type === 'TopUp' ? 'NẠP VÍ' : 'RÚT VÍ'}
                      </div>
                    </td>
                    <td style={{ color: tx._type === 'TopUp' ? '#16a34a' : '#ea580c', fontWeight: 'bold' }}>
                      {tx._type === 'TopUp' ? '+' : '-'}{formatCurrency(tx.amount)}
                    </td>
                    <td>
                      <span className={`wallet-status-badge ${getTopUpStatusClass(tx.status as WalletTopUpStatus)}`}>
                        {getTopUpStatusIcon(tx.status as WalletTopUpStatus)}
                        {tx._type === 'TopUp' ? getTopUpStatusText(tx.status as WalletTopUpStatus) : tx.status}
                      </span>
                    </td>
                    <td>{tx._type === 'TopUp' ? formatDateTime((tx as WalletTopUpResponse).expiresAt) : '-'}</td>
                    <td>
                      {tx._type === 'TopUp' && tx.status === 'Pending' && (tx as WalletTopUpResponse).providerCheckoutUrl ? (
                        <button
                          type="button"
                          className="wallet-refresh-btn"
                          onClick={() => continueTopUpPayment((tx as WalletTopUpResponse).providerCheckoutUrl)}
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
              onClick={() => fetchTransactions(topUpPage + 1)}
              disabled={isTopUpsLoading}
            >
              {isTopUpsLoading ? 'Đang tải...' : 'Xem thêm'}
            </Button>
          </div>
        )}
      </div>

      {toastMessage && (
        <Toast
          message={toastMessage}
          type={toastType}
          onClose={() => setToastMessage(null)}
        />
      )}
    </div>
  );
};
