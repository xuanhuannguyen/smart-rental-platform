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
  const [scheduledAt, setScheduledAt] = useState('');
  const [durationMinutes, setDurationMinutes] = useState(30);
  const [tenantNote, setTenantNote] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Get current date time formatted for datetime-local input min attribute
  const getMinDateTime = () => {
    const now = new Date();
    // Format to YYYY-MM-DDTHH:MM
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scheduledAt) {
      setError('Vui lòng chọn thời gian hẹn xem phòng.');
      return;
    }

    const scheduledDate = new Date(scheduledAt);
    if (scheduledDate <= new Date()) {
      setError('Thời gian hẹn phải ở tương lai.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await createViewingAppointment({
        roomId,
        scheduledAt: scheduledDate.toISOString(),
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
            &times;
          </button>
        </header>

        <form onSubmit={handleSubmit} className="viewing-modal-form">
          <div className="viewing-modal-info">
            <p><strong>Khu trọ:</strong> {houseName}</p>
            <p><strong>Phòng trọ:</strong> Phòng {roomNumber}</p>
          </div>

          {error && <div className="viewing-modal-error">{error}</div>}

          <div className="viewing-modal-field">
            <label htmlFor="scheduledAt">Thời gian xem phòng <span className="required">*</span></label>
            <input
              id="scheduledAt"
              type="datetime-local"
              min={getMinDateTime()}
              value={scheduledAt}
              onChange={(e) => setScheduledAt(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <div className="viewing-modal-field">
            <label htmlFor="durationMinutes">Thời lượng ước tính</label>
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
          </div>

          <div className="viewing-modal-field">
            <label htmlFor="tenantNote">Ghi chú gửi cho Chủ nhà</label>
            <textarea
              id="tenantNote"
              placeholder="Ví dụ: Tôi muốn dắt thêm người thân cùng xem..."
              rows={3}
              value={tenantNote}
              onChange={(e) => setTenantNote(e.target.value)}
              disabled={loading}
            />
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
              {loading ? 'Đang gửi yêu cầu...' : 'Xác nhận đặt lịch'}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}
