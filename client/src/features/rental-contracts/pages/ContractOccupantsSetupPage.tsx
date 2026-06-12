import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { uploadImage } from '../../files/api';
import { contractApi } from '../../contracts/api';
import type { ContractDetailResponse, ContractOccupantResponse } from '../../contracts/types';
import './ContractOccupantsSetupPage.css';

interface OccupantForm {
  id: string;
  isMainTenant: boolean;
  hasAccount: boolean;
  email: string;
  fullName: string;
  phoneNumber: string;
  dateOfBirth: string;
  relationship: string;
  moveInDate: string;
  documentType: string;
  documentNumber: string;
  frontImageObjectKey: string;
  backImageObjectKey: string;
  extraImageObjectKey: string;
}

function toDateInput(value?: string | null) {
  if (!value) {
    return new Date().toISOString().split('T')[0];
  }

  return value.slice(0, 10);
}

function createMainTenantForm(email?: string | null): OccupantForm {
  return {
    id: crypto.randomUUID(),
    isMainTenant: true,
    hasAccount: true,
    email: email ?? '',
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '',
    relationship: 'Chủ hợp đồng',
    moveInDate: toDateInput(),
    documentType: 'CCCD',
    documentNumber: '',
    frontImageObjectKey: '',
    backImageObjectKey: '',
    extraImageObjectKey: ''
  };
}

function createEmptyOccupantForm(moveInDate: string): OccupantForm {
  return {
    id: crypto.randomUUID(),
    isMainTenant: false,
    hasAccount: false,
    email: '',
    fullName: '',
    phoneNumber: '',
    dateOfBirth: '',
    relationship: 'Người ở cùng',
    moveInDate,
    documentType: 'CCCD',
    documentNumber: '',
    frontImageObjectKey: '',
    backImageObjectKey: '',
    extraImageObjectKey: ''
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
    email: hasAccount ? occupant.email ?? (isMainTenant ? currentUserEmail : null) ?? '' : '',
    fullName: hasAccount ? '' : occupant.fullName ?? '',
    phoneNumber: hasAccount ? '' : occupant.phoneNumber ?? '',
    dateOfBirth: hasAccount ? '' : toDateInput(occupant.dateOfBirth),
    relationship: occupant.relationshipToMainTenant ?? '',
    moveInDate: toDateInput(occupant.moveInDate),
    documentType: occupant.document?.documentType ?? 'CCCD',
    documentNumber: '',
    frontImageObjectKey: occupant.document?.frontImageObjectKey ?? '',
    backImageObjectKey: occupant.document?.backImageObjectKey ?? '',
    extraImageObjectKey: occupant.document?.extraImageObjectKey ?? ''
  };
}

export function ContractOccupantsSetupPage() {
  const { id: contractId } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [occupants, setOccupants] = useState<OccupantForm[]>([]);
  const [uploadingField, setUploadingField] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let isMounted = true;

    async function loadContract() {
      if (!contractId) {
        setError('Không tìm thấy mã hợp đồng.');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError('');

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

        setOccupants(mappedOccupants);
      } catch (err) {
        if (isMounted) {
          setError(getApiErrorMessage(err, 'Không thể tải thông tin hợp đồng.'));
          setOccupants([createMainTenantForm(currentUser?.email)]);
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
  }, [contractId, currentUser?.email, currentUser?.userId]);

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

  const updateOccupant = (id: string, field: keyof OccupantForm, value: string | boolean) => {
    setOccupants((current) =>
      current.map((occupant) => (occupant.id === id ? { ...occupant, [field]: value } : occupant))
    );
  };

  const uploadDocumentImage = async (
    occupantId: string,
    field: 'frontImageObjectKey' | 'backImageObjectKey' | 'extraImageObjectKey',
    file: File | null
  ) => {
    if (!file) {
      return;
    }

    const uploadKey = `${occupantId}:${field}`;
    setUploadingField(uploadKey);
    setError('');

    try {
      const uploaded = await uploadImage(file, 'KycDocument');
      updateOccupant(occupantId, field, uploaded.objectKey);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải ảnh giấy tờ lên.'));
    } finally {
      setUploadingField(null);
    }
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!contractId) {
      setError('Không tìm thấy mã hợp đồng.');
      return;
    }

    const missingDocumentNumber = occupants.some(
      (occupant) => !occupant.hasAccount && !occupant.documentNumber.trim()
    );

    if (missingDocumentNumber) {
      setError('Người ở chưa có tài khoản cần nhập lại số giấy tờ trước khi gửi.');
      return;
    }

    setError('');
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
                frontImageObjectKey: occupant.frontImageObjectKey.trim(),
                backImageObjectKey: occupant.backImageObjectKey.trim() || null,
                extraImageObjectKey: occupant.extraImageObjectKey.trim() || null
              }
            : null
        }))
      });

      navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS, {
        state: { message: 'Đã gửi thông tin người ở thành công.' }
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể lưu thông tin. Vui lòng kiểm tra lại các trường bắt buộc.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="occupants-setup-container">
      <div className="setup-header">
        <button className="back-btn" onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_REQUESTS)}>
          Quay lại
        </button>
        <h2>Nhập thông tin người ở</h2>
        <p className="setup-subtitle">
          Khai báo danh sách người sẽ sinh sống tại phòng thuê để chủ trọ tạo và ký hợp đồng.
        </p>
      </div>

      {contract && (
        <div style={{ marginBottom: 20 }}>
          <Alert type="info">
            Hợp đồng {contract.contractNumber} - Phòng {contract.roomNumber}, {contract.roomingHouseName}
          </Alert>
        </div>
      )}

      {error && (
        <div style={{ marginBottom: 20 }}>
          <Alert type="error">{error}</Alert>
        </div>
      )}

      {loading ? (
        <p>Đang tải thông tin người ở...</p>
      ) : (
        <form onSubmit={handleSubmit}>
          <div className="occupants-list">
            {occupants.map((occupant, index) => (
              <div key={occupant.id} className="occupant-card">
                <div className="occupant-card-header">
                  <h3>{occupant.isMainTenant ? 'Người đại diện thuê' : `Người ở cùng #${index + 1}`}</h3>
                  {!occupant.isMainTenant && (
                    <button type="button" className="remove-btn" onClick={() => removeOccupant(occupant.id)}>
                      Xóa
                    </button>
                  )}
                </div>

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

                      <div className="form-group">
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
                        objectKey={occupant.frontImageObjectKey}
                        uploading={uploadingField === `${occupant.id}:frontImageObjectKey`}
                        onUpload={(file) => void uploadDocumentImage(occupant.id, 'frontImageObjectKey', file)}
                        onRemove={() => updateOccupant(occupant.id, 'frontImageObjectKey', '')}
                      />

                      <DocumentImageUploadField
                        label="Ảnh mặt sau giấy tờ"
                        objectKey={occupant.backImageObjectKey}
                        uploading={uploadingField === `${occupant.id}:backImageObjectKey`}
                        onUpload={(file) => void uploadDocumentImage(occupant.id, 'backImageObjectKey', file)}
                        onRemove={() => updateOccupant(occupant.id, 'backImageObjectKey', '')}
                      />

                      <DocumentImageUploadField
                        label="Ảnh bổ sung"
                        objectKey={occupant.extraImageObjectKey}
                        uploading={uploadingField === `${occupant.id}:extraImageObjectKey`}
                        onUpload={(file) => void uploadDocumentImage(occupant.id, 'extraImageObjectKey', file)}
                        onRemove={() => updateOccupant(occupant.id, 'extraImageObjectKey', '')}
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
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="setup-actions">
            <Button type="button" variant="secondary" onClick={addOccupant}>
              + Thêm người ở cùng
            </Button>
            <div className="submit-wrapper">
              <Button type="submit" disabled={submitting}>
                {submitting ? 'Đang gửi...' : 'Hoàn tất & gửi chủ trọ'}
              </Button>
            </div>
          </div>
        </form>
      )}
    </div>
  );
}

interface DocumentImageUploadFieldProps {
  label: string;
  objectKey: string;
  required?: boolean;
  uploading: boolean;
  onUpload: (file: File | null) => void;
  onRemove: () => void;
}

function DocumentImageUploadField({
  label,
  objectKey,
  required = false,
  uploading,
  onUpload,
  onRemove
}: DocumentImageUploadFieldProps) {
  return (
    <div className="form-group">
      <label>
        {label} {required ? '*' : ''}
      </label>
      {objectKey ? (
        <div style={{ display: 'grid', gap: 8 }}>
          <img
            src={toAssetUrl(objectKey)}
            alt={label}
            style={{
              width: '100%',
              maxHeight: 140,
              objectFit: 'cover',
              borderRadius: 8,
              border: '1px solid #e2e8f0'
            }}
          />
          <span style={{ fontSize: 12, color: '#64748b', wordBreak: 'break-all' }}>{objectKey}</span>
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
        <label className="back-btn" style={{ cursor: uploading ? 'wait' : 'pointer' }}>
          {uploading ? 'Đang tải...' : objectKey ? 'Thay ảnh' : 'Tải ảnh'}
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
        {objectKey && (
          <button type="button" className="remove-btn" onClick={onRemove} disabled={uploading}>
            Xóa
          </button>
        )}
      </div>
      {required && <input value={objectKey} onChange={() => undefined} required style={{ display: 'none' }} />}
    </div>
  );
}
