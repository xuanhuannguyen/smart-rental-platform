import { useEffect, useState } from 'react';
import { isAxiosError } from 'axios';
import { setDevUserId } from '../api/apiClient';
import { getMyKycHistory, getMyKycStatus, type KycHistoryItem, type KycStatus } from '../api/kycApi';

function getRejectedReason(status: KycStatus | KycHistoryItem) {
  return status.rejected_reason ?? status.rejectedReason ?? null;
}

export default function KycStatusPage() {
  const [devUserId, setDevUserIdState] = useState(
    () => localStorage.getItem('srp_dev_user_id') ?? ''
  );
  const [status, setStatus] = useState<KycStatus | null>(null);
  const [history, setHistory] = useState<KycHistoryItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    if (!devUserId) {
      setError('Dev User ID is required until JWT auth is integrated.');
      return;
    }

    setDevUserId(devUserId);
    setLoading(true);
    setError(null);

    try {
      const [statusRes, historyRes] = await Promise.all([getMyKycStatus(), getMyKycHistory()]);
      setStatus(statusRes.data);
      setHistory(historyRes.data ?? []);
    } catch (err) {
      if (isAxiosError(err) && err.response?.data) {
        const payload = err.response.data as { message?: string; code?: string };
        setError(`${payload.code ?? 'ERROR'}: ${payload.message ?? 'Failed to load KYC data'}`);
      } else {
        setError('Failed to load KYC data.');
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const rejectedReason = status ? getRejectedReason(status) : null;

  return (
    <section className="card">
      <h2>KYC status</h2>

      <label className="field">
        Dev User ID (temporary until JWT)
        <input
          value={devUserId}
          onChange={(e) => setDevUserIdState(e.target.value)}
          placeholder="00000000-0000-0000-0000-000000000001"
        />
      </label>

      <button type="button" className="btn-primary" onClick={() => void load()} disabled={loading}>
        {loading ? 'Loading...' : 'Refresh'}
      </button>

      {error ? <p className="error-text">{error}</p> : null}

      {!status?.hasSubmission ? (
        <p className="muted">No KYC submission found for this user.</p>
      ) : (
        <div className="status-panel">
          <p>
            <strong>Status:</strong> {status.status}
          </p>
          <p>
            <strong>eKYC result:</strong> {status.ekycResult}
          </p>
          <p>
            <strong>Risk level:</strong> {status.riskLevel}
          </p>
          <p>
            <strong>Document:</strong> {status.documentType}
          </p>
          {status.ocrFullName ? (
            <p>
              <strong>Name:</strong> {status.ocrFullName}
            </p>
          ) : null}
          {status.status === 'Rejected' && rejectedReason ? (
            <p className="rejected-box">
              <strong>Rejection reason:</strong> {rejectedReason}
            </p>
          ) : null}
        </div>
      )}

      <h3>History</h3>
      {history.length === 0 ? (
        <p className="muted">No history records.</p>
      ) : (
        <ul className="history-list">
          {history.map((item) => (
            <li key={item.kycId}>
              <div>
                <strong>{item.status}</strong> — {item.documentType} —{' '}
                {new Date(item.submittedAt).toLocaleString()}
              </div>
              {item.status === 'Rejected' && getRejectedReason(item) ? (
                <div className="rejected-box">Reason: {getRejectedReason(item)}</div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
