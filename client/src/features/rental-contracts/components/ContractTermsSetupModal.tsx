import React, { useState, useEffect } from 'react';
import { contractApi } from '../../contracts/api';
import { Button } from '../../../shared/components/ui/Button';
import './ContractTermsSetupModal.css';

interface ContractTermsSetupModalProps {
  contractId?: string;
  rentalRequestId?: string;
  onClose: () => void;
  onSuccess: () => void;
}

export const ContractTermsSetupModal: React.FC<ContractTermsSetupModalProps> = ({ contractId, onClose, onSuccess }) => {
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [paymentDay, setPaymentDay] = useState<number | ''>('');

  useEffect(() => {
    const fetchContract = async () => {
      try {
        setLoading(true);
        setError(null);
        const res = await contractApi.getContract(contractId!);
        if (res.data) {
          setStartDate(res.data.startDate);
          setEndDate(res.data.endDate);
          setPaymentDay(res.data.paymentDay);
        } else {
          setError('Không tìm thấy dữ liệu hợp đồng.');
        }
      } catch (err: any) {
        setError(err.message || 'Có lỗi xảy ra khi tải hợp đồng.');
      } finally {
        setLoading(false);
      }
    };
    fetchContract();
  }, [contractId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!startDate || !endDate || !paymentDay) {
      setError('Vui lòng điền đầy đủ các trường.');
      return;
    }

    const day = Number(paymentDay);
    if (day < 1 || day > 28) {
      setError('Ngày thanh toán phải từ 1 đến 28.');
      return;
    }

    if (new Date(startDate) >= new Date(endDate)) {
      setError('Ngày kết thúc phải sau ngày bắt đầu.');
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      await contractApi.updateContractTerms(contractId!, {
        startDate,
        endDate,
        paymentDay: day
      });
      onSuccess();
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra khi cập nhật điều khoản hợp đồng.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="contract-terms-modal-overlay">
        <div className="contract-terms-modal" style={{ width: 'auto', padding: '24px' }}>
          <p style={{ margin: 0 }}>Đang tải dữ liệu...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="contract-terms-modal-overlay" onClick={onClose}>
      <div className="contract-terms-modal" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Sửa đổi điều khoản hợp đồng</h3>
          <button className="close-btn" onClick={onClose} title="Đóng">&times;</button>
        </div>

        <div className="modal-body">
          {error && <div style={{ color: 'red', marginBottom: '16px', fontSize: '0.9rem', background: '#fee2e2', padding: '8px 12px', borderRadius: '4px' }}>{error}</div>}
          
          <form id="terms-form" onSubmit={handleSubmit}>
            <div className="form-group">
              <label>Ngày bắt đầu</label>
              <input
                type="date"
                value={startDate}
                onChange={e => setStartDate(e.target.value)}
                required
              />
            </div>
            
            <div className="form-group">
              <label>Ngày kết thúc</label>
              <input
                type="date"
                value={endDate}
                onChange={e => setEndDate(e.target.value)}
                required
              />
            </div>
            
            <div className="form-group">
              <label>Ngày thanh toán hàng tháng (1-28)</label>
              <input
                type="number"
                min="1"
                max="28"
                value={paymentDay}
                onChange={e => setPaymentDay(e.target.value ? Number(e.target.value) : '')}
                required
              />
              <span className="help-text">Ví dụ: Điền "15" tức là ngày 15 hàng tháng người thuê sẽ đóng tiền.</span>
            </div>
          </form>
        </div>

        <div className="modal-footer">
          <Button variant="secondary" onClick={onClose} disabled={submitting}>Hủy</Button>
          <Button form="terms-form" type="submit" disabled={submitting}>
            {submitting ? 'Đang cập nhật...' : 'Cập nhật'}
          </Button>
        </div>
      </div>
    </div>
  );
};
