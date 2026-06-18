import { useCallback, useEffect, useState, useRef, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { LoadingState } from '../../../shared/components/feedback/LoadingState';
import { kycApi } from '../../kyc/services/kycApi';
import type { KycStatusResponse } from '../../kyc/types/kyc.types';
import { profileApi } from '../services/profileApi';
import type { UserProfileResponse } from '../types/profile.types';
import { uploadImage } from '../../files/api';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { AvatarCropper, cropAvatar } from '../../../shared/components/ui/AvatarCropper';
import './MyProfilePage.css'; // Reuse original CSS for now

function display(value?: string | null) {
  return value && value.trim() ? value : 'Chưa có';
}

function formatDate(value?: string | null) {
  return value ? value.slice(0, 10) : 'Chưa có';
}

function getKycMessage(kycStatus?: string | null) {
  if (!kycStatus) {
    return 'Bạn chưa gửi KYC. Hãy xác thực KYC để hệ thống cập nhật hồ sơ định danh.';
  }

  if (kycStatus === 'PendingAdminReview') {
    return 'Hồ sơ KYC của bạn đang được admin duyệt.';
  }

  if (kycStatus === 'Approved') {
    return 'KYC đã được duyệt. Thông tin định danh bên dưới được lấy từ hồ sơ KYC.';
  }

  if (kycStatus === 'Rejected') {
    return 'KYC đã bị từ chối. Vui lòng gửi lại hồ sơ KYC.';
  }

  if (kycStatus === 'EkycFailed') {
    return 'KYC chưa hoàn tất do lỗi eKYC. Vui lòng gửi lại ảnh rõ hơn.';
  }

  return `Trạng thái KYC hiện tại: ${kycStatus}.`;
}

export function ProfileInfoPage() {
  const { currentUser, refreshMe } = useAuth();
  const navigate = useNavigate();

  const [profile, setProfile] = useState<UserProfileResponse | null>(null);
  const [latestKyc, setLatestKyc] = useState<KycStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSuccessMessage, setProfileSuccessMessage] = useState<string | null>(null);

  const [profileForm, setProfileForm] = useState({
    displayName: '',
    phoneNumber: '',
    emergencyContactName: '',
    emergencyContactPhone: '',
    avatarUrl: ''
  });

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [showCropper, setShowCropper] = useState(false);
  const [rawImageSrc, setRawImageSrc] = useState<string | null>(null);
  const [cropParams, setCropParams] = useState<{ zoom: number; position: { x: number; y: number } }>({
    zoom: 1,
    position: { x: 0, y: 0 }
  });
  const [isCropChanged, setIsCropChanged] = useState(false);

  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isEditingProfile, setIsEditingProfile] = useState(false);

  const loadProfile = useCallback(async () => {
    setIsLoading(true);
    setProfileError(null);

    try {
      const [profileResponse, kycResponse] = await Promise.all([
        profileApi.getProfile(),
        kycApi.getMyStatus()
      ]);
      setProfile(profileResponse.data);
      setLatestKyc(kycResponse.data);
      setProfileForm({
        displayName: profileResponse.data?.displayName || '',
        phoneNumber: profileResponse.data?.phoneNumber || '',
        emergencyContactName: profileResponse.data?.emergencyContactName || '',
        emergencyContactPhone: profileResponse.data?.emergencyContactPhone || '',
        avatarUrl: profileResponse.data?.avatarUrl || ''
      });
    } catch (loadError) {
      setProfileError(getApiErrorMessage(loadError, 'Không thể tải hồ sơ.'));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  const previewUrlRef = useRef<string | null>(null);
  const rawImageSrcRef = useRef<string | null>(null);

  useEffect(() => {
    previewUrlRef.current = previewUrl;
  }, [previewUrl]);

  useEffect(() => {
    rawImageSrcRef.current = rawImageSrc;
  }, [rawImageSrc]);

  useEffect(() => {
    return () => {
      if (previewUrlRef.current) {
        URL.revokeObjectURL(previewUrlRef.current);
      }
      if (rawImageSrcRef.current && rawImageSrcRef.current !== previewUrlRef.current) {
        URL.revokeObjectURL(rawImageSrcRef.current);
      }
    };
  }, []);

  if (!currentUser) {
    return null;
  }

  const displayedKycStatus = profile?.kycStatus ?? (latestKyc?.hasSubmission ? latestKyc.status : null);

  async function handleSaveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setProfileError(null);
    setProfileSuccessMessage(null);

    if (!profileForm.displayName.trim()) {
      setProfileError('Tên hiển thị không được để trống.');
      return;
    }

    setIsSavingProfile(true);

    try {
      let finalAvatarUrl = profileForm.avatarUrl;
      if (selectedFile || isCropChanged) {
        const srcToCrop = previewUrl || toAssetUrl(profileForm.avatarUrl);
        const croppedFile = await cropAvatar(srcToCrop, cropParams.zoom, cropParams.position);
        const uploadResult = await uploadImage(croppedFile, 'Avatar');
        finalAvatarUrl = uploadResult.url;
      }

      const response = await profileApi.updateProfile({
        displayName: profileForm.displayName.trim(),
        phoneNumber: profileForm.phoneNumber.trim() || null,
        emergencyContactName: profileForm.emergencyContactName.trim() || null,
        emergencyContactPhone: profileForm.emergencyContactPhone.trim() || null,
        avatarUrl: finalAvatarUrl || null
      });

      setProfile(response.data);
      setProfileForm(current => ({
        ...current,
        avatarUrl: response.data?.avatarUrl || ''
      }));

      setSelectedFile(null);
      setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
      setIsCropChanged(false);
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
        setPreviewUrl(null);
      }

      await refreshMe();
      setProfileSuccessMessage('Cập nhật thông tin hồ sơ thành công.');
      setIsEditingProfile(false);
    } catch (saveError) {
      setProfileError(getApiErrorMessage(saveError, 'Không thể cập nhật hồ sơ.'));
    } finally {
      setIsSavingProfile(false);
    }
  }

  return (
    <div>
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">TÀI KHOẢN</p>
          <h2>Thông tin hồ sơ</h2>
          <p className="overview-description">Cập nhật thông tin cá nhân và thông tin liên hệ của bạn bên dưới.</p>
        </div>
        <div className="overview-right">
          {!isEditingProfile && !isLoading && (
            <Button type="button" onClick={() => setIsEditingProfile(true)}>
              Chỉnh sửa
            </Button>
          )}
        </div>
      </section>

      {isLoading ? <LoadingState message="Đang tải hồ sơ..." /> : null}

      {!isLoading ? (
        <form className="auth-form" onSubmit={handleSaveProfile}>
          {profileError ? <Alert type="error">{profileError}</Alert> : null}
          {profileSuccessMessage ? <Alert type="success">{profileSuccessMessage}</Alert> : null}

          {/* Avatar Section */}
          <div className="profile-avatar-section">
            {isEditingProfile ? (
              <div className="profile-avatar-container is-editing">
                {previewUrl || (profileForm.avatarUrl && profileForm.avatarUrl.trim() !== '') ? (
                  <>
                    {previewUrl || isCropChanged ? (
                      <div className="profile-avatar-preview-wrapper">
                        <img
                          src={previewUrl || toAssetUrl(profileForm.avatarUrl)}
                          alt="Avatar"
                          className="profile-avatar-preview-img"
                          style={{
                            transform: `translate(${cropParams.position.x * 0.5}px, ${cropParams.position.y * 0.5}px) scale(${cropParams.zoom * 1.6})`
                          }}
                        />
                      </div>
                    ) : (
                      <img
                        src={toAssetUrl(profileForm.avatarUrl)}
                        alt="Avatar"
                        className="profile-avatar-preview"
                      />
                    )}
                    <button 
                      type="button"
                      className="profile-avatar-edit-overlay"
                      title="Chỉnh sửa khung ảnh"
                      onClick={() => {
                        const currentSrc = previewUrl || toAssetUrl(profileForm.avatarUrl);
                        if (currentSrc) {
                          const finalSrc = currentSrc.startsWith('blob:') 
                            ? currentSrc 
                            : `${currentSrc}${currentSrc.includes('?') ? '&' : '?'}t=${Date.now()}`;
                          setRawImageSrc(finalSrc);
                          setShowCropper(true);
                        }
                      }}
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                        <path strokeLinecap="round" strokeLinejoin="round" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                      </svg>
                    </button>
                  </>
                ) : (
                  <div className="profile-avatar-fallback">
                    {profileForm.displayName
                      ? profileForm.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
                      : 'U'}
                  </div>
                )}

                <label className="profile-avatar-upload-btn" htmlFor="avatar-file-input">
                  <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                </label>

                <input
                  type="file"
                  id="avatar-file-input"
                  accept="image/jpeg,image/png,image/webp"
                  style={{ display: 'none' }}
                  onChange={e => {
                    const file = e.target.files?.[0];
                    if (file) {
                      const objectUrl = URL.createObjectURL(file);
                      setSelectedFile(file);
                      setRawImageSrc(objectUrl);
                      setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
                      setShowCropper(true);
                      e.target.value = '';
                    }
                  }}
                />
              </div>
            ) : (
              <div className="profile-avatar-container">
                {profileForm.avatarUrl && profileForm.avatarUrl.trim() !== '' ? (
                  <img
                    src={toAssetUrl(profileForm.avatarUrl)}
                    alt="Avatar"
                    className="profile-avatar-preview"
                  />
                ) : (
                  <div className="profile-avatar-fallback">
                    {profileForm.displayName
                      ? profileForm.displayName.split(' ').map((n) => n[0]).join('').substring(0, 2).toUpperCase()
                      : 'U'}
                  </div>
                )}
              </div>
            )}
            <div className="profile-avatar-info">
              {isEditingProfile ? (
                <span className="profile-avatar-hint">
                  {profile?.isGoogleUser ? 'Đang dùng ảnh Google. ' : ''}Click ngòi bút để chỉnh sửa khung tròn, click biểu tượng camera để đổi ảnh
                </span>
              ) : profile?.isGoogleUser ? (
                <span className="profile-avatar-badge google-badge">
                  Tài khoản liên kết Google (Có thể thay đổi ảnh)
                </span>
              ) : null}
            </div>
          </div>

          <div className="profile-grid">
            <FormField label="Tên hiển thị" htmlFor="profile-display-name">
              <input
                id="profile-display-name"
                className="ui-input"
                value={profileForm.displayName}
                disabled={!isEditingProfile || isSavingProfile}
                onChange={event =>
                  setProfileForm(current => ({
                    ...current,
                    displayName: event.target.value
                  }))
                }
              />
            </FormField>

            <FormField label="Số điện thoại liên hệ" htmlFor="profile-phone-number">
              <input
                id="profile-phone-number"
                className="ui-input"
                value={profileForm.phoneNumber}
                disabled={!isEditingProfile || isSavingProfile}
                onChange={event =>
                  setProfileForm(current => ({
                    ...current,
                    phoneNumber: event.target.value
                  }))
                }
              />
            </FormField>
          </div>

          <div className="profile-grid">
            <FormField label="Tên liên hệ khẩn cấp" htmlFor="profile-emergency-name">
              <input
                id="profile-emergency-name"
                className="ui-input"
                value={profileForm.emergencyContactName}
                disabled={!isEditingProfile || isSavingProfile}
                onChange={event =>
                  setProfileForm(current => ({
                    ...current,
                    emergencyContactName: event.target.value
                  }))
                }
              />
            </FormField>

            <FormField label="Số điện thoại khẩn cấp" htmlFor="profile-emergency-phone">
              <input
                id="profile-emergency-phone"
                className="ui-input"
                value={profileForm.emergencyContactPhone}
                disabled={!isEditingProfile || isSavingProfile}
                onChange={event =>
                  setProfileForm(current => ({
                    ...current,
                    emergencyContactPhone: event.target.value
                  }))
                }
              />
            </FormField>
          </div>

          {isEditingProfile && (
            <div className="auth-actions" style={{ marginTop: '16px', display: 'flex', gap: '12px' }}>
              <Button type="submit" disabled={isSavingProfile}>
                {isSavingProfile ? 'Đang lưu...' : 'Lưu thay đổi'}
              </Button>
              <Button type="button" variant="secondary" disabled={isSavingProfile} onClick={() => {
                setIsEditingProfile(false);
                setProfileError(null);
                setProfileSuccessMessage(null);
                setProfileForm({
                  displayName: profile?.displayName || '',
                  phoneNumber: profile?.phoneNumber || '',
                  emergencyContactName: profile?.emergencyContactName || '',
                  emergencyContactPhone: profile?.emergencyContactPhone || '',
                  avatarUrl: profile?.avatarUrl || ''
                });
                setSelectedFile(null);
                setCropParams({ zoom: 1, position: { x: 0, y: 0 } });
                setIsCropChanged(false);
                if (previewUrl) {
                  URL.revokeObjectURL(previewUrl);
                  setPreviewUrl(null);
                }
              }}>
                Hủy
              </Button>
            </div>
          )}
        </form>
      ) : null}

      {!isLoading ? (
        <div style={{ marginTop: '32px' }}>
          <hr style={{ margin: '32px 0', border: 'none', borderTop: '1px solid #dce4ef' }} />
          
          <h2 className="section-title">Thông tin định danh (eKYC)</h2>
          <p className="subtle" style={{ marginBottom: '16px' }}>
            Thông tin định danh được lấy trực tiếp từ hồ sơ eKYC của bạn và không thể chỉnh sửa thủ công.
          </p>

          <Alert type={displayedKycStatus === 'Approved' ? 'success' : 'info'}>
            {getKycMessage(displayedKycStatus)}
          </Alert>

          <dl className="user-summary" style={{ marginTop: '24px' }}>
            <div>
              <dt>Họ tên</dt>
              <dd>{display(profile?.fullName)}</dd>
            </div>
            <div>
              <dt>Ngày sinh</dt>
              <dd>{formatDate(profile?.dateOfBirth)}</dd>
            </div>
            <div>
              <dt>Giới tính</dt>
              <dd>{display(profile?.gender)}</dd>
            </div>
            <div>
              <dt>Địa chỉ</dt>
              <dd>{display(profile?.addressLine)}</dd>
            </div>
            <div>
              <dt>CCCD</dt>
              <dd>{display(profile?.verifiedCitizenIdMasked)}</dd>
            </div>
            <div>
              <dt>Duyệt lúc</dt>
              <dd>{profile?.kycReviewedAt ? new Date(profile.kycReviewedAt).toLocaleString() : 'Chưa duyệt'}</dd>
            </div>
          </dl>

          <div className="auth-actions" style={{ marginTop: '24px' }}>
            {displayedKycStatus !== 'Approved' && (
              <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.KYC)}>
                {profile?.identityVerified ? 'Cập nhật lại eKYC' : 'Xác thực eKYC'}
              </Button>
            )}
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ME.KYC_STATUS)}>
              Xem trạng thái KYC
            </Button>
          </div>
        </div>
      ) : null}
      
      {showCropper && rawImageSrc && (
        <AvatarCropper
          imageSrc={rawImageSrc}
          initialZoom={cropParams.zoom}
          initialPosition={cropParams.position}
          onConfirm={(zoom, position) => {
            setCropParams({ zoom, position });
            setIsCropChanged(true);
            
            setShowCropper(false);
          }}
          onCancel={() => {
            setShowCropper(false);
            if (!selectedFile && !isCropChanged) {
              setRawImageSrc(null);
            }
          }}
        />
      )}
    </div>
  );
}
