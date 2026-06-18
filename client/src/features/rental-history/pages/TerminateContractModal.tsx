import React, { useState } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { contractApi } from '../../contracts/api';
import type { ContractDetailResponse, ContractHistoryItemResponse } from '../../contracts/types';
import './HistoryModals.css';

interface Props {
  contract: ContractHistoryItemResponse | ContractDetailResponse;
  onClose: () => void;
  onTerminated?: (contract: ContractDetailResponse) => void;
  terminationActor?: 'Tenant' | 'Landlord';
}

export const TerminateContractModal: React.FC<Props> = ({
  contract,
  onClose,
  onTerminated,
  terminationActor = 'Tenant'
}) => {
  const [reason, setReason] = useState('');
  const [date, setDate] = useState('');
  const [landlordTerminationType, setLandlordTerminationType] = useState<'LandlordUnilateral' | 'MutualAgreement' | 'NormalExpiration'>('LandlordUnilateral');
  const [damageFee, setDamageFee] = useState('0');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      setIsSubmitting(true);
      setError(null);
      const terminationType = terminationActor === 'Landlord'
        ? landlordTerminationType
        : 'TenantUnilateral';
      const dateLabel = resolveDateLabel(terminationType);
      const parsedDamageFee = Number.parseFloat(damageFee.replace(/,/g, '').trim());
      const response = await contractApi.terminateContract(contract.id, {
        terminationType,
        terminationDate: date || null,
        damageFee: terminationActor === 'Landlord' && terminationType !== 'LandlordUnilateral' && Number.isFinite(parsedDamageFee)
          ? Math.max(parsedDamageFee, 0)
          : 0,
        reason: date ? `${reason.trim()} ${dateLabel}: ${date}.` : reason.trim()
      });

      onTerminated?.(response.data);
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể gửi yêu cầu chấm dứt hợp đồng. Vui lòng thử lại sau.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content">
        <div className="history-modal-header">
          <h2>Yêu cầu chấm dứt hợp đồng</h2>
          <button className="history-modal-close" onClick={onClose}>&times;</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="history-modal-body">
            <p style={{ color: '#dc2626', marginBottom: '20px', fontSize: '0.95rem' }}>
              <strong>Lưu ý:</strong> Việc chấm dứt hợp đồng trước hạn có thể ảnh hưởng đến tiền cọc theo điều khoản hợp đồng.
            </p>
            {terminationActor === 'Landlord' && (
              <div className="history-form-group">
                <label>Kiểu chấm dứt</label>
                <select
                  className="ui-input"
                  value={landlordTerminationType}
                  onChange={(e) => setLandlordTerminationType(e.target.value as 'LandlordUnilateral' | 'MutualAgreement' | 'NormalExpiration')}
                  required
                >
                  <option value="LandlordUnilateral">Đơn phương chấm dứt</option>
                  <option value="MutualAgreement">Hai bên thỏa thuận chấm dứt</option>
                  <option value="NormalExpiration">Đáo hạn hợp đồng</option>
                </select>
              </div>
            )}
            <div className="history-form-group">
              <label>{terminationActor === 'Landlord' ? resolveDateLabel(landlordTerminationType) : 'Ngày dự kiến chuyển đi'}</label>
              <input
                type="date"
                className="ui-input"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                required
              />
            </div>
            {terminationActor === 'Landlord' && landlordTerminationType !== 'LandlordUnilateral' && (
              <div className="history-form-group">
                <label>Phí khấu trừ/hư hỏng</label>
                <input
                  type="number"
                  min="0"
                  className="ui-input"
                  value={damageFee}
                  onChange={(e) => setDamageFee(e.target.value)}
                />
              </div>
            )}
            <div className="history-form-group">
              <label>Lý do chấm dứt</label>
              <textarea
                className="ui-input"
                placeholder="VD: Chuyển chỗ làm, về quê..."
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                required
              />
            </div>
            {error && (
              <p style={{ color: '#dc2626', marginTop: '12px', fontSize: '0.95rem' }}>
                {error}
              </p>
            )}
          </div>
          <div className="history-modal-footer">
            <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
            <Button variant="danger" type="submit" disabled={isSubmitting}>Xác nhận yêu cầu</Button>
          </div>
        </form>
      </div>
    </div>
  );
};

function resolveDateLabel(terminationType: string) {
  switch (terminationType) {
    case 'NormalExpiration':
      return 'Ngày thanh lý';
    case 'MutualAgreement':
      return 'Ngày thỏa thuận chấm dứt';
    case 'LandlordUnilateral':
      return 'Ngày dự kiến chấm dứt';
    default:
      return 'Ngày dự kiến rời đi';
  }
}
