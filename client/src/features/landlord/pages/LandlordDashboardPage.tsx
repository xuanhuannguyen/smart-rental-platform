import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { formatDateVi } from '../../../shared/utils/format';
import { formatStatus, getCreateHouseBlockedMessage, getStatusToneClass } from '../../../shared/utils/status';
import { getMyRoomingHouseOnboarding, getMyRoomingHouses } from '../../rooming-houses/api';
import type { RoomingHouseOnboarding, RoomingHouseSummary } from '../../rooming-houses/types';
import './LandlordDashboardPage.css';

const fallbackImage = 'https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=600&q=80';

export default function LandlordDashboardPage() {
  const navigate = useNavigate();
  const [houses, setHouses] = useState<RoomingHouseSummary[]>([]);
  const [onboarding, setOnboarding] = useState<RoomingHouseOnboarding | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');

  useEffect(() => {
    async function loadData() {
      setLoading(true);
      setMessage('');

      try {
        const [housesData, onboardingData] = await Promise.all([
          getMyRoomingHouses(),
          getMyRoomingHouseOnboarding(),
        ]);

        setHouses(housesData);
        setOnboarding(onboardingData);
      } catch (err) {
        setMessage(getApiErrorMessage(err, 'Không thể tải thông tin kênh chủ trọ.'));
      } finally {
        setLoading(false);
      }
    }

    void loadData();
  }, []);

  const houseStats = useMemo(() => ({
    total: houses.length,
    approved: houses.filter((house) => house.approvalStatus === 'Approved').length,
    pending: houses.filter((house) => house.approvalStatus === 'Pending').length,
    rejected: houses.filter((house) => house.approvalStatus === 'Rejected').length,
    draft: houses.filter((house) => house.approvalStatus === 'Draft').length,
  }), [houses]);

  const blockingHouse = useMemo(
    () => houses.find((house) =>
      house.approvalStatus === 'Draft' ||
      house.approvalStatus === 'Pending' ||
      house.approvalStatus === 'Rejected'
    ),
    [houses]
  );

  const canCreateNew = !blockingHouse;
  const canEnterDashboard = onboarding?.canEnterLandlordDashboard ?? true;

  function handleCreateNewHouse() {
    if (blockingHouse) {
      setMessage(getCreateHouseBlockedMessage(blockingHouse.approvalStatus));
      return;
    }

    navigate(`${ROUTE_PATHS.LANDLORD.REGISTER}?mode=new`);
  }

  function handleCardClick(house: RoomingHouseSummary) {
    if (house.approvalStatus === 'Approved') {
      navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSE_DETAIL(house.id));
      return;
    }

    if (house.approvalStatus === 'Draft' || house.approvalStatus === 'Rejected') {
      navigate(`${ROUTE_PATHS.LANDLORD.REGISTER}?id=${house.id}`);
    }
  }

  if (loading) {
    return (
      <div className="landlord-dashboard">
        <aside className="dashboard-sidebar">
          <h1>Chủ trọ</h1>
          <button className="sidebar-item active">Khu trọ của tôi</button>
        </aside>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải bảng quản lý...</div>
        </main>
      </div>
    );
  }

  return (
    <div className="landlord-dashboard landlord-dashboard-page">
      <aside className="dashboard-sidebar">
        <h1>Chủ trọ</h1>
        <button className="sidebar-item active" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Quản lý khu trọ
        </button>
        <button className="sidebar-item" onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}>
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
            <p className="eyebrow">Quản lý</p>
            <h2>Khu trọ của tôi</h2>
            <p className="overview-description">Quản lý danh sách các khu trọ của bạn.</p>
          </div>

          <div className="overview-right">
            <div className="overview-stats">
              <div className="stat-item stat-item--total">
                <BuildingIcon />
                <span>Tổng khu trọ</span>
                <strong className="stat-badge">{houseStats.total}</strong>
              </div>
              <div className="stat-item stat-item--approved">
                <CheckCircleIcon />
                <span>Đã duyệt</span>
                <strong className="stat-badge">{houseStats.approved}</strong>
              </div>
              <div className="stat-item stat-item--pending">
                <ClockIcon />
                <span>Chờ duyệt</span>
                <strong className="stat-badge">{houseStats.pending}</strong>
              </div>
              <div className="stat-item stat-item--rejected">
                <BanIcon />
                <span>Bản nháp / lỗi</span>
                <strong className="stat-badge">{houseStats.draft + houseStats.rejected}</strong>
              </div>
            </div>

            <div className="overview-actions">
              <button
                className="primary-action"
                disabled={!canCreateNew || !canEnterDashboard}
                onClick={handleCreateNewHouse}
                title={blockingHouse ? getCreateHouseBlockedMessage(blockingHouse.approvalStatus) : 'Tạo khu trọ mới'}
              >
                + Tạo khu trọ mới
              </button>
            </div>
          </div>
        </section>

        {message && <p className="dashboard-message">{message}</p>}

        <section className="card-grid">
          {houses.length === 0 ? (
            <div className="empty-panel">
              <h2>Bạn chưa có khu trọ nào</h2>
              <p>Tạo khu trọ đầu tiên để bắt đầu quản lý phòng cho thuê.</p>
            </div>
          ) : (
            houses.map((house) => (
              <button
                className="dashboard-card"
                key={house.id}
                onClick={() => handleCardClick(house)}
              >
                <div className="card-media-wrapper">
                  <img
                    src={house.coverImageUrl ? toAssetUrl(house.coverImageUrl) : fallbackImage}
                    alt={house.name}
                    className="card-cover-image"
                  />
                  <div className="card-status-overlay">
                    <span className={`status-pill ${getStatusToneClass(house.approvalStatus)}`}>
                      {formatStatus(house.approvalStatus)}
                    </span>
                    <span className={`status-pill ${getStatusToneClass(house.visibilityStatus)}`}>
                      {formatStatus(house.visibilityStatus)}
                    </span>
                  </div>
                </div>

                <div className="card-body-content">
                  <div className="card-title-row">
                    <h3>{house.name}</h3>
                    <div className="card-menu-trigger" onClick={(event) => event.stopPropagation()}>
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="5" r="1.5" fill="currentColor" />
                        <circle cx="12" cy="12" r="1.5" fill="currentColor" />
                        <circle cx="12" cy="19" r="1.5" fill="currentColor" />
                      </svg>
                    </div>
                  </div>

                  <div className="card-location">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                      <circle cx="12" cy="10" r="3" />
                    </svg>
                    <span>{house.addressDisplay}</span>
                  </div>

                  <hr className="card-divider" />

                  <div className="card-stats-row">
                    <div className="card-rooms-count">
                      <span className="stats-label">TRỐNG</span>
                      <strong className="stats-value">
                        {house.availableRooms ?? 0}/{house.totalRooms ?? 0}
                      </strong>
                    </div>
                    <span className="card-update-time">Cập nhật: {formatDateVi(house.updatedAt)}</span>
                  </div>

                  {house.rejectedReason && (
                    <div className="card-error-row">
                      <p className="warning-text">Lý do từ chối: {house.rejectedReason}</p>
                    </div>
                  )}

                  <div className="card-footer-action">
                    {house.approvalStatus === 'Approved' ? (
                      <span className="action-link text-success">Quản lý phòng và thông tin</span>
                    ) : house.approvalStatus === 'Draft' || house.approvalStatus === 'Rejected' ? (
                      <span className="action-link text-warning">Chỉnh sửa hồ sơ</span>
                    ) : (
                      <span className="action-link text-info">Đang chờ quản trị viên duyệt...</span>
                    )}
                  </div>
                </div>
              </button>
            ))
          )}
        </section>
      </main>
    </div>
  );
}

function BuildingIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
      <line x1="9" y1="22" x2="9" y2="16" />
      <line x1="15" y1="22" x2="15" y2="16" />
      <line x1="9" y1="16" x2="15" y2="16" />
      <path d="M9 6h.01" />
      <path d="M15 6h.01" />
      <path d="M9 10h.01" />
      <path d="M15 10h.01" />
    </svg>
  );
}

function CheckCircleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
      <polyline points="22 4 12 14.01 9 11.01" />
    </svg>
  );
}

function ClockIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  );
}

function BanIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
      <circle cx="12" cy="12" r="10" />
      <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
    </svg>
  );
}
