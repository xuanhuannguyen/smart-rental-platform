import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import type { ContractBriefResponse } from '../../contracts/types';
import { ContractOccupantsSetupModal } from '../../rental-contracts/components/ContractOccupantsSetupModal';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import { rentalRequestApi } from '../api';
import type { RentalRequestResponse, RoomDepositResponse } from '../types';
import './TenantRentalRequestDetailPage.css';

export const TenantRentalRequestDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [request, setRequest] = useState<RentalRequestResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedContractIdForSetup, setSelectedContractIdForSetup] = useState<string | null>(null);
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    async function fetchRequest() {
      setLoading(true);
      try {
        const response = await rentalRequestApi.getMyRentalRequests();
        const found = response.data.find((item) => item.id === id);
        setRequest(found ?? null);
        setError(found ? null : 'Không tìm thấy yêu cầu thuê phòng này.');
      } catch {
        setError('Lỗi khi tải thông tin yêu cầu.');
      } finally {
        setLoading(false);
      }
    }

    void fetchRequest();
  }, [id]);

  const handleCancelConfirm = async () => {
    if (!request) return;

    try {
      const response = await rentalRequestApi.cancelRentalRequest(request.id);
      setRequest(response.data);
      setShowCancelModal(false);
      setToast({ message: 'Đã hủy yêu cầu thuê phòng.', type: 'success' });
    } catch {
      setToast({ message: 'Không thể hủy yêu cầu. Vui lòng thử lại sau.', type: 'error' });
      setShowCancelModal(false);
    }
  };

  const handlePayDeposit = async () => {
    if (!request?.deposit) return;

    try {
      const response = await rentalRequestApi.markDepositPaid(request.deposit.id);
      setRequest({
        ...request,
        deposit: response.data
      });
      setToast({ message: 'Đã thanh toán cọc thành công (mock).', type: 'success' });
    } catch {
      setToast({ message: 'Không thể thanh toán cọc.', type: 'error' });
    }
  };

  if (loading) return <div>Đang tải dữ liệu...</div>;
  if (error || !request) return <div><Alert type="error">{error || 'Có lỗi xảy ra.'}</Alert></div>;

  return (
    <div className="tenant-request-detail-page">
      <section className="overview-band">
        <div className="overview-header-title-area">
          <button
            type="button"
            className="back-icon-btn"
            onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS)}
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
              Thời gian gửi: {formatDateTime(request.createdAt)}
            </p>
          </div>
        </div>

        <div className="overview-right" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '12px' }}>
          <span className={`status-badge ${getRequestStatusTone(request.status)}`}>
            {formatRequestStatus(request.status)}
          </span>
          {request.status === 'Pending' && (
            <div style={{ display: 'flex', gap: '8px' }}>
              <Button variant="outline" onClick={() => setShowCancelModal(true)}>Hủy yêu cầu</Button>
            </div>
          )}
        </div>
      </section>

      <div className="tenant-request-detail-container">
        <div className="request-detail-body">
          {request.rejectedReason && (
            <div style={{ marginBottom: '20px' }}>
              <Alert type="error">
                <strong>Lý do:</strong> {request.rejectedReason}
              </Alert>
            </div>
          )}
          <RequestInfoBlock request={request} />

          <DepositInfoBlock
            deposit={request.deposit}
            onPay={request.deposit?.status === 'PendingPayment' ? handlePayDeposit : undefined}
          />

          <ContractProgressBlock
            contract={request.contract}
            actor="tenant"
            onSetupOccupants={(contractId) => setSelectedContractIdForSetup(contractId)}
            onPreview={(contractId) => setPreviewContractId(contractId)}
          />
        </div>

      </div>

      {selectedContractIdForSetup && (
        <ContractOccupantsSetupModal
          contractId={selectedContractIdForSetup}
          onClose={() => setSelectedContractIdForSetup(null)}
          onSuccess={() => {
            setSelectedContractIdForSetup(null);
            window.location.reload();
          }}
        />
      )}

      {previewContractId && (
        <ContractPreviewModal
          contractId={previewContractId}
          role="tenant"
          onClose={() => setPreviewContractId(null)}
          onSuccess={() => {
            setPreviewContractId(null);
            window.location.reload();
          }}
        />
      )}

      {showCancelModal && (
        <div className="approve-modal-overlay" style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div className="approve-modal-content" style={{ backgroundColor: 'white', padding: '24px', borderRadius: '8px', width: '500px', maxWidth: '95%', boxShadow: '0 4px 6px rgba(0,0,0,0.1)' }}>
            <h3 style={{ marginTop: 0, marginBottom: '16px', fontSize: '1.25rem' }}>Xác nhận hủy yêu cầu?</h3>
            <p style={{ color: '#475569', fontSize: '1rem', marginBottom: '24px', lineHeight: '1.5' }}>
              Bạn có chắc chắn muốn hủy yêu cầu thuê phòng này không? Hành động này không thể hoàn tác.
            </p>
            <div className="form-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
              <Button variant="outline" onClick={() => setShowCancelModal(false)}>Quay lại</Button>
              <Button variant="danger" onClick={handleCancelConfirm}>Đồng ý hủy</Button>
            </div>
          </div>
        </div>
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
};

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

function DepositInfoBlock({ deposit, onPay }: { deposit?: RoomDepositResponse | null; onPay?: () => void }) {
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

        {onPay && (
          <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
            <Button onClick={onPay}>Thanh toán ngay</Button>
          </div>
        )}
      </div>
    </div>
  );
}

function ContractProgressBlock({
  contract,
  actor,
  onSetupOccupants,
  onPreview
}: {
  contract?: ContractBriefResponse | null;
  actor: 'tenant' | 'landlord';
  onSetupOccupants?: (contractId: string) => void;
  onPreview?: (contractId: string) => void;
}) {
  const tone = getContractTone(contract?.status);
  const canSetupOccupants = contract?.status === 'WaitingTenantOccupants';
  const canEditOccupants = contract?.status === 'LandlordRevisionRequested';
  const canSignContract = contract?.status === 'PendingTenantSignature';

  return (
    <div className="request-info-card">
      <h3>Tiến độ hợp đồng</h3>
      <div className={`contract-box status-${tone}`}>
        <div className="request-info-grid">
          <InfoItem label="Trạng thái" value={formatContractStatus(contract?.status, actor)} />
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

        {contract && (canSetupOccupants || canEditOccupants || canSignContract) && (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {contract.status === 'WaitingTenantOccupants' && onSetupOccupants && (
              <Button onClick={() => onSetupOccupants(contract.id)}>Nhập thông tin người ở</Button>
            )}
            {contract.status === 'LandlordRevisionRequested' && onSetupOccupants && (
              <Button onClick={() => onSetupOccupants(contract.id)}>Chỉnh sửa thông tin</Button>
            )}
            {contract.status === 'PendingTenantSignature' && onPreview && (
              <Button onClick={() => onPreview(contract.id)}>Xem và ký hợp đồng</Button>
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
    case 'Accepted': return 'Chấp nhận';
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

function formatContractStatus(status: string | undefined | null, actor: 'tenant' | 'landlord') {
  switch (status) {
    case undefined:
    case null:
      return 'Chưa tạo';
    case 'WaitingTenantOccupants':
      return actor === 'tenant' ? 'Chờ bạn nhập thông tin người ở' : 'Chờ khách nhập thông tin người ở';
    case 'PendingLandlordSignature':
      return actor === 'tenant' ? 'Chờ chủ trọ ký' : 'Chờ bạn ký';
    case 'PendingTenantSignature':
      return actor === 'tenant' ? 'Chờ bạn ký' : 'Chờ khách ký';
    case 'LandlordRevisionRequested':
      return actor === 'tenant' ? 'Chủ trọ yêu cầu sửa đổi' : 'Đang chờ khách sửa';
    case 'TenantRevisionRequested':
      return actor === 'tenant' ? 'Đang chờ chủ trọ sửa' : 'Khách yêu cầu sửa đổi';
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
