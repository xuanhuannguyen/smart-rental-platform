import { FormEvent, useState } from 'react';
import { contractApi } from '../api';
import type {
  ContractAppendixChangeRequest,
  ContractAppendixResponse,
  ContractDetailResponse
} from '../types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import '../../rental-history/pages/TenantRentalHistoryDetailPage.css';

type LandlordAppendixChangeMode = 'monthlyRent' | 'paymentDay';

interface LandlordAppendixChangeForm {
  id: string;
  mode: LandlordAppendixChangeMode;
  value: string;
}

export function LandlordCreateAppendixModalV2({
  contract,
  appendix,
  onClose,
  onCreated,
}: {
  contract: ContractDetailResponse;
  appendix?: ContractAppendixResponse;
  onClose: () => void;
  onCreated: (appendix: ContractAppendixResponse) => void;
}) {
  const today = new Date().toISOString().slice(0, 10);
  const [effectiveDate, setEffectiveDate] = useState(appendix?.effectiveDate ?? today);
  const [changes, setChanges] = useState<LandlordAppendixChangeForm[]>(() =>
    buildLandlordAppendixChangeForms(appendix, contract)
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function addChange() {
    setChanges((current) => {
      const mode: LandlordAppendixChangeMode = current.some((item) => item.mode === 'monthlyRent')
        ? 'paymentDay'
        : 'monthlyRent';

      return [...current, createLandlordAppendixChange(mode, getLandlordAppendixDefaultValue(mode, contract))];
    });
  }

  function removeChange(id: string) {
    setChanges((current) => current.filter((item) => item.id !== id));
  }

  function updateChange(id: string, patch: Partial<LandlordAppendixChangeForm>) {
    setChanges((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const payloadChanges: ContractAppendixChangeRequest[] = [];
    const seenModes = new Set<LandlordAppendixChangeMode>();

    for (let index = 0; index < changes.length; index++) {
      const change = changes[index];
      const number = index + 1;

      if (seenModes.has(change.mode)) {
        setError(`Thay đổi #${number}: loại thay đổi bị trùng.`);
        return;
      }

      seenModes.add(change.mode);

      if (change.mode === 'monthlyRent') {
        const monthlyRent = Number.parseFloat(change.value);
        if (!Number.isFinite(monthlyRent) || monthlyRent <= 0) {
          setError(`Thay đổi #${number}: giá thuê mới phải lớn hơn 0.`);
          return;
        }

        if (!appendix && monthlyRent === contract.monthlyRent) {
          setError(`Thay đổi #${number}: giá thuê mới phải khác giá thuê hiện tại.`);
          return;
        }

        payloadChanges.push({
          changeType: 'Update',
          targetType: 'Contract',
          fieldName: 'monthlyRent',
          newValue: String(monthlyRent),
        });
        continue;
      }

      const paymentDay = Number.parseInt(change.value, 10);
      if (!Number.isInteger(paymentDay) || paymentDay < 1 || paymentDay > 28) {
        setError(`Thay đổi #${number}: ngày thanh toán mới phải nằm trong khoảng 1 đến 28.`);
        return;
      }

      if (!appendix && paymentDay === contract.paymentDay) {
        setError(`Thay đổi #${number}: ngày thanh toán mới phải khác ngày hiện tại.`);
        return;
      }

      payloadChanges.push({
        changeType: 'Update',
        targetType: 'Contract',
        fieldName: 'paymentDay',
        newValue: String(paymentDay),
      });
    }

    if (payloadChanges.length === 0) {
      setError('Vui lòng thêm ít nhất một thay đổi trước khi gửi phụ lục.');
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);

      const payload = {
        effectiveDate,
        changes: payloadChanges,
      };
      const response = appendix
        ? await contractApi.updateAppendix(contract.id, appendix.id, payload)
        : await contractApi.createAppendix(contract.id, payload);

      onCreated(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo phụ lục. Vui lòng kiểm tra lại thông tin.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="occupants-setup-overlay">
      <div className="occupants-setup-container-modal">
        <div className="occupants-setup-header">
          <h2>{appendix ? 'Sửa phụ lục' : 'Tạo phụ lục'}</h2>
          <button type="button" className="occupants-setup-close-btn" onClick={onClose}>&times;</button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
          <div className="occupants-setup-modal-content">
            <div className="form-group" style={{ marginBottom: '24px' }}>
              <label>Ngày hiệu lực</label>
              <input
                type="date"
                value={effectiveDate}
                min={today}
                onChange={(event) => setEffectiveDate(event.target.value)}
                required
              />
            </div>

            <div className="occupants-list">
              {changes.map((change, index) => (
                <div key={change.id} className="occupant-card">
                  <div className="occupant-card-header">
                    <h3 style={{ margin: 0, fontSize: '1rem' }}>Thay đổi #{index + 1}</h3>
                    {changes.length > 1 && (
                      <button type="button" className="remove-btn" onClick={() => removeChange(change.id)}>
                        Xóa
                      </button>
                    )}
                  </div>

                  <div className="form-group">
                    <label>Loại thay đổi</label>
                    <select
                      value={change.mode}
                      onChange={(event) => {
                        const mode = event.target.value as LandlordAppendixChangeMode;
                        updateChange(change.id, {
                          mode,
                          value: getLandlordAppendixDefaultValue(mode, contract)
                        });
                      }}
                    >
                      <option value="monthlyRent">Thay đổi giá thuê</option>
                      <option value="paymentDay">Thay đổi ngày thanh toán</option>
                    </select>
                  </div>

                  {change.mode === 'monthlyRent' ? (
                    <div className="form-group">
                      <label>Giá thuê hàng tháng</label>
                      <input
                        type="number"
                        value={change.value}
                        onChange={(event) => updateChange(change.id, { value: event.target.value })}
                        required
                      />
                    </div>
                  ) : (
                    <div className="form-group">
                      <label>Ngày thanh toán hàng tháng</label>
                      <input
                        type="number"
                        min="1"
                        max="28"
                        value={change.value}
                        onChange={(event) => updateChange(change.id, { value: event.target.value })}
                        required
                      />
                    </div>
                  )}
                </div>
              ))}
            </div>

            {error && (
              <p style={{ color: '#dc2626', marginTop: '12px', fontSize: '0.95rem' }}>
                {error}
              </p>
            )}
          </div>

          <div className="setup-actions" style={{ padding: '16px 24px', backgroundColor: '#fff', borderRadius: '0 0 16px 16px', columnGap: '12px' }}>
            <Button
              variant="secondary"
              type="button"
              onClick={addChange}
              disabled={isSubmitting || changes.length >= 2}
              style={{ marginRight: 'auto' }}
            >
              + Thêm thay đổi
            </Button>
            <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'Đang lưu...' : 'Gửi phụ lục'}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function createLandlordAppendixChange(
  mode: LandlordAppendixChangeMode,
  value: string
): LandlordAppendixChangeForm {
  return {
    id: crypto.randomUUID(),
    mode,
    value,
  };
}

function buildLandlordAppendixChangeForms(
  appendix: ContractAppendixResponse | undefined,
  contract: ContractDetailResponse
): LandlordAppendixChangeForm[] {
  if (!appendix) {
    return [createLandlordAppendixChange('monthlyRent', String(contract.monthlyRent))];
  }

  const forms = appendix.changes
    .filter((change) => change.changeType === 'Update' && change.targetType === 'Contract')
    .map((change) => {
      const fieldName = normalizeLandlordAppendixFieldName(change.fieldName);
      if (fieldName === 'paymentday') {
        return createLandlordAppendixChange('paymentDay', parseAppendixScalarValue(change.newValue) || String(contract.paymentDay));
      }

      if (fieldName === 'monthlyrent') {
        return createLandlordAppendixChange('monthlyRent', parseAppendixScalarValue(change.newValue) || String(contract.monthlyRent));
      }

      return null;
    })
    .filter((change): change is LandlordAppendixChangeForm => Boolean(change));

  return forms.length > 0 ? forms : [createLandlordAppendixChange('monthlyRent', String(contract.monthlyRent))];
}

function getLandlordAppendixDefaultValue(mode: LandlordAppendixChangeMode, contract: ContractDetailResponse) {
  return mode === 'monthlyRent' ? String(contract.monthlyRent) : String(contract.paymentDay);
}

function normalizeLandlordAppendixFieldName(value?: string | null) {
  return value?.replace(/_/g, '').toLowerCase() ?? '';
}

function parseAppendixScalarValue(value?: string | null) {
  if (!value) return '';

  try {
    const parsed = JSON.parse(value);
    return parsed !== null && parsed !== undefined ? String(parsed) : '';
  } catch {
    return value;
  }
}
