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
          navigate(ROUTE_PATHS.LANDLORD.DASHBOARD);
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
    return (
      <main className="create-rooming-house-page">
        <section className="create-rooming-house-page__state">
          <h1>{getLockedTitle(onboarding.status)}</h1>
          <p>{getLockedDescription(onboarding.status)}</p>
          {onboarding.roomingHouse && (
            <div className="create-rooming-house-page__summary">
              <strong>{onboarding.roomingHouse.name}</strong>
              <span>{onboarding.roomingHouse.addressDisplay}</span>
              {onboarding.roomingHouse.rejectedReason && (
                <span>Lý do từ chối: {onboarding.roomingHouse.rejectedReason}</span>
              )}
            </div>
          )}
          <div className="create-rooming-house-page__actions">
            {onboarding.canEnterLandlordDashboard && (
              <Link
                className="create-rooming-house-page__primary"
                to="/landlord/dashboard"
              >
                Vào kênh chủ trọ
              </Link>
            )}
            <Link
              className="create-rooming-house-page__secondary"
              to={ROUTE_PATHS.ME.ROOT}
            >
              Quay lại trang chủ
            </Link>
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
              navigate(ROUTE_PATHS.LANDLORD.DASHBOARD);
            } else {
              setRoomingHouse(data.roomingHouse ?? null);
            }
          } catch (err) {
            navigate(ROUTE_PATHS.LANDLORD.DASHBOARD);
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
