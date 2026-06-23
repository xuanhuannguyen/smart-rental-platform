import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import type { ContractBriefResponse } from '../../contracts/types';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import { ContractTermsSetupModal } from '../../rental-contracts/components/ContractTermsSetupModal';
import { rentalRequestApi } from '../../rental-requests/api';
import type { RentalRequestResponse, RoomDepositResponse } from '../../rental-requests/types';
import '../../rental-requests/pages/RentalRequestDetail.shared.css';
import './LandlordRentalRequestDetailPage.css';

function toDateInput(date: Date) { return date.toISOString().slice(0, 10); }
function addDays(days: number) {
  const d = new Date(); d.setDate(d.getDate() + days); return toDateInput(d);
}

export function LandlordRentalRequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [request, setRequest] = useState<RentalRequestResponse | null>(null);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState('');
  const [showApproveModal, setShowApproveModal] = useState(false);
  const [showRejectModal, setShowRejectModal]   = useState(false);
  const [rejectReason, setRejectReason]         = useState('');
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);
  const [termsContractId, setTermsContractId]     = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const minimumApproveStartDate = addDays(2);
  const canApproveByStartDate   = !request || request.desiredStartDate >= minimumApproveStartDate;

  useEffect(() => {
    async function fetch() {
      try {
        const res  = await rentalRequestApi.getIncomingRentalRequests();
        const found = res.data.find(item => item.id === id);
        setRequest(found ?? null);
        setError(found ? '' : 'Không tìm thấy yêu cầu thuê phòng này.');
      } catch { setError('Lỗi khi tải thông tin yêu cầu.'); }
      finally  { setLoading(false); }
    }
    if (id) void fetch();
  }, [id]);

  const handleApproveConfirm = async () => {
    if (!request) return;
    try {
      const deadline = new Date(); deadline.setHours(deadline.getHours() + 24);
      const res = await rentalRequestApi.approveRentalRequest(request.id, { paymentDeadlineAt: deadline.toISOString() });
      setRequest(res.data); setShowApproveModal(false);
      setToast({ message: 'Đã duyệt yêu cầu thành công.', type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể duyệt yêu cầu thuê.'), type: 'error' });
    }
  };

  const handleRejectConfirm = async () => {
    if (!request) return;
    if (!rejectReason.trim()) { setToast({ message: 'Vui lòng nhập lý do từ chối.', type: 'error' }); return; }
    try {
      const res = await rentalRequestApi.rejectRentalRequest(request.id, { rejectedReason: rejectReason.trim() });
      setRequest(res.data); setShowRejectModal(false);
      setToast({ message: 'Đã từ chối yêu cầu.', type: 'success' });
    } catch { setToast({ message: 'Không thể từ chối yêu cầu thuê.', type: 'error' }); }
  };

  if (loading) return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        <div className="rd-page">
          <div style={{ padding: '60px 0', textAlign: 'center', color: '#94a3b8' }}>Đang tải yêu cầu thuê...</div>
        </div>
      </main>
    </div>
  );

  if (error || !request) return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main"><Alert type="error">{error || 'Có lỗi xảy ra.'}</Alert></main>
    </div>
  );

  const statusBadgeCls = getRequestStatusBadge(request.status);

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        <div className="rd-page">

          {/* ── Hero Header ── */}
          <div className="rd-hero">
            <div className="rd-hero__left">
              <button
                type="button"
                className="rd-hero__back"
                onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}
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
                  <button className="rd-btn rd-btn--danger" onClick={() => setShowRejectModal(true)}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                    </svg>
                    Từ chối
                  </button>
                  <button
                    className="rd-btn rd-btn--primary"
                    onClick={() => setShowApproveModal(true)}
                    disabled={!canApproveByStartDate}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="20 6 9 17 4 12"/>
                    </svg>
                    Duyệt yêu cầu
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
          <LandlordDepositSection deposit={request.deposit} />

          {/* ── Contract Progress ── */}
          <LandlordContractSection
            contract={request.contract}
            deposit={request.deposit}
            onEditTerms={cid => setTermsContractId(cid)}
            onPreview={cid => setPreviewContractId(cid)}
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

        </div>
      </main>

      {/* ── Approve Modal ── */}
      {showApproveModal && (
        <div className="rd-modal-overlay">
          <div className="rd-modal">
            <h3 className="rd-modal__title">Xác nhận duyệt yêu cầu?</h3>
            <p className="rd-modal__sub">
              Hệ thống sẽ tạo yêu cầu cọc với thời hạn 24 giờ kể từ thời điểm này. Bạn có chắc chắn muốn duyệt?
            </p>
            {!canApproveByStartDate && (
              <Alert type="error">Ngày bắt đầu thuê phải còn cách hôm nay ít nhất 2 ngày.</Alert>
            )}
            <div className="rd-modal__actions">
              <button className="rd-btn rd-btn--outline" onClick={() => setShowApproveModal(false)}>Hủy</button>
              <button className="rd-btn rd-btn--primary" onClick={handleApproveConfirm} disabled={!canApproveByStartDate}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
                Đồng ý duyệt
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Reject Modal ── */}
      {showRejectModal && (
        <div className="rd-modal-overlay">
          <div className="rd-modal">
            <h3 className="rd-modal__title">Từ chối yêu cầu thuê</h3>
            <p className="rd-modal__sub">Nhập lý do để khách thuê được biết nguyên nhân từ chối.</p>
            <label className="rd-modal__label">Lý do từ chối:</label>
            <textarea
              className="rd-modal__textarea"
              rows={4}
              value={rejectReason}
              onChange={e => setRejectReason(e.target.value)}
              placeholder="Nhập lý do để khách thuê biết..."
            />
            <div className="rd-modal__actions">
              <button className="rd-btn rd-btn--outline" onClick={() => setShowRejectModal(false)}>Hủy</button>
              <button className="rd-btn rd-btn--danger" onClick={handleRejectConfirm}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
                Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}

      {previewContractId && (
        <ContractPreviewModal
          contractId={previewContractId}
          role="landlord"
          onClose={() => setPreviewContractId(null)}
          onSuccess={() => { setPreviewContractId(null); window.location.reload(); }}
        />
      )}

      {termsContractId && (
        <ContractTermsSetupModal
          rentalRequestId={request.id}
          contractId={termsContractId === 'new' ? undefined : termsContractId}
          onClose={() => setTermsContractId(null)}
          onSuccess={() => { setTermsContractId(null); window.location.reload(); }}
        />
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}

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

function LandlordDepositSection({ deposit }: { deposit?: RoomDepositResponse | null }) {
  const boxCls = getDepositBoxCls(deposit?.status);
  const iconCls = deposit?.status === 'PendingPayment' ? 'rd-deposit-icon--amber'
    : ['Paid', 'Refunded'].includes(deposit?.status ?? '') ? 'rd-deposit-icon--green'
    : 'rd-deposit-icon--slate';

  const title   = formatDepositStatus(deposit?.status);
  const subtitle = !deposit ? 'Chưa có yêu cầu cọc.'
    : deposit.status === 'PendingPayment' ? 'Đang chờ khách thanh toán cọc để giữ phòng.'
    : deposit.status === 'Paid'          ? 'Khách đã thanh toán cọc thành công.'
    : deposit.status === 'Refunded'      ? 'Tiền cọc đã được hoàn trả cho khách.'
    : deposit.status === 'Forfeited'     ? 'Tiền cọc đã bị mất do khách hủy.'
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
        </div>
      </div>
    </div>
  );
}

const CONTRACT_STEPS = [
  { key: 'created',    label: 'Chờ tạo hợp đồng',  sub: 'Đang chờ chủ trọ tạo hợp đồng' },
  { key: 'sent',       label: 'Hợp đồng đã gửi',   sub: 'Chủ trọ đã gửi hợp đồng' },
  { key: 'signing',   label: 'Chờ ký hợp đồng',   sub: 'Khách thuê ký hợp đồng' },
  { key: 'completed', label: 'Hoàn tất',            sub: 'Hợp đồng đã được ký' },
];

function getContractStep(status?: string | null): number {
  if (!status) return 0;
  if (['WaitingTenantOccupants', 'LandlordRevisionRequested', 'TenantRevisionRequested', 'PendingLandlordSignature'].includes(status)) return 1;
  if (status === 'PendingTenantSignature') return 2;
  if (['Active', 'Expired', 'Cancelled'].includes(status)) return 3;
  return 0;
}

function LandlordContractSection({
  contract, deposit, onEditTerms, onPreview
}: {
  contract?: ContractBriefResponse | null;
  deposit?: RoomDepositResponse | null;
  onEditTerms: (id: string) => void;
  onPreview: (id: string) => void;
}) {
  const step = getContractStep(contract?.status);
  const canEditTerms   = contract?.status === 'TenantRevisionRequested';
  const canSignByDate  = !contract?.startDate || contract.startDate >= addDays(2);
  const canSignContract = contract?.status === 'PendingLandlordSignature' && canSignByDate;

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

        {contract?.status === 'PendingLandlordSignature' && !canSignByDate && (
          <div style={{ marginTop: 16 }}>
            <Alert type="error">Ngày bắt đầu hợp đồng phải còn cách hôm nay ít nhất 2 ngày.</Alert>
          </div>
        )}

        {contract && (canEditTerms || canSignContract) && (
          <div style={{ display: 'flex', gap: 10, marginTop: 20 }}>
            {canEditTerms && (
              <button className="rd-btn rd-btn--primary" onClick={() => onEditTerms(contract.id)}>
                Sửa điều khoản
              </button>
            )}
            {canSignContract && (
              <button className="rd-btn rd-btn--primary" onClick={() => onPreview(contract.id)}>
                Xem và ký hợp đồng
              </button>
            )}
          </div>
        )}
        {deposit?.status === 'Paid' && !contract && (
          <div style={{ marginTop: 16 }}>
            <button className="rd-btn rd-btn--primary" onClick={() => onEditTerms('new')}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
              Tạo hợp đồng ngay
            </button>
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
