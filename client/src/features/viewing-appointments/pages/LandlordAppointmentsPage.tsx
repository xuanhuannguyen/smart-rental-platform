import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getLandlordAppointments,
  checkConflict,
  confirmViewingAppointment,
  rejectViewingAppointment,
  cancelViewingAppointmentByLandlord,
  completeViewingAppointment,
} from '../api';
import type { ViewingAppointment, ViewingAppointmentStatus, ConflictCheckResponse } from '../types';
import { Alert } from '../../../shared/components/ui/Alert';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { formatDateVi } from '../../../shared/utils/format';
import './LandlordAppointmentsPage.css';

export default function LandlordAppointmentsPage() {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<ViewingAppointment[]>([]);
  const [conflictMap, setConflictMap] = useState<Record<string, ConflictCheckResponse>>({});
  const [activeTab, setActiveTab] = useState<'all' | 'pending' | 'confirmed' | 'history'>('pending');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Dialog action states
  const [confirmingAppointment, setConfirmingAppointment] = useState<ViewingAppointment | null>(null);
  const [landlordNote, setLandlordNote] = useState('');
  const [confirmDespiteConflict, setConfirmDespiteConflict] = useState(false);
  const [modalConflictLoading, setModalConflictLoading] = useState(false);
  
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  
  const [actionLoading, setActionLoading] = useState(false);
  // Separate error state for modals so errors show inside the modal, not behind it
  const [modalError, setModalError] = useState('');

  const loadData = async () => {
    setLoading(true);
    setError('');
    try {
      const appointmentsData = await getLandlordAppointments();
      setAppointments(appointmentsData);

      // Check conflicts for Pending appointments (for card-level warning badges)
      const pendingApps = appointmentsData.filter((a) => a.status === 'Pending');
      const conflicts: Record<string, ConflictCheckResponse> = {};
      await Promise.all(
        pendingApps.map(async (app) => {
          try {
            const conflictResult = await checkConflict(app.id);
            conflicts[app.id] = conflictResult;
          } catch (e) {
            console.error(`Error checking conflict for ${app.id}:`, e);
          }
        })
      );
      setConflictMap(conflicts);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách lịch hẹn của chủ nhà.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  // Real-time conflict check when opening confirm modal
  const handleConfirmClick = async (app: ViewingAppointment) => {
    setConfirmingAppointment(app);
    setLandlordNote('');
    setConfirmDespiteConflict(false);
    setModalError('');
    setModalConflictLoading(true);
    try {
      const freshConflict = await checkConflict(app.id);
      setConflictMap(prev => ({ ...prev, [app.id]: freshConflict }));
    } catch {
      // If check fails, still allow modal to open (server will re-check on submit)
    } finally {
      setModalConflictLoading(false);
    }
  };

  const handleConfirmSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!confirmingAppointment) return;

    const hasConflict = conflictMap[confirmingAppointment.id]?.hasConflict;
    if (hasConflict && !confirmDespiteConflict) {
      setModalError('Bạn cần đánh dấu đồng ý xác nhận mặc dù lịch trùng giờ.');
      return;
    }

    setActionLoading(true);
    setModalError('');
    try {
      await confirmViewingAppointment(confirmingAppointment.id, {
        confirmDespiteConflict: !!hasConflict && confirmDespiteConflict,
        landlordNote: landlordNote.trim() || null,
      });
      setSuccess('Xác nhận lịch hẹn thành công.');
      setConfirmingAppointment(null);
      void loadData();
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      setModalError(getApiErrorMessage(err, 'Không thể xác nhận lịch hẹn.'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleRejectClick = (id: string) => {
    setRejectingId(id);
    setRejectReason('');
    setModalError('');
  };

  const handleRejectSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rejectingId || !rejectReason.trim()) return;

    setActionLoading(true);
    setModalError('');
    try {
      await rejectViewingAppointment(rejectingId, {
        rejectReason: rejectReason.trim(),
      });
      setSuccess('Đã từ chối lịch hẹn xem phòng.');
      setRejectingId(null);
      void loadData();
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      setModalError(getApiErrorMessage(err, 'Không thể từ chối lịch hẹn.'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancelClick = (id: string) => {
    setCancellingId(id);
    setCancelReason('');
    setModalError('');
  };

  const handleCancelSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cancellingId || !cancelReason.trim()) return;

    setActionLoading(true);
    setModalError('');
    try {
      await cancelViewingAppointmentByLandlord(cancellingId, {
        cancelReason: cancelReason.trim(),
      });
      setSuccess('Hủy lịch hẹn thành công.');
      setCancellingId(null);
      void loadData();
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      setModalError(getApiErrorMessage(err, 'Không thể hủy lịch hẹn.'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleCompleteClick = async (id: string) => {
    if (!window.confirm('Đánh dấu buổi xem phòng trọ đã hoàn tất?')) return;
    setError('');
    try {
      await completeViewingAppointment(id);
      setSuccess('Đánh dấu hoàn thành buổi xem phòng thành công.');
      void loadData();
      setTimeout(() => setSuccess(''), 5000);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể đánh dấu hoàn thành.'));
    }
  };

  // Filter
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

  const isAppointmentInPast = (scheduledAt: string) => {
    return new Date(scheduledAt) <= new Date();
  };

  return (
    <div className="landlord-dashboard landlord-appointments-page">
      <aside className="dashboard-sidebar">
        <h1>Chủ trọ</h1>
        <button className="sidebar-item" onClick={() => navigate('/landlord/dashboard')}>
          Quản lý khu trọ
        </button>
        <button className="sidebar-item active" onClick={() => navigate('/landlord/viewing-appointments')}>
          Lịch hẹn xem phòng
        </button>
        <button className="sidebar-item sidebar-back-btn" onClick={() => navigate('/home')}>
          ← Quay lại trang chủ
        </button>
      </aside>

      <main className="dashboard-main">
        <section className="overview-band">
          <div className="overview-left">
            <p className="eyebrow">Yêu cầu</p>
            <h2>Lịch hẹn xem phòng</h2>
            <p className="overview-description">Duyệt và kiểm tra lịch hẹn từ khách thuê</p>
          </div>
        </section>

        {error && <Alert type="error">{error}</Alert>}
        {success && <Alert type="success">{success}</Alert>}

        <div className="landlord-tabs">
          <button className={activeTab === 'pending' ? 'active' : ''} onClick={() => setActiveTab('pending')}>
            Chờ duyệt ({appointments.filter(a => a.status === 'Pending').length})
          </button>
          <button className={activeTab === 'confirmed' ? 'active' : ''} onClick={() => setActiveTab('confirmed')}>
            Đã xác nhận ({appointments.filter(a => a.status === 'Confirmed').length})
          </button>
          <button className={activeTab === 'all' ? 'active' : ''} onClick={() => setActiveTab('all')}>
            Tất cả
          </button>
          <button className={activeTab === 'history' ? 'active' : ''} onClick={() => setActiveTab('history')}>
            Lịch sử duyệt
          </button>
        </div>

        {loading ? (
          <div className="appointments-loading">Đang tải lịch hẹn...</div>
        ) : filteredAppointments.length === 0 ? (
          <div className="appointments-empty">
            <p>Không có lịch hẹn nào.</p>
          </div>
        ) : (
          <div className="landlord-appointments-list">
            {filteredAppointments.map((item) => {
              const houseName = item.roomingHouseName ?? 'Khu trọ';
              const roomNumber = item.roomNumber ?? 'phòng';
              const tenantName = item.tenantDisplayName ?? `Khách ${item.tenantUserId.substring(0, 8)}`;
              const conflict = conflictMap[item.id];
              const isPast = isAppointmentInPast(item.scheduledAt);

              return (
                <div key={item.id} className={`landlord-card status-${item.status.toLowerCase()}`}>
                  <div className="landlord-card__header">
                    <div>
                      <h3>{houseName} - Phòng {roomNumber}</h3>
                      <p className="tenant-info">👤 Khách hẹn: {tenantName}</p>
                    </div>
                    <span className={`status-tag status-tag--${item.status.toLowerCase()}`}>
                      {getStatusText(item.status)}
                    </span>
                  </div>

                  <div className="landlord-card__body">
                    <p><strong>Thời gian hẹn:</strong> {formatDateVi(item.scheduledAt)} ({item.durationMinutes} phút)</p>
                    {item.tenantNote && <p className="note-text"><strong>Lời nhắn khách thuê:</strong> "{item.tenantNote}"</p>}
                    {item.landlordNote && <p className="note-text"><strong>Ghi chú phản hồi:</strong> "{item.landlordNote}"</p>}
                    {item.cancelReason && <p className="cancel-reason"><strong>Lý do hủy/từ chối:</strong> "{item.cancelReason}"</p>}
                    
                    {/* Visual overlap check warning for Pending state */}
                    {item.status === 'Pending' && conflict?.hasConflict && (
                      <div className="conflict-warning-card">
                        <p className="conflict-warning-title">⚠️ Cảnh báo trùng giờ hẹn!</p>
                        <ul>
                          {conflict.conflictingAppointments.map((c) => (
                            <li key={c.id}>
                              Trùng với lịch đã xác nhận ở <strong>{c.roomingHouseName} - Phòng {c.roomNumber}</strong> ({formatDateVi(c.scheduledAt)})
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </div>

                  <div className="landlord-card__footer">
                    {item.status === 'Pending' && (
                      <div className="action-buttons-group">
                        <button className="btn-confirm" onClick={() => handleConfirmClick(item)}>
                          Xác nhận
                        </button>
                        <button className="btn-reject" onClick={() => handleRejectClick(item.id)}>
                          Từ chối
                        </button>
                      </div>
                    )}

                    {item.status === 'Confirmed' && (
                      <div className="action-buttons-group">
                        {isPast ? (
                          <button className="btn-complete" onClick={() => handleCompleteClick(item.id)}>
                            Hoàn tất xem phòng
                          </button>
                        ) : (
                          <span className="info-time-waiting">Chờ thời gian hẹn diễn ra để hoàn tất</span>
                        )}
                        <button className="btn-cancel-landlord" onClick={() => handleCancelClick(item.id)}>
                          Hủy lịch
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </main>

      {/* Confirm Modal */}
      {confirmingAppointment && (
        <div className="viewing-modal-overlay" onClick={() => setConfirmingAppointment(null)}>
          <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
            <header className="viewing-modal-header">
              <h2>Xác nhận lịch hẹn xem phòng</h2>
              <button className="viewing-modal-close-btn" onClick={() => setConfirmingAppointment(null)}>
                &times;
              </button>
            </header>
            <form onSubmit={handleConfirmSubmit} className="viewing-modal-form">
              {modalError && <div className="viewing-modal-error">{modalError}</div>}

              {modalConflictLoading ? (
                <div className="modal-conflict-loading">Đang kiểm tra lịch trùng giờ...</div>
              ) : conflictMap[confirmingAppointment.id]?.hasConflict && (
                <div className="modal-conflict-alert">
                  <p>⚠️ <strong>Chú ý:</strong> Lịch hẹn này đang bị trùng giờ hẹn với lịch đã xác nhận khác của bạn.</p>
                  <label className="checkbox-field">
                    <input
                      type="checkbox"
                      checked={confirmDespiteConflict}
                      onChange={(e) => setConfirmDespiteConflict(e.target.checked)}
                      required
                    />
                    <span>Đồng ý xác nhận đè mặc dù trùng giờ.</span>
                  </label>
                </div>
              )}

              <div className="viewing-modal-field" style={{ marginTop: '12px' }}>
                <label htmlFor="landlordNoteInput">Ghi chú gửi Khách thuê (Tùy chọn)</label>
                <textarea
                  id="landlordNoteInput"
                  placeholder="Ví dụ: Bạn có thể liên hệ số điện thoại 090xxx để gặp tôi..."
                  rows={3}
                  value={landlordNote}
                  onChange={(e) => setLandlordNote(e.target.value)}
                  disabled={actionLoading}
                />
              </div>

              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setConfirmingAppointment(null)}
                  disabled={actionLoading}
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn viewing-modal-btn--primary"
                  disabled={actionLoading || modalConflictLoading}
                >
                  {actionLoading ? 'Đang xác nhận...' : 'Xác nhận duyệt'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}

      {/* Reject Modal */}
      {rejectingId && (
        <div className="viewing-modal-overlay" onClick={() => setRejectingId(null)}>
          <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
            <header className="viewing-modal-header">
              <h2>Từ chối lịch hẹn xem phòng</h2>
              <button className="viewing-modal-close-btn" onClick={() => setRejectingId(null)}>
                &times;
              </button>
            </header>
            <form onSubmit={handleRejectSubmit} className="viewing-modal-form">
              {modalError && <div className="viewing-modal-error">{modalError}</div>}

              <div className="viewing-modal-field">
                <label htmlFor="rejectReasonInput">Lý do từ chối <span className="required">*</span></label>
                <textarea
                  id="rejectReasonInput"
                  placeholder="Nhập lý do từ chối (bắt buộc)..."
                  rows={3}
                  value={rejectReason}
                  onChange={(e) => setRejectReason(e.target.value)}
                  disabled={actionLoading}
                  required
                />
              </div>

              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setRejectingId(null)}
                  disabled={actionLoading}
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn"
                  style={{ background: '#ef4444', color: '#ffffff' }}
                  disabled={actionLoading || !rejectReason.trim()}
                >
                  {actionLoading ? 'Đang từ chối...' : 'Từ chối duyệt'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}

      {/* Cancel Modal */}
      {cancellingId && (
        <div className="viewing-modal-overlay" onClick={() => setCancellingId(null)}>
          <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
            <header className="viewing-modal-header">
              <h2>Hủy lịch hẹn đã xác nhận</h2>
              <button className="viewing-modal-close-btn" onClick={() => setCancellingId(null)}>
                &times;
              </button>
            </header>
            <form onSubmit={handleCancelSubmit} className="viewing-modal-form">
              {modalError && <div className="viewing-modal-error">{modalError}</div>}

              <div className="viewing-modal-field">
                <label htmlFor="cancelReasonInput">Lý do hủy lịch <span className="required">*</span></label>
                <textarea
                  id="cancelReasonInput"
                  placeholder="Nhập lý do hủy (bắt buộc)..."
                  rows={3}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  disabled={actionLoading}
                  required
                />
              </div>

              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setCancellingId(null)}
                  disabled={actionLoading}
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn"
                  style={{ background: '#ef4444', color: '#ffffff' }}
                  disabled={actionLoading || !cancelReason.trim()}
                >
                  {actionLoading ? 'Đang hủy...' : 'Hủy lịch hẹn'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
