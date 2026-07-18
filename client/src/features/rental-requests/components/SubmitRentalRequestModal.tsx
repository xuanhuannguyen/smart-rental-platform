import { Alert } from '../../../shared/components/ui/Alert';
import { useRef, useState } from 'react';
import { rentalRequestApi } from '../api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Toast } from '../../../shared/components/ui/Toast';
import './SubmitRentalRequestModal.css';

interface SubmitRentalRequestModalProps {
  roomId: string;
  roomNumber: string;
  houseName: string;
  maxOccupants: number;
  minRentalMonths?: number | null;
  maxRentalMonths?: number | null;
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

function addMonths(dateInput: string, months: number) {
  const date = new Date(`${dateInput}T00:00:00`);
  date.setMonth(date.getMonth() + months);
  return toDateInput(date);
}

const minimumStartDate = addDays(3);

export default function SubmitRentalRequestModal({
  roomId,
  roomNumber,
  houseName,
  maxOccupants,
  minRentalMonths,
  maxRentalMonths,
  onClose,
  onSuccess,
}: SubmitRentalRequestModalProps) {
  const policyMinMonths = Math.max(1, minRentalMonths ?? 6);
  const policyMaxMonths = Math.max(policyMinMonths, maxRentalMonths ?? 120);
  const defaultStartDate = addDays(7);

  const [desiredStartDate, setDesiredStartDate] = useState(addDays(7));
  const [expectedEndDate, setExpectedEndDate] = useState(addMonths(defaultStartDate, policyMinMonths));
  const [expectedOccupantCount, setExpectedOccupantCount] = useState<number>(1);
  const [tenantNote, setTenantNote] = useState('');
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const submittingRef = useRef(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submittingRef.current) return;

    if (!desiredStartDate || !expectedEndDate) {
      setError('Vui lòng chọn ngày dự kiến bắt đầu và kết thúc.');
      return;
    }

    if (desiredStartDate < minimumStartDate) {
      setError('Ngày bắt đầu thuê phải cách hôm nay ít nhất 3 ngày để hai bên có thời gian hoàn tất hợp đồng.');
      return;
    }

    if (new Date(desiredStartDate) >= new Date(expectedEndDate)) {
      setError('Ngày kết thúc phải lớn hơn ngày bắt đầu.');
      return;
    }

    const minimumEndDate = addMonths(desiredStartDate, policyMinMonths);
    const maximumEndDate = addMonths(desiredStartDate, policyMaxMonths);
    if (expectedEndDate < minimumEndDate || expectedEndDate > maximumEndDate) {
      setError(`Thời gian thuê phải nằm trong chính sách của khu trọ (${policyMinMonths}-${policyMaxMonths} tháng).`);
      return;
    }

    submittingRef.current = true;
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
      setToast({ message: getApiErrorMessage(err, 'Không thể gửi yêu cầu thuê. Vui lòng thử lại.'), type: 'error' });
    } finally {
      submittingRef.current = false;
      setLoading(false);
    }
  };

  return (
    <div className="rental-modal-overlay" onClick={onClose}>
      <div className="rental-modal-container" onClick={(e) => e.stopPropagation()}>
        <header className="rental-modal-header">
          <h2>Gửi yêu cầu thuê phòng</h2>
          <button className="rental-modal-close-btn" onClick={onClose} aria-label="Đóng">
            <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </header>

        <form onSubmit={handleSubmit} className="rental-modal-form" noValidate>
          <div className="rental-modal-info">
            <div className="info-icon-wrapper">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                <polyline points="9 22 9 12 15 12 15 22" />
              </svg>
            </div>
            <div className="info-text">
              <p>
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="info-inline-icon icon-building">
                  <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
                  <line x1="9" y1="22" x2="9" y2="16" />
                  <line x1="15" y1="22" x2="15" y2="16" />
                  <line x1="9" y1="16" x2="15" y2="16" />
                  <path d="M8 6h2" />
                  <path d="M14 6h2" />
                  <path d="M8 10h2" />
                  <path d="M14 10h2" />
                </svg>
                <span className="info-label">Khu trọ:</span> <span className="info-value">{houseName}</span>
              </p>
              <p>
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" className="info-inline-icon icon-room">
                  <path d="M19 21V5a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v16" />
                  <path d="M12 11h.01" strokeWidth="3" />
                </svg>
                <span className="info-label">Phòng trọ:</span> <span className="info-value">Phòng {roomNumber}</span>
              </p>
            </div>

            {/* Building Watermark Illustration on the right */}
            <svg className="info-watermark" viewBox="0 0 100 60" fill="none" stroke="currentColor" xmlns="http://www.w3.org/2000/svg">
              <path d="M10 60V30h20v30" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M30 60V15h30v45" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
              <path d="M60 60V40h15v20" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
              <circle cx="85" cy="48" r="8" strokeWidth="1.2" />
              <path d="M85 56v4" strokeWidth="1.2" strokeLinecap="round" />
              <path d="M5 60h90" strokeWidth="1.2" strokeLinecap="round" />
            </svg>
          </div>

          {error && <Alert type="error">{error}</Alert>}

          <div className="rental-modal-row">
            <div className="rental-modal-field">
              <label htmlFor="desiredStartDate">Ngày dự kiến bắt đầu <span className="required">*</span></label>
              <div className="input-with-icon icon-right">
                <input
                  id="desiredStartDate"
                  type="date"
                  min={minimumStartDate}
                  value={desiredStartDate}
                  onChange={(e) => {
                    const nextStartDate = e.target.value;
                    setDesiredStartDate(nextStartDate);
                    setExpectedEndDate(addMonths(nextStartDate, policyMinMonths));
                  }}
                  disabled={loading}
                  required
                />
                <div className="input-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
              </div>
            </div>

            <div className="rental-modal-field">
              <label htmlFor="expectedEndDate">Ngày dự kiến kết thúc <span className="required">*</span></label>
              <div className="input-with-icon icon-right">
                <input
                  id="expectedEndDate"
                  type="date"
                  min={addMonths(desiredStartDate, policyMinMonths)}
                  max={addMonths(desiredStartDate, policyMaxMonths)}
                  value={expectedEndDate}
                  onChange={(e) => setExpectedEndDate(e.target.value)}
                  disabled={loading}
                  required
                />
                <div className="input-icon-wrapper">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                </div>
              </div>
            </div>
          </div>
          <p className="field-helper-text">
            Chính sách khu trọ: thuê từ {policyMinMonths} đến {policyMaxMonths} tháng.
          </p>

          <div className="rental-modal-field">
            <label htmlFor="expectedOccupantCount">Số người dự kiến ở <span className="required">*</span></label>
            <input
              id="expectedOccupantCount"
              type="number"
              className="rental-plain-input"
              min={1}
              max={maxOccupants}
              value={expectedOccupantCount}
              onChange={(e) => setExpectedOccupantCount(Number(e.target.value))}
              disabled={loading}
              required
            />
            <small className="field-helper-text">Tối đa {maxOccupants} người</small>
          </div>

          <div className="rental-modal-field">
            <label htmlFor="tenantNote">Ghi chú cho Chủ nhà</label>
            <textarea
              id="tenantNote"
              className="rental-plain-textarea"
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
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="22" y1="2" x2="11" y2="13" />
                <polygon points="22 2 15 22 11 13 2 9 22 2" />
              </svg>
              <span>{loading ? 'Đang gửi...' : 'Xác nhận gửi'}</span>
            </button>
          </footer>
        </form>
      </div>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
