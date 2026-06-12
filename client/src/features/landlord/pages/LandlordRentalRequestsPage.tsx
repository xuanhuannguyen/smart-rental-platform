import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { rentalRequestApi } from '../../rental-requests/api';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { ContractPreviewModal } from '../../rental-contracts/components/ContractPreviewModal';
import './LandlordDashboardPage.css'; // Reuse dashboard layout styles
import './LandlordRentalRequestsPage.css';

interface RoomDeposit {
  depositAmount: number;
  status: string;
}

interface ContractBrief {
  id: string;
  status: string;
}

interface RentalRequest {
  id: string;
  roomNumber: string;
  roomingHouseName: string;
  tenantName: string;
  expectedOccupantCount: number;
  monthlyRentSnapshot: number;
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
  
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  // Contract Modal state
  const [previewContractId, setPreviewContractId] = useState<string | null>(null);

  const fetchRequests = async () => {
    try {
      const res = await rentalRequestApi.getIncomingRentalRequests();
      // Lọc bỏ các yêu cầu đã bị hủy bởi khách để tránh rác giao diện
      const filteredRequests = res.data.filter((req: RentalRequest) => req.status !== 'Cancelled');
      setRequests(filteredRequests);
    } catch (err: any) {
      setError('Không thể tải danh sách yêu cầu thuê.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchRequests();
  }, []);

  const handleApprove = async (id: string) => {
    if (!window.confirm('Bạn muốn duyệt yêu cầu này? Hệ thống sẽ tạo một khoản cọc cần người thuê thanh toán.')) {
      return;
    }
    try {
      // Hạn thanh toán cọc: mặc định 24h từ bây giờ
      const deadline = new Date();
      deadline.setHours(deadline.getHours() + 24);

      await rentalRequestApi.approveRentalRequest(id, { paymentDeadlineAt: deadline.toISOString() });
      void fetchRequests();
    } catch (err: any) {
      alert('Không thể duyệt yêu cầu thuê.');
    }
  };

  const handleReject = async (id: string) => {
    if (!rejectReason.trim()) {
      alert('Vui lòng nhập lý do từ chối.');
      return;
    }
    try {
      await rentalRequestApi.rejectRentalRequest(id, { rejectedReason: rejectReason });
      setRejectingId(null);
      setRejectReason('');
      void fetchRequests();
    } catch (err: any) {
      alert('Không thể từ chối yêu cầu thuê.');
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Pending':
        return <span className="status-badge pending">Chờ duyệt</span>;
      case 'Accepted':
        return <span className="status-badge accepted">Đã duyệt</span>;
      case 'Rejected':
        return <span className="status-badge rejected">Đã từ chối</span>;
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
          <button className="sidebar-item" disabled style={{ opacity: 0.6, cursor: 'not-allowed' }}>
            Quản lý doanh thu (Sau này)
          </button>
          <button className="sidebar-item sidebar-back-btn" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
            ← Quay lại trang chủ
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
        <button className="sidebar-item" disabled style={{ opacity: 0.6, cursor: 'not-allowed' }}>
          Quản lý doanh thu (Sau này)
        </button>
        <button className="sidebar-item sidebar-back-btn" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
          ← Quay lại trang chủ
        </button>
      </aside>

      <main className="dashboard-main">
        <section className="overview-band">
          <div className="overview-left">
            <p className="eyebrow">Yêu cầu thuê</p>
            <h2>Danh sách gửi đến khu trọ của bạn</h2>
          </div>
        </section>

        {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}

        <section className="requests-section">
          {requests.length === 0 ? (
            <div className="empty-panel">
              <p>Chưa có yêu cầu thuê nào.</p>
            </div>
          ) : (
            <div className="landlord-requests-list">
              {requests.map(req => (
                <div key={req.id} className="landlord-request-card">
                  <div className="request-header">
                    <div className="request-title">
                      <h3>Phòng {req.roomNumber} - {req.roomingHouseName}</h3>
                      <p className="tenant-name">Khách thuê: <strong>{req.tenantName}</strong></p>
                    </div>
                    {getStatusBadge(req.status)}
                  </div>
                  
                  <div className="request-body">
                    <div className="request-details">
                      <p><strong>Ngày gửi:</strong> {new Date(req.createdAt).toLocaleDateString('vi-VN')}</p>
                      <p><strong>Số người:</strong> {req.expectedOccupantCount} người</p>
                      <p><strong>Giá chốt:</strong> {req.monthlyRentSnapshot.toLocaleString()} đ/tháng</p>
                    </div>

                    {req.status === 'Pending' && (
                      <div className="request-actions">
                        {rejectingId === req.id ? (
                          <div className="reject-form">
                            <input
                              type="text"
                              placeholder="Nhập lý do từ chối..."
                              value={rejectReason}
                              onChange={e => setRejectReason(e.target.value)}
                            />
                            <div className="reject-form-buttons">
                              <Button variant="secondary" onClick={() => setRejectingId(null)}>Hủy</Button>
                              <Button onClick={() => void handleReject(req.id)}>Xác nhận từ chối</Button>
                            </div>
                          </div>
                        ) : (
                          <>
                            <Button variant="secondary" onClick={() => setRejectingId(req.id)}>
                              Từ chối
                            </Button>
                            <Button onClick={() => void handleApprove(req.id)}>
                              Duyệt yêu cầu
                            </Button>
                          </>
                        )}
                      </div>
                    )}
                    
                    {req.status === 'Rejected' && req.rejectedReason && (
                      <div className="reject-reason">
                        <strong>Lý do từ chối:</strong> {req.rejectedReason}
                      </div>
                    )}

                    {req.deposit && (
                      <div className={`deposit-info ${getStatusTone(req.deposit.status)}`}>
                        <p>
                          <strong>Trạng thái cọc:</strong>{' '}
                          <span className={`status-badge ${getStatusTone(req.deposit.status)}`}>
                            {formatDepositStatus(req.deposit.status)}
                          </span>
                        </p>
                        <p><strong>Số tiền:</strong> {req.deposit.depositAmount.toLocaleString()} đ</p>
                        
                        {req.deposit.status === 'Paid' && !req.contract && (
                          <div style={{ marginTop: 12, padding: '8px 12px', background: '#eff6ff', borderLeft: '4px solid #3b82f6', borderRadius: 4 }}>
                            <p style={{ margin: 0, color: '#1d4ed8', fontSize: '0.9rem' }}>
                              <strong>Hợp đồng:</strong> Đang chờ khách nhập thông tin người ở...
                            </p>
                          </div>
                        )}

                        {req.contract && (
                          <div className={`contract-status-info ${getStatusTone(req.contract.status)}`}>
                            <p style={{ margin: 0, fontSize: '0.9rem', marginBottom: req.contract.status === 'PendingLandlordSignature' ? '8px' : '0' }}>
                              <strong>Hợp đồng:</strong> {
                                req.contract.status === 'PendingLandlordSignature' ? 'Khách đã nhập thông tin. Chờ bạn tạo và ký hợp đồng.' :
                                req.contract.status === 'LandlordRevisionRequested' ? 'Đang chờ khách chỉnh sửa thông tin người ở.' :
                                req.contract.status === 'PendingTenantSignature' ? 'Chờ khách thuê ký hợp đồng.' :
                                req.contract.status === 'TenantRevisionRequested' ? 'Khách yêu cầu sửa đổi.' :
                                req.contract.status === 'Active' ? 'Hợp đồng đã có hiệu lực.' :
                                req.contract.status === 'Rejected' ? 'Hợp đồng đã bị hủy.' : req.contract.status
                              }
                            </p>
                            {req.contract.status === 'PendingLandlordSignature' && (
                              <Button onClick={() => setPreviewContractId(req.contract!.id)}>
                                Tạo hợp đồng và Ký
                              </Button>
                            )}
                            {req.contract.status === 'TenantRevisionRequested' && (
                              <Button variant="secondary" onClick={() => setPreviewContractId(req.contract!.id)}>
                                Xem yêu cầu & Sửa đổi
                              </Button>
                            )}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>

      {previewContractId && (
        <ContractPreviewModal
          contractId={previewContractId}
          onClose={() => setPreviewContractId(null)}
          onSuccess={() => {
            setPreviewContractId(null);
            void fetchRequests();
            alert('Đã cập nhật trạng thái hợp đồng thành công!');
          }}
        />
      )}
    </div>
  );
}
