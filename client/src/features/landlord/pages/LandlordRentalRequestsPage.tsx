import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { rentalRequestApi } from '../../rental-requests/api';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import './LandlordDashboardPage.css'; // Reuse dashboard layout styles
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

function getStatusTone(status: string) {
  if (['Accepted', 'Approved', 'Paid', 'Active', 'Completed', 'Refunded'].includes(status)) {
    return 'success';
  }

  if (['Rejected', 'Reject', 'Failed', 'Forfeited'].includes(status)) {
    return 'danger';
  }

  if (['Cancelled', 'Canceled', 'Expired'].includes(status)) {
    return 'neutral';
  }

  return 'warning';
}

function formatDepositStatus(status: string) {
  if (status === 'PendingPayment') return 'Chờ khách thanh toán';
  if (status === 'Paid') return 'Khách đã thanh toán';
  if (status === 'Refunded') return 'Đã hoàn cọc';
  if (status === 'Forfeited') return 'Đã mất cọc';
  if (status === 'Expired') return 'Đã quá hạn';
  return status;
}

export function LandlordRentalRequestsPage() {
  const navigate = useNavigate();
  const [requests, setRequests] = useState<RentalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [activeTab, setActiveTab] = useState<'all' | 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Expired'>('all');

  const fetchRequests = async () => {
    try {
      const res = await rentalRequestApi.getIncomingRentalRequests();
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

  const filteredRequests = requests.filter(req => {
    if (activeTab === 'all') return true;
    return req.status === activeTab;
  });

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Pending':
        return <span className="status-badge pending">Chờ duyệt</span>;
      case 'Accepted':
        return <span className="status-badge accepted">Đã duyệt</span>;
      case 'Rejected':
        return <span className="status-badge rejected">Từ chối</span>;
      case 'Cancelled':
        return <span className="status-badge cancelled">Bị hủy bởi khách</span>;
      default:
        return <span className="status-badge">{status}</span>;
    }
  };

  if (loading) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
          <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
            Quản lý khu trọ
          </button>
          <button className="sidebar-item active">
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
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải yêu cầu thuê...</div>
        </main>
      </div>
    );
  }

  return (
    <div className="landlord-dashboard landlord-dashboard-page">
      <aside className="dashboard-sidebar">
        <h1>Chủ trọ</h1>
        <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Quản lý khu trọ
        </button>
        <button className="sidebar-item active">
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

      <main className="dashboard-main">
        <section className="overview-band">
          <div className="overview-left">
            <p className="eyebrow">QUẢN LÝ</p>
            <h2>Yêu cầu thuê phòng</h2>
            <p className="overview-description">Duyệt và kiểm tra yêu cầu thuê từ khách</p>
          </div>
        </section>

        {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

        <div className="landlord-tabs">
          <button className={activeTab === 'all' ? 'active' : ''} onClick={() => setActiveTab('all')}>
            Tất cả
          </button>
          <button className={activeTab === 'Pending' ? 'active' : ''} onClick={() => setActiveTab('Pending')}>
            Chờ duyệt ({requests.filter(r => r.status === 'Pending').length})
          </button>
          <button className={activeTab === 'Accepted' ? 'active' : ''} onClick={() => setActiveTab('Accepted')}>
            Đã duyệt ({requests.filter(r => r.status === 'Accepted').length})
          </button>
          <button className={activeTab === 'Rejected' ? 'active' : ''} onClick={() => setActiveTab('Rejected')}>
            Từ chối
          </button>
          <button className={activeTab === 'Cancelled' ? 'active' : ''} onClick={() => setActiveTab('Cancelled')}>
            Đã hủy
          </button>
          <button className={activeTab === 'Expired' ? 'active' : ''} onClick={() => setActiveTab('Expired')}>
            Hết hạn
          </button>
        </div>

        <section className="requests-section">
          {filteredRequests.length === 0 ? (
            <div className="empty-panel">
              <p>Không có yêu cầu thuê nào.</p>
            </div>
          ) : (
            <div className="requests-grid">
              {filteredRequests.map(req => (
                <div key={req.id} className="request-card">
                  <div className="request-card-header">
                    <div className="request-card-title">
                      <h3>Phòng {req.roomNumber} - {req.roomingHouseName}</h3>
                      <p className="tenant-name">Khách thuê: <strong>{req.tenantName}</strong></p>
                    </div>
                    {getStatusBadge(req.status)}
                  </div>

                  <div className="request-card-body">
                    <div className="info-row">
                      <span className="label">Ngày gửi:</span>
                      <span className="value">{new Date(req.createdAt).toLocaleDateString('vi-VN')}</span>
                    </div>
                    <div className="info-row">
                      <span className="label">Thời gian thuê:</span>
                      <span className="value">{new Date(req.desiredStartDate).toLocaleDateString('vi-VN')} - {new Date(req.expectedEndDate).toLocaleDateString('vi-VN')}</span>
                    </div>
                    <div className="info-row">
                      <span className="label">Giá chốt:</span>
                      <span className="value">{req.monthlyRentSnapshot.toLocaleString()} đ/tháng</span>
                    </div>
                  </div>

                  <div className="request-card-footer">
                    <Button onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUEST_DETAIL(req.id))}>
                      Xem chi tiết
                    </Button>
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
