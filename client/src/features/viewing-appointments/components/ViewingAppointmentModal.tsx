import { useState } from 'react';
import { createViewingAppointment } from '../api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import './ViewingAppointmentModal.css';

interface ViewingAppointmentModalProps {
  roomId: string;
  roomNumber: string;
  houseName: string;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

export default function ViewingAppointmentModal({
  roomId,
  roomNumber,
  houseName,
  onClose,
  onSuccess,
}: ViewingAppointmentModalProps) {
  const [scheduledDate, setScheduledDate] = useState('');
  const [scheduledTime, setScheduledTime] = useState('');
  const [durationMinutes, setDurationMinutes] = useState(30);
  const [tenantNote, setTenantNote] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Get current date formatted for date input min attribute
  const getMinDate = () => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scheduledDate || !scheduledTime) {
      setError('Vui lòng chọn đầy đủ ngày và giờ xem phòng.');
      return;
    }

    const scheduledDateObj = new Date(`${scheduledDate}T${scheduledTime}`);
    if (isNaN(scheduledDateObj.getTime())) {
      setError('Thời gian hẹn không hợp lệ.');
      return;
    }

    if (scheduledDateObj <= new Date()) {
      setError('Thời gian hẹn phải ở tương lai.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await createViewingAppointment({
        roomId,
        scheduledAt: scheduledDateObj.toISOString(),
        durationMinutes,
        tenantNote: tenantNote.trim() || null,
      });
      onSuccess('Đặt lịch xem phòng thành công! Vui lòng chờ chủ trọ xác nhận.');
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể đặt lịch xem phòng. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="viewing-modal-overlay" onClick={onClose}>
      <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
        <header className="viewing-modal-header">
          <h2>Đặt lịch hẹn xem phòng</h2>
          <button className="viewing-modal-close-btn" onClick={onClose} aria-label="Đóng">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </header>

        <form onSubmit={handleSubmit} className="viewing-modal-form">
          <div className="viewing-modal-info">
            <div className="info-icon-wrapper">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
            </div>
            <div className="info-text">
              <p><strong>Khu trọ:</strong> {houseName}</p>
              <p><strong>Phòng trọ:</strong> Phòng {roomNumber}</p>
            </div>
          </div>

          {error && <div className="viewing-modal-error">{error}</div>}

          <div className="viewing-modal-field">
            <label>Thời gian xem phòng <span className="required">*</span></label>
            <div className="datetime-split-row">
              <div className="input-with-icon">
                <div className="input-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
                <input
                  type="date"
                  min={getMinDate()}
                  value={scheduledDate}
                  onChange={(e) => setScheduledDate(e.target.value)}
                  disabled={loading}
                  required
                />
              </div>
              <div className="input-with-icon">
                <div className="input-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <polyline points="12 6 12 12 16 14" />
                  </svg>
                </div>
                <input
                  type="time"
                  value={scheduledTime}
                  onChange={(e) => setScheduledTime(e.target.value)}
                  disabled={loading}
                  required
                />
              </div>
            </div>
          </div>

          <div className="viewing-modal-field">
            <label htmlFor="durationMinutes">Thời lượng ước tính</label>
            <div className="input-with-icon">
              <div className="input-icon-wrapper">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
              </div>
              <select
                id="durationMinutes"
                value={durationMinutes}
                onChange={(e) => setDurationMinutes(Number(e.target.value))}
                disabled={loading}
              >
                <option value={15}>15 phút</option>
                <option value={30}>30 phút</option>
                <option value={45}>45 phút</option>
                <option value={60}>1 tiếng (60 phút)</option>
              </select>
              <div className="select-chevron-wrapper">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <polyline points="6 9 12 15 18 9" />
                </svg>
              </div>
            </div>
          </div>

          <div className="viewing-modal-field">
            <label htmlFor="tenantNote">Ghi chú gửi cho Chủ nhà</label>
            <div className="input-with-icon align-start">
              <div className="input-icon-wrapper offset-top">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                </svg>
              </div>
              <textarea
                id="tenantNote"
                placeholder="Ví dụ: Tôi muốn dắt thêm người thân cùng xem..."
                rows={3}
                value={tenantNote}
                onChange={(e) => setTenantNote(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>

          <footer className="viewing-modal-footer">
            <button
              type="button"
              className="viewing-modal-btn viewing-modal-btn--secondary"
              onClick={onClose}
              disabled={loading}
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              className="viewing-modal-btn viewing-modal-btn--primary"
              disabled={loading}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
                <polyline points="10 14 12 16 16 12" />
              </svg>
              <span>{loading ? 'Đang gửi yêu cầu...' : 'Xác nhận đặt lịch'}</span>
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}
