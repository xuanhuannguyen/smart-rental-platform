import { useEffect, useState } from 'react';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useNavigate } from 'react-router-dom';
import { rentalRequestApi } from '../api';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { Card, CardMetaRow, type CardAction, type CardStatusTone } from '../../../shared/components/ui/Card';
import { ContractOccupantsSetupModal } from '../../rental-contracts/components/ContractOccupantsSetupModal';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import { ConfirmModal } from '../../../shared/components/ui/ConfirmModal';
import './TenantRentalRequestsPage.css';

interface RoomDeposit {
  id: string;
  depositAmount: number;
  status: string;
  paymentDeadlineAt?: string | null;
  paidAt?: string | null;
}

interface RentalRequest {
  id: string;
  roomNumber: string;
  roomingHouseName: string;
  expectedOccupantCount: number;
  monthlyRentSnapshot: number;
  status: string;
  createdAt: string;
  rejectedReason?: string | null;
  deposit?: RoomDeposit | null;
  contract?: { id: string; status: string; signatureDeadlineAt?: string | null; statusReason?: string | null; } | null;
}

type RentalRequestTab = 'all' | 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Expired';

const STATUS_CONFIG: Record<string, { label: string; tone: CardStatusTone }> = {
  Pending: { label: 'Chờ duyệt', tone: 'warning' },
  Accepted: { label: 'Chấp nhận', tone: 'success' },
  Rejected: { label: 'Từ chối', tone: 'danger' },
  Cancelled: { label: 'Đã hủy', tone: 'neutral' },
  Expired: { label: 'Hết hạn', tone: 'neutral' },
};

const getStatusLabel = (status: string) => STATUS_CONFIG[status]?.label ?? status;
const getStatusTone = (status: string) => STATUS_CONFIG[status]?.tone ?? 'neutral';

function getRentalRequestTabIcon(tab: RentalRequestTab) {
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
    case 'Pending':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      );
    case 'Accepted':
      return (
        <svg {...props}>
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
          <polyline points="22 4 12 14.01 9 11.01" />
        </svg>
      );
    case 'Rejected':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
      );
    case 'Cancelled':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
        </svg>
      );
    case 'Expired':
      return (
        <svg {...props}>
          <polyline points="23 4 23 10 17 10" />
          <path d="M20.49 15a9 9 0 1 1-.18-4.95" />
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

export function TenantRentalRequestsPage() {
  const [requests, setRequests] = useState<RentalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedContractIdForSetup, setSelectedContractIdForSetup] = useState<string | null>(null);
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);

  // States cho ConfirmModal huỷ yêu cầu
  const [requestToCancel, setRequestToCancel] = useState<string | null>(null);
  const [showCancelModal, setShowCancelModal] = useState(false);
  const [cancelError, setCancelError] = useState('');
  const [activeTab, setActiveTab] = useState<RentalRequestTab>('all');
  const navigate = useNavigate();

  const fetchRequests = async () => {
    try {
      const res = await rentalRequestApi.getMyRentalRequests();
      setRequests(res.data);
    } catch (err: any) {
      setError('Không thể tải danh sách yêu cầu thuê.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchRequests();
  }, []);

  const handleCancelClick = (id: string) => {
    setRequestToCancel(id);
    setCancelError('');
    setShowCancelModal(true);
  };

  const handleConfirmCancel = async () => {
    if (!requestToCancel) return;

    try {
      await rentalRequestApi.cancelRentalRequest(requestToCancel);
      void fetchRequests();
      setShowCancelModal(false);
      setRequestToCancel(null);
    } catch (err: any) {
      setCancelError('Đã xảy ra lỗi khi hủy yêu cầu thuê.');
    }
  };

  const filteredRequests = requests.filter(req => {
    if (activeTab === 'all') return true;
    return req.status === activeTab;
  });

  return (
    <div className="tenant-requests-container">
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16c0 1.1.9 2 2 2h12a2 2 0 0 0 2-2V8l-6-6z" />
              <path d="M14 3v5h5M16 13H8M16 17H8M10 9H8" />
            </svg>
          </div>
        }
        eyebrow="QUẢN LÝ"
        title="Yêu cầu thuê phòng"
        description="Theo dõi trạng thái các yêu cầu thuê phòng của bạn"
      />

      {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

      <Tabs
        className="attached-bottom"
        variant="segmented-secondary"
        activeId={activeTab}
        onChange={(tab) => setActiveTab(tab as RentalRequestTab)}
        items={[
          { id: 'all', label: 'Tất cả', icon: getRentalRequestTabIcon('all') },
          { id: 'Pending', label: 'Chờ duyệt', icon: getRentalRequestTabIcon('Pending') },
          { id: 'Accepted', label: 'Chấp nhận', icon: getRentalRequestTabIcon('Accepted') },
          { id: 'Rejected', label: 'Từ chối', icon: getRentalRequestTabIcon('Rejected') },
          { id: 'Cancelled', label: 'Đã hủy', icon: getRentalRequestTabIcon('Cancelled') },
          { id: 'Expired', label: 'Hết hạn', icon: getRentalRequestTabIcon('Expired') },
        ]}
      />

      <section className="tab-attached-panel tab-attached-panel--cards">
        {loading ? (
          <div className="empty-state">
            <p>Đang tải dữ liệu...</p>
          </div>
        ) : filteredRequests.length === 0 ? (
          <div className="empty-state">
            <p>{requests.length === 0 ? 'Bạn chưa gửi yêu cầu thuê phòng nào.' : 'Không tìm thấy yêu cầu thuê nào tương ứng.'}</p>
          </div>
        ) : (
          <div className="requests-list">
            {filteredRequests.map(req => {
              const actionItems: CardAction[] = [];

              if (req.status === 'Pending') {
                actionItems.push({
                  label: 'Hủy yêu cầu',
                  variant: 'danger',
                  onClick: () => handleCancelClick(req.id),
                });
              }

              actionItems.push({
                label: 'Xem chi tiết',
                onClick: () => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUEST_DETAIL(req.id)),
                icon: (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                ),
              });

              return (
                <Card
                  key={req.id}
                  title={`Phòng ${req.roomNumber} - ${req.roomingHouseName}`}
                  status={getStatusLabel(req.status)}
                  statusTone={getStatusTone(req.status)}
                  bodyColumns={2}
                  actionItems={actionItems}
                >
                  <CardMetaRow
                    label="Ngày gửi:"
                    value={new Date(req.createdAt).toLocaleDateString('vi-VN')}
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
                    label="Số người ở:"
                    value={`${req.expectedOccupantCount} người`}
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                        <circle cx="9" cy="7" r="4" />
                        <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                        <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                      </svg>
                    }
                  />
                  <CardMetaRow
                    label="Giá thuê dự kiến:"
                    value={`${req.monthlyRentSnapshot.toLocaleString('vi-VN')} đ/tháng`}
                    valueClassName="tenant-request-price"
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10" />
                        <line x1="12" y1="8" x2="12" y2="12" />
                        <line x1="12" y1="16" x2="12.01" y2="16" />
                      </svg>
                    }
                  />
                </Card>
              );
            })}
          </div>
        )}
      </section>

      {selectedContractIdForSetup && (
        <ContractOccupantsSetupModal
          contractId={selectedContractIdForSetup}
          onClose={() => setSelectedContractIdForSetup(null)}
          onSuccess={() => {
            setSelectedContractIdForSetup(null);
            void fetchRequests();
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
            void fetchRequests();
          }}
        />
      )}

      <ConfirmModal
        isOpen={showCancelModal}
        title="Hủy yêu cầu thuê"
        message={cancelError || "Bạn có chắc chắn muốn hủy yêu cầu thuê này không?"}
        confirmText="Hủy yêu cầu"
        isDanger={true}
        onConfirm={handleConfirmCancel}
        onCancel={() => {
          setShowCancelModal(false);
          setRequestToCancel(null);
        }}
      />
    </div>
  );
}
