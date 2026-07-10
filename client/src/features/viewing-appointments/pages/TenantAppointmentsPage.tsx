import { useEffect, useState } from 'react';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useNavigate } from 'react-router-dom';
import { getTenantAppointments, cancelViewingAppointmentByTenant, acceptProposal, rejectProposal } from '../api';
import type { ViewingAppointment, ViewingAppointmentStatus } from '../types';
import { Alert } from '../../../shared/components/ui/Alert';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { Toast } from '../../../shared/components/ui/Toast';
import { Card, CardMetaRow, type CardAction, type CardStatusTone } from '../../../shared/components/ui/Card';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { formatDateTimeVi, formatDateVi } from '../../../shared/utils/format';
import './TenantAppointmentsPage.css';

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
    case 'cancelledbytenant':
    case 'cancelledbylandlord':
      return 'neutral';
    default:
      return 'neutral';
  }
}

export default function TenantAppointmentsPage() {
  const navigate = useNavigate();
  const [appointments, setAppointments] = useState<ViewingAppointment[]>([]);
  const [activeTab, setActiveTab] = useState<AppointmentTab>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  
  // Cancel dialog states
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);

  // Proposal states
  const [proposalActionId, setProposalActionId] = useState<string | null>(null);
  const [proposalLoading, setProposalLoading] = useState(false);

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
      setToast({ message: 'Hủy lịch hẹn thành công.', type: 'success' });
      setCancellingId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể hủy lịch hẹn.'), type: 'error' });
    } finally {
      setCancelling(false);
    }
  };

  const handleAcceptProposal = async (id: string) => {
    setProposalActionId(id);
    setProposalLoading(true);
    setError('');
    try {
      await acceptProposal(id);
      setToast({ message: 'Bạn đã chấp nhận đề xuất lịch xem phòng mới.', type: 'success' });
      setProposalActionId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể chấp nhận đề xuất.'), type: 'error' });
      setProposalActionId(null);
    } finally {
      setProposalLoading(false);
    }
  };

  const handleRejectProposal = async (id: string) => {
    setProposalActionId(id);
    setProposalLoading(true);
    setError('');
    try {
      await rejectProposal(id);
      setToast({ message: 'Bạn đã từ chối đề xuất lịch xem phòng.', type: 'success' });
      setProposalActionId(null);
      void loadData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể từ chối đề xuất.'), type: 'error' });
      setProposalActionId(null);
    } finally {
      setProposalLoading(false);
    }
  };

  const hasProposal = (app: ViewingAppointment) =>
    app.status === 'Rejected' && !!app.proposedScheduledAt;

  // Filter logic
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

  const pendingCount = appointments.filter(a => a.status === 'Pending' || hasProposal(a)).length;

  return (
    <div className="tenant-appointments-page">
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
              <line x1="16" y1="2" x2="16" y2="6"></line>
              <line x1="8" y1="2" x2="8" y2="6"></line>
              <line x1="3" y1="10" x2="21" y2="10"></line>
            </svg>
          </div>
        }
        eyebrow="QUẢN LÝ"
        title="Lịch hẹn xem phòng của tôi"
        description="Theo dõi và quản lý các lịch hẹn xem phòng của bạn"
      />

      <main className="appointments-container">
        {error && <Alert type="error">{error}</Alert>}

        <Tabs
          className="attached-bottom"
          variant="segmented-secondary"
          activeId={activeTab}
          onChange={(tab) => setActiveTab(tab as AppointmentTab)}
          items={[
            { id: 'all', label: 'Tất cả', icon: getAppointmentTabIcon('all') },
            { id: 'pending', label: `Cần phản hồi (${pendingCount})`, icon: getAppointmentTabIcon('pending') },
            { id: 'confirmed', label: 'Đã xác nhận', icon: getAppointmentTabIcon('confirmed') },
            { id: 'history', label: 'Lịch sử cuộc hẹn', icon: getAppointmentTabIcon('history') },
          ]}
        />

        <section className="tab-attached-panel tab-attached-panel--cards">
        {loading ? (
          <div className="appointments-loading">Đang tải lịch hẹn...</div>
        ) : filteredAppointments.length === 0 ? (
          <div className="appointments-empty">
            <p>Không tìm thấy lịch hẹn nào tương ứng.</p>
          </div>
        ) : (
          <div className="appointments-grid tenant-appointment-list">
            {filteredAppointments.map((item) => {
              const houseName = item.roomingHouseName ?? 'Khu trọ';
              const roomNumber = item.roomNumber ?? 'phòng';
              const isCancellable = item.status === 'Pending' || item.status === 'Confirmed';
              const statusKey = hasProposal(item) ? 'proposal' : item.status.toLowerCase();
              const actionItems: CardAction[] = [];

              if (item.status === 'Rejected' && item.proposedScheduledAt) {
                actionItems.push(
                  {
                    label: proposalActionId === item.id && proposalLoading ? 'Đang xử lý...' : 'Chấp nhận lịch mới',
                    variant: 'success',
                    disabled: proposalLoading,
                    onClick: () => handleAcceptProposal(item.id),
                  },
                  {
                    label: proposalActionId === item.id && proposalLoading ? 'Đang xử lý...' : 'Từ chối đề xuất',
                    variant: 'danger',
                    disabled: proposalLoading,
                    onClick: () => handleRejectProposal(item.id),
                  }
                );
              }

              if (isCancellable) {
                actionItems.push({
                  label: 'Hủy lịch hẹn',
                  variant: 'danger',
                  onClick: () => handleCancelClick(item.id),
                });
              }

              return (
                <Card
                  key={item.id}
                  title={`${houseName} - Phòng ${roomNumber}`}
                  status={hasProposal(item) ? 'Chờ phản hồi' : getStatusText(item.status)}
                  statusTone={getAppointmentStatusTone(statusKey)}
                  bodyColumns={2}
                  actionItems={actionItems}
                >
                    <CardMetaRow
                      label="Thời gian xem:"
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
                    <CardMetaRow
                      label="Ngày tạo:"
                      value={formatDateVi(item.createdAt)}
                      icon={
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="12" r="10" />
                          <polyline points="12 6 12 12 16 14" />
                        </svg>
                      }
                    />
                    {item.tenantNote && (
                      <div className="tenant-appointment-note tenant-appointment-note--tenant tenant-appointment-span-full">
                        <strong>Ghi chú của bạn:</strong> "{item.tenantNote}"
                      </div>
                    )}
                    {item.landlordNote && (
                      <div className="tenant-appointment-note tenant-appointment-note--landlord tenant-appointment-span-full">
                        <strong>Phản hồi chủ nhà:</strong> "{item.landlordNote}"
                      </div>
                    )}
                    {item.cancelReason && (
                      <div className="cancel-reason tenant-appointment-span-full">
                        <strong>Lý do hủy/từ chối:</strong> "{item.cancelReason}"
                      </div>
                    )}
                    {item.proposedScheduledAt && item.status === 'Rejected' && (
                      <div className="proposal-banner tenant-appointment-span-full">
                        <p className="proposal-title">📅 Chủ trọ đề xuất khung giờ mới</p>
                        <p><strong>Giờ đề xuất:</strong> {formatDateTimeVi(item.proposedScheduledAt)} ({item.proposedDurationMinutes ?? item.durationMinutes} phút)</p>
                      </div>
                    )}
                </Card>
              );
            })}
          </div>
        )}
        </section>
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

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
