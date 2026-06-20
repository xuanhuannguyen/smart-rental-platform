import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import type { ContractBriefResponse } from '../../contracts/types';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import { ContractTermsSetupModal } from '../../rental-contracts/components/ContractTermsSetupModal';
import { rentalRequestApi } from '../../rental-requests/api';
import type { RentalRequestResponse, RoomDepositResponse } from '../../rental-requests/types';
import './LandlordRentalRequestDetailPage.css';

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addDays(days: number) {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toDateInput(date);
}

export function LandlordRentalRequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [request, setRequest] = useState<RentalRequestResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showApproveForm, setShowApproveForm] = useState(false);
  const [showRejectForm, setShowRejectForm] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);
  const [termsContractId, setTermsContractId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const minimumApproveStartDate = addDays(2);
  const canApproveByStartDate = !request || request.desiredStartDate >= minimumApproveStartDate;

  useEffect(() => {
    async function fetchRequest() {
      try {
        const response = await rentalRequestApi.getIncomingRentalRequests();
        const found = response.data.find((item) => item.id === id);
        setRequest(found ?? null);
        setError(found ? '' : 'Không tìm thấy yêu cầu thuê phòng này.');
      } catch {
        setError('Lỗi khi tải thông tin yêu cầu.');
      } finally {
        setLoading(false);
      }
    }

    if (id) void fetchRequest();
  }, [id]);

  const handleApproveConfirm = async () => {
    if (!request) return;
    if (!canApproveByStartDate) {
      setToast({
        message: 'Ngày bắt đầu thuê phải còn cách hôm nay ít nhất 2 ngày để hai bên có thời gian hoàn tất hợp đồng.',
        type: 'error'
      });
      return;
    }

    try {
      const deadline = new Date();
      deadline.setHours(deadline.getHours() + 24);
      const response = await rentalRequestApi.approveRentalRequest(request.id, {
        paymentDeadlineAt: deadline.toISOString()
      });
      setRequest(response.data);
      setShowApproveForm(false);
      setToast({ message: 'Đã duyệt yêu cầu thành công.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể duyệt yêu cầu thuê.'), type: 'error' });
    }
  };

  const handleRejectConfirm = async () => {
    if (!request) return;
    if (!rejectReason.trim()) {
      setToast({ message: 'Vui lòng nhập lý do từ chối.', type: 'error' });
      return;
    }

    try {
      const response = await rentalRequestApi.rejectRentalRequest(request.id, {
        rejectedReason: rejectReason.trim()
      });
      setRequest(response.data);
      setShowRejectForm(false);
      setToast({ message: 'Đã từ chối yêu cầu.', type: 'success' });
    } catch {
      setToast({ message: 'Không thể từ chối yêu cầu thuê.', type: 'error' });
    }
  };

  if (loading) {
    return (
      <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải yêu cầu thuê...</div>
        </main>
      </div>
    );
  }

  if (error || !request) {
    return (
      <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <Alert type="error">{error || 'Có lỗi xảy ra.'}</Alert>
        </main>
      </div>
    );
  }

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>

      <main className="dashboard-main">
        <div className="landlord-request-detail-page">
          <section className="overview-band">
            <div className="overview-header-title-area">
              <button
                type="button"
                className="back-icon-btn"
                onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}
                title="Quay về danh sách yêu cầu"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="19" y1="12" x2="5" y2="12" />
                  <polyline points="12 19 5 12 12 5" />
                </svg>
              </button>
              <div className="overview-left">
                <p className="eyebrow">{request.roomingHouseName}</p>
                <h2>Phòng {request.roomNumber}</h2>
                <p className="overview-description">
                  Khách thuê: <strong>{request.tenantName}</strong>
                </p>
              </div>
            </div>

            <div className="overview-right" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '12px' }}>
              <span className={`status-badge ${getRequestStatusTone(request.status)}`}>
                {formatRequestStatus(request.status)}
              </span>
              {request.status === 'Pending' && !showApproveForm && !showRejectForm && (
                <div style={{ display: 'flex', gap: '8px' }}>
                  <Button variant="danger" onClick={() => setShowRejectForm(true)}>Từ chối</Button>
                  <Button variant="primary" onClick={() => setShowApproveForm(true)} disabled={!canApproveByStartDate}>Duyệt yêu cầu</Button>
                </div>
              )}
            </div>
          </section>

          <div className="landlord-request-detail-container">
            <div className="request-detail-body">
              {request.rejectedReason && (
                <div style={{ marginBottom: '20px' }}>
                  <Alert type="error">
                    <strong>Lý do:</strong> {request.rejectedReason}
                  </Alert>
                </div>
              )}
              <RequestInfoBlock request={request} />





              <DepositInfoBlock deposit={request.deposit} />

              <ContractProgressBlock
                contract={request.contract}
                deposit={request.deposit}
                onEditTerms={(contractId) => setTermsContractId(contractId)}
                onPreview={(contractId) => setPreviewContractId(contractId)}
              />
            </div>
          </div>
        </div>
      </main>
      {showApproveForm && (
        <div className="approve-modal-overlay" style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div className="approve-modal-content" style={{ backgroundColor: 'white', padding: '24px', borderRadius: '8px', width: '500px', maxWidth: '95%', boxShadow: '0 4px 6px rgba(0,0,0,0.1)' }}>
            <h3 style={{ marginTop: 0, marginBottom: '16px', fontSize: '1.25rem' }}>Xác nhận duyệt yêu cầu?</h3>
            <p style={{ color: '#475569', fontSize: '1rem', marginBottom: '24px', lineHeight: '1.5' }}>
              Hệ thống sẽ tạo yêu cầu cọc với thời hạn thanh toán 24 giờ kể từ thời điểm này. Bạn có chắc chắn muốn duyệt?
            </p>
            {!canApproveByStartDate && (
              <Alert type="error">
                Ngày bắt đầu thuê phải còn cách hôm nay ít nhất 2 ngày để hai bên có thời gian hoàn tất hợp đồng.
              </Alert>
            )}
            <div className="form-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
              <Button variant="outline" onClick={() => setShowApproveForm(false)}>Hủy</Button>
              <Button variant="primary" onClick={handleApproveConfirm} disabled={!canApproveByStartDate}>Đồng ý duyệt</Button>
            </div>
          </div>
        </div>
      )}

      {showRejectForm && (
        <div className="reject-modal-overlay" style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div className="reject-modal-content" style={{ backgroundColor: 'white', padding: '24px', borderRadius: '8px', width: '550px', maxWidth: '95%', boxShadow: '0 4px 6px rgba(0,0,0,0.1)' }}>
            <h3 style={{ marginTop: 0, marginBottom: '16px', fontSize: '1.25rem' }}>Từ chối yêu cầu thuê</h3>
            <div className="form-group" style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, color: '#374151' }}>Lý do từ chối:</label>
              <textarea
                rows={5}
                value={rejectReason}
                onChange={(event) => setRejectReason(event.target.value)}
                placeholder="Nhập lý do để khách thuê biết..."
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #d1d5db', fontSize: '0.95rem' }}
              />
            </div>
            <div className="form-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
              <Button variant="outline" onClick={() => setShowRejectForm(false)}>Hủy</Button>
              <Button variant="danger" onClick={handleRejectConfirm}>Xác nhận từ chối</Button>
            </div>
          </div>
        </div>
      )}

      {previewContractId && (
        <ContractPreviewModal
          contractId={previewContractId}
          role="landlord"
          onClose={() => setPreviewContractId(null)}
          onSuccess={() => {
            setPreviewContractId(null);
            window.location.reload();
          }}
        />
      )}

      {termsContractId && (
        <ContractTermsSetupModal
          rentalRequestId={request.id}
          contractId={termsContractId === 'new' ? undefined : termsContractId}
          onClose={() => setTermsContractId(null)}
          onSuccess={() => {
            setTermsContractId(null);
            window.location.reload();
          }}
        />
      )}

      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
}

function LandlordSidebar({ navigate }: { navigate: ReturnType<typeof useNavigate> }) {
  return (
    <aside className="dashboard-sidebar">
      <h1>Chủ trọ</h1>
      <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}>
        Quản lý khu trọ
      </button>
      <button className="sidebar-item active" onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}>
        Yêu cầu thuê
      </button>
      <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS)}>
        Lịch hẹn xem phòng
      </button>
      <button className="sidebar-item" disabled style={{ opacity: 0.6, cursor: 'not-allowed' }}>
        Quản lý doanh thu (sau này)
      </button>
      <button className="sidebar-item sidebar-back-btn" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
        Quay lại trang chủ
      </button>
    </aside>
  );
}

function RequestInfoBlock({ request }: { request: RentalRequestResponse }) {
  return (
    <div className="request-info-card">
      <h3>Thông tin yêu cầu</h3>
      <div className="info-box">
        <div className="request-info-grid">
          <InfoItem label="Ngày gửi" value={formatDate(request.createdAt)} />
          <InfoItem label="Số người ở dự kiến" value={`${request.expectedOccupantCount} người`} />
          <InfoItem label="Giá thuê dự kiến" value={`${formatCurrency(request.monthlyRentSnapshot)} đ/tháng`} />
          <InfoItem
            label="Thời hạn thuê dự kiến"
            value={`${formatDate(request.desiredStartDate)} - ${formatDate(request.expectedEndDate)}`}
          />
          <InfoItem label="Ghi chú" value={request.tenantNote || 'Không có'} />
        </div>
      </div>
    </div>
  );
}

function DepositInfoBlock({ deposit }: { deposit?: RoomDepositResponse | null }) {
  const tone = getDepositTone(deposit?.status);

  return (
    <div className="request-info-card">
      <h3>Thông tin đặt cọc</h3>
      <div className={`deposit-box status-${tone}`}>
        <div className="request-info-grid">
          <InfoItem label="Trạng thái cọc" value={formatDepositStatus(deposit?.status)} />
          <InfoItem label="Số tiền" value={deposit ? `${formatCurrency(deposit.depositAmount)} đ` : 'Chưa xác định'} />
          {deposit?.status === 'PendingPayment' && deposit.paymentDeadlineAt && (
            <InfoItem label="Thời hạn cọc" value={formatDateTime(deposit.paymentDeadlineAt)} />
          )}
          {deposit?.status === 'Paid' && deposit.paidAt && (
            <InfoItem label="Đặt cọc lúc" value={formatDateTime(deposit.paidAt)} />
          )}
          {deposit?.status === 'Refunded' && deposit.refundedAt && (
            <InfoItem label="Hoàn cọc lúc" value={formatDateTime(deposit.refundedAt)} />
          )}
        </div>
      </div>
    </div>
  );
}

function ContractProgressBlock({
  contract,
  onEditTerms,
  onPreview
}: {
  contract?: ContractBriefResponse | null;
  deposit?: RoomDepositResponse | null;
  onEditTerms: (contractId: string) => void;
  onPreview: (contractId: string) => void;
}) {
  const tone = getContractTone(contract?.status);
  const canEditTerms = contract?.status === 'TenantRevisionRequested';
  const canSignByStartDate = !contract?.startDate || contract.startDate >= addDays(2);
  const canSignContract = contract?.status === 'PendingLandlordSignature' && canSignByStartDate;

  return (
    <div className="request-info-card">
      <h3>Tiến độ hợp đồng</h3>
      <div className={`contract-box status-${tone}`}>
        <div className="request-info-grid">
          <InfoItem label="Trạng thái" value={formatContractStatus(contract?.status)} />
          {contract?.status === 'PendingTenantSignature' && contract.signatureDeadlineAt && (
            <InfoItem label="Thời hạn ký" value={formatDateTime(contract.signatureDeadlineAt)} />
          )}
          {contract?.statusReason && isContractRevisionStatus(contract.status) && (
            <InfoItem label="Nội dung yêu cầu sửa" value={contract.statusReason} />
          )}
          {contract?.statusReason && contract.status === 'Rejected' && (
            <InfoItem label="Lý do" value={contract.statusReason} />
          )}
        </div>

        {contract?.status === 'PendingLandlordSignature' && !canSignByStartDate && (
          <Alert type="error">
            Ngày bắt đầu hợp đồng phải còn cách hôm nay ít nhất 2 ngày để người thuê có thời gian ký hợp đồng.
          </Alert>
        )}

        {contract && (canEditTerms || canSignContract) && (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 0 }}>
            {contract.status === 'TenantRevisionRequested' && (
              <Button variant="primary" onClick={() => onEditTerms(contract.id)}>
                Sửa điều khoản
              </Button>
            )}
            {contract.status === 'PendingLandlordSignature' && (
              <Button variant="primary" onClick={() => onPreview(contract.id)}>
                Xem và ký hợp đồng
              </Button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function InfoItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="info-item">
      <span className="label">{label}</span>
      <span className="value">{value}</span>
    </div>
  );
}

function formatRequestStatus(status: string) {
  switch (status) {
    case 'Pending': return 'Chờ duyệt';
    case 'Accepted': return 'Đã duyệt';
    case 'Rejected': return 'Từ chối';
    case 'Cancelled': return 'Đã hủy';
    case 'Expired': return 'Đã quá hạn';
    default: return status;
  }
}

function getRequestStatusTone(status: string) {
  if (status === 'Accepted') return 'success';
  if (status === 'Pending') return 'pending';
  if (['Rejected', 'Cancelled', 'Expired'].includes(status)) return 'danger';
  return 'neutral';
}

function formatDepositStatus(status?: string | null) {
  switch (status) {
    case undefined:
    case null:
      return 'Chưa tạo';
    case 'PendingPayment': return 'Chờ thanh toán';
    case 'Paid': return 'Đã thanh toán';
    case 'Refunded': return 'Đã hoàn cọc';
    case 'Forfeited': return 'Mất cọc';
    case 'Expired': return 'Đã quá hạn';
    case 'Cancelled': return 'Đã hủy';
    default: return status;
  }
}

function getDepositTone(status?: string | null) {
  if (!status) return 'neutral';
  if (['Paid', 'Refunded', 'Forfeited'].includes(status)) return 'success';
  if (status === 'PendingPayment') return 'warning';
  if (['Rejected', 'Cancelled', 'Expired'].includes(status)) return 'danger';
  return 'neutral';
}

function formatContractStatus(status?: string | null) {
  switch (status) {
    case undefined:
    case null:
      return 'Chưa tạo';
    case 'WaitingTenantOccupants':
      return 'Chờ khách nhập thông tin người ở';
    case 'PendingLandlordSignature':
      return 'Chờ bạn ký';
    case 'PendingTenantSignature':
      return 'Chờ khách ký';
    case 'LandlordRevisionRequested':
      return 'Đang chờ khách sửa';
    case 'TenantRevisionRequested':
      return 'Khách yêu cầu sửa đổi';
    case 'Active':
      return 'Đang hiệu lực';
    case 'Expired':
      return 'Đã hết hạn';
    case 'Rejected':
      return 'Từ chối';
    case 'Cancelled':
      return 'Đã hủy';
    default:
      return status;
  }
}

function getContractTone(status?: string | null) {
  if (!status) return 'neutral';
  if (['Active', 'Expired'].includes(status)) return 'success';
  if ([
    'WaitingTenantOccupants',
    'PendingLandlordSignature',
    'PendingTenantSignature',
    'LandlordRevisionRequested',
    'TenantRevisionRequested'
  ].includes(status)) return 'warning';
  if (['Rejected', 'Cancelled'].includes(status)) return 'danger';
  return 'neutral';
}

function isContractRevisionStatus(status: string) {
  return ['LandlordRevisionRequested', 'TenantRevisionRequested'].includes(status);
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('vi-VN');
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString('vi-VN');
}

function formatCurrency(value: number) {
  return value.toLocaleString('vi-VN');
}
