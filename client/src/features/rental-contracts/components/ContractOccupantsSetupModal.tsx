import { useEffect, useState } from 'react';
import { useAuth } from '../../../app/providers/AuthProvider';
import { apiClient } from '../../../shared/api/apiClient';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { Alert } from '../../../shared/components/ui/Alert';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import { uploadImage } from '../../files/api';
import { contractApi } from '../../contracts/api';
import type { ContractDetailResponse, ContractOccupantResponse } from '../../contracts/types';
import './ContractOccupantsSetupModal.css';

interface OccupantForm {
  id: string;
  isMainTenant: boolean;
  hasAccount: boolean;
  isSaved: boolean;
  saveMessage: string;
  saveMessageType: 'success' | 'error' | '';
  email: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  relationship: string;
  moveInDate: string;
  documentType: string;
  documentNumber: string;
  frontMediaAssetId: string | null;
  frontImageUrl: string;
  backMediaAssetId: string | null;
  backImageUrl: string;
  extraMediaAssetId: string | null;
  extraImageUrl: string;
}

interface OccupantAccountLookupResponse {
  email: string;
  Email?: string;
  exists: boolean;
  Exists?: boolean;
  isKycApproved: boolean;
  IsKycApproved?: boolean;
  displayName?: string | null;
  DisplayName?: string | null;
}

function toDateInput(value?: string | null) {
  if (!value) {
    return new Date().toISOString().split('T')[0];
  }

  return value.slice(0, 10);
}

function createMainTenantForm(email?: string | null, moveInDate?: string): OccupantForm {
  return {
    id: crypto.randomUUID(),
    isMainTenant: true,
    hasAccount: true,
    isSaved: true,
    saveMessage: '',
    saveMessageType: '',
    email: email ?? '',
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '',
    relationship: 'Chủ hợp đồng',
    moveInDate: moveInDate ?? toDateInput(),
    documentType: 'CCCD',
    documentNumber: '',
    frontMediaAssetId: null,
    frontImageUrl: '',
    backMediaAssetId: null,
    backImageUrl: '',
    extraMediaAssetId: null,
    extraImageUrl: ''
  };
}

function createEmptyOccupantForm(moveInDate: string): OccupantForm {
  return {
    id: crypto.randomUUID(),
    isMainTenant: false,
    hasAccount: false,
    isSaved: false,
    saveMessage: '',
    saveMessageType: '',
    email: '',
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '',
    relationship: 'Người ở cùng',
    moveInDate,
    documentType: 'CCCD',
    documentNumber: '',
    frontMediaAssetId: null,
    frontImageUrl: '',
    backMediaAssetId: null,
    backImageUrl: '',
    extraMediaAssetId: null,
    extraImageUrl: ''
  };
}

function mapOccupantToForm(
  occupant: ContractOccupantResponse,
  mainTenantUserId: string,
  currentUserEmail?: string | null
): OccupantForm {
  const hasAccount = Boolean(occupant.userId);
  const isMainTenant = occupant.userId === mainTenantUserId;

  return {
    id: occupant.id,
    isMainTenant,
    hasAccount,
    isSaved: true,
    saveMessage: '',
    saveMessageType: '',
    email: hasAccount ? occupant.email ?? (isMainTenant ? currentUserEmail : null) ?? '' : '',
    fullName: hasAccount ? '' : occupant.fullName ?? '',
    phoneNumber: hasAccount ? '' : occupant.phoneNumber ?? '',
    dateOfBirth: hasAccount ? '' : toDateInput(occupant.dateOfBirth),
    relationship: occupant.relationshipToMainTenant ?? '',
    moveInDate: toDateInput(occupant.moveInDate),
    documentType: occupant.document?.documentType ?? 'CCCD',
    documentNumber: '',
    frontMediaAssetId: occupant.document?.frontMediaAssetId ?? null,
    frontImageUrl: occupant.document?.frontImageUrl ?? '',
    backMediaAssetId: occupant.document?.backMediaAssetId ?? null,
    backImageUrl: occupant.document?.backImageUrl ?? '',
    extraMediaAssetId: occupant.document?.extraMediaAssetId ?? null,
    extraImageUrl: occupant.document?.extraImageUrl ?? ''
  };
}

function normalizeOccupantSlots(
  forms: OccupantForm[],
  expectedOccupantCount?: number,
  contractStartDate?: string | null,
  currentUserEmail?: string | null
) {
  if (!expectedOccupantCount || expectedOccupantCount <= 0) {
    return forms;
  }

  const targetCount = Math.max(expectedOccupantCount, 1);
  const moveInDate = toDateInput(contractStartDate);
  const mainTenant = forms.find((item) => item.isMainTenant) ?? createMainTenantForm(currentUserEmail, moveInDate);
  const coOccupants = forms.filter((item) => !item.isMainTenant).slice(0, targetCount - 1);

  while (coOccupants.length < targetCount - 1) {
    coOccupants.push(createEmptyOccupantForm(moveInDate));
  }

  return [{ ...mainTenant, isSaved: true }, ...coOccupants];
}

function getCoOccupantIndex(occupants: OccupantForm[], currentIndex: number) {
  return occupants.slice(0, currentIndex + 1).filter((occupant) => !occupant.isMainTenant).length;
}

interface ContractOccupantsSetupModalProps {
  contractId: string;
  expectedOccupantCount?: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function ContractOccupantsSetupModal({
  contractId,
  expectedOccupantCount,
  onClose,
  onSuccess
}: ContractOccupantsSetupModalProps) {
  const { currentUser } = useAuth();

  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [occupants, setOccupants] = useState<OccupantForm[]>([]);
  const [uploadingField, setUploadingField] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [savingOccupantId, setSavingOccupantId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadContract() {
      if (!contractId) {
        return;
      }

      try {
        setLoading(true);
        setToast(null);

        const response = await contractApi.getContract(contractId);
        if (!isMounted) {
          return;
        }

        setContract(response.data);

        const mappedOccupants = response.data.occupants.length > 0
          ? response.data.occupants
              .map((occupant) =>
                mapOccupantToForm(
                  occupant,
                  response.data.mainTenantUserId,
                  currentUser?.email
                )
              )
              .sort((left, right) => Number(right.isMainTenant) - Number(left.isMainTenant))
          : [createMainTenantForm(currentUser?.email)];

        setOccupants(normalizeOccupantSlots(mappedOccupants, expectedOccupantCount, response.data.startDate, currentUser?.email));
      } catch (err) {
        if (isMounted) {
          setToast({ message: getApiErrorMessage(err, 'Không thể tải thông tin hợp đồng.'), type: 'error' });
          setOccupants(normalizeOccupantSlots([createMainTenantForm(currentUser?.email)], expectedOccupantCount, undefined, currentUser?.email));
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    void loadContract();

    return () => {
      isMounted = false;
    };
  }, [contractId, currentUser?.email, currentUser?.userId, expectedOccupantCount]);

  const addOccupant = () => {
    setOccupants((current) => [
      ...current,
      createEmptyOccupantForm(toDateInput(contract?.startDate))
    ]);
  };

  const removeOccupant = (id: string) => {
    setOccupants((current) => {
      if (current.length <= 1) {
        return current;
      }

      return current.filter((occupant) => occupant.id !== id);
    });
  };

  const updateOccupant = (id: string, field: keyof OccupantForm, value: string | boolean | null) => {
    setOccupants((current) =>
      current.map((occupant) =>
        occupant.id === id
          ? {
              ...occupant,
              [field]: value,
              isSaved: occupant.isMainTenant ? true : false,
              saveMessage: '',
              saveMessageType: ''
            }
          : occupant
      )
    );
  };

  const setOccupantSaveFeedback = (
    id: string,
    saveMessage: string,
    saveMessageType: OccupantForm['saveMessageType'],
    isSaved = false
  ) => {
    setOccupants((current) =>
      current.map((occupant) =>
        occupant.id === id
          ? {
              ...occupant,
              isSaved,
              saveMessage,
              saveMessageType
            }
          : occupant
      )
    );
  };

  const validateOccupant = (occupant: OccupantForm) => {
    if (!occupant.relationship.trim()) {
      return 'Cần nhập quan hệ với người thuê chính.';
    }

    if (!occupant.moveInDate) {
      return 'Cần chọn ngày dọn vào.';
    }

    if (contract) {
      const contractStartDate = toDateInput(contract.startDate);
      const contractEndDate = toDateInput(contract.endDate);
      
      if (occupant.moveInDate < contractStartDate || occupant.moveInDate > contractEndDate) {
        return `Ngày dọn vào phải nằm trong khoảng từ ${new Date(contractStartDate).toLocaleDateString('vi-VN')} đến ${new Date(contractEndDate).toLocaleDateString('vi-VN')}.`;
      }
    }

    if (occupant.hasAccount) {
      if (!occupant.email.trim()) {
        return 'Cần nhập email tài khoản.';
      }

      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(occupant.email.trim())) {
        return 'Email tài khoản chưa đúng định dạng.';
      }

      return null;
    }

    if (!occupant.fullName.trim()) {
      return 'Cần nhập họ tên người ở.';
    }

    if (!occupant.phoneNumber.trim()) {
      return 'Cần nhập số điện thoại người ở.';
    }

    if (!occupant.dateOfBirth) {
      return 'Cần nhập ngày sinh người ở.';
    }

    if (
      !occupant.documentType.trim() ||
      !occupant.documentNumber.trim() ||
      !occupant.frontMediaAssetId
    ) {
      return 'Người ở nhập thủ công cần có loại giấy tờ, số giấy tờ và ảnh mặt trước.';
    }

    return null;
  };

  const saveOccupant = async (id: string) => {
    const occupant = occupants.find((item) => item.id === id);
    if (!occupant) {
      return;
    }

    const validationError = validateOccupant(occupant);
    if (validationError) {
      setOccupantSaveFeedback(id, validationError, 'error');
      return;
    }

    setToast(null);
    setSavingOccupantId(id);

    try {
      if (occupant.hasAccount) {
        const query = new URLSearchParams({ email: occupant.email.trim() });
        const lookup = await apiClient<ApiResponse<OccupantAccountLookupResponse>>(
          `${ENDPOINTS.USERS.OCCUPANT_LOOKUP}?${query.toString()}`,
          {
            method: 'GET',
            auth: true
          }
        );
        const lookupData = lookup.data;
        const exists = lookupData.exists ?? lookupData.Exists ?? false;
        const isKycApproved = lookupData.isKycApproved ?? lookupData.IsKycApproved ?? false;
        const displayName = lookupData.displayName ?? lookupData.DisplayName ?? occupant.email.trim();

        if (!exists) {
          setOccupantSaveFeedback(id, `Không tìm thấy tài khoản ${occupant.email.trim()}.`, 'error');
          return;
        }

        if (!isKycApproved) {
          setOccupantSaveFeedback(id, `Tài khoản ${occupant.email.trim()} chưa KYC thành công.`, 'error');
          return;
        }

        setOccupantSaveFeedback(id, `Đã lưu thành công tài khoản ${displayName}.`, 'success', true);
        return;
      }

      setOccupantSaveFeedback(id, 'Đã lưu thành công thông tin người ở này.', 'success', true);
    } catch (err) {
      setOccupantSaveFeedback(id, getApiErrorMessage(err, 'Không thể kiểm tra tài khoản người ở.'), 'error');
    } finally {
      setSavingOccupantId(null);
    }
  };

  const uploadDocumentImage = async (
    occupantId: string,
    field: 'front' | 'back' | 'extra',
    file: File | null
  ) => {
    if (!file) {
      return;
    }

    const uploadKey = `${occupantId}:${field}`;
    setUploadingField(uploadKey);
    setToast(null);

    try {
      const uploaded = await uploadImage(file, 'LegalDocument');
      updateOccupant(occupantId, `${field}MediaAssetId` as keyof OccupantForm, uploaded.mediaAssetId || null);
      updateOccupant(occupantId, `${field}ImageUrl` as keyof OccupantForm, uploaded.url);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể tải ảnh giấy tờ lên.'), type: 'error' });
    } finally {
      setUploadingField(null);
    }
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!contractId) {
      setToast({ message: 'Không tìm thấy mã hợp đồng.', type: 'error' });
      return;
    }

    const invalidOccupant = occupants.find((occupant) => validateOccupant(occupant));

    if (invalidOccupant) {
      setToast({ message: validateOccupant(invalidOccupant) ?? 'Vui lòng kiểm tra lại thông tin người ở.', type: 'error' });
      return;
    }

    const unsavedOccupant = occupants.find((occupant) => !occupant.isMainTenant && !occupant.isSaved);
    if (unsavedOccupant) {
      setToast({ message: 'Vui lòng bấm lưu từng người ở trước khi gửi danh sách cho chủ trọ.', type: 'error' });
      return;
    }

    setToast(null);
    setSubmitting(true);

    try {
      await contractApi.submitContractOccupants(contractId, {
        occupants: occupants.map((occupant) => ({
          email: occupant.hasAccount ? occupant.email.trim() : null,
          fullName: !occupant.hasAccount ? occupant.fullName.trim() : null,
          phoneNumber: !occupant.hasAccount ? occupant.phoneNumber.trim() : null,
          dateOfBirth: !occupant.hasAccount ? occupant.dateOfBirth : null,
          relationshipToMainTenant: occupant.relationship.trim(),
          moveInDate: occupant.moveInDate,
          document: !occupant.hasAccount
            ? {
                documentType: occupant.documentType,
                documentNumber: occupant.documentNumber.trim(),
                frontMediaAssetId: occupant.frontMediaAssetId || null,
                backMediaAssetId: occupant.backMediaAssetId || null,
                extraMediaAssetId: occupant.extraMediaAssetId || null
              }
            : null
        }))
      });

      onSuccess();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không thể lưu thông tin. Vui lòng kiểm tra lại các trường bắt buộc.'), type: 'error' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="occupants-setup-overlay" onClick={onClose}>
      <div className="occupants-setup-container-modal" onClick={(e) => e.stopPropagation()}>
        <header className="occupants-setup-header">
          <h2>Nhập thông tin người ở</h2>
          <button className="occupants-setup-close-btn" onClick={onClose} aria-label="Đóng">
            &times;
          </button>
        </header>

        <div className="occupants-setup-modal-content">
        <p style={{ color: '#475569', fontSize: '0.95rem', margin: 0, paddingBottom: 8 }}>
          Khai báo danh sách người sẽ sinh sống tại phòng thuê. Nếu yêu cầu thuê dự kiến {expectedOccupantCount ?? 'nhiều'} người, hệ thống sẽ giữ người thuê chính và tạo sẵn các ô người ở cùng cần nhập.
        </p>

        {contract && (
          <Alert type="info">
            Hợp đồng {contract.contractNumber} - Phòng {contract.roomNumber}, {contract.roomingHouseName}
          </Alert>
        )}



        {loading ? (
          <p>Đang tải thông tin người ở...</p>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
            <div className="occupants-list">
              {occupants.map((occupant, index) => (
                <div key={occupant.id} className="occupant-card">
                  <div className="occupant-card-header">
                    <h3>{occupant.isMainTenant ? 'Người đại diện thuê' : `Người ở cùng #${getCoOccupantIndex(occupants, index)}`}</h3>
                    <span className={`occupant-save-status ${occupant.isSaved ? 'saved' : 'draft'}`}>
                      {occupant.isMainTenant ? 'Người thuê chính' : occupant.isSaved ? 'Đã lưu' : 'Chưa lưu'}
                    </span>
                    {!occupant.isMainTenant && !expectedOccupantCount && (
                      <button type="button" className="remove-btn" onClick={() => removeOccupant(occupant.id)}>
                        Xóa
                      </button>
                    )}
                  </div>

                  {occupant.saveMessage && (
                    <div className={`occupant-save-message ${occupant.saveMessageType}`}>
                      {occupant.saveMessage}
                    </div>
                  )}

                  <div className="occupant-grid">
                    {!occupant.isMainTenant && (
                      <div className="form-group full-width">
                        <label className="checkbox-label">
                          <input
                            type="checkbox"
                            checked={occupant.hasAccount}
                            onChange={(event) => updateOccupant(occupant.id, 'hasAccount', event.target.checked)}
                          />
                          Người này đã có tài khoản trên hệ thống
                        </label>
                      </div>
                    )}

                    {occupant.hasAccount ? (
                      <div className="form-group full-width">
                        <label>Email tài khoản *</label>
                        <input
                          type="email"
                          required
                          value={occupant.email}
                          disabled={occupant.isMainTenant}
                          onChange={(event) => updateOccupant(occupant.id, 'email', event.target.value)}
                          placeholder="user@example.com"
                        />
                      </div>
                    ) : (
                      <>
                        <div className="form-group">
                          <label>Họ và tên *</label>
                          <input
                            type="text"
                            required
                            value={occupant.fullName}
                            onChange={(event) => updateOccupant(occupant.id, 'fullName', event.target.value)}
                            placeholder="Nguyễn Văn A"
                          />
                        </div>

                        <div className="form-group">
                          <label>Số điện thoại *</label>
                          <input
                            type="tel"
                            required
                            value={occupant.phoneNumber}
                            onChange={(event) => updateOccupant(occupant.id, 'phoneNumber', event.target.value)}
                            placeholder="0912345678"
                          />
                        </div>

                        <div className="form-group">
                          <label>Ngày sinh *</label>
                          <input
                            type="date"
                            required
                            value={occupant.dateOfBirth}
                            onChange={(event) => updateOccupant(occupant.id, 'dateOfBirth', event.target.value)}
                          />
                        </div>

                        <div className="form-group">
                          <label>Loại giấy tờ *</label>
                          <select
                            required
                            value={occupant.documentType}
                            onChange={(event) => updateOccupant(occupant.id, 'documentType', event.target.value)}
                          >
                            <option value="CCCD">Căn cước công dân</option>
                            <option value="CMND">Chứng minh nhân dân</option>
                            <option value="Passport">Hộ chiếu</option>
                            <option value="BirthCertificate">Giấy khai sinh</option>
                          </select>
                        </div>

                        <div className="form-group full-width">
                          <label>Số giấy tờ *</label>
                          <input
                            type="text"
                            required
                            value={occupant.documentNumber}
                            onChange={(event) => updateOccupant(occupant.id, 'documentNumber', event.target.value)}
                            placeholder="Nhập lại số giấy tờ"
                          />
                        </div>

                        <DocumentImageUploadField
                          label="Ảnh mặt trước giấy tờ"
                          required
                          imageUrl={occupant.frontImageUrl}
                          uploading={uploadingField === `${occupant.id}:front`}
                          onUpload={(file) => void uploadDocumentImage(occupant.id, 'front', file)}
                          onRemove={() => {
                            updateOccupant(occupant.id, 'frontMediaAssetId', null);
                            updateOccupant(occupant.id, 'frontImageUrl', '');
                          }}
                        />

                        <DocumentImageUploadField
                          label="Ảnh mặt sau giấy tờ"
                          imageUrl={occupant.backImageUrl}
                          uploading={uploadingField === `${occupant.id}:back`}
                          onUpload={(file) => void uploadDocumentImage(occupant.id, 'back', file)}
                          onRemove={() => {
                            updateOccupant(occupant.id, 'backMediaAssetId', null);
                            updateOccupant(occupant.id, 'backImageUrl', '');
                          }}
                        />

                        <DocumentImageUploadField
                          label="Ảnh bổ sung"
                          imageUrl={occupant.extraImageUrl}
                          uploading={uploadingField === `${occupant.id}:extra`}
                          onUpload={(file) => void uploadDocumentImage(occupant.id, 'extra', file)}
                          onRemove={() => {
                            updateOccupant(occupant.id, 'extraMediaAssetId', null);
                            updateOccupant(occupant.id, 'extraImageUrl', '');
                          }}
                        />
                      </>
                    )}

                    <div className="form-group">
                      <label>Quan hệ với chủ hợp đồng *</label>
                      <input
                        type="text"
                        required
                        value={occupant.relationship}
                        disabled={occupant.isMainTenant}
                        onChange={(event) => updateOccupant(occupant.id, 'relationship', event.target.value)}
                      />
                    </div>

                    <div className="form-group">
                      <label>Ngày dọn vào *</label>
                      <input
                        type="date"
                        required
                        value={occupant.moveInDate}
                        onChange={(event) => updateOccupant(occupant.id, 'moveInDate', event.target.value)}
                        min={contract ? toDateInput(contract.startDate) : undefined}
                        max={contract ? toDateInput(contract.endDate) : undefined}
                      />
                    </div>

                    {!occupant.isMainTenant && (
                      <div className="form-group full-width occupant-card-actions">
                        <Button type="button" variant="secondary" onClick={() => saveOccupant(occupant.id)}>
                          {savingOccupantId === occupant.id ? 'Đang lưu...' : 'Lưu người này'}
                        </Button>
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>

            <div className="setup-actions">
              {!expectedOccupantCount && (
                <Button type="button" variant="secondary" onClick={addOccupant}>
                  + Thêm người ở cùng
                </Button>
              )}
              {expectedOccupantCount && (
                <span className="occupants-count-hint">
                  Đã tạo {Math.max(expectedOccupantCount - 1, 0)} ô người ở cùng ngoài người thuê chính.
                </span>
              )}
              <div className="submit-wrapper">
                <Button type="submit" disabled={submitting}>
                  {submitting ? 'Đang gửi...' : 'Hoàn tất & gửi chủ trọ'}
                </Button>
              </div>
            </div>
          </form>
        )}
        </div>
      </div>
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}

interface DocumentImageUploadFieldProps {
  label: string;
  imageUrl?: string;
  required?: boolean;
  uploading: boolean;
  onUpload: (file: File | null) => void;
  onRemove: () => void;
}

function DocumentImageUploadField({
  label,
  imageUrl,
  required = false,
  uploading,
  onUpload,
  onRemove
}: DocumentImageUploadFieldProps) {
  const previewSrc = imageUrl || '';
  return (
    <div className="form-group">
      <label>
        {label} {required ? '*' : ''}
      </label>
      {previewSrc ? (
        <div style={{ display: 'grid', gap: 8 }}>
          <PrivateMediaImage
            source={previewSrc}
            alt={label}
            style={{
              width: '100%',
              maxHeight: 140,
              objectFit: 'cover',
              borderRadius: 8,
              border: '1px solid #e2e8f0'
            }}
          />
        </div>
      ) : (
        <div
          style={{
            height: 96,
            display: 'grid',
            placeItems: 'center',
            border: '1px dashed #cbd5e1',
            borderRadius: 8,
            color: '#64748b',
            background: '#f8fafc'
          }}
        >
          Chưa tải ảnh
        </div>
      )}
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <label style={{ 
          cursor: uploading ? 'wait' : 'pointer', 
          fontSize: '0.85rem', 
          fontWeight: 500, 
          color: '#2563eb',
          textDecoration: 'underline'
        }}>
          {uploading ? 'Đang tải...' : previewSrc ? 'Thay ảnh' : 'Tải ảnh'}
          <input
            type="file"
            accept="image/*"
            disabled={uploading}
            style={{ display: 'none' }}
            onChange={(event) => {
              onUpload(event.target.files?.[0] ?? null);
              event.target.value = '';
            }}
          />
        </label>
        {previewSrc && (
          <button type="button" className="remove-btn" onClick={onRemove} disabled={uploading}>
            Xóa
          </button>
        )}
      </div>
      {required && <input value={previewSrc} onChange={() => undefined} required style={{ display: 'none' }} />}
    </div>
  );
}
