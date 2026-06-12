import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { rentalRequestApi } from '../api';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
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
  contract?: { id: string; status: string; } | null;
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
        return <span className="status-badge accepted">Được chấp nhận</span>;
      case 'Rejected':
        return <span className="status-badge rejected">Bị từ chối</span>;
      case 'Cancelled':
        return <span className="status-badge cancelled">Đã hủy</span>;
      default:
        return <span className="status-badge">{status}</span>;
    }
  };

  return (
    <div className="tenant-requests-container">
      <h2 className="page-title">Yêu cầu thuê của bạn</h2>
      
      {error && <div style={{ marginBottom: 16 }}><Alert type="error">{error}</Alert></div>}
      
      {loading ? (
        <p>Đang tải dữ liệu...</p>
      ) : requests.length === 0 ? (
        <div className="empty-state">
          <p>Bạn chưa gửi yêu cầu thuê phòng nào.</p>
        </div>
      ) : (
        <div className="requests-list">
          {requests.map(req => (
            <div key={req.id} className="request-card">
              <div className="request-header">
                <h3>Phòng {req.roomNumber} - {req.roomingHouseName}</h3>
                {getStatusBadge(req.status)}
              </div>
              <div className="request-body">
                <p><strong>Ngày gửi:</strong> {new Date(req.createdAt).toLocaleDateString('vi-VN')}</p>
                <p><strong>Số người ở:</strong> {req.expectedOccupantCount} người</p>
                <p><strong>Giá thuê dự kiến:</strong> {req.monthlyRentSnapshot.toLocaleString()} đ/tháng</p>
                
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
                    <p><strong>Số tiền cọc:</strong> {req.deposit.depositAmount.toLocaleString()} đ</p>
                    
                    {req.deposit.status === 'PendingPayment' && req.deposit.paymentDeadlineAt && (
                      <p><strong>Hạn thanh toán:</strong> {new Date(req.deposit.paymentDeadlineAt).toLocaleString('vi-VN')}</p>
                    )}
                    
                    {req.deposit.status === 'Paid' && req.deposit.paidAt && (
                      <p><strong>Thanh toán lúc:</strong> {new Date(req.deposit.paidAt).toLocaleString('vi-VN')}</p>
                    )}
                    
                    {req.deposit.status === 'PendingPayment' && (
                      <div style={{ marginTop: 12 }}>
                        <Button onClick={() => void handlePayDeposit(req.deposit!.id)}>
                          Thanh toán cọc
                        </Button>
                      </div>
                    )}

                    {req.deposit.status === 'Paid' && req.contract?.id && (
                      <div style={{ marginTop: 12 }}>
                        {['PendingLandlordSignature', 'PendingTenantSignature', 'TenantRevisionRequested', 'LandlordRevisionRequested', 'Active', 'Rejected'].includes(req.contract.status) ? (
                          <div className={`contract-status-info ${getStatusTone(req.contract.status)}`}>
                            <p style={{ margin: 0, fontSize: '0.9rem' }}>
                              <strong>Hợp đồng:</strong> {
                                req.contract.status === 'PendingLandlordSignature' ? 'Đã gửi thông tin người ở. Đang chờ chủ trọ tạo và ký hợp đồng.' :
                                req.contract.status === 'LandlordRevisionRequested' ? 'Chủ trọ yêu cầu bạn chỉnh sửa thông tin người ở.' :
                                req.contract.status === 'PendingTenantSignature' ? 'Chủ trọ đã ký. Bạn cần ký hợp đồng (Sắp ra mắt).' :
                                req.contract.status === 'TenantRevisionRequested' ? 'Đã gửi yêu cầu sửa đổi đến chủ trọ.' :
                                req.contract.status === 'Active' ? 'Hợp đồng đã có hiệu lực.' :
                                req.contract.status === 'Rejected' ? 'Hợp đồng đã bị hủy.' : req.contract.status
                              }
                            </p>
                            {req.contract.status === 'LandlordRevisionRequested' && (
                              <div style={{ marginTop: 8 }}>
                                <Button onClick={() => navigate(ROUTE_PATHS.ACCOUNT.CONTRACT_SETUP(req.contract!.id))}>
                                  Chỉnh sửa thông tin người ở
                                </Button>
                              </div>
                            )}
                          </div>
                        ) : (
                          <Button onClick={() => navigate(ROUTE_PATHS.ACCOUNT.CONTRACT_SETUP(req.contract!.id))}>
                            Nhập thông tin người ở để tạo hợp đồng
                          </Button>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {req.status === 'Pending' && (
                  <div style={{ marginTop: 16 }}>
                    <Button variant="secondary" onClick={() => void handleCancel(req.id)}>
                      Hủy yêu cầu
                    </Button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
