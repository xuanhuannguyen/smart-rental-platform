import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { Button } from '../../../shared/components/ui/Button';
import { Alert } from '../../../shared/components/ui/Alert';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { rentalRequestApi } from '../api';
import { apiClient } from '../../../shared/api/apiClient';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import { toAssetUrl } from '../../../shared/api/assets';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { HomeHeader } from '../../../shared/components/layout/HomeHeader';
import './SubmitRentalRequestPage.css';

interface RoomDetail {
  id: string;
  roomNumber: string;
  floor: number;
  area: number;
  maxOccupants: number;
  status: string;
  images: { id: string; url: string; order: number }[];
  priceTiers: { occupantCount: number; monthlyRent: number }[];
}

function toDateInput(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addDays(days: number) {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toDateInput(date);
}

export function SubmitRentalRequestPage() {
  const { currentUser } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const roomId = searchParams.get('roomId');

  const [room, setRoom] = useState<RoomDetail | null>(null);
  const [loadingRoom, setLoadingRoom] = useState(true);

  const [desiredStartDate, setDesiredStartDate] = useState(addDays(7));
  const [expectedEndDate, setExpectedEndDate] = useState(addDays(97));
  const [expectedOccupantCount, setExpectedOccupantCount] = useState<number>(1);
  const [tenantNote, setTenantNote] = useState('');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!roomId) return;

    async function fetchRoomDetail() {
      try {
        const res = await apiClient<{ data: RoomDetail }>(ENDPOINTS.PUBLIC.ROOM_BY_ID(roomId!), { method: 'GET' });
        setRoom(res.data);
      } catch (err) {
        setError('Không thể tải thông tin phòng. Phòng có thể không tồn tại hoặc đã được thuê.');
      } finally {
        setLoadingRoom(false);
      }
    }

    void fetchRoomDetail();
  }, [roomId]);

  if (!roomId) {
    return (
      <div className="submit-rental-container">
        <Alert type="error">Mã phòng không hợp lệ. Vui lòng quay lại và thử lại.</Alert>
        <Button variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)} style={{ marginTop: 16 }}>Quay về trang chủ</Button>
      </div>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentUser) {
      setError('Bạn cần đăng nhập để gửi yêu cầu thuê.');
      return;
    }

    setIsSubmitting(true);
    setError('');

    try {
      await rentalRequestApi.createRentalRequest(roomId, {
        desiredStartDate,
        expectedEndDate,
        expectedOccupantCount,
        tenantNote
      });
      // Redirect to test flow or dashboard after success
      navigate(ROUTE_PATHS.ME.ROOT, { state: { message: 'Gửi yêu cầu thuê phòng thành công!' } });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Đã xảy ra lỗi khi gửi yêu cầu thuê.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <HomeHeader />
      <div className="submit-rental-container">


      {error && <div style={{ marginBottom: 24 }}><Alert type="error">{error}</Alert></div>}

      {loadingRoom ? (
        <p>Đang tải thông tin phòng...</p>
      ) : room ? (
        <div className="submit-rental-content">
          {/* LEFT PANE: ROOM INFO */}
          <div className="room-summary-pane">
            {room.images && room.images.length > 0 ? (
              <img src={toAssetUrl(room.images[0].url)} alt={`Phòng ${room.roomNumber}`} />
            ) : (
              <div style={{ width: '100%', height: '200px', background: '#f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#94a3b8' }}>Không có ảnh</div>
            )}
            <div className="room-summary-details">
              <h2>Phòng {room.roomNumber}</h2>
              <p>Tầng {room.floor} • Diện tích: {room.area}m² • Tối đa: {room.maxOccupants} người</p>
              
              <h4 style={{ marginBottom: 8, color: '#0f172a' }}>Bảng giá thuê</h4>
              {room.priceTiers && room.priceTiers.length > 0 ? (
                <table className="price-tier-table">
                  <thead>
                    <tr>
                      <th>Số người ở</th>
                      <th>Giá tiền / tháng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {room.priceTiers.map(tier => (
                      <tr key={tier.occupantCount}>
                        <td>{tier.occupantCount} người</td>
                        <td style={{ fontWeight: 600, color: '#2563eb' }}>{tier.monthlyRent.toLocaleString()}đ</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p style={{ fontSize: '0.9rem' }}>Chưa cập nhật bảng giá.</p>
              )}
            </div>
          </div>

          {/* RIGHT PANE: FORM */}
          <div className="rental-form-pane">
            <h3>Thông tin yêu cầu thuê</h3>
            <form onSubmit={handleSubmit}>
              <div className="form-grid">
                <div className="form-group">
                  <label>Ngày bắt đầu thuê *</label>
                  <input
                    type="date"
                    required
                    value={desiredStartDate}
                    onChange={(e) => setDesiredStartDate(e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label>Ngày dự kiến kết thúc *</label>
                  <input
                    type="date"
                    required
                    value={expectedEndDate}
                    onChange={(e) => setExpectedEndDate(e.target.value)}
                  />
                </div>
              </div>

              <div className="form-group" style={{ marginBottom: 24 }}>
                <label>Số người dự kiến ở *</label>
                <input
                  type="number"
                  min={1}
                  max={room.maxOccupants}
                  required
                  value={expectedOccupantCount}
                  onChange={(e) => setExpectedOccupantCount(Number(e.target.value))}
                />
              </div>

              <div className="form-group">
                <label>Ghi chú cho chủ trọ</label>
                <textarea
                  placeholder="Ghi chú thêm về yêu cầu của bạn (ví dụ: thời gian dọn vào cụ thể, yêu cầu thêm nội thất...)"
                  value={tenantNote}
                  onChange={(e) => setTenantNote(e.target.value)}
                />
              </div>

              <div className="submit-actions">
                <Button type="button" variant="secondary" onClick={() => navigate(-1)} style={{ marginRight: 16 }}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting}>
                  {isSubmitting ? 'Đang gửi...' : 'Gửi yêu cầu thuê'}
                </Button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </div>
    </>
  );
}
