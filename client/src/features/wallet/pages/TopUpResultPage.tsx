import React, { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Button } from '../../../shared/components/ui/Button';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { walletApi } from '../api';
import type { WalletTopUpStatus } from '../types';
import './Wallet.css';

type ResultStatus = 'checking' | 'success' | 'error' | 'pending';

const MAX_POLL_ATTEMPTS = 8;
const POLL_INTERVAL_MS = 2000;

export const TopUpResultPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<ResultStatus>('checking');
  const [message, setMessage] = useState('Đang xác nhận giao dịch nạp ví...');

  useEffect(() => {
    const paymentTransactionId = searchParams.get('paymentTransactionId');
    const isCancelReturn = searchParams.get('cancel') === 'true';
    let isDisposed = false;
    let pollTimer: ReturnType<typeof setTimeout> | undefined;

    const setResult = (nextStatus: ResultStatus, nextMessage: string) => {
      if (isDisposed) return;
      setStatus(nextStatus);
      setMessage(nextMessage);
    };

    const handleTerminalStatus = (topUpStatus: WalletTopUpStatus, isCancel: boolean) => {
      switch (topUpStatus) {
        case 'Succeeded':
          setResult('success', 'Nạp tiền vào ví thành công.');
          return true;
        case 'Failed':
          setResult('error', 'Giao dịch nạp ví thất bại.');
          return true;
        case 'Expired':
          setResult('error', 'Giao dịch nạp ví đã hết hạn.');
          return true;
        case 'Cancelled':
          setResult('error', 'Giao dịch nạp ví đã bị hủy.');
          return true;
        case 'Pending':
          if (isCancel) {
            setResult('error', 'Bạn đã hủy giao dịch nạp ví. Nếu chưa thanh toán, giao dịch sẽ tự hết hạn.');
            return true;
          }

          return false;
        default:
          setResult('error', 'Trạng thái giao dịch nạp ví không hợp lệ.');
          return true;
      }
    };

    const pollTopUpStatus = async (attempt: number) => {
      if (!paymentTransactionId) {
        if (isCancelReturn) {
          setResult('error', 'Bạn đã hủy giao dịch nạp ví.');
        } else {
          setResult('pending', 'Không tìm thấy mã giao dịch để xác nhận. Vui lòng kiểm tra lại lịch sử ví.');
        }
        return;
      }

      try {
        const res = await walletApi.getTopUp(paymentTransactionId);
        const topUp = res.data;

        if (!res.success || !topUp) {
          setResult('error', res.message || 'Không thể xác nhận trạng thái giao dịch nạp ví.');
          return;
        }

        if (handleTerminalStatus(topUp.status, isCancelReturn)) {
          return;
        }

        if (attempt >= MAX_POLL_ATTEMPTS) {
          setResult(
            'pending',
            'Giao dịch đang chờ PayOS xác nhận. Vui lòng kiểm tra lại ví sau ít phút.'
          );
          return;
        }

        setResult('checking', 'Đang chờ PayOS xác nhận giao dịch...');
        pollTimer = setTimeout(() => pollTopUpStatus(attempt + 1), POLL_INTERVAL_MS);
      } catch (err: any) {
        setResult(err?.message ? 'error' : 'pending', err?.message || 'Chưa thể xác nhận giao dịch. Vui lòng kiểm tra lại ví sau ít phút.');
      }
    };

    pollTopUpStatus(0);

    return () => {
      isDisposed = true;
      if (pollTimer) {
        clearTimeout(pollTimer);
      }
    };
  }, [searchParams]);

  const isSuccess = status === 'success';
  const isPending = status === 'pending' || status === 'checking';

  return (
    <div className="result-container">
      {isSuccess ? (
        <div className="result-icon success">
          <i className="bi bi-check-circle-fill"></i>
        </div>
      ) : isPending ? (
        <div className="result-icon pending">
          <i className="bi bi-hourglass-split"></i>
        </div>
      ) : (
        <div className="result-icon error">
          <i className="bi bi-x-circle-fill"></i>
        </div>
      )}

      <h2 className="mb-3">
        {isSuccess
          ? 'Giao dịch thành công'
          : isPending
            ? 'Đang xác nhận giao dịch'
            : 'Giao dịch không thành công'}
      </h2>
      <p className="text-secondary mb-4">{message}</p>

      <Button variant="primary" onClick={() => navigate(ROUTE_PATHS.ACCOUNT.WALLET)}>
        Quay lại Ví của tôi
      </Button>
    </div>
  );
};
