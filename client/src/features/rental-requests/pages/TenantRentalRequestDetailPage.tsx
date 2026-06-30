import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import type { ContractBriefResponse } from '../../contracts/types';
import { ContractOccupantsSetupModal } from '../../rental-contracts/components/ContractOccupantsSetupModal';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import { WalletPaymentConfirmModal } from '../../wallet/components/WalletPaymentConfirmModal';
import { rentalRequestApi } from '../api';
import type { RentalRequestResponse, RoomDepositResponse } from '../types';
import '../../rental-requests/pages/RentalRequestDetail.shared.css';
import './TenantRentalRequestDetailPage.css';

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

export const TenantRentalRequestDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [request, setRequest] = useState<RentalRequestResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedContractIdForSetup, setSelectedContractIdForSetup] = useState<string | null>(null);
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [showDepositPaymentConfirm, setShowDepositPaymentConfirm] = useState(false);
  const [isPayingDeposit, setIsPayingDeposit] = useState(false);
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
    if (!request?.deposit || isPayingDeposit) return;

    setIsPayingDeposit(true);
    try {
      const paymentResponse = await rentalRequestApi.payDeposit(request.deposit.id);
      const requestsResponse = await rentalRequestApi.getMyRentalRequests();
      const refreshedRequest = requestsResponse.data.find((item) => item.id === request.id);

      setRequest(refreshedRequest ?? {
        ...request,
        deposit: paymentResponse.data
      });
      setToast({ message: 'Đã thanh toán cọc thành công.', type: 'success' });
    } catch (err) {
      setToast({
        message: getApiErrorMessage(err, 'Không thể thanh toán cọc. Vui lòng thử lại.'),
        type: 'error'
      });
    } finally {
      setIsPayingDeposit(false);
      setShowDepositPaymentConfirm(false);
    }
  };

  if (loading) return (
    <div className="rd-page">
      <div style={{ padding: '60px 0', textAlign: 'center', color: '#94a3b8' }}>Đang tải yêu cầu thuê...</div>
    </div>
  );

  if (error || !request) return (
    <div style={{ padding: '24px' }}><Alert type="error">{error || 'Có lỗi xảy ra.'}</Alert></div>
  );

  const statusBadgeCls = getRequestStatusBadge(request.status);

  return (
    <div className="rd-page">
      {/* ── Hero Header ── */}
      <div className="rd-hero">
        <div className="rd-hero__left">
          <button
            type="button"
            className="rd-hero__back"
            onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS)}
            title="Quay về danh sách"
          >
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/>
            </svg>
          </button>
          <div className="rd-hero__icon">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="7" width="20" height="15" rx="2"/>
              <path d="M16 21V7a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v14"/>
            </svg>
          </div>
          <div className="rd-hero__texts">
            <p className="rd-hero__house">{request.roomingHouseName}</p>
            <h2 className="rd-hero__room">Phòng {request.roomNumber}</h2>
            <p className="rd-hero__meta">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
              </svg>
              Thời gian gửi: {formatDateTime(request.createdAt)}
            </p>
          </div>
        </div>
        <div className="rd-hero__right">
          <span className={`rd-badge rd-badge--${statusBadgeCls}`}>
            {formatRequestStatus(request.status)}
          </span>
          {request.status === 'Pending' && (
            <div className="rd-hero__actions">
              <button className="rd-btn rd-btn--danger" onClick={() => setShowCancelModal(true)}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
                Hủy yêu cầu
              </button>
            </div>
          )}
        </div>
      </div>

      {/* ── Rejected Reason ── */}
      {request.rejectedReason && (
        <div className="rd-rejected-alert">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          <span><strong>Lý do từ chối:</strong> {request.rejectedReason}</span>
        </div>
      )}

      {/* ── Request Info ── */}
      <div className="rd-section">
        <div className="rd-section__heading">
          <span className="rd-section__heading-icon rd-section__heading-icon--blue">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
              <polyline points="14 2 14 8 20 8"/>
              <line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>
            </svg>
          </span>
          <h3 className="rd-section__title">Thông tin yêu cầu</h3>
        </div>
        <div className="rd-section__body">
          <div className="rd-stats-grid">
            <StatBox icon="calendar" tone="blue" label="Ngày gửi" value={formatDate(request.createdAt)} />
            <StatBox icon="users"    tone="green" label="Số người ở dự kiến" value={`${request.expectedOccupantCount} người`} />
            <StatBox icon="coin"     tone="amber" label="Giá thuê dự kiến" value={`${formatCurrency(request.monthlyRentSnapshot)} đ/tháng`} />
            <StatBox icon="date-range" tone="violet" label="Thời hạn thuê dự kiến" value={`${formatDate(request.desiredStartDate)} - ${formatDate(request.expectedEndDate)}`} />
            <StatBox icon="note"     tone="slate" label="Ghi chú" value={request.tenantNote || 'Không có'} />
          </div>
        </div>
      </div>

      {/* ── Deposit Info ── */}
      <TenantDepositSection
        deposit={request.deposit}
        onPay={request.deposit?.status === 'PendingPayment' ? () => setShowDepositPaymentConfirm(true) : undefined}
        isPaying={isPayingDeposit}
      />

      {/* ── Contract Progress ── */}
      <TenantContractSection
        contract={request.contract}
        onSetupOccupants={(contractId) => setSelectedContractIdForSetup(contractId)}
        onPreview={(contractId) => setPreviewContractId(contractId)}
      />

      {/* ── Supplementary Notes ── */}
      <div className="rd-section">
        <div className="rd-section__heading">
          <span className="rd-section__heading-icon rd-section__heading-icon--slate">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
            </svg>
          </span>
          <h3 className="rd-section__title">Thông tin bổ sung</h3>
        </div>
        <div className="rd-section__body">
          <div className="rd-notes-grid">
            <div className="rd-note-box">
              <p className="rd-note-box__label">Mô tả thêm từ khách thuê</p>
              <p className={`rd-note-box__text ${!request.tenantNote ? 'rd-note-box__text--empty' : ''}`}>
                {request.tenantNote || 'Không có ghi chú'}
              </p>
            </div>
            <div className="rd-note-box">
              <p className="rd-note-box__label">Lý do từ chối (nếu có)</p>
              <p className={`rd-note-box__text ${!request.rejectedReason ? 'rd-note-box__text--empty' : ''}`}>
                {request.rejectedReason || 'Không có'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ── Cancel Modal ── */}
      {showCancelModal && (
        <div className="rd-modal-overlay">
          <div className="rd-modal">
            <h3 className="rd-modal__title">Hủy yêu cầu thuê phòng?</h3>
            <p className="rd-modal__sub">
              Bạn có chắc chắn muốn hủy yêu cầu thuê phòng này không? Hành động này không thể hoàn tác.
            </p>
            <div className="rd-modal__actions">
              <button className="rd-btn rd-btn--outline" onClick={() => setShowCancelModal(false)}>Quay lại</button>
              <button className="rd-btn rd-btn--danger" onClick={handleCancelConfirm}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
                Xác nhận hủy
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Occupants Setup Modal ── */}
      {selectedContractIdForSetup && (
        <ContractOccupantsSetupModal
          contractId={selectedContractIdForSetup}
          expectedOccupantCount={request.expectedOccupantCount}
          onClose={() => setSelectedContractIdForSetup(null)}
          onSuccess={() => {
            setSelectedContractIdForSetup(null);
            window.location.reload();
          }}
        />
      )}

      {/* ── Contract Preview Modal ── */}
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

      {/* ── Wallet Payment Modal ── */}
      <WalletPaymentConfirmModal
        isOpen={showDepositPaymentConfirm}
        title="Xác nhận thanh toán tiền cọc"
        description={request.deposit ? `Thanh toán tiền cọc phòng ${request.roomNumber}` : undefined}
        amount={request.deposit?.depositAmount ?? 0}
        confirmLabel="Thanh toán cọc"
        isSubmitting={isPayingDeposit}
        onConfirm={handlePayDeposit}
        onClose={() => setShowDepositPaymentConfirm(false)}
      />

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
};

/* ─── Sub-components ─── */

function StatBox({ icon, tone, label, value }: { icon: string; tone: string; label: string; value: string }) {
  const icons: Record<string, any> = {
    calendar: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/>
        <line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
      </svg>
    ),
    users: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/>
        <path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>
      </svg>
    ),
    coin: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10"/>
        <line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/>
      </svg>
    ),
    'date-range': (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/>
        <line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
      </svg>
    ),
    note: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
      </svg>
    ),
  };
  return (
    <div className="rd-stat">
      <div className={`rd-stat__icon rd-stat__icon--${tone}`}>{icons[icon]}</div>
      <span className="rd-stat__label">{label}</span>
      <span className="rd-stat__value">{value}</span>
    </div>
  );
}

function TenantDepositSection({
  deposit,
  onPay,
  isPaying
}: {
  deposit?: RoomDepositResponse | null;
  onPay?: () => void;
  isPaying: boolean;
}) {
  const boxCls = getDepositBoxCls(deposit?.status);
  const iconCls = deposit?.status === 'PendingPayment' ? 'rd-deposit-icon--amber'
    : ['Paid', 'Refunded'].includes(deposit?.status ?? '') ? 'rd-deposit-icon--green'
    : 'rd-deposit-icon--slate';

  const title   = formatDepositStatus(deposit?.status);
  const subtitle = !deposit ? 'Chưa có yêu cầu cọc.'
    : deposit.status === 'PendingPayment' ? 'Đang chờ bạn thanh toán cọc để hoàn thành bước giữ phòng.'
    : deposit.status === 'Paid'          ? 'Bạn đã thanh toán cọc thành công.'
    : deposit.status === 'Refunded'      ? 'Tiền cọc đã được hoàn trả lại cho bạn.'
    : deposit.status === 'Forfeited'     ? 'Tiền cọc đã bị mất do hủy hợp đồng hoặc vi phạm.'
    : '';

  return (
    <div className="rd-section">
      <div className="rd-section__heading">
        <span className="rd-section__heading-icon rd-section__heading-icon--amber">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/>
          </svg>
        </span>
        <h3 className="rd-section__title">Thông tin đặt cọc</h3>
      </div>
      <div className="rd-section__body">
        <div className={`rd-deposit-box ${boxCls}`}>
          <div className="rd-deposit-header">
            <div className={`rd-deposit-icon ${iconCls}`}>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/>
              </svg>
            </div>
            <div>
              <p className="rd-deposit-title">{title}</p>
              <p className="rd-deposit-subtitle">{subtitle}</p>
            </div>
          </div>
          {deposit && (
            <div className="rd-deposit-stats">
              <div className="rd-deposit-stat">
                <span className="rd-deposit-stat__label">Số tiền cọc</span>
                <span className="rd-deposit-stat__value">{formatCurrency(deposit.depositAmount)} đ</span>
              </div>
              {deposit.paymentDeadlineAt && (
                <div className="rd-deposit-stat">
                  <span className="rd-deposit-stat__label">Hạn thanh toán</span>
                  <span className="rd-deposit-stat__value">{formatDateTime(deposit.paymentDeadlineAt)}</span>
                </div>
              )}
              {deposit.paidAt && (
                <div className="rd-deposit-stat">
                  <span className="rd-deposit-stat__label">Đặt cọc lúc</span>
                  <span className="rd-deposit-stat__value">{formatDateTime(deposit.paidAt)}</span>
                </div>
              )}
            </div>
          )}
          {onPay && (
            <div style={{ marginTop: 16 }}>
              <button className="rd-btn rd-btn--primary" onClick={onPay} disabled={isPaying}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/>
                </svg>
                Thanh toán cọc ngay
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

const CONTRACT_STEPS = [
  { key: 'created',    label: 'Chờ tạo hợp đồng',  sub: 'Đang chờ chủ trọ tạo hợp đồng' },
  { key: 'sent',       label: 'Hợp đồng đã gửi',   sub: 'Chủ trọ đã gửi hợp đồng' },
  { key: 'signing',   label: 'Chờ ký hợp đồng',   sub: 'Chờ bạn ký hợp đồng' },
  { key: 'completed', label: 'Hoàn tất',            sub: 'Hợp đồng đã được ký' },
];

function getContractStep(status?: string | null): number {
  if (!status) return 0;
  if (['WaitingTenantOccupants', 'LandlordRevisionRequested', 'TenantRevisionRequested', 'PendingLandlordSignature'].includes(status)) return 1;
  if (status === 'PendingTenantSignature') return 2;
  if (['Active', 'Expired', 'Cancelled'].includes(status)) return 3;
  return 0;
}

function TenantContractSection({
  contract, onSetupOccupants, onPreview
}: {
  contract?: ContractBriefResponse | null;
  onSetupOccupants?: (contractId: string) => void;
  onPreview?: (contractId: string) => void;
}) {
  const step = getContractStep(contract?.status);
  const canSetupOccupants = contract?.status === 'WaitingTenantOccupants';
  const canEditOccupants  = contract?.status === 'LandlordRevisionRequested';
  const isPastContractStartDate = Boolean(contract?.startDate && toDateInput(new Date()) > contract.startDate);
  const canSignContract = contract?.status === 'PendingTenantSignature' && !isPastContractStartDate;

  return (
    <div className="rd-section">
      <div className="rd-section__heading">
        <span className="rd-section__heading-icon rd-section__heading-icon--indigo">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
            <polyline points="14 2 14 8 20 8"/>
          </svg>
        </span>
        <h3 className="rd-section__title">Tiến độ hợp đồng</h3>
      </div>
      <div className="rd-section__body">
        <div className="rd-contract-steps">
          {CONTRACT_STEPS.map((s, i) => {
            const isDone   = i < step;
            const isActive = i === step;
            return (
              <div key={s.key} className={`rd-step ${isDone ? 'rd-step--done' : ''} ${isActive ? 'rd-step--active' : ''}`}>
                <div className="rd-step__circle">
                  {isDone ? (
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="20 6 9 17 4 12"/>
                    </svg>
                  ) : (
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                      <polyline points="14 2 14 8 20 8"/>
                    </svg>
                  )}
                </div>
                <span className="rd-step__label">{s.label}</span>
                <span className="rd-step__sub">{s.sub}</span>
              </div>
            );
          })}
        </div>

        {contract?.status === 'PendingTenantSignature' && isPastContractStartDate && (
          <div style={{ marginTop: 16 }}>
            <Alert type="error">Hợp đồng đã quá ngày bắt đầu thuê nên không thể ký.</Alert>
          </div>
        )}

        {contract && (canSetupOccupants || canEditOccupants || canSignContract) && (
          <div style={{ display: 'flex', gap: 10, marginTop: 20 }}>
            {canSetupOccupants && onSetupOccupants && (
              <button className="rd-btn rd-btn--primary" onClick={() => onSetupOccupants(contract.id)}>
                Nhập thông tin người ở
              </button>
            )}
            {canEditOccupants && onSetupOccupants && (
              <button className="rd-btn rd-btn--primary" onClick={() => onSetupOccupants(contract.id)}>
                Chỉnh sửa thông tin
              </button>
            )}
            {canSignContract && onPreview && (
              <button className="rd-btn rd-btn--primary" onClick={() => onPreview(contract.id)}>
                Xem và ký hợp đồng
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

/* ─── Helper formatters ─── */
function getRequestStatusBadge(status: string) {
  if (status === 'Pending')   return 'pending';
  if (status === 'Accepted')  return 'accepted';
  if (status === 'Rejected')  return 'rejected';
  if (status === 'Cancelled') return 'cancelled';
  return 'expired';
}

function formatRequestStatus(status: string) {
  const map: Record<string, string> = {
    Pending: 'Chờ duyệt', Accepted: 'Đã duyệt',
    Rejected: 'Từ chối', Cancelled: 'Đã hủy', Expired: 'Đã quá hạn'
  };
  return map[status] ?? status;
}

function formatDepositStatus(status?: string | null) {
  const map: Record<string, string> = {
    PendingPayment: 'Đang chờ thanh toán cọc', Paid: 'Đã thanh toán cọc',
    Refunded: 'Đã hoàn cọc', Forfeited: 'Đã mất cọc',
    Expired: 'Đã quá hạn', Cancelled: 'Đã hủy'
  };
  return status ? (map[status] ?? status) : 'Chưa có thông tin cọc';
}

function getDepositBoxCls(status?: string | null) {
  if (status === 'PendingPayment') return 'rd-deposit-box--pending';
  if (['Paid', 'Refunded'].includes(status ?? '')) return 'rd-deposit-box--paid';
  if (['Forfeited', 'Expired'].includes(status ?? '')) return 'rd-deposit-box--failed';
  return 'rd-deposit-box--neutral';
}

function formatDate(v: string)     { return new Date(v).toLocaleDateString('vi-VN'); }
function formatDateTime(v: string) { return new Date(v).toLocaleString('vi-VN'); }
function formatCurrency(v: number) { return v.toLocaleString('vi-VN'); }
