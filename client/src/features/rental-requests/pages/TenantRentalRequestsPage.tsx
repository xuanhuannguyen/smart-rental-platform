import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { rentalRequestApi } from '../api';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { ContractOccupantsSetupModal } from '../../rental-contracts/components/ContractOccupantsSetupModal';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
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
  if (status === 'PendingPayment') return 'Chờ thanh toán';
  if (status === 'Paid') return 'Đã thanh toán';
  if (status === 'Refunded') return 'Đã hoàn cọc';
  if (status === 'Forfeited') return 'Đã mất cọc';
  if (status === 'Expired') return 'Đã quá hạn';
  return status;
}

export function TenantRentalRequestsPage() {
  const [requests, setRequests] = useState<RentalRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedContractIdForSetup, setSelectedContractIdForSetup] = useState<string | null>(null);
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('all');
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

  const handleCancel = async (id: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn hủy yêu cầu thuê này?')) {
      return;
    }

    try {
      await rentalRequestApi.cancelRentalRequest(id);
      void fetchRequests();
    } catch (err: any) {
      alert('Đã xảy ra lỗi khi hủy yêu cầu thuê.');
    }
  };

  const handlePayDeposit = async (depositId: string) => {
    try {
      await rentalRequestApi.markDepositPaid(depositId);
      alert('Thanh toán cọc thành công (Mô phỏng)!');
      void fetchRequests();
    } catch (err: any) {
      alert('Không thể thanh toán cọc.');
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Pending':
        return <span className="status-badge pending">Chờ duyệt</span>;
      case 'Accepted':
        return <span className="status-badge accepted">Chấp nhận</span>;
      case 'Rejected':
        return <span className="status-badge rejected">Từ chối</span>;
      case 'Cancelled':
        return <span className="status-badge cancelled">Đã hủy</span>;
      case 'Expired':
        return <span className="status-badge neutral">Hết hạn</span>;
      default:
        return <span className="status-badge">{status}</span>;
    }
  };

  const filteredRequests = requests.filter(req => {
    if (activeTab === 'all') return true;
    return req.status === activeTab;
  });

  return (
    <div className="tenant-requests-container">
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
          <h2>Yêu cầu thuê phòng</h2>
          <p className="overview-description">Theo dõi trạng thái các yêu cầu thuê phòng của bạn</p>
        </div>
      </section>

      {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

      {loading ? (
        <p>Đang tải dữ liệu...</p>
      ) : requests.length === 0 ? (
        <div className="empty-state">
          <p>Bạn chưa gửi yêu cầu thuê phòng nào.</p>
        </div>
      ) : (
        <>
          <div className="tabs-navigation">
            <button className={activeTab === 'all' ? 'active' : ''} onClick={() => setActiveTab('all')}>
              Tất cả
            </button>
            <button className={activeTab === 'Pending' ? 'active' : ''} onClick={() => setActiveTab('Pending')}>
              Chờ duyệt
            </button>
            <button className={activeTab === 'Accepted' ? 'active' : ''} onClick={() => setActiveTab('Accepted')}>
              Chấp nhận
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
          
          <div className="requests-list">
            {filteredRequests.map(req => (
            <div key={req.id} className="request-card">
              <div className="request-header">
                <h3>Phòng {req.roomNumber} - {req.roomingHouseName}</h3>
                {getStatusBadge(req.status)}
              </div>
              <div className="request-body">
                <p><strong>Ngày gửi:</strong> {new Date(req.createdAt).toLocaleDateString('vi-VN')}</p>
                <p><strong>Số người ở:</strong> {req.expectedOccupantCount} người</p>
                <p><strong>Giá thuê dự kiến:</strong> {req.monthlyRentSnapshot.toLocaleString()} đ/tháng</p>
              </div>
              <div className="request-card-footer" style={{ borderTop: '1px solid #e2e8f0', marginTop: '16px', paddingTop: '16px', display: 'flex', justifyContent: 'flex-end' }}>
                <Button onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUEST_DETAIL(req.id))}>
                  Xem chi tiết
                </Button>
              </div>
            </div>
          ))}
        </div>
        </>
      )}

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
    </div>
  );
}
