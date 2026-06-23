import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { getApiErrorMessage } from '../../shared/api/apiError';
import { getMyRoomingHouseOnboarding, getRoomingHouseDetail } from './api';
import RoomingHouseEditor from './components/RoomingHouseEditor';
import type { RoomingHouseDetail, RoomingHouseOnboarding } from './types';
import { ROUTE_PATHS } from '../../app/router/routePaths';
import { profileApi } from '../profile/services/profileApi';
import type { LandlordEligibilityResponse } from '../profile/types/profile.types';
import { Button } from '../../shared/components/ui/Button';
import './CreateRoomingHousePage.css';

export default function CreateRoomingHousePage() {
  const [onboarding, setOnboarding] = useState<RoomingHouseOnboarding | null>(null);
  const [eligibility, setEligibility] = useState<LandlordEligibilityResponse | null>(null);
  const [roomingHouse, setRoomingHouse] = useState<RoomingHouseDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [viewSubmitted, setViewSubmitted] = useState(false);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const mode = searchParams.get('mode');
  const queryId = searchParams.get('id');

  useEffect(() => {
    async function loadOnboarding() {
      try {
        const [data, eligibilityData] = await Promise.all([
          getMyRoomingHouseOnboarding(),
          profileApi.getLandlordEligibility().then(res => res.data)
        ]);
        setOnboarding(data);
        setEligibility(eligibilityData);

        if (!mode && !queryId && data.canEnterLandlordDashboard) {
          navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES);
          return;
        }

        if (mode === 'new') {
          setRoomingHouse(null);
        } else if (queryId) {
          const detail = await getRoomingHouseDetail(queryId);
          setRoomingHouse(detail);
        } else if (shouldContinueExistingDraft(data)) {
          setRoomingHouse(data.roomingHouse ?? null);
        }
      } catch (error) {
        setMessage(getApiErrorMessage(error, 'Không thể tải trạng thái đăng ký chủ trọ.'));
      } finally {
        setLoading(false);
      }
    }

    loadOnboarding();
  }, [mode, queryId, navigate]);

  const pageCopy = useMemo(() => {
    if (mode === 'new') {
      return {
        title: 'Tạo khu trọ mới',
        subtitle: 'Bổ sung thông tin khu trọ để gửi quản trị viên xét duyệt.',
        submitLabel: 'Gửi duyệt khu trọ',
      };
    }

    if (onboarding?.canEnterLandlordDashboard) {
      return {
        title: 'Tạo khu trọ mới',
        subtitle: 'Bổ sung thông tin khu trọ để gửi quản trị viên xét duyệt.',
        submitLabel: 'Gửi duyệt khu trọ',
      };
    }

    if (onboarding?.status === 'Rejected') {
      return {
        title: 'Bổ sung hồ sơ chủ trọ',
        subtitle: 'Cập nhật khu trọ theo lý do từ chối và gửi duyệt lại.',
        submitLabel: 'Gửi duyệt lại',
      };
    }

    if (onboarding?.status === 'Draft') {
      return {
        title: 'Tiếp tục đăng ký chủ trọ',
        subtitle: 'Hoàn tất thông tin khu trọ đầu tiên để gửi duyệt hồ sơ chủ trọ.',
        submitLabel: 'Gửi duyệt hồ sơ',
      };
    }

    return {
      title: 'Đăng ký trở thành chủ trọ',
      subtitle: 'Hoàn tất thông tin khu trọ đầu tiên để gửi duyệt hồ sơ chủ trọ.',
      submitLabel: 'Gửi duyệt hồ sơ',
    };
  }, [onboarding, mode]);

  if (loading) {
    return <main className="create-rooming-house-page">Đang tải...</main>;
  }

  if (message) {
    return (
      <main className="create-rooming-house-page">
        <section className="create-rooming-house-page__state">
          <h1>Không thể mở trang tạo khu trọ</h1>
          <p>{message}</p>
        </section>
      </main>
    );
  }

  if (eligibility && !eligibility.canContinue && (eligibility.nextStep === 'KycSubmitPage' || eligibility.nextStep === 'KycStatusPage')) {
    return (
      <main className="create-rooming-house-page">
        <section className="create-rooming-house-page__state">
          <h1>Bạn chưa xác thực eKYC</h1>
          <p style={{ margin: '14px 0 24px', color: '#64748b', lineHeight: 1.5 }}>
            Để đăng ký làm chủ trọ và đăng tin cho thuê phòng, bạn cần thực hiện xác thực danh tính eKYC.
          </p>
          <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.KYC)}>
            Xác thực eKYC ngay
          </Button>
        </section>
      </main>
    );
  }

  if (eligibility && !eligibility.canContinue && eligibility.nextStep === 'CompleteProfilePage') {
    return (
      <main className="create-rooming-house-page">
        <section className="create-rooming-house-page__state">
          <h1>Thông tin cá nhân chưa đầy đủ</h1>
          <p style={{ margin: '14px 0 24px', color: '#64748b', lineHeight: 1.5 }}>
            Vui lòng bổ sung đầy đủ thông tin cá nhân và số điện thoại trước khi đăng ký làm chủ trọ.
          </p>
          <Button type="button" onClick={() => navigate(ROUTE_PATHS.ACCOUNT.PROFILE)}>
            Cập nhật Profile
          </Button>
        </section>
      </main>
    );
  }

  if (onboarding && !canEditOrCreate(onboarding, mode)) {
    if (viewSubmitted) {
      return (
        <main className="create-rooming-house-page" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div className="create-rooming-house-page__view-submitted-header">
            <button
              type="button"
              className="create-rooming-house-page__back-btn"
              onClick={() => setViewSubmitted(false)}
            >
              ← Quay lại trạng thái duyệt
            </button>
          </div>
          <div style={{ width: '100%', maxWidth: '960px', margin: '0 auto' }}>
            <RoomingHouseEditor
              title="Thông tin hồ sơ đã gửi"
              subtitle="Hồ sơ đang trong quá trình xét duyệt và không thể chỉnh sửa."
              initialRoomingHouse={onboarding.roomingHouse}
              allowSubmit={false}
            />
          </div>
        </main>
      );
    }

    const isApproved = onboarding.status === 'Approved';

    return (
      <main className="create-rooming-house-page">
        <section className="create-rooming-house-page__pending-card">
          <PendingIllustration />
          
          <h1 className="create-rooming-house-page__pending-title">
            {getLockedTitle(onboarding.status)}
          </h1>
          <p className="create-rooming-house-page__pending-subtitle">
            {getLockedDescription(onboarding.status)}
          </p>

          {onboarding.roomingHouse && (
            <div className="create-rooming-house-page__pending-house">
              <MapPinCircleIcon />
              <div className="create-rooming-house-page__pending-house-info">
                <h3>{onboarding.roomingHouse.name}</h3>
                <p>{onboarding.roomingHouse.addressDisplay}</p>
                {onboarding.roomingHouse.rejectedReason && (
                  <span style={{ color: '#ef4444', fontSize: '12px', display: 'block', marginTop: '6px', fontWeight: 600 }}>
                    Lý do từ chối: {onboarding.roomingHouse.rejectedReason}
                  </span>
                )}
              </div>
            </div>
          )}

          {/* Timeline steps */}
          <div className="create-rooming-house-page__timeline">
            {/* Step 1: Submitted */}
            <div className="create-rooming-house-page__timeline-step">
              <div className="create-rooming-house-page__timeline-icon create-rooming-house-page__timeline-icon--completed">
                <SendAirplaneIcon />
              </div>
              <div className="create-rooming-house-page__timeline-content">
                <strong>Đã gửi hồ sơ</strong>
                <span>{formatOnboardingDate(onboarding.roomingHouse?.updatedAt)}</span>
              </div>
            </div>

            {/* Line 1 */}
            <div className="create-rooming-house-page__timeline-line create-rooming-house-page__timeline-line--completed"></div>

            {/* Step 2: Pending Approval */}
            <div className="create-rooming-house-page__timeline-step">
              <div className={`create-rooming-house-page__timeline-icon create-rooming-house-page__timeline-icon--${isApproved ? 'completed' : 'active'}`}>
                <HourglassIcon />
              </div>
              <div className="create-rooming-house-page__timeline-content">
                <strong>Đang chờ duyệt</strong>
                <span>{isApproved ? 'Đã duyệt xong' : 'Quản trị viên đang xem xét'}</span>
              </div>
            </div>

            {/* Line 2 */}
            <div className={`create-rooming-house-page__timeline-line create-rooming-house-page__timeline-line--${isApproved ? 'completed' : 'pending'}`}></div>

            {/* Step 3: Approved */}
            <div className="create-rooming-house-page__timeline-step">
              <div className={`create-rooming-house-page__timeline-icon create-rooming-house-page__timeline-icon--${isApproved ? 'completed' : 'pending'}`}>
                <TimelineCheckIcon />
              </div>
              <div className="create-rooming-house-page__timeline-content">
                <strong>Duyệt thành công</strong>
                <span>{isApproved ? 'Tài khoản hoạt động' : 'Bạn sẽ nhận thông báo'}</span>
              </div>
            </div>
          </div>

          <div className="create-rooming-house-page__pending-actions">
            {onboarding.canEnterLandlordDashboard ? (
              <Link
                className="create-rooming-house-page__primary-btn"
                to="/landlord/rooming-houses"
              >
                <span>Vào kênh chủ trọ</span>
              </Link>
            ) : (
              <Link
                className="create-rooming-house-page__primary-btn"
                to={ROUTE_PATHS.ME.ROOT}
              >
                <RefreshIcon />
                <span>Quay lại trang chủ</span>
              </Link>
            )}

            {onboarding.roomingHouse && (
              <button
                type="button"
                className="create-rooming-house-page__link-btn"
                onClick={() => setViewSubmitted(true)}
              >
                <span>Xem lại thông tin đã gửi</span>
                <span className="arrow-right">›</span>
              </button>
            )}
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="create-rooming-house-page">
      <RoomingHouseEditor
        title={pageCopy.title}
        subtitle={pageCopy.subtitle}
        initialRoomingHouse={roomingHouse}
        submitLabel={pageCopy.submitLabel}
        onChange={setRoomingHouse}
        onSubmitSuccess={async () => {
          setLoading(true);
          try {
            const [data, eligibilityData] = await Promise.all([
              getMyRoomingHouseOnboarding(),
              profileApi.getLandlordEligibility().then(res => res.data)
            ]);
            setOnboarding(data);
            setEligibility(eligibilityData);

            if (data.canEnterLandlordDashboard) {
              navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES);
            } else {
              setRoomingHouse(data.roomingHouse ?? null);
            }
          } catch (err) {
            navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES);
          } finally {
            setLoading(false);
          }
        }}
      />
    </main>
  );
}

function shouldContinueExistingDraft(onboarding: RoomingHouseOnboarding) {
  if (onboarding.canEnterLandlordDashboard && onboarding.canCreateDraft) {
    return false;
  }
  return onboarding.canEdit && Boolean(onboarding.roomingHouse);
}

function canEditOrCreate(onboarding: RoomingHouseOnboarding, mode: string | null) {
  if (mode === 'new' && onboarding.canEnterLandlordDashboard) {
    return true;
  }
  return onboarding.canCreateDraft || onboarding.canEdit;
}

function getLockedTitle(status: string) {
  if (status === 'Pending') return 'Hồ sơ đang chờ duyệt';
  if (status === 'Approved') return 'Bạn đã trở thành chủ trọ';
  return 'Chưa thể tạo khu trọ mới';
}

function getLockedDescription(status: string) {
  if (status === 'Pending') {
    return 'Khu trọ của bạn đã được gửi cho quản trị viên xét duyệt. Vui lòng chờ kết quả.';
  }
  if (status === 'Approved') {
    return 'Khu trọ đầu tiên của bạn đã được duyệt. Bạn có thể vào kênh chủ trọ để quản lý hoặc tạo thêm khu trọ.';
  }
  return 'Hiện tại bạn đang có một hồ sơ khu trọ cần xử lý trước khi tạo mới.';
}

// ==========================================
// PENDING PAGE SVG ILLUSTRATIONS & ICONS
// ==========================================

const PendingIllustration = () => (
  <svg viewBox="0 0 160 160" width="140" height="140" fill="none" style={{ margin: '0 auto' }}>
    <circle cx="80" cy="80" r="50" fill="#eff6ff" />
    
    {/* Confetti pieces */}
    <rect x="35" y="50" width="3" height="8" rx="1.5" fill="#3b82f6" transform="rotate(25 35 50)" />
    <rect x="125" y="45" width="3" height="8" rx="1.5" fill="#10b981" transform="rotate(-35 125 45)" />
    <rect x="50" y="115" width="3" height="8" rx="1.5" fill="#f59e0b" transform="rotate(-15 50 115)" />
    <rect x="110" y="120" width="3" height="8" rx="1.5" fill="#6366f1" transform="rotate(45 110 120)" />
    <circle cx="45" cy="80" r="3.5" fill="#f59e0b" />
    <circle cx="115" cy="85" r="3" fill="#3b82f6" />
    
    {/* Clipboard */}
    <rect x="62" y="48" width="36" height="48" rx="4" fill="#ffffff" stroke="#246bfe" strokeWidth="2.5" />
    <path d="M72 48v-2a3 3 0 0 1 3-3h10a3 3 0 0 1 3 3v2" fill="#bfdbfe" stroke="#246bfe" strokeWidth="2" />
    <line x1="69" y1="58" x2="83" y2="58" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" />
    <line x1="69" y1="66" x2="91" y2="66" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" />
    <line x1="69" y1="74" x2="80" y2="74" stroke="#cbd5e1" strokeWidth="2.5" strokeLinecap="round" />
    
    {/* Glowing check circle */}
    <circle cx="94" cy="90" r="14" fill="#10b981" stroke="#ffffff" strokeWidth="2.5" />
    <path d="M89 90l3.5 3.5 6.5-6.5" stroke="#ffffff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const MapPinCircleIcon = () => (
  <div style={{
    width: '44px',
    height: '44px',
    borderRadius: '50%',
    background: '#ffffff',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: '0 4px 10px rgba(36, 107, 254, 0.08)',
    border: '1px solid #e2e8f0',
    flexShrink: 0
  }}>
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#246bfe" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
      <circle cx="12" cy="10" r="3" />
    </svg>
  </div>
);

const SendAirplaneIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="22" y1="2" x2="11" y2="13" />
    <polygon points="22 2 15 22 11 13 2 9 22 2" />
  </svg>
);

const HourglassIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M5 2h14M5 22h14M19 2v4c0 3-3 5-7 5s-7-2-7-5V2M12 11c-4 0-7 2-7 5v6h14v-6c0-3-3-5-7-5z" />
  </svg>
);

const TimelineCheckIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);

const RefreshIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
  </svg>
);

function formatOnboardingDate(dateStr?: string) {
  if (!dateStr) return '22/06/2026 - 10:30';
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return '22/06/2026 - 10:30';
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    const hours = String(d.getHours()).padStart(2, '0');
    const minutes = String(d.getMinutes()).padStart(2, '0');
    return `${day}/${month}/${year} - ${hours}:${minutes}`;
  } catch {
    return '22/06/2026 - 10:30';
  }
}
