import { useState } from 'react';
import { rentalRequestApi } from '../api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import './SubmitRentalRequestModal.css';

interface SubmitRentalRequestModalProps {
  roomId: string;
  roomNumber: string;
  houseName: string;
  maxOccupants: number;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addDays(days: number) {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toDateInput(date);
}

export default function SubmitRentalRequestModal({
  roomId,
  roomNumber,
  houseName,
  maxOccupants,
  onClose,
  onSuccess,
}: SubmitRentalRequestModalProps) {
  const [desiredStartDate, setDesiredStartDate] = useState(addDays(7));
  const [expectedEndDate, setExpectedEndDate] = useState(addDays(97));
  const [expectedOccupantCount, setExpectedOccupantCount] = useState<number>(1);
  const [tenantNote, setTenantNote] = useState('');
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!desiredStartDate || !expectedEndDate) {
      setError('Vui lòng chọn ngày dự kiến bắt đầu và kết thúc.');
      return;
    }

    if (new Date(desiredStartDate) >= new Date(expectedEndDate)) {
      setError('Ngày kết thúc phải lớn hơn ngày bắt đầu.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await rentalRequestApi.createRentalRequest(roomId, {
        desiredStartDate,
        expectedEndDate,
        expectedOccupantCount,
        tenantNote
      });
      onSuccess('Gửi yêu cầu thuê thành công! Vui lòng chờ chủ trọ xác nhận.');
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể gửi yêu cầu thuê. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="rental-modal-overlay" onClick={onClose}>
      <div className="rental-modal-container" onClick={(e) => e.stopPropagation()}>
        <header className="rental-modal-header">
          <h2>Gửi yêu cầu thuê phòng</h2>
          <button className="rental-modal-close-btn" onClick={onClose} aria-label="Đóng">
            &times;
          </button>
        </header>

        <form onSubmit={handleSubmit} className="rental-modal-form">
          <div className="rental-modal-info">
            <p><strong>Khu trọ:</strong> {houseName}</p>
            <p><strong>Phòng trọ:</strong> Phòng {roomNumber}</p>
          </div>

          {error && <div className="rental-modal-error">{error}</div>}

          <div className="rental-modal-row">
            <div className="rental-modal-field">
              <label htmlFor="desiredStartDate">Ngày dự kiến bắt đầu <span className="required">*</span></label>
              <input
                id="desiredStartDate"
                type="date"
                value={desiredStartDate}
                onChange={(e) => setDesiredStartDate(e.target.value)}
                disabled={loading}
                required
              />
            </div>

            <div className="rental-modal-field">
              <label htmlFor="expectedEndDate">Ngày dự kiến kết thúc <span className="required">*</span></label>
              <input
                id="expectedEndDate"
                type="date"
                value={expectedEndDate}
                onChange={(e) => setExpectedEndDate(e.target.value)}
                disabled={loading}
                required
              />
            </div>
          </div>

          <div className="rental-modal-field">
            <label htmlFor="expectedOccupantCount">Số người dự kiến ở <span className="required">*</span></label>
            <input
              id="expectedOccupantCount"
              type="number"
              min={1}
              max={maxOccupants}
              value={expectedOccupantCount}
              onChange={(e) => setExpectedOccupantCount(Number(e.target.value))}
              disabled={loading}
              required
            />
            <small style={{ color: '#64748b', marginTop: 4 }}>Tối đa {maxOccupants} người</small>
          </div>

          <div className="rental-modal-field">
            <label htmlFor="tenantNote">Ghi chú cho Chủ nhà</label>
            <textarea
              id="tenantNote"
              placeholder="Ghi chú thêm về yêu cầu của bạn (ví dụ: yêu cầu thêm nội thất, thời gian dọn vào cụ thể...)"
              rows={3}
              value={tenantNote}
              onChange={(e) => setTenantNote(e.target.value)}
              disabled={loading}
            />
          </div>

          <footer className="rental-modal-footer">
            <button
              type="button"
              className="rental-modal-btn rental-modal-btn--secondary"
              onClick={onClose}
              disabled={loading}
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              className="rental-modal-btn rental-modal-btn--primary"
              disabled={loading}
            >
              {loading ? 'Đang gửi yêu cầu...' : 'Xác nhận gửi'}
            </button>
          </footer>
        </form>
      </div>
    </div>
  );
}
