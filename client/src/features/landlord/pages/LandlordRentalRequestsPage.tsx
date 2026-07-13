import { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { rentalRequestApi } from '../../rental-requests/api';
import { Alert } from '../../../shared/components/ui/Alert';
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

const STATUS_CONFIG: Record<string, { label: string; cls: string }> = {
  Pending:   { label: 'Chờ duyệt',         cls: 'rr-badge--pending'   },
  Accepted:  { label: 'Đã duyệt',           cls: 'rr-badge--accepted'  },
  Rejected:  { label: 'Từ chối',            cls: 'rr-badge--rejected'  },
  Cancelled: { label: 'Đã hủy',             cls: 'rr-badge--cancelled' },
  Expired:   { label: 'Hết hạn',            cls: 'rr-badge--expired'   },
};

function StatusBadge({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status] ?? { label: status, cls: '' };
  return (
    <span className={`rr-badge ${cfg.cls}`}>
      {status === 'Accepted' && (
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
          <polyline points="22 4 12 14.01 9 11.01" />
        </svg>
      )}
      {cfg.label}
    </span>
  );
}

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
  const location = useLocation();
  const query = new URLSearchParams(location.search);
  const queryStatus = query.get('status') ?? 'all';
  const [requests, setRequests] = useState<RentalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [activeTab, setActiveTab] = useState<TabKey>(
    TABS.some(tab => tab.key === queryStatus) ? queryStatus as TabKey : 'all'
  );

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

  useEffect(() => {
    setActiveTab(TABS.some(tab => tab.key === queryStatus) ? queryStatus as TabKey : 'all');
  }, [queryStatus]);

  const filteredRequests = requests.filter(req =>
    activeTab === 'all' ? true : req.status === activeTab
  );

  const countFor = (key: string) =>
    key === 'all' ? requests.length : requests.filter(r => r.status === key).length;

  const accentClass = (status: string) => {
    if (['Accepted'].includes(status)) return 'rr-card--accepted';
    if (['Rejected'].includes(status)) return 'rr-card--rejected';
    if (['Cancelled', 'Expired'].includes(status)) return 'rr-card--neutral';
    return 'rr-card--pending';
  };

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">

        {/* ── Page Header ── */}
        <section className="rr-page-header">
          <div className="rr-page-header__left">
            <p className="rr-eyebrow">QUẢN LÝ</p>
            <h2 className="rr-page-title">Yêu cầu thuê phòng</h2>
            <p className="rr-page-subtitle">Duyệt và kiểm tra yêu cầu thuê từ khách</p>
          </div>
          {/* Decorative illustration */}
          <div className="rr-page-header__illus" aria-hidden="true">
            <svg width="110" height="90" viewBox="0 0 120 100" fill="none" xmlns="http://www.w3.org/2000/svg">
              {/* House */}
              <path d="M20 55 L50 30 L80 55 L80 85 L20 85 Z" fill="#dbeafe" stroke="#93c5fd" strokeWidth="2"/>
              <rect x="35" y="65" width="14" height="20" rx="2" fill="#93c5fd"/>
              <rect x="55" y="60" width="14" height="12" rx="2" fill="#bfdbfe"/>
              {/* Clipboard */}
              <rect x="60" y="15" width="44" height="52" rx="5" fill="#eff6ff" stroke="#93c5fd" strokeWidth="1.5"/>
              <rect x="72" y="10" width="20" height="8" rx="4" fill="#93c5fd"/>
              <line x1="68" y1="34" x2="96" y2="34" stroke="#93c5fd" strokeWidth="2" strokeLinecap="round"/>
              <line x1="68" y1="44" x2="96" y2="44" stroke="#bfdbfe" strokeWidth="2" strokeLinecap="round"/>
              <line x1="68" y1="54" x2="86" y2="54" stroke="#bfdbfe" strokeWidth="2" strokeLinecap="round"/>
              {/* Check circle */}
              <circle cx="98" cy="66" r="14" fill="#246bfe"/>
              <polyline points="91,66 96,71 105,60" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" fill="none"/>
            </svg>
          </div>
        </section>

        {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

        {/* ── Tabs ── */}
        <div className="rr-tabs">
          {TABS.map(tab => (
            <button
              key={tab.key}
              id={`rr-tab-${tab.key}`}
              className={`rr-tab ${activeTab === tab.key ? 'rr-tab--active' : ''}`}
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.icon}
              {tab.label}
              {tab.key !== 'all' && (
                <span className="rr-tab-count">{countFor(tab.key)}</span>
              )}
            </button>
          ))}
        </div>

        {/* ── Content ── */}
        <section className="rr-section">
          {loading ? (
            <div className="rr-state-box">
              <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" style={{ animation: 'rrSpin 1s linear infinite' }}>
                <line x1="12" y1="2" x2="12" y2="6"/><line x1="12" y1="18" x2="12" y2="22"/>
                <line x1="4.93" y1="4.93" x2="7.76" y2="7.76"/><line x1="16.24" y1="16.24" x2="19.07" y2="19.07"/>
                <line x1="2" y1="12" x2="6" y2="12"/><line x1="18" y1="12" x2="22" y2="12"/>
                <line x1="4.93" y1="19.07" x2="7.76" y2="16.24"/><line x1="16.24" y1="7.76" x2="19.07" y2="4.93"/>
              </svg>
              <p>Đang tải yêu cầu thuê...</p>
            </div>
          ) : filteredRequests.length === 0 ? (
            <div className="rr-state-box rr-state-box--empty">
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#cbd5e1" strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                <polyline points="14 2 14 8 20 8"/>
                <line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>
                <polyline points="10 9 9 9 8 9"/>
              </svg>
              <p>Không có yêu cầu thuê nào trong danh mục này.</p>
            </div>
          ) : (
            <div className="rr-grid">
              {filteredRequests.map(req => (
                <div key={req.id} className={`rr-card ${accentClass(req.status)}`}>

                  {/* Card Header */}
                  <div className="rr-card__header">
                    <div className="rr-card__title-row">
                      <div className="rr-card__icon">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="2" y="7" width="20" height="15" rx="2"/>
                          <path d="M16 21V7a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v14"/>
                        </svg>
                      </div>
                      <div>
                        <h3 className="rr-card__room">Phòng {req.roomNumber} - {req.roomingHouseName}</h3>
                        <p className="rr-card__tenant">Khách thuê: <strong>{req.tenantName}</strong></p>
                      </div>
                    </div>
                    <StatusBadge status={req.status} />
                  </div>

                  {/* Card Info Rows */}
                  <div className="rr-card__body">
                    <div className="rr-info-row">
                      <span className="rr-info-row__icon">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                          <line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/>
                          <line x1="3" y1="10" x2="21" y2="10"/>
                        </svg>
                      </span>
                      <span className="rr-info-row__label">Ngày gửi:</span>
                      <span className="rr-info-row__value">{new Date(req.createdAt).toLocaleDateString('vi-VN')}</span>
                    </div>

                    <div className="rr-info-row">
                      <span className="rr-info-row__icon">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                          <line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/>
                          <line x1="3" y1="10" x2="21" y2="10"/>
                        </svg>
                      </span>
                      <span className="rr-info-row__label">Thời gian thuê:</span>
                      <span className="rr-info-row__value">
                        {new Date(req.desiredStartDate).toLocaleDateString('vi-VN')} - {new Date(req.expectedEndDate).toLocaleDateString('vi-VN')}
                      </span>
                    </div>

                    <div className="rr-info-row">
                      <span className="rr-info-row__icon">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="12" r="10"/>
                          <line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
                        </svg>
                      </span>
                      <span className="rr-info-row__label">Giá chốt:</span>
                      <span className="rr-info-row__value rr-info-row__value--price">
                        {req.monthlyRentSnapshot.toLocaleString('vi-VN')} đ/tháng
                      </span>
                    </div>
                  </div>

                  {/* Footer */}
                  <div className="rr-card__footer">
                    <button
                      className="rr-btn-detail"
                      onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUEST_DETAIL(req.id))}
                    >
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                        <circle cx="12" cy="12" r="3"/>
                      </svg>
                      Xem chi tiết
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                        <polyline points="9 18 15 12 9 6"/>
                      </svg>
                    </button>
                  </div>

                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
