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
import { toAvatarImageUrl } from '../../../shared/api/assets';
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
    avatarUrl: '',
    avatarMediaAssetId: null as string | null
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
        avatarUrl: profileResponse.data?.avatarUrl || '',
        avatarMediaAssetId: profileResponse.data?.avatarMediaAssetId || null
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
      let finalAvatarMediaAssetId = profileForm.avatarMediaAssetId;
      if (selectedFile || isCropChanged) {
        const srcToCrop = previewUrl || toAvatarImageUrl(profileForm);
        const croppedFile = await cropAvatar(srcToCrop, cropParams.zoom, cropParams.position);
        const uploadResult = await uploadImage(croppedFile, 'Avatar');
        finalAvatarMediaAssetId = uploadResult.mediaAssetId || null;
      }

      const response = await profileApi.updateProfile({
        displayName: profileForm.displayName.trim(),
        phoneNumber: profileForm.phoneNumber.trim() || null,
        emergencyContactName: profileForm.emergencyContactName.trim() || null,
        emergencyContactPhone: profileForm.emergencyContactPhone.trim() || null,
        avatarMediaAssetId: finalAvatarMediaAssetId || null
      });

      setProfile(response.data);
      setProfileForm(current => ({
        ...current,
        avatarUrl: response.data?.avatarUrl || '',
        avatarMediaAssetId: response.data?.avatarMediaAssetId || null
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
      <div className="profile-info-card">
        <section className="overview-band">
          <div className="overview-left">
            <p className="eyebrow">TÀI KHOẢN</p>
            <h2>Thông tin hồ sơ</h2>
            <p className="overview-description">Cập nhật thông tin cá nhân và thông tin liên hệ của bạn.</p>
          </div>
          <div className="overview-right">
            {!isEditingProfile && !isLoading && (
              <Button type="button" onClick={() => setIsEditingProfile(true)} className="profile-edit-btn">
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 1 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
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
            <div className="profile-avatar-row">
              {isEditingProfile ? (
                <div className="profile-avatar-container is-editing">
                  {previewUrl || (profileForm.avatarUrl && profileForm.avatarUrl.trim() !== '') ? (
                    <>
                      {previewUrl || isCropChanged ? (
                        <div className="profile-avatar-preview-wrapper">
                          <img
                            src={previewUrl || toAvatarImageUrl(profileForm)}
                            alt="Avatar"
                            className="profile-avatar-preview-img"
                            style={{
                              transform: `translate(${cropParams.position.x * 0.5}px, ${cropParams.position.y * 0.5}px) scale(${cropParams.zoom * 1.6})`
                            }}
                          />
                        </div>
                      ) : (
                        <img
                          src={toAvatarImageUrl(profileForm)}
                          alt="Avatar"
                          className="profile-avatar-preview"
                        />
                      )}
                      <button 
                        type="button"
                        className="profile-avatar-edit-overlay"
                        title="Chỉnh sửa khung ảnh"
                        onClick={() => {
                          const currentSrc = previewUrl || toAvatarImageUrl(profileForm);
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
                      src={toAvatarImageUrl(profileForm)}
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
              <div className="profile-avatar-details">
                {isEditingProfile ? (
                  <div className="profile-avatar-edit-info">
                    <h3>{profileForm.displayName || 'Họ và tên'}</h3>
                    <span className="profile-avatar-hint">
                      Click overlay để chỉnh khung, camera để đổi ảnh.
                    </span>
                  </div>
                ) : (
                  <>
                    <h3>{profileForm.displayName || 'Họ và tên'}</h3>
                    {profile?.isGoogleUser && (
                      <div className="google-linked-badge">
                        <svg viewBox="0 0 24 24" width="14" height="14" className="google-icon" style={{ marginRight: '4px' }}>
                          <path
                            fill="#EA4335"
                            d="M5.266 9.765A7.077 7.077 0 0 1 12 4.909c1.69 0 3.218.6 4.418 1.582L19.91 3C17.782 1.145 15.055 0 12 0 7.33 0 3.321 2.68 1.341 6.578L5.266 9.765Z"
                          />
                          <path
                            fill="#34A853"
                            d="M16.04 15.342c-1.044.697-2.38 1.12-4.04 1.12a7.07 7.07 0 0 1-6.755-4.887l-3.939 3.056C3.267 18.528 7.29 21.2 12 21.2c2.97 0 5.753-1.063 7.828-2.946l-3.788-2.912Z"
                          />
                          <path
                            fill="#4285F4"
                            d="M23.49 12.275c0-.79-.07-1.54-.19-2.275H12v4.51h6.47c-.29 1.48-1.14 2.73-2.43 3.56l3.788 2.912c2.22-2.05 3.662-5.074 3.662-8.707Z"
                          />
                          <path
                            fill="#FBBC05"
                            d="M5.245 11.575a7.042 7.042 0 0 1 0-2.35L1.311 6.173a11.97 11.97 0 0 0 0 9.796l3.934-3.394Z"
                          />
                        </svg>
                        <span>Tài khoản liên kết Google</span>
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>

            <div className="profile-grid">
              <FormField label="Tên hiển thị" htmlFor="profile-display-name">
                <div className="input-with-icon icon-left">
                  <svg className="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                  </svg>
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
                </div>
              </FormField>

              <FormField label="Số điện thoại liên hệ" htmlFor="profile-phone-number">
                <div className="input-with-icon icon-left">
                  <svg className="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
                  </svg>
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
                </div>
              </FormField>
            </div>

            <div className="profile-grid">
              <FormField label="Tên liên hệ khẩn cấp" htmlFor="profile-emergency-name">
                <div className="input-with-icon icon-left">
                  <svg className="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                  </svg>
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
                </div>
              </FormField>

              <FormField label="Số điện thoại khẩn cấp" htmlFor="profile-emergency-phone">
                <div className="input-with-icon icon-left">
                  <svg className="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
                  </svg>
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
                </div>
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
                    avatarUrl: profile?.avatarUrl || '',
                    avatarMediaAssetId: profile?.avatarMediaAssetId || null
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
      </div>

      {!isLoading && (
        <div className="profile-ekyc-card">
          <div className="ekyc-section-header">
            <div className="ekyc-header-left">
              <h2>Thông tin định danh (eKYC)</h2>
              <p className="subtle">
                Thông tin định danh được lấy trực tiếp từ hồ sơ eKYC của bạn.
              </p>
            </div>
            <div className="ekyc-header-right">
              {displayedKycStatus === 'Approved' && (
                <span className="ekyc-status-badge approved">
                  <svg className="ekyc-status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                    <path d="m9 11 2 2 4-4" />
                  </svg>
                  KYC đã được duyệt
                </span>
              )}
              {displayedKycStatus === 'PendingAdminReview' && (
                <span className="ekyc-status-badge pending">
                  <svg className="ekyc-status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <polyline points="12 6 12 12 16 14" />
                  </svg>
                  Đang chờ duyệt
                </span>
              )}
              {displayedKycStatus === 'Rejected' && (
                <span className="ekyc-status-badge rejected">
                  <svg className="ekyc-status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <line x1="15" y1="9" x2="9" y2="15" />
                    <line x1="9" y1="9" x2="15" y2="15" />
                  </svg>
                  Bị từ chối
                </span>
              )}
              {(displayedKycStatus === 'EkycFailed' || !displayedKycStatus) && (
                <span className="ekyc-status-badge unverified">
                  <svg className="ekyc-status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                    <line x1="12" y1="9" x2="12" y2="13" />
                    <line x1="12" y1="17" x2="12.01" y2="17" />
                  </svg>
                  Chưa xác thực
                </span>
              )}
            </div>
          </div>

          <div className="ekyc-summary-grid">
            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">Họ tên</span>
                <span className="ekyc-value name-bold-uppercase">{display(profile?.fullName)}</span>
              </div>
            </div>

            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                  <circle cx="12" cy="10" r="3" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">Địa chỉ</span>
                <span className="ekyc-value">{display(profile?.addressLine)}</span>
              </div>
            </div>

            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                  <line x1="16" y1="2" x2="16" y2="6" />
                  <line x1="8" y1="2" x2="8" y2="6" />
                  <line x1="3" y1="10" x2="21" y2="10" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">Ngày sinh</span>
                <span className="ekyc-value">{formatDate(profile?.dateOfBirth)}</span>
              </div>
            </div>

            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4" width="18" height="16" rx="2" />
                  <line x1="7" y1="8" x2="11" y2="8" />
                  <line x1="7" y1="12" x2="13" y2="12" />
                  <line x1="7" y1="16" x2="9" y2="16" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">CCCD</span>
                <span className="ekyc-value">{display(profile?.verifiedCitizenIdMasked)}</span>
              </div>
            </div>

            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
                  <path d="M2 12h20" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">Giới tính</span>
                <span className="ekyc-value">{display(profile?.gender)}</span>
              </div>
            </div>

            <div className="ekyc-item">
              <div className="ekyc-icon-box">
                <svg viewBox="0 0 24 24" className="ekyc-icon" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
              </div>
              <div className="ekyc-info">
                <span className="ekyc-label">Duyệt lúc</span>
                <span className="ekyc-value">
                  {profile?.kycReviewedAt ? new Date(profile.kycReviewedAt).toLocaleString() : 'Chưa duyệt'}
                </span>
              </div>
            </div>
          </div>

          <div className="ekyc-actions">
            {displayedKycStatus !== 'Approved' && (
              <Button type="button" onClick={() => navigate(ROUTE_PATHS.ME.KYC)}>
                {profile?.identityVerified ? 'Cập nhật lại eKYC' : 'Xác thực eKYC'}
              </Button>
            )}
            <button
              type="button"
              className="ekyc-action-btn-outline"
              onClick={() => navigate(ROUTE_PATHS.ME.KYC_STATUS)}
            >
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                <circle cx="12" cy="12" r="3" />
              </svg>
              Xem trạng thái KYC
            </button>
          </div>
        </div>
      )}

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
