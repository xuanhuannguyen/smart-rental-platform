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
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { Toast } from '../../../shared/components/ui/Toast';
import { Card, CardMetaRow, type CardAction, type CardStatusTone } from '../../../shared/components/ui/Card';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { formatDateTimeVi } from '../../../shared/utils/format';
import '../../landlord/pages/LandlordDashboardPage.css';
import './LandlordAppointmentsPage.css';
import '../components/ViewingAppointmentModal.css';

type AppointmentTab = 'all' | 'pending' | 'confirmed' | 'history';

function getAppointmentTabIcon(tab: AppointmentTab) {
  const props = {
    width: 15,
    height: 15,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2.2,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
  };

  switch (tab) {
    case 'pending':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      );
    case 'confirmed':
      return (
        <svg {...props}>
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
          <polyline points="22 4 12 14.01 9 11.01" />
        </svg>
      );
    case 'history':
      return (
        <svg {...props}>
          <polyline points="1 4 1 10 7 10" />
          <path d="M3.51 15a9 9 0 1 0 .49-4.95" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <line x1="8" y1="6" x2="21" y2="6" />
          <line x1="8" y1="12" x2="21" y2="12" />
          <line x1="8" y1="18" x2="21" y2="18" />
          <line x1="3" y1="6" x2="3.01" y2="6" />
          <line x1="3" y1="12" x2="3.01" y2="12" />
          <line x1="3" y1="18" x2="3.01" y2="18" />
        </svg>
      );
  }
}

function getAppointmentStatusTone(statusKey: string): CardStatusTone {
  switch (statusKey) {
    case 'confirmed':
      return 'success';
    case 'pending':
      return 'warning';
    case 'rejected':
      return 'danger';
    case 'proposal':
      return 'info';
    case 'completed':
      return 'reserved';
    case 'expired':
    case 'cancelledbylandlord':
    case 'cancelledbytenant':
      return 'neutral';
    default:
      return 'neutral';
  }
}

export default function LandlordAppointmentsPage() {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<ViewingAppointment[]>([]);
  const [conflictMap, setConflictMap] = useState<Record<string, ConflictCheckResponse>>({});
  const [activeTab, setActiveTab] = useState<AppointmentTab>('pending');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  // Dialog action states
  const [confirmingAppointment, setConfirmingAppointment] = useState<ViewingAppointment | null>(null);
  const [landlordNote, setLandlordNote] = useState('');
  const [confirmDespiteConflict, setConfirmDespiteConflict] = useState(false);
  const [modalConflictLoading, setModalConflictLoading] = useState(false);

  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [proposeNewTime, setProposeNewTime] = useState(false);
  const [proposedDate, setProposedDate] = useState('');
  const [proposedTime, setProposedTime] = useState('');

  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');

  const [completingId, setCompletingId] = useState<string | null>(null);

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
      setToast({ message: 'Xác nhận lịch hẹn thành công.', type: 'success' });
      setConfirmingAppointment(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể xác nhận lịch hẹn.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleRejectClick = (id: string) => {
    setRejectingId(id);
    setRejectReason('');
    setProposeNewTime(false);
    setProposedDate('');
    setProposedTime('');
    setModalError('');
  };

  const handleRejectSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rejectingId || !rejectReason.trim()) return;

    setActionLoading(true);
    setModalError('');

    let proposedScheduledAt: string | null = null;
    let proposedDurationMinutes: number | null = null;
    if (proposeNewTime && proposedDate && proposedTime) {
      proposedScheduledAt = new Date(`${proposedDate}T${proposedTime}`).toISOString();
    }

    try {
      await rejectViewingAppointment(rejectingId, {
        rejectReason: rejectReason.trim(),
        proposedScheduledAt,
        proposedDurationMinutes,
      });
      setToast({ message: 'Đã từ chối lịch hẹn xem phòng.', type: 'success' });
      setRejectingId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể từ chối lịch hẹn.'), type: 'error' });
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
      setToast({ message: 'Hủy lịch hẹn thành công.', type: 'success' });
      setCancellingId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể hủy lịch hẹn.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleCompleteClick = (id: string) => {
    setCompletingId(id);
    setModalError('');
  };

  const handleCompleteSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!completingId) return;

    setActionLoading(true);
    setModalError('');
    try {
      await completeViewingAppointment(completingId);
      setToast({ message: 'Đánh dấu hoàn thành buổi xem phòng thành công.', type: 'success' });
      setCompletingId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể đánh dấu hoàn thành.'), type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const hasProposal = (app: ViewingAppointment) =>
    app.status === 'Rejected' && !!app.proposedScheduledAt;

  // Filter
  const filteredAppointments = appointments.filter((appointment) => {
    if (activeTab === 'pending') {
      return appointment.status === 'Pending' || hasProposal(appointment);
    }
    if (activeTab === 'confirmed') {
      return appointment.status === 'Confirmed';
    }
    if (activeTab === 'history') {
      return ['CancelledByTenant', 'CancelledByLandlord', 'Completed', 'Expired'].includes(
        appointment.status
      ) || (appointment.status === 'Rejected' && !appointment.proposedScheduledAt);
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

  const pendingCount = appointments.filter(a => a.status === 'Pending' || hasProposal(a)).length;

  return (
    <div className="landlord-dashboard-page landlord-appointments-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        {/* === PAGE HEADER === */}
        <PageHeader
          icon={
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
              <line x1="16" y1="2" x2="16" y2="6" />
              <line x1="8" y1="2" x2="8" y2="6" />
              <line x1="3" y1="10" x2="21" y2="10" />
            </svg>
          }
          eyebrow="QUẢN LÝ"
          title="Lịch hẹn xem phòng"
          description="Duyệt và kiểm tra lịch hẹn từ khách thuê"
        />

        <div className="dashboard-content">
          {error && <div className="mb-4"><Alert type="error">{error}</Alert></div>}

          {/* === TABS === */}
          <Tabs
            className="attached-bottom"
            variant="segmented-secondary"
            activeId={activeTab}
            onChange={(tab) => setActiveTab(tab as AppointmentTab)}
            items={[
              { id: 'all', label: 'Tất cả', icon: getAppointmentTabIcon('all') },
              { id: 'pending', label: `Chờ xử lý (${pendingCount})`, icon: getAppointmentTabIcon('pending') },
              { id: 'confirmed', label: `Đã xác nhận (${appointments.filter(a => a.status === 'Confirmed').length})`, icon: getAppointmentTabIcon('confirmed') },
              { id: 'history', label: 'Lịch sử duyệt', icon: getAppointmentTabIcon('history') },
            ]}
          />

          <section className="tab-attached-panel tab-attached-panel--cards">
            {loading ? (
              <div className="appt-state-box">
                <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ animation: 'spin 1s linear infinite' }}>
                  <line x1="12" y1="2" x2="12" y2="6" /><line x1="12" y1="18" x2="12" y2="22" />
                  <line x1="4.93" y1="4.93" x2="7.76" y2="7.76" /><line x1="16.24" y1="16.24" x2="19.07" y2="19.07" />
                  <line x1="2" y1="12" x2="6" y2="12" /><line x1="18" y1="12" x2="22" y2="12" />
                  <line x1="4.93" y1="19.07" x2="7.76" y2="16.24" /><line x1="16.24" y1="7.76" x2="19.07" y2="4.93" />
                </svg>
                <p>Đang tải lịch hẹn...</p>
              </div>
            ) : filteredAppointments.length === 0 ? (
              <div className="appt-state-box appt-state-box--empty">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                  <line x1="16" y1="2" x2="16" y2="6" />
                  <line x1="8" y1="2" x2="8" y2="6" />
                  <line x1="3" y1="10" x2="21" y2="10" />
                </svg>
                <p>Không có lịch hẹn nào trong danh mục này.</p>
              </div>
            ) : (
              <div className="appt-list">
                {filteredAppointments.map((item) => {
                  const houseName = item.roomingHouseName ?? 'Khu trọ';
                  const roomNumber = item.roomNumber ?? 'phòng';
                  const tenantName = item.tenantDisplayName ?? `Khách ${item.tenantUserId.substring(0, 8)}`;
                  const conflict = conflictMap[item.id];
                  const isPast = isAppointmentInPast(item.scheduledAt);
                  const statusKey = hasProposal(item) ? 'proposal' : item.status.toLowerCase();
                  const statusTone = getAppointmentStatusTone(statusKey);
                  const actionItems: CardAction[] = [];

                  if (item.status === 'Pending') {
                    actionItems.push(
                      {
                        label: 'Từ chối',
                        variant: 'danger',
                        onClick: () => handleRejectClick(item.id),
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <line x1="18" y1="6" x2="6" y2="18" />
                            <line x1="6" y1="6" x2="18" y2="18" />
                          </svg>
                        ),
                      },
                      {
                        label: 'Xác nhận',
                        variant: 'success',
                        onClick: () => handleConfirmClick(item),
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <polyline points="20 6 9 17 4 12" />
                          </svg>
                        ),
                      }
                    );
                  }

                  if (item.status === 'Confirmed') {
                    if (isPast) {
                      actionItems.push({
                        label: 'Hoàn tất xem phòng',
                        variant: 'primary',
                        onClick: () => handleCompleteClick(item.id),
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                            <polyline points="20 6 9 17 4 12" />
                          </svg>
                        ),
                      });
                    }

                    actionItems.push({
                      label: 'Hủy lịch',
                      variant: 'secondary',
                      onClick: () => handleCancelClick(item.id),
                    });
                  }

                  return (
                    <Card
                      key={item.id}
                      title={`${houseName} - Phòng ${roomNumber}`}
                      status={hasProposal(item) ? 'Chờ phản hồi' : getStatusText(item.status)}
                      statusTone={statusTone}
                      bodyColumns={2}
                      actionItems={actionItems}
                      actions={
                        item.status === 'Confirmed' && !isPast ? (
                          <span className="appt-waiting-info">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <circle cx="12" cy="12" r="10" />
                              <polyline points="12 6 12 12 16 14" />
                            </svg>
                            Chờ thời gian hẹn diễn ra để hoàn tất
                          </span>
                        ) : null
                      }
                    >
                      <CardMetaRow
                        label="Khách hẹn:"
                        value={tenantName}
                        icon={
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                            <circle cx="12" cy="7" r="4" />
                          </svg>
                        }
                      />
                      <CardMetaRow
                        label="Thời gian hẹn:"
                        value={`${formatDateTimeVi(item.scheduledAt)} (${item.durationMinutes} phút)`}
                        icon={
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                            <line x1="16" y1="2" x2="16" y2="6" />
                            <line x1="8" y1="2" x2="8" y2="6" />
                            <line x1="3" y1="10" x2="21" y2="10" />
                          </svg>
                        }
                      />

                        {item.tenantNote && (
                          <div className="appt-note-box appt-note-box--tenant appt-card-span-full">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                            </svg>
                            <span><strong>Lời nhắn khách thuê:</strong> "{item.tenantNote}"</span>
                          </div>
                        )}
                        {item.landlordNote && (
                          <div className="appt-note-box appt-note-box--landlord appt-card-span-full">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                            </svg>
                            <span><strong>Ghi chú phản hồi:</strong> "{item.landlordNote}"</span>
                          </div>
                        )}
                        {item.cancelReason && (
                          <div className="appt-note-box appt-note-box--cancel appt-card-span-full">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <circle cx="12" cy="12" r="10" />
                              <line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" />
                            </svg>
                            <span><strong>Lý do hủy/từ chối:</strong> "{item.cancelReason}"</span>
                          </div>
                        )}

                        {/* Proposal banner */}
                        {item.status === 'Rejected' && item.proposedScheduledAt && (
                          <div className="appt-proposal-banner appt-card-span-full">
                            <div className="appt-proposal-banner__title">
                              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                                <circle cx="12" cy="12" r="10" />
                                <polyline points="12 6 12 12 16 14" />
                              </svg>
                              Đã đề xuất giờ mới — đang chờ khách phản hồi
                            </div>
                            <p><strong>Giờ đề xuất:</strong> {formatDateTimeVi(item.proposedScheduledAt)} ({item.proposedDurationMinutes ?? item.durationMinutes} phút)</p>
                          </div>
                        )}

                        {/* Conflict warning */}
                        {item.status === 'Pending' && conflict?.hasConflict && (
                          <div className="appt-conflict-banner appt-card-span-full">
                            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                              <line x1="12" y1="9" x2="12" y2="13" />
                              <line x1="12" y1="17" x2="12.01" y2="17" />
                            </svg>
                            <div>
                              <strong>Cảnh báo trùng giờ hẹn!</strong>
                              <ul>
                                {conflict.conflictingAppointments.map((c) => (
                                  <li key={c.id}>Trùng với lịch đã xác nhận ở <strong>{c.roomingHouseName} - Phòng {c.roomNumber}</strong> ({formatDateTimeVi(c.scheduledAt)})</li>
                                ))}
                              </ul>
                            </div>
                          </div>
                        )}
                    </Card>
                  );
                })}
              </div>
            )}
          </section>
        </div>
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
              {modalError && <Alert type="error">{modalError}</Alert>}

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
          <div className="viewing-modal-container" style={{ width: 'min(92%, 560px)' }} onClick={(e) => e.stopPropagation()}>
            {/* Header */}
            <header className="viewing-modal-header" style={{ paddingBottom: '0' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <div style={{
                  width: '36px', height: '36px', borderRadius: '50%',
                  background: '#fef2f2', border: '1px solid #fca5a5',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  flexShrink: 0,
                }}>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" stroke="#ef4444">
                    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                    <line x1="12" y1="9" x2="12" y2="13" />
                    <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                </div>
                <h2 style={{ margin: 0, fontSize: '20px', fontWeight: 700, color: '#0f172a' }}>Từ chối lịch hẹn xem phòng</h2>
              </div>
              <button className="viewing-modal-close-btn" onClick={() => setRejectingId(null)}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
            </header>

            <form onSubmit={handleRejectSubmit} className="viewing-modal-form" style={{ paddingTop: '8px' }}>
              {/* Subtitle */}
              <p style={{ margin: '0 0 4px', fontSize: '13.5px', color: '#64748b', lineHeight: 1.5 }}>
                Vui lòng cung cấp lý do từ chối và tùy chọn đề xuất khung giờ khác cho người thuê.
              </p>

              {modalError && <Alert type="error">{modalError}</Alert>}

              {/* Reject reason textarea */}
              <div className="viewing-modal-field">
                <label htmlFor="rejectReasonInput">
                  Lý do từ chối <span className="required">*</span>
                </label>
                <div style={{ position: 'relative' }}>
                  <textarea
                    id="rejectReasonInput"
                    placeholder="Nhập lý do từ chối (bắt buộc)..."
                    rows={4}
                    maxLength={500}
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    disabled={actionLoading}
                    required
                    style={{
                      width: '100%',
                      border: '1px solid #cbd5e1',
                      borderRadius: '10px',
                      padding: '10px 14px 28px',
                      fontSize: '14px',
                      fontFamily: 'inherit',
                      color: '#0f172a',
                      outline: 'none',
                      resize: 'vertical',
                      lineHeight: 1.5,
                      boxSizing: 'border-box',
                      transition: 'border-color 0.2s ease, box-shadow 0.2s ease',
                    }}
                    onFocus={(e) => { e.target.style.borderColor = '#2563eb'; e.target.style.boxShadow = '0 0 0 3px rgba(37,99,235,0.1)'; }}
                    onBlur={(e) => { e.target.style.borderColor = '#cbd5e1'; e.target.style.boxShadow = 'none'; }}
                  />
                  <span style={{
                    position: 'absolute', bottom: '8px', right: '12px',
                    fontSize: '11px', color: '#94a3b8', pointerEvents: 'none',
                  }}>
                    {rejectReason.length}/500
                  </span>
                </div>
              </div>

              {/* Proposal toggle – blue checkbox style */}
              <div style={{
                border: '1px solid #e2e8f0',
                borderRadius: '10px',
                padding: '14px 16px',
                background: proposeNewTime ? '#f0f9ff' : '#fafafa',
                transition: 'background 0.2s ease',
              }}>
                <label style={{
                  display: 'flex', alignItems: 'flex-start', gap: '10px',
                  cursor: actionLoading ? 'not-allowed' : 'pointer',
                }}>
                  <input
                    type="checkbox"
                    checked={proposeNewTime}
                    onChange={(e) => setProposeNewTime(e.target.checked)}
                    disabled={actionLoading}
                    style={{ width: '16px', height: '16px', marginTop: '2px', cursor: 'pointer', accentColor: '#2563eb' }}
                  />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: '14px', color: '#0f172a' }}>
                      Đề xuất khung giờ khác cho người thuê
                    </div>
                    <div style={{ fontSize: '12.5px', color: '#64748b', marginTop: '2px' }}>
                      Chọn các khung giờ phù hợp để người thuê có thể chọn lại.
                    </div>
                  </div>
                </label>

                {proposeNewTime && (
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginTop: '14px' }}>
                    <div className="viewing-modal-field">
                      <label htmlFor="proposedDateInput">
                        Ngày đề xuất <span className="required">*</span>
                      </label>
                      <div className="input-with-icon">
                        <span className="input-icon-wrapper">
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                            <line x1="16" y1="2" x2="16" y2="6" />
                            <line x1="8" y1="2" x2="8" y2="6" />
                            <line x1="3" y1="10" x2="21" y2="10" />
                          </svg>
                        </span>
                        <input
                          id="proposedDateInput"
                          type="date"
                          value={proposedDate}
                          onChange={(e) => setProposedDate(e.target.value)}
                          disabled={actionLoading}
                          required={proposeNewTime}
                          placeholder="Chọn ngày"
                        />
                      </div>
                    </div>

                    <div className="viewing-modal-field">
                      <label htmlFor="proposedTimeInput">
                        Giờ đề xuất <span className="required">*</span>
                      </label>
                      <div className="input-with-icon">
                        <span className="input-icon-wrapper">
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <circle cx="12" cy="12" r="10" />
                            <polyline points="12 6 12 12 16 14" />
                          </svg>
                        </span>
                        <input
                          id="proposedTimeInput"
                          type="time"
                          value={proposedTime}
                          onChange={(e) => setProposedTime(e.target.value)}
                          disabled={actionLoading}
                          required={proposeNewTime}
                          placeholder="Chọn giờ"
                        />
                      </div>
                    </div>
                  </div>
                )}
              </div>

              {/* Info note box */}
              <div style={{
                display: 'flex', gap: '10px', alignItems: 'flex-start',
                background: '#eff6ff', border: '1px solid #bfdbfe',
                borderRadius: '10px', padding: '12px 14px',
              }}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0, marginTop: '2px' }}>
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
                <div>
                  <div style={{ fontWeight: 700, fontSize: '13px', color: '#1d4ed8' }}>Lưu ý</div>
                  <div style={{ fontSize: '13px', color: '#3b82f6', marginTop: '2px', lineHeight: 1.5 }}>
                    Người thuê sẽ nhận được thông báo từ chối cùng với lý do và khung giờ đề xuất (nếu có).
                  </div>
                </div>
              </div>

              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setRejectingId(null)}
                  disabled={actionLoading}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn"
                  style={{
                    background: actionLoading || !rejectReason.trim() ? '#fca5a5' : '#ef4444',
                    color: '#ffffff',
                    boxShadow: !actionLoading && rejectReason.trim() ? '0 4px 12px rgba(239,68,68,0.3)' : 'none',
                    transition: 'all 0.2s ease',
                  }}
                  disabled={actionLoading || !rejectReason.trim()}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="3 6 5 6 21 6" />
                    <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
                    <path d="M10 11v6" />
                    <path d="M14 11v6" />
                    <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
                  </svg>
                  {actionLoading ? 'Đang từ chối...' : 'Từ chối lịch hẹn'}
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
              {modalError && <Alert type="error">{modalError}</Alert>}

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
      {/* Complete Modal */}
      {completingId && (
        <div className="viewing-modal-overlay" onClick={() => setCompletingId(null)}>
          <div className="viewing-modal-container" onClick={(e) => e.stopPropagation()}>
            <header className="viewing-modal-header">
              <h2>Xác nhận hoàn tất</h2>
              <button className="viewing-modal-close-btn" onClick={() => setCompletingId(null)}>
                &times;
              </button>
            </header>
            <form onSubmit={handleCompleteSubmit} className="viewing-modal-form">
              {modalError && <Alert type="error">{modalError}</Alert>}
              <div className="viewing-modal-field">
                <p>Đánh dấu buổi xem phòng trọ đã diễn ra thành công?</p>
              </div>
              <footer className="viewing-modal-footer">
                <button
                  type="button"
                  className="viewing-modal-btn viewing-modal-btn--secondary"
                  onClick={() => setCompletingId(null)}
                  disabled={actionLoading}
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="viewing-modal-btn viewing-modal-btn--primary"
                  disabled={actionLoading}
                >
                  {actionLoading ? 'Đang cập nhật...' : 'Hoàn tất'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
