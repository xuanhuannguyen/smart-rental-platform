import React, { useMemo, useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { buildPrivateMediaViewUrl } from '../../../shared/api/media';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { Button } from '../../../shared/components/ui/Button';
import { uploadImage } from '../../files/api';
import { contractApi } from '../../contracts/api';
import type {
  ContractAppendixChangeRequest,
  ContractAppendixResponse,
  ContractHistoryItemResponse,
  ContractOccupantResponse
} from '../../contracts/types';
import '../../rental-contracts/components/ContractOccupantsSetupModal.css';

type AppendixMode = 'addOccupant' | 'removeOccupant' | 'transferMainTenant' | 'renewContract';

interface AppendixChangeForm {
  id: string;
  mode: AppendixMode;
  newOccupantHasVerifiedAccount: boolean;
  newOccupantEmail: string;
  newOccupantFullName: string;
  newOccupantPhoneNumber: string;
  newOccupantDateOfBirth: string;
  newOccupantRelationship: string;
  newOccupantMoveInDate: string;
  transferNewOccupantToMainTenant: boolean;
  documentType: string;
  documentNumber: string;
  frontMediaAssetId: string | null;
  frontImageUrl: string;
  backMediaAssetId: string | null;
  backImageUrl: string;
  extraMediaAssetId: string | null;
  extraImageUrl: string;
  removeOccupantId: string;
  newMainTenantUserId: string;
  currentMainTenantLeaves: boolean;
  newEndDate: string;
}

function createEmptyChange(today: string, defaultEndDate: string): AppendixChangeForm {
  return {
    id: crypto.randomUUID(),
    mode: 'addOccupant',
    newOccupantHasVerifiedAccount: true,
    newOccupantEmail: '',
    newOccupantFullName: '',
    newOccupantPhoneNumber: '',
    newOccupantDateOfBirth: '',
    newOccupantRelationship: '',
    newOccupantMoveInDate: today,
    transferNewOccupantToMainTenant: false,
    documentType: 'CCCD',
    documentNumber: '',
    frontMediaAssetId: null,
    frontImageUrl: '',
    backMediaAssetId: null,
    backImageUrl: '',
    extraMediaAssetId: null,
    extraImageUrl: '',
    removeOccupantId: '',
    newMainTenantUserId: '',
    currentMainTenantLeaves: false,
    newEndDate: defaultEndDate
  };
}

function buildChangeFormsFromAppendix(
  appendix: ContractAppendixResponse,
  today: string,
  contract: ContractHistoryItemResponse
): AppendixChangeForm[] {
  const forms: AppendixChangeForm[] = [];
  const consumedChangeIds = new Set<string>();
  const sortedChanges = [...appendix.changes].sort((a, b) => a.sortOrder - b.sortOrder);
  const mainTenantTransferChange = sortedChanges.find(
    (change) =>
      change.changeType === 'Update' &&
      change.targetType === 'Contract' &&
      normalizeFieldName(change.fieldName) === 'maintenantuserid'
  );
  const mainTenantTransferEmail = mainTenantTransferChange
    ? parseUserReferenceEmail(mainTenantTransferChange.newValue)
    : '';
  const currentMainTenantRemoveChange = sortedChanges.find(
    (change) =>
      change.changeType === 'Remove' &&
      change.targetType === 'ContractOccupant' &&
      change.targetId === contract.currentUserOccupantId
  );

  if (mainTenantTransferChange && !mainTenantTransferEmail) {
    consumedChangeIds.add(mainTenantTransferChange.id);
    if (currentMainTenantRemoveChange) {
      consumedChangeIds.add(currentMainTenantRemoveChange.id);
    }

    forms.push({
      ...createEmptyChange(today, contract.endDate),
      mode: 'transferMainTenant',
      newMainTenantUserId: parseJsonScalar(mainTenantTransferChange.newValue),
      currentMainTenantLeaves: Boolean(currentMainTenantRemoveChange),
    });
  }

  for (const change of sortedChanges) {
    if (consumedChangeIds.has(change.id)) continue;

    if (change.changeType === 'Add' && change.targetType === 'ContractOccupant') {
      const form = buildAddOccupantForm(change.newValue, today, contract.endDate);
      if (
        mainTenantTransferChange &&
        mainTenantTransferEmail &&
        form.newOccupantEmail.trim().toLowerCase() === mainTenantTransferEmail.toLowerCase()
      ) {
        form.transferNewOccupantToMainTenant = true;
        consumedChangeIds.add(mainTenantTransferChange.id);
      }
      forms.push(form);
      continue;
    }

    if (change.changeType === 'Remove' && change.targetType === 'ContractOccupant') {
      forms.push({
        ...createEmptyChange(today, contract.endDate),
        mode: 'removeOccupant',
        removeOccupantId: change.targetId ?? '',
      });
      continue;
    }

    if (
      change.changeType === 'Update' &&
      change.targetType === 'Contract' &&
      normalizeFieldName(change.fieldName) === 'enddate'
    ) {
      forms.push({
        ...createEmptyChange(today, contract.endDate),
        mode: 'renewContract',
        newEndDate: parseJsonScalar(change.newValue) || contract.endDate,
      });
    }
  }

  return forms.length > 0 ? forms : [createEmptyChange(today, contract.endDate)];
}

function buildAddOccupantForm(newValue: string | null | undefined, today: string, defaultEndDate: string): AppendixChangeForm {
  const payload = parseJsonObject(newValue);
  const document = parseJsonObject(payload.document);
  const hasVerifiedAccount = Boolean(payload.email);
  const frontMediaAssetId = toFormString(document.frontMediaAssetId) || null;
  const backMediaAssetId = toFormString(document.backMediaAssetId) || null;
  const extraMediaAssetId = toFormString(document.extraMediaAssetId) || null;

  return {
    ...createEmptyChange(today, defaultEndDate),
    mode: 'addOccupant',
    newOccupantHasVerifiedAccount: hasVerifiedAccount,
    newOccupantEmail: toFormString(payload.email),
    newOccupantFullName: toFormString(payload.fullName),
    newOccupantPhoneNumber: toFormString(payload.phoneNumber),
    newOccupantDateOfBirth: toFormString(payload.dateOfBirth),
    newOccupantRelationship: toFormString(payload.relationshipToMainTenant),
    newOccupantMoveInDate: toFormString(payload.moveInDate) || today,
    documentType: toFormString(document.documentType) || 'CCCD',
    documentNumber: toFormString(document.documentNumber),
    frontMediaAssetId,
    frontImageUrl: frontMediaAssetId ? buildPrivateMediaViewUrl(frontMediaAssetId) : '',
    backMediaAssetId,
    backImageUrl: backMediaAssetId ? buildPrivateMediaViewUrl(backMediaAssetId) : '',
    extraMediaAssetId,
    extraImageUrl: extraMediaAssetId ? buildPrivateMediaViewUrl(extraMediaAssetId) : '',
  };
}

function parseJsonObject(value: unknown): Record<string, unknown> {
  if (!value) return {};
  if (typeof value === 'object' && !Array.isArray(value)) return value as Record<string, unknown>;
  if (typeof value !== 'string') return {};

  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function parseJsonScalar(value: unknown) {
  if (typeof value !== 'string') return '';

  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'string' ? parsed : '';
  } catch {
    return value;
  }
}

function parseUserReferenceEmail(value: unknown) {
  const payload = parseJsonObject(value);
  return toFormString(payload.email).trim();
}

function toFormString(value: unknown) {
  return typeof value === 'string' ? value : '';
}

function normalizeFieldName(value?: string | null) {
  return value?.replace(/_/g, '').toLowerCase() ?? '';
}

interface Props {
  contract: ContractHistoryItemResponse;
  appendix?: ContractAppendixResponse;
  onClose: () => void;
  onCreated?: (appendix: ContractAppendixResponse) => void;
}

export const CreateAppendixModal: React.FC<Props> = ({ contract, appendix, onClose, onCreated }) => {
  const today = new Date().toISOString().slice(0, 10);
  const activeOccupants = useMemo(
    () => contract.occupants.filter((occupant) => occupant.status === 'Active' || occupant.status === 'PendingMoveIn'),
    [contract.occupants]
  );
  
  const removableOccupants = activeOccupants;
  const mainTenantCandidates = activeOccupants;

  const [effectiveDate, setEffectiveDate] = useState(appendix?.effectiveDate ?? today);
  const [changes, setChanges] = useState<AppendixChangeForm[]>(() =>
    appendix ? buildChangeFormsFromAppendix(appendix, today, contract) : [createEmptyChange(today, contract.endDate)]
  );
  
  const [uploadingField, setUploadingField] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const addChange = () => {
    setChanges(prev => [...prev, createEmptyChange(today, contract.endDate)]);
  };

  const removeChange = (id: string) => {
    setChanges(prev => prev.filter(c => c.id !== id));
  };

  const updateChange = (id: string, field: keyof AppendixChangeForm, value: any) => {
    setChanges(prev => prev.map(c => c.id === id ? { ...c, [field]: value } : c));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      setIsSubmitting(true);
      setError(null);

      const builtChanges = buildChanges();
      
      const payload = {
        effectiveDate,
        changes: builtChanges
      };
      const response = appendix
        ? await contractApi.updateAppendix(contract.id, appendix.id, payload)
        : await contractApi.createAppendix(contract.id, payload);

      onCreated?.(response.data);
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err, err instanceof Error ? err.message : 'Không thể tạo phụ lục. Vui lòng kiểm tra lại thông tin.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const uploadDocumentImage = async (
    changeId: string,
    field: 'front' | 'back' | 'extra',
    file: File | null
  ) => {
    if (!file) {
      return;
    }

    setUploadingField(`${changeId}:${field}`);
    setError(null);

    try {
      const uploaded = await uploadImage(file, 'LegalDocument');
      updateChange(changeId, `${field}MediaAssetId` as keyof AppendixChangeForm, uploaded.mediaAssetId || null);
      updateChange(changeId, `${field}ImageUrl` as keyof AppendixChangeForm, uploaded.url);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải ảnh giấy tờ lên.'));
    } finally {
      setUploadingField(null);
    }
  };

  const buildChanges = (): ContractAppendixChangeRequest[] => {
    const payload: ContractAppendixChangeRequest[] = [];

    for (let i = 0; i < changes.length; i++) {
      const change = changes[i];
      const changeNum = i + 1;

      if (change.mode === 'addOccupant') {
        if (!change.newOccupantRelationship.trim() || !change.newOccupantMoveInDate) {
          throw new Error(`Thay đổi #${changeNum}: Vui lòng nhập quan hệ với người thuê chính và ngày chuyển vào.`);
        }

        const renewalChange = changes.find(c => c.mode === 'renewContract');
        const resolvedEndDate = renewalChange?.newEndDate || contract.endDate;
        const resolvedEndDateStr = toFormString(resolvedEndDate);

        if (change.newOccupantMoveInDate < effectiveDate || change.newOccupantMoveInDate > resolvedEndDateStr) {
          throw new Error(`Thay đổi #${changeNum}: Ngày chuyển vào của người ở mới phải nằm trong khoảng thời gian hiệu lực của phụ lục (từ ${new Date(effectiveDate).toLocaleDateString('vi-VN')} đến ${new Date(resolvedEndDateStr).toLocaleDateString('vi-VN')}).`);
        }

        if (change.newOccupantHasVerifiedAccount) {
          if (!change.newOccupantEmail.trim()) {
            throw new Error(`Thay đổi #${changeNum}: Vui lòng nhập email người ở mới.`);
          }

          payload.push({
            changeType: 'Add',
            targetType: 'ContractOccupant',
            newValue: JSON.stringify({
              email: change.newOccupantEmail.trim(),
              relationshipToMainTenant: change.newOccupantRelationship.trim(),
              moveInDate: change.newOccupantMoveInDate
            })
          });

          if (change.transferNewOccupantToMainTenant) {
            payload.push({
              changeType: 'Update',
              targetType: 'Contract',
              fieldName: 'mainTenantUserId',
              newValue: JSON.stringify({ email: change.newOccupantEmail.trim() })
            });
          }
        } else {
          if (
            !change.newOccupantFullName.trim() ||
            !change.newOccupantPhoneNumber.trim() ||
            !change.newOccupantDateOfBirth ||
            !change.documentType.trim() ||
            !change.documentNumber.trim() ||
            !change.frontMediaAssetId
          ) {
            throw new Error(`Thay đổi #${changeNum}: Vui lòng nhập đầy đủ thông tin cá nhân và ảnh mặt trước giấy tờ.`);
          }

          payload.push({
            changeType: 'Add',
            targetType: 'ContractOccupant',
            newValue: JSON.stringify({
              fullName: change.newOccupantFullName.trim(),
              phoneNumber: change.newOccupantPhoneNumber.trim(),
              dateOfBirth: change.newOccupantDateOfBirth,
              relationshipToMainTenant: change.newOccupantRelationship.trim(),
              moveInDate: change.newOccupantMoveInDate,
              document: {
                documentType: change.documentType,
                documentNumber: change.documentNumber.trim(),
                frontMediaAssetId: change.frontMediaAssetId || null,
                backMediaAssetId: change.backMediaAssetId || null,
                extraMediaAssetId: change.extraMediaAssetId || null
              }
            })
          });
        }
      } else if (change.mode === 'removeOccupant') {
        if (!change.removeOccupantId) {
          throw new Error(`Thay đổi #${changeNum}: Vui lòng chọn người ở cần rời đi.`);
        }

        payload.push({
          changeType: 'Remove',
          targetType: 'ContractOccupant',
          targetId: change.removeOccupantId,
          newValue: effectiveDate
        });
      } else if (change.mode === 'transferMainTenant') {
        if (!change.newMainTenantUserId) {
          throw new Error(`Thay đổi #${changeNum}: Vui lòng chọn người thuê chính mới.`);
        }

        if (change.currentMainTenantLeaves && contract.currentUserOccupantId) {
          payload.push({
            changeType: 'Remove',
            targetType: 'ContractOccupant',
            targetId: contract.currentUserOccupantId,
            newValue: effectiveDate
          });
        }

        payload.push({
          changeType: 'Update',
          targetType: 'Contract',
          fieldName: 'mainTenantUserId',
          newValue: change.newMainTenantUserId
        });
      } else if (change.mode === 'renewContract') {
        if (!change.newEndDate || change.newEndDate <= contract.endDate) {
          throw new Error(`Thay đổi #${changeNum}: Ngày kết thúc mới phải sau ngày kết thúc hiện tại.`);
        }

        payload.push({
          changeType: 'Update',
          targetType: 'Contract',
          fieldName: 'endDate',
          newValue: change.newEndDate
        });
      }
    }

    return payload;
  };

  return (
    <div className="occupants-setup-overlay">
      <div className="occupants-setup-container-modal">
        <div className="occupants-setup-header">
          <h2>{appendix ? 'Sửa phụ lục hợp đồng' : 'Tạo phụ lục hợp đồng'}</h2>
          <button type="button" className="occupants-setup-close-btn" onClick={onClose}>&times;</button>
        </div>
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
          <div className="occupants-setup-modal-content">
          <div className="form-group" style={{ marginBottom: '24px' }}>
            <label style={{ fontSize: '1.1rem', fontWeight: 600 }}>Ngày hiệu lực của toàn bộ phụ lục</label>
            <input
              type="date"
              value={effectiveDate}
              min={today > contract.startDate ? today : contract.startDate}
              max={contract.endDate}
              onChange={(e) => setEffectiveDate(e.target.value)}
              required
            />
          </div>

          <div className="occupants-list">
            {changes.map((change, index) => (
              <div key={change.id} className="occupant-card">
                <div className="occupant-card-header">
                  <h3>Thay đổi #{index + 1}</h3>
                  {changes.length > 1 && (
                    <button type="button" className="remove-btn" onClick={() => removeChange(change.id)}>Xóa</button>
                  )}
                </div>

                <div className="form-group">
                  <label>Loại thay đổi</label>
                  <select value={change.mode} onChange={(e) => updateChange(change.id, 'mode', e.target.value as AppendixMode)}>
                    <option value="addOccupant">Thêm người ở</option>
                    <option value="removeOccupant">Người ở rời đi</option>
                    <option value="transferMainTenant">Chuyển người thuê chính</option>
                    <option value="renewContract">Gia hạn hợp đồng</option>
                  </select>
                </div>

                {change.mode === 'addOccupant' && (
                  <>
                    <div className="form-group">
                      <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                        <input
                          type="checkbox"
                          checked={change.newOccupantHasVerifiedAccount}
                          onChange={(e) => updateChange(change.id, 'newOccupantHasVerifiedAccount', e.target.checked)}
                        />
                        Người ở có tài khoản đã xác minh danh tính
                      </label>
                    </div>

                    {change.newOccupantHasVerifiedAccount ? (
                      <>
                      <div className="form-group">
                        <label>Email người ở mới</label>
                        <input
                          type="email"
                          
                          placeholder="email@example.com"
                          value={change.newOccupantEmail}
                          onChange={(e) => updateChange(change.id, 'newOccupantEmail', e.target.value)}
                          required
                        />
                      </div>
                      <div className="form-group">
                        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                          <input
                            type="checkbox"
                            checked={change.transferNewOccupantToMainTenant}
                            onChange={(e) => updateChange(change.id, 'transferNewOccupantToMainTenant', e.target.checked)}
                          />
                          Chuyển giao người thuê chính cho người ở này
                        </label>
                      </div>
                      </>
                    ) : (
                      <>
                        <div className="form-group">
                          <label>Họ và tên</label>
                          <input
                            type="text"
                            
                            value={change.newOccupantFullName}
                            onChange={(e) => updateChange(change.id, 'newOccupantFullName', e.target.value)}
                            required
                          />
                        </div>
                        <div className="form-group">
                          <label>Số điện thoại</label>
                          <input
                            type="tel"
                            
                            value={change.newOccupantPhoneNumber}
                            onChange={(e) => updateChange(change.id, 'newOccupantPhoneNumber', e.target.value)}
                            required
                          />
                        </div>
                        <div className="form-group">
                          <label>Ngày sinh</label>
                          <input
                            type="date"
                            
                            value={change.newOccupantDateOfBirth}
                            onChange={(e) => updateChange(change.id, 'newOccupantDateOfBirth', e.target.value)}
                            required
                          />
                        </div>
                        <div className="form-group">
                          <label>Loại giấy tờ</label>
                          <select  value={change.documentType} onChange={(e) => updateChange(change.id, 'documentType', e.target.value)} required>
                            <option value="CCCD">Căn cước công dân</option>
                            <option value="CMND">Chứng minh nhân dân</option>
                            <option value="Passport">Hộ chiếu</option>
                            <option value="BirthCertificate">Giấy khai sinh</option>
                          </select>
                        </div>
                        <div className="form-group">
                          <label>Số giấy tờ</label>
                          <input
                            type="text"
                            
                            value={change.documentNumber}
                            onChange={(e) => updateChange(change.id, 'documentNumber', e.target.value)}
                            required
                          />
                        </div>
                        <DocumentImageUploadField
                          label="Ảnh mặt trước giấy tờ"
                          required
                          imageUrl={change.frontImageUrl}
                          uploading={uploadingField === `${change.id}:front`}
                          onUpload={(file) => void uploadDocumentImage(change.id, 'front', file)}
                          onRemove={() => {
                            updateChange(change.id, 'frontMediaAssetId', null);
                            updateChange(change.id, 'frontImageUrl', '');
                          }}
                        />
                        <DocumentImageUploadField
                          label="Ảnh mặt sau giấy tờ"
                          imageUrl={change.backImageUrl}
                          uploading={uploadingField === `${change.id}:back`}
                          onUpload={(file) => void uploadDocumentImage(change.id, 'back', file)}
                          onRemove={() => {
                            updateChange(change.id, 'backMediaAssetId', null);
                            updateChange(change.id, 'backImageUrl', '');
                          }}
                        />
                        <DocumentImageUploadField
                          label="Ảnh bổ sung"
                          imageUrl={change.extraImageUrl}
                          uploading={uploadingField === `${change.id}:extra`}
                          onUpload={(file) => void uploadDocumentImage(change.id, 'extra', file)}
                          onRemove={() => {
                            updateChange(change.id, 'extraMediaAssetId', null);
                            updateChange(change.id, 'extraImageUrl', '');
                          }}
                        />
                      </>
                    )}

                    <div className="form-group">
                      <label>Quan hệ với người thuê chính</label>
                      <input
                        type="text"
                        
                        placeholder="VD: Bạn cùng phòng, anh/chị/em..."
                        value={change.newOccupantRelationship}
                        onChange={(e) => updateChange(change.id, 'newOccupantRelationship', e.target.value)}
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>Ngày chuyển vào</label>
                      <input
                        type="date"
                        value={change.newOccupantMoveInDate}
                        onChange={(e) => updateChange(change.id, 'newOccupantMoveInDate', e.target.value)}
                        min={effectiveDate}
                        max={toFormString(changes.find(c => c.mode === 'renewContract')?.newEndDate || contract.endDate)}
                        required
                      />
                    </div>
                  </>
                )}

                {change.mode === 'removeOccupant' && (
                  <div className="form-group">
                    <label>Người ở cần rời đi</label>
                    <select  value={change.removeOccupantId} onChange={(e) => updateChange(change.id, 'removeOccupantId', e.target.value)} required>
                      <option value="">Chọn người ở</option>
                      {removableOccupants.map((occupant) => (
                        <option key={occupant.id} value={occupant.id}>
                          {formatOccupantName(occupant)}
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                {change.mode === 'transferMainTenant' && (
                  <>
                    <div className="form-group">
                      <label>Người thuê chính mới</label>
                      <select  value={change.newMainTenantUserId} onChange={(e) => updateChange(change.id, 'newMainTenantUserId', e.target.value)} required>
                        <option value="">Chọn người thuê chính mới</option>
                        {mainTenantCandidates
                          .filter((occupant) => occupant.id !== contract.currentUserOccupantId)
                          .map((occupant) => (
                            <option key={occupant.id} value={occupant.userId ?? ''}>
                              {formatOccupantName(occupant)}
                            </option>
                          ))}
                      </select>
                    </div>
                    {contract.currentUserOccupantId && (
                      <div className="form-group">
                        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                          <input
                            type="checkbox"
                            checked={change.currentMainTenantLeaves}
                            onChange={(e) => updateChange(change.id, 'currentMainTenantLeaves', e.target.checked)}
                          />
                          Người thuê chính hiện tại cũng rời đi từ ngày hiệu lực
                        </label>
                      </div>
                    )}
                  </>
                )}

                {change.mode === 'renewContract' && (
                  <div className="form-group">
                    <label>Ngày kết thúc mới</label>
                    <input
                      type="date"
                      
                      value={change.newEndDate}
                      min={contract.endDate}
                      onChange={(e) => updateChange(change.id, 'newEndDate', e.target.value)}
                      required
                    />
                  </div>
                )}
              </div>
            ))}
          </div>

          {error && (
            <p style={{ color: '#dc2626', marginTop: '16px', fontSize: '0.95rem', padding: '12px', background: '#fef2f2', borderRadius: '8px' }}>
              {error}
            </p>
          )}

          </div>

          <div className="setup-actions" style={{ padding: '16px 24px', backgroundColor: '#fff', borderRadius: '0 0 16px 16px' }}>
            <Button type="button" variant="secondary" onClick={addChange} disabled={isSubmitting}>
              + Thêm thay đổi
            </Button>
            <div className="submit-wrapper" style={{ display: 'flex', gap: '8px' }}>
              <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
              <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Đang lưu...' : 'Gửi phụ lục'}</Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};

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
        <label
          style={{
            cursor: uploading ? 'wait' : 'pointer',
            fontSize: '0.85rem',
            fontWeight: 500,
            color: '#2563eb',
            textDecoration: 'underline'
          }}
        >
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

function formatOccupantName(occupant: ContractOccupantResponse) {
  return occupant.email ? `${occupant.fullName} (${occupant.email})` : occupant.fullName;
}
