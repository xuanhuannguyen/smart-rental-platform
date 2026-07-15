import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { kycApi } from '../services/kycApi';
import type { KycHistoryItemResponse, KycStatusResponse } from '../types/kyc.types';
import '../../profile/pages/MyProfilePage.css';
import '../../wallet/pages/Wallet.css';

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

  const getStatusTheme = (statusStr?: string | null) => {
    if (statusStr === 'Approved') return 'approved';
    if (statusStr === 'Rejected' || statusStr === 'EkycFailed') return 'rejected';
    return 'pending';
  };

  const getHistoryStatusClass = (status: string) => {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected' || status === 'EkycFailed') return 'failed';
    return 'pending';
  };

  const getHistoryStatusIcon = (status: string) => {
    if (status === 'Approved') {
      return (
        <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
          <polyline points="20 6 9 17 4 12" />
        </svg>
      );
    }
    if (status === 'Rejected' || status === 'EkycFailed') {
      return (
        <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
      );
    }
    return (
      <svg className="wallet-status-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '4px' }}>
        <circle cx="12" cy="12" r="10" />
        <polyline points="12 6 12 12 16 14" />
      </svg>
    );
  };

  return (
    <main className="auth-page" style={{ padding: '40px 24px', background: '#f4f7fb', minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'flex-start' }}>
      <section className="auth-panel kyc-panel" style={{ width: '100%', maxWidth: '800px', background: 'transparent', boxShadow: 'none', padding: 0 }}>
        <div style={{ marginBottom: '20px', display: 'flex', justifyContent: 'flex-start' }}>
          <button
            type="button"
            className="wallet-refresh-btn"
            onClick={() => navigate(-1)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '8px 16px', color: '#246bfe' }}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            <span style={{ color: '#246bfe', fontWeight: '600' }}>Quay lại</span>
          </button>
        </div>
        <p className="eyebrow" style={{ color: '#246bfe', fontWeight: 'bold', margin: '0 0 4px 0' }}>KYC</p>
        <h1 style={{ fontSize: '28px', fontWeight: '800', color: '#0f172a', margin: '0 0 8px 0' }}>Trạng thái xác minh</h1>
        <p className="subtle" style={{ color: '#64748b', margin: '0 0 28px 0', fontSize: '14px' }}>Theo dõi lần gửi KYC mới nhất và lịch sử xác minh danh tính.</p>

        {isLoading ? <LoadingState message="Đang tải trạng thái KYC..." /> : null}
        {error ? <Alert type="error">{error}</Alert> : null}

        {!isLoading && !status?.hasSubmission ? (
          <Alert type="info">Bạn chưa có hồ sơ KYC nào.</Alert>
        ) : null}

        {status?.hasSubmission ? (
          <div className="profile-ekyc-card" style={{ marginTop: 0, marginBottom: '24px' }}>
            <div className={`ekyc-status-banner ${getStatusTheme(status.status)}`}>
              <div className={`ekyc-status-banner-icon-box ${getStatusTheme(status.status)}`}>
                {getStatusTheme(status.status) === 'approved' ? (
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                    <polyline points="9 11 11 13 15 9" />
                  </svg>
                ) : getStatusTheme(status.status) === 'rejected' ? (
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                    <line x1="12" y1="9" x2="12" y2="13" />
                    <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                ) : (
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <polyline points="12 6 12 12 16 14" />
                  </svg>
                )}
              </div>
              <div className="ekyc-status-banner-info">
                <span className="ekyc-status-banner-label">Trạng thái</span>
                <strong className="ekyc-status-banner-value">{status.status}</strong>
              </div>
              <div className="ekyc-status-banner-watermark">
                <svg width="80" height="80" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  {getStatusTheme(status.status) === 'approved' && <polyline points="9 11 11 13 15 9" />}
                  {getStatusTheme(status.status) === 'rejected' && (
                    <>
                      <line x1="12" y1="9" x2="12" y2="13" />
                      <line x1="12" y1="17" x2="12.01" y2="17" />
                    </>
                  )}
                </svg>
              </div>
            </div>

            <div className="ekyc-summary-grid">
              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="16" rx="2" />
                    <line x1="7" y1="8" x2="11" y2="8" />
                    <line x1="7" y1="12" x2="13" y2="12" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">eKYC</span>
                  <span className="ekyc-value">{status.ekycResult}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Họ tên OCR</span>
                  <span className="ekyc-value name-bold-uppercase">{status.ocrFullName || 'Chưa có'}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                    <line x1="12" y1="9" x2="12" y2="13" />
                    <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Rủi ro</span>
                  <span className="ekyc-value" style={{ color: status.riskLevel === 'High' ? '#dc2626' : '#10b981' }}>{status.riskLevel}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Ngày sinh OCR</span>
                  <span className="ekyc-value">
                    {status.ocrDateOfBirth ? new Date(status.ocrDateOfBirth).toLocaleDateString('vi-VN') : 'Chưa có'}
                  </span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Giấy tờ</span>
                  <span className="ekyc-value">{status.documentType}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Giới tính OCR</span>
                  <span className="ekyc-value">{status.ocrGender || 'Chưa có'}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Ngày gửi</span>
                  <span className="ekyc-value">{formatDate(status.submittedAt)}</span>
                </div>
              </div>

              <div className="ekyc-item">
                <div className="ekyc-icon-box">
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                    <circle cx="12" cy="10" r="3" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label">Địa chỉ OCR</span>
                  <span className="ekyc-value">{status.ocrAddress || 'Chưa có'}</span>
                </div>
              </div>
            </div>

            <div className="kyc-document-images" aria-label="Ảnh hồ sơ KYC">
              <h2>Ảnh đã gửi</h2>
              <div className="kyc-document-images__grid">
                {[
                  { label: 'Mặt trước giấy tờ', mediaAssetId: status.frontMediaAssetId },
                  { label: 'Mặt sau giấy tờ', mediaAssetId: status.backMediaAssetId },
                  { label: 'Ảnh chân dung', mediaAssetId: status.selfieMediaAssetId }
                ].map(({ label, mediaAssetId }) => (
                  <figure className="kyc-document-image" key={label}>
                    {mediaAssetId ? (
                      <PrivateMediaImage
                        mediaAssetId={mediaAssetId}
                        alt={label}
                        loadingLabel={`Đang tải ${label.toLowerCase()}...`}
                        errorLabel={`Không tải được ${label.toLowerCase()}.`}
                      />
                    ) : (
                      <span>Không có ảnh</span>
                    )}
                    <figcaption>{label}</figcaption>
                  </figure>
                ))}
              </div>
            </div>

            {status.rejectedReason ? (
              <div className="ekyc-item" style={{ marginTop: '16px', borderColor: '#fecaca', background: '#fef2f2' }}>
                <div className="ekyc-icon-box" style={{ background: '#fecaca', color: '#dc2626' }}>
                  <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <line x1="12" y1="8" x2="12" y2="12" />
                    <line x1="12" y1="16" x2="12.01" y2="16" />
                  </svg>
                </div>
                <div className="ekyc-info">
                  <span className="ekyc-label" style={{ color: '#b91c1c' }}>Lý do từ chối</span>
                  <span className="ekyc-value" style={{ color: '#991b1b' }}>{status.rejectedReason}</span>
                </div>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="ekyc-actions" style={{ display: 'flex', gap: '16px', marginBottom: '32px' }}>
          <button
            type="button"
            className="wallet-refresh-btn"
            onClick={() => void loadKyc()}
            disabled={isLoading}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '10px 20px', minHeight: '44px' }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className={isLoading ? 'spin' : ''}>
              <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
            </svg>
            Làm mới
          </button>
          <Link to={ROUTE_PATHS.ME.KYC} style={{ textDecoration: 'none' }}>
            <Button
              type="button"
              variant="primary"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', minHeight: '44px', padding: '10px 20px' }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="22" y1="2" x2="11" y2="13" />
                <polygon points="22 2 15 22 11 13 2 9 22 2" />
              </svg>
              Gửi KYC
            </Button>
          </Link>
        </div>

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
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: '700', color: '#0f172a' }}>Lịch sử</h3>
              </div>
            </div>
          </div>

          <div className="table-responsive">
            <table className="transaction-table">
              <thead>
                <tr>
                  <th>Trạng thái</th>
                  <th>Giấy tờ</th>
                  <th>eKYC</th>
                  <th>Ngày gửi</th>
                </tr>
              </thead>
              <tbody>
                {history.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="text-center py-4">Chưa có lịch sử xác minh KYC nào</td>
                  </tr>
                ) : (
                  history.map(item => (
                    <tr key={item.kycId}>
                      <td>
                        <span className={`wallet-status-badge ${getHistoryStatusClass(item.status)}`}>
                          {getHistoryStatusIcon(item.status)}
                          {item.status}
                        </span>
                      </td>
                      <td>{item.documentType}</td>
                      <td>{item.ekycResult}</td>
                      <td>{formatDate(item.submittedAt)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </main>
  );
}
