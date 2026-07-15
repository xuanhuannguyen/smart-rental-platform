import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { rentalRequestApi } from '../../rental-requests/api';
import { Alert } from '../../../shared/components/ui/Alert';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { Card, CardMetaRow, type CardStatusTone } from '../../../shared/components/ui/Card';
import './LandlordDashboardPage.css';
import './LandlordRentalRequestsPage.css';

interface RoomDeposit {
  depositAmount: number;
  status: string;
  paymentDeadlineAt?: string | null;
  paidAt?: string | null;
}

interface ContractBrief {
  id: string;
  status: string;
  signatureDeadlineAt?: string | null;
  statusReason?: string | null;
}

interface RentalRequest {
  id: string;
  roomNumber: string;
  roomingHouseName: string;
  tenantName: string;
  expectedOccupantCount: number;
  monthlyRentSnapshot: number;
  desiredStartDate: string;
  expectedEndDate: string;
  status: string;
  createdAt: string;
  rejectedReason?: string | null;
  deposit?: RoomDeposit | null;
  contract?: ContractBrief | null;
}

const STATUS_CONFIG: Record<string, { label: string; tone: CardStatusTone }> = {
  Pending: { label: 'Chờ duyệt', tone: 'warning' },
  Accepted: { label: 'Đã duyệt', tone: 'success' },
  Rejected: { label: 'Từ chối', tone: 'danger' },
  Cancelled: { label: 'Đã hủy', tone: 'neutral' },
  Expired: { label: 'Hết hạn', tone: 'neutral' },
};

const getStatusLabel = (status: string) => STATUS_CONFIG[status]?.label ?? status;
const getStatusTone = (status: string) => STATUS_CONFIG[status]?.tone ?? 'neutral';

const TABS = [
  {
    key: 'all', label: 'Tất cả',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" />
        <line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" />
        <line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" />
      </svg>
    ),
  },
  {
    key: 'Pending', label: 'Chờ duyệt',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
      </svg>
    ),
  },
  {
    key: 'Accepted', label: 'Đã duyệt',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" /><polyline points="22 4 12 14.01 9 11.01" />
      </svg>
    ),
  },
  {
    key: 'Rejected', label: 'Từ chối',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10" /><line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" />
      </svg>
    ),
  },
  {
    key: 'Cancelled', label: 'Đã hủy',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10" /><line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
      </svg>
    ),
  },
  {
    key: 'Expired', label: 'Hết hạn',
    icon: (
      <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="23 4 23 10 17 10" /><path d="M20.49 15a9 9 0 1 1-.18-4.95" />
      </svg>
    ),
  },
] as const;

type TabKey = (typeof TABS)[number]['key'];

export function LandlordRentalRequestsPage() {
  const navigate = useNavigate();
  const [requests, setRequests] = useState<RentalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [activeTab, setActiveTab] = useState<TabKey>('all');

  const fetchRequests = async () => {
    try {
      const res = await rentalRequestApi.getIncomingRentalRequests();
      setRequests(res.data);
    } catch {
      setError('Không thể tải danh sách yêu cầu thuê.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void fetchRequests(); }, []);

  const filteredRequests = requests.filter(req =>
    activeTab === 'all' ? true : req.status === activeTab
  );

  const countFor = (key: string) =>
    key === 'all' ? requests.length : requests.filter(r => r.status === key).length;

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">

        {/* ── Page Header ── */}
        <PageHeader
          icon={
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
          }
          eyebrow="QUẢN LÝ"
          title="Yêu cầu thuê phòng"
          description="Duyệt và kiểm tra yêu cầu thuê từ khách"
        />

        {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

        {/* ── Tabs ── */}
        <Tabs
          className="attached-bottom"
          variant="segmented-secondary"
          activeId={activeTab}
          onChange={(tab) => setActiveTab(tab as TabKey)}
          items={TABS.map((tab) => ({
            id: tab.key,
            label: tab.key === 'all' ? tab.label : `${tab.label} (${countFor(tab.key)})`,
            icon: tab.icon,
          }))}
        />

        {/* ── Content ── */}
        <section className="rr-section tab-attached-panel tab-attached-panel--cards">
          {loading ? (
            <div className="rr-state-box">
              <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ animation: 'rrSpin 1s linear infinite' }}>
                <line x1="12" y1="2" x2="12" y2="6" /><line x1="12" y1="18" x2="12" y2="22" />
                <line x1="4.93" y1="4.93" x2="7.76" y2="7.76" /><line x1="16.24" y1="16.24" x2="19.07" y2="19.07" />
                <line x1="2" y1="12" x2="6" y2="12" /><line x1="18" y1="12" x2="22" y2="12" />
                <line x1="4.93" y1="19.07" x2="7.76" y2="16.24" /><line x1="16.24" y1="7.76" x2="19.07" y2="4.93" />
              </svg>
              <p>Đang tải yêu cầu thuê...</p>
            </div>
          ) : filteredRequests.length === 0 ? (
            <div className="rr-state-box rr-state-box--empty">
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" />
                <polyline points="10 9 9 9 8 9" />
              </svg>
              <p>Không có yêu cầu thuê nào trong danh mục này.</p>
            </div>
          ) : (
            <div className="rr-grid">
              {filteredRequests.map(req => (
                <Card
                  key={req.id}
                  title={`Phòng ${req.roomNumber} - ${req.roomingHouseName}`}
                  status={getStatusLabel(req.status)}
                  statusTone={getStatusTone(req.status)}
                  bodyColumns={2}
                  actionItems={[
                    {
                      label: 'Xem chi tiết',
                      onClick: () => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUEST_DETAIL(req.id)),
                      icon: (
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                      ),
                    },
                  ]}
                >
                  <CardMetaRow
                    label="Khách thuê:"
                    value={req.tenantName}
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                        <circle cx="12" cy="7" r="4" />
                      </svg>
                    }
                  />

                  <CardMetaRow
                    label="Ngày gửi:"
                    value={new Date(req.createdAt).toLocaleDateString('vi-VN')}
                    icon={
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                          <line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" />
                          <line x1="3" y1="10" x2="21" y2="10" />
                        </svg>
                    }
                  />

                  <CardMetaRow
                    label="Thời gian thuê:"
                    value={`${new Date(req.desiredStartDate).toLocaleDateString('vi-VN')} - ${new Date(req.expectedEndDate).toLocaleDateString('vi-VN')}`}
                    icon={
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                          <line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" />
                          <line x1="3" y1="10" x2="21" y2="10" />
                        </svg>
                    }
                  />

                  <CardMetaRow
                    label="Giá chốt:"
                    value={`${req.monthlyRentSnapshot.toLocaleString('vi-VN')} đ/tháng`}
                    icon={
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="12" r="10" />
                          <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
                        </svg>
                    }
                    valueClassName="rr-price-value"
                  />
                </Card>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
