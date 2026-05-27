import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { kycApi } from '../services/kycApi';
import type { KycHistoryItemResponse, KycStatusResponse } from '../types/kyc.types';

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'Chưa có';
}

export function KycStatusPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<KycStatusResponse | null>(null);
  const [history, setHistory] = useState<KycHistoryItemResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const loadKyc = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [statusResponse, historyResponse] = await Promise.all([
        kycApi.getMyStatus(),
        kycApi.getMyHistory()
      ]);
      setStatus(statusResponse.data);
      setHistory(historyResponse.data ?? []);
    } catch (loadError) {
      setError(getApiErrorMessage(loadError, 'Không thể tải trạng thái KYC.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadKyc();
  }, [loadKyc]);

  return (
    <main className="auth-page">
      <section className="auth-panel kyc-panel">
        <div style={{ marginBottom: '16px', display: 'flex', justifyContent: 'flex-start' }}>
          <Button
            type="button"
            variant="secondary"
            onClick={() => navigate(-1)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            Quay lại
          </Button>
        </div>
        <p className="eyebrow">KYC</p>
        <h1>Trạng thái xác minh</h1>
        <p className="subtle">Theo dõi lần gửi KYC mới nhất và lịch sử xác minh danh tính.</p>

        {isLoading ? <LoadingState message="Đang tải trạng thái KYC..." /> : null}
        {error ? <Alert type="error">{error}</Alert> : null}

        {!isLoading && !status?.hasSubmission ? (
          <Alert type="info">Bạn chưa có hồ sơ KYC nào.</Alert>
        ) : null}

        {status?.hasSubmission ? (
          <dl className="user-summary">
            <div>
              <dt>Trạng thái</dt>
              <dd>{status.status}</dd>
            </div>
            <div>
              <dt>eKYC</dt>
              <dd>{status.ekycResult}</dd>
            </div>
            <div>
              <dt>Rủi ro</dt>
              <dd>{status.riskLevel}</dd>
            </div>
            <div>
              <dt>Giấy tờ</dt>
              <dd>{status.documentType}</dd>
            </div>
            <div>
              <dt>Ngày gửi</dt>
              <dd>{formatDate(status.submittedAt)}</dd>
            </div>
            {status.ocrFullName ? (
              <div>
                <dt>Họ tên OCR</dt>
                <dd>{status.ocrFullName}</dd>
              </div>
            ) : null}
            {status.ocrDateOfBirth ? (
              <div>
                <dt>Ngày sinh OCR</dt>
                <dd>{new Date(status.ocrDateOfBirth).toLocaleDateString()}</dd>
              </div>
            ) : null}
            {status.ocrGender ? (
              <div>
                <dt>Giới tính OCR</dt>
                <dd>{status.ocrGender}</dd>
              </div>
            ) : null}
            {status.ocrAddress ? (
              <div>
                <dt>Địa chỉ OCR</dt>
                <dd>{status.ocrAddress}</dd>
              </div>
            ) : null}
            {status.rejectedReason ? (
              <div>
                <dt>Lý do từ chối</dt>
                <dd>{status.rejectedReason}</dd>
              </div>
            ) : null}
          </dl>
        ) : null}

        <div className="auth-actions">
          <Button type="button" variant="secondary" onClick={() => void loadKyc()} disabled={isLoading}>
            Làm mới
          </Button>
          <Link className="ui-link-button" to={ROUTE_PATHS.ME.KYC}>
            Gửi KYC
          </Link>
        </div>

        <h2 className="section-title">Lịch sử</h2>
        {history.length === 0 ? (
          <p className="subtle">Chưa có lịch sử KYC.</p>
        ) : (
          <ul className="history-list">
            {history.map(item => (
              <li key={item.kycId}>
                <strong>{item.status}</strong>
                <span>{item.documentType}</span>
                <span>{item.ekycResult}</span>
                <span>{formatDate(item.submittedAt)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
