import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getTenantAppointments, cancelViewingAppointmentByTenant } from '../api';
import type { ViewingAppointment, ViewingAppointmentStatus } from '../types';
import { Alert } from '../../../shared/components/ui/Alert';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { formatDateVi } from '../../../shared/utils/format';
import './TenantAppointmentsPage.css';

export default function TenantAppointmentsPage() {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<ViewingAppointment[]>([]);
  const [activeTab, setActiveTab] = useState<'all' | 'pending' | 'confirmed' | 'history'>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  
  // Cancel dialog states
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  const loadData = async () => {
    setLoading(true);
    setError('');
    try {
      const appointmentsData = await getTenantAppointments();
      setAppointments(appointmentsData);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách lịch hẹn.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  const handleCancelClick = (id: string) => {
    setCancellingId(id);
    setCancelReason('');
  };

  const handleConfirmCancel = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cancellingId) return;

    setCancelling(true);
    try {
      await cancelViewingAppointmentByTenant(cancellingId, {
        cancelReason: cancelReason.trim() || null,
      });
      setSuccess('Hủy lịch hẹn thành công.');
      setCancellingId(null);
      void loadData();
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể hủy lịch hẹn.'));
    } finally {
      setCancelling(false);
    }
  };

  // Filter logic
  const filteredAppointments = appointments.filter((appointment) => {
    if (activeTab === 'pending') {
      return appointment.status === 'Pending';
    }
    if (activeTab === 'confirmed') {
      return appointment.status === 'Confirmed';
    }
    if (activeTab === 'history') {
      return ['Rejected', 'CancelledByTenant', 'CancelledByLandlord', 'Completed', 'Expired'].includes(
        appointment.status
      );
    }
    return true;
  });

  const getStatusText = (status: ViewingAppointmentStatus) => {
    switch (status) {
      case 'Pending': return 'Chờ duyệt';
      case 'Confirmed': return 'Đã xác nhận';
      case 'Rejected': return 'Đã từ chối';
      case 'CancelledByTenant': return 'Đã hủy (Khách thuê)';
      case 'CancelledByLandlord': return 'Đã hủy (Chủ nhà)';
      case 'Completed': return 'Đã hoàn thành';
      case 'Expired': return 'Hết hạn';
      default: return status;
    }
  };

  return (
    <div className="tenant-appointments-page">
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
          <h2>Lịch hẹn xem phòng của tôi</h2>
          <p className="overview-description">Theo dõi và quản lý các lịch hẹn xem phòng của bạn</p>
        </div>
      </section>

      <main className="appointments-container">
        {error && <Alert type="error">{error}</Alert>}
        {success && <Alert type="success">{success}</Alert>}

        <div className="tabs-navigation">
          <button className={activeTab === 'all' ? 'active' : ''} onClick={() => setActiveTab('all')}>
            Tất cả
          </button>
          <button className={activeTab === 'pending' ? 'active' : ''} onClick={() => setActiveTab('pending')}>
            Chờ duyệt
          </button>
          <button className={activeTab === 'confirmed' ? 'active' : ''} onClick={() => setActiveTab('confirmed')}>
            Đã xác nhận
          </button>
          <button className={activeTab === 'history' ? 'active' : ''} onClick={() => setActiveTab('history')}>
            Lịch sử cuộc hẹn
          </button>
        </div>

        {loading ? (
          <div className="appointments-loading">Đang tải lịch hẹn...</div>
        ) : filteredAppointments.length === 0 ? (
          <div className="appointments-empty">
            <p>Không tìm thấy lịch hẹn nào tương ứng.</p>
          </div>
        ) : (
          <div className="appointments-grid">
            {filteredAppointments.map((item) => {
              const houseName = item.roomingHouseName ?? 'Khu trọ';
              const roomNumber = item.roomNumber ?? 'phòng';
              const isCancellable = item.status === 'Pending' || item.status === 'Confirmed';

              return (
                <div key={item.id} className={`appointment-card appointment-card--${item.status.toLowerCase()}`}>
                  <div className="appointment-card__header">
                    <h3>{houseName} - Phòng {roomNumber}</h3>
                    <span className={`status-tag status-tag--${item.status.toLowerCase()}`}>
                      {getStatusText(item.status)}
                    </span>
                  </div>

                  <div className="appointment-card__body">
                    <p><strong>Thời gian xem:</strong> {formatDateVi(item.scheduledAt)} ({item.durationMinutes} phút)</p>
                    {item.tenantNote && <p><strong>Ghi chú của bạn:</strong> "{item.tenantNote}"</p>}
                    {item.landlordNote && <p><strong>Phản hồi chủ nhà:</strong> "{item.landlordNote}"</p>}
                    {item.cancelReason && <p className="cancel-reason"><strong>Lý do hủy/từ chối:</strong> "{item.cancelReason}"</p>}
                    <p className="created-at">Được tạo ngày: {formatDateVi(item.createdAt)}</p>
                  </div>

                  {isCancellable && (
                    <div className="appointment-card__footer">
                      <button
                        className="btn-cancel"
                        onClick={() => handleCancelClick(item.id)}
                      >
                        Hủy lịch hẹn
                      </button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </main>

      {/* Cancel Dialog Modal */}
      {cancellingId && (
        <div className="viewing-modal-overlay" onClick={() => setCancellingId(null)}>
          <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
            <header className="viewing-modal-header">
              <h2>Xác nhận hủy lịch hẹn</h2>
              <button className="viewing-modal-close-btn" onClick={() => setCancellingId(null)}>
                &times;
              </button>
            </header>
            <form onSubmit={handleConfirmCancel} className="viewing-modal-form">
              <p>Bạn có chắc chắn muốn hủy lịch hẹn xem phòng này? Hành động này không thể hoàn tác.</p>
              
              <div className="viewing-modal-field" style={{ marginTop: '16px' }}>
                <label htmlFor="cancelReasonInput">Lý do hủy (Tùy chọn)</label>
                <textarea
                  id="cancelReasonInput"
                  placeholder="Nhập lý do hủy..."
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  disabled={cancelling}
                />
              </div>

              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setCancellingId(null)}
                  disabled={cancelling}
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn"
                  style={{ background: '#ef4444', color: '#ffffff' }}
                  disabled={cancelling}
                >
                  {cancelling ? 'Đang hủy...' : 'Đồng ý hủy'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
