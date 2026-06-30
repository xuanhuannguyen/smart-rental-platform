import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { formatMoneyString } from '../../../shared/utils/format';
import { walletApi } from '../api';
import type { WalletResponse } from '../types';
import './WalletPaymentConfirmModal.css';

type WalletPaymentConfirmModalProps = {
  isOpen: boolean;
  title: string;
  description?: string;
  amount: number;
  confirmLabel?: string;
  isSubmitting?: boolean;
  onConfirm: () => void;
  onClose: () => void;
};

export function WalletPaymentConfirmModal({
  isOpen,
  title,
  description,
  amount,
  confirmLabel = 'Thanh toán',
  isSubmitting = false,
  onConfirm,
  onClose
}: WalletPaymentConfirmModalProps) {
  const navigate = useNavigate();
  const [wallet, setWallet] = useState<WalletResponse | null>(null);
  const [walletError, setWalletError] = useState('');

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    let isDisposed = false;

    async function loadWallet() {
      setWallet(null);
      setWalletError('');

      try {
        const response = await walletApi.getMyWallet();
        if (!isDisposed) {
          setWallet(response.data);
        }
      } catch (err) {
        if (!isDisposed) {
          setWallet(null);
          setWalletError(getApiErrorMessage(err, 'Không thể tải số dư ví hiện tại.'));
        }
      }
    }

    void loadWallet();

    return () => {
      isDisposed = true;
    };
  }, [isOpen]);

  if (!isOpen) {
    return null;
  }

  const remainingBalance = wallet ? wallet.availableBalance - amount : null;
  const isInsufficient = remainingBalance !== null && remainingBalance < 0;
  const isCheckingWallet = !wallet && !walletError;
  const canConfirm = !isSubmitting && !isCheckingWallet && !isInsufficient;

  return (
    <div className="wallet-payment-modal-backdrop">
      <div className="wallet-payment-modal">
        <h3>{title}</h3>
        {description && <p className="wallet-payment-description">{description}</p>}

        <div className="wallet-payment-summary">
          <div className="wallet-payment-summary-row">
            <span>Số tiền thanh toán</span>
            <strong>{formatMoney(amount)}</strong>
          </div>

          {walletError ? (
            <div className="wallet-payment-alert wallet-payment-alert-info">
              {walletError} Bạn vẫn có thể thử thanh toán, hệ thống sẽ kiểm tra lại số dư ở bước cuối.
            </div>
          ) : wallet ? (
            <>
              <div className="wallet-payment-summary-row">
                <span>Số dư khả dụng</span>
                <strong>{formatMoney(wallet.availableBalance)}</strong>
              </div>
              <div className="wallet-payment-summary-row">
                <span>Số tiền đang giữ</span>
                <strong>{formatMoney(wallet.reservedBalance)}</strong>
              </div>
              <div className="wallet-payment-summary-row">
                <span>Số dư sau thanh toán</span>
                <strong className={isInsufficient ? 'wallet-payment-danger-text' : ''}>
                  {formatMoney(Math.max(remainingBalance ?? 0, 0))}
                </strong>
              </div>
              {isInsufficient && (
                <div className="wallet-payment-alert wallet-payment-alert-error">
                  Số dư khả dụng không đủ. Bạn cần nạp thêm {formatMoney(Math.abs(remainingBalance ?? 0))}.
                </div>
              )}
            </>
          ) : (
            <div className="wallet-payment-summary-row">
              <span>Số dư ví</span>
              <strong>Đang tải...</strong>
            </div>
          )}
        </div>

        <div className="wallet-payment-actions">
          <Button type="button" variant="outline" onClick={onClose} disabled={isSubmitting}>
            Đóng
          </Button>
          {isInsufficient && (
            <Button
              type="button"
              variant="secondary"
              onClick={() => navigate(ROUTE_PATHS.ACCOUNT.WALLET)}
              disabled={isSubmitting}
            >
              Nạp ví
            </Button>
          )}
          <Button type="button" onClick={onConfirm} disabled={!canConfirm}>
            {isSubmitting ? 'Đang xử lý...' : confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}

function formatMoney(value: number) {
  if (value === 0) {
    return '0 đ';
  }

  return `${formatMoneyString(value)} đ`;
}
