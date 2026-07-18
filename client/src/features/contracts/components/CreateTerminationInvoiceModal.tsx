import { useEffect, useMemo, useState } from 'react';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { billingApi } from '../../billing/api';
import type { LatestMeterReading, RoomInvoicePreview } from '../../billing/types';
import type { ContractDetailResponse } from '../types';
import {
  buildFinalInvoiceMeterReadings,
  FinalInvoicePreviewSection,
  type ReadingDraft
} from '../../rental-history/pages/TerminateContractModal';
import '../../rental-history/pages/HistoryModals.css';

interface Props {
  contract: ContractDetailResponse;
  onClose: () => void;
  onCreated: () => void;
}

export function CreateTerminationInvoiceModal({ contract, onClose, onCreated }: Props) {
  const [preview, setPreview] = useState<RoomInvoicePreview | null>(null);
  const [readings, setReadings] = useState<Record<string, ReadingDraft>>({});
  const [discountAmount, setDiscountAmount] = useState(0);
  const [note, setNote] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingServiceId, setUploadingServiceId] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function loadPreview() {
      setLoading(true);
      setError(null);
      try {
        const response = await billingApi.getTerminationInvoicePreview(contract.id);
        if (cancelled) return;
        setPreview(response.data);
        setReadings(Object.fromEntries(response.data.meteredServices.map(service => [
          service.serviceTypeId,
          {
            previousReading: service.latestReading?.currentReading ?? 0,
            currentReading: service.latestReading?.currentReading ?? 0
          }
        ])));
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err, 'Không thể tải thông tin hóa đơn cần tạo.'));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadPreview();
    return () => {
      cancelled = true;
    };
  }, [contract.id]);

  const latestReadingByServiceType = useMemo(
    () => Object.fromEntries(
      (preview?.meteredServices ?? [])
        .filter(service => service.latestReading)
        .map(service => [service.serviceTypeId, service.latestReading as LatestMeterReading])
    ),
    [preview]
  );

  const utilityPreview = useMemo(
    () => (preview?.meteredServices ?? []).reduce((sum, service) => {
      const draft = readings[service.serviceTypeId] ?? { previousReading: 0, currentReading: 0 };
      const previousReading = service.latestReading?.currentReading ?? Number(draft.previousReading);
      const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
      return sum + consumption * service.unitPrice;
    }, 0),
    [preview, readings]
  );

  const totalAmount = Math.max(
    0,
    (preview?.rentAmount ?? 0) +
      (preview?.fixedServiceAmount ?? 0) +
      utilityPreview -
      discountAmount
  );
  const isFinalPeriod = Boolean(preview && contract.terminationDate && preview.billingPeriodEnd === contract.terminationDate);
  const actionLabel = isFinalPeriod
    ? 'Tạo hóa đơn kỳ cuối'
    : preview
      ? `Tạo hóa đơn kỳ ${formatMonth(preview.billingPeriodStart)}`
      : 'Tạo hóa đơn';

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!preview || !preview.canGenerate) return;

    const meterReadings = buildFinalInvoiceMeterReadings(preview, readings, latestReadingByServiceType);
    const invalidReading = meterReadings.some(reading => {
      const service = preview.meteredServices.find(item => item.serviceTypeId === reading.serviceTypeId);
      const previousReading = service?.latestReading?.currentReading ?? reading.previousReading ?? 0;
      return reading.currentReading < previousReading;
    });
    if (invalidReading) {
      setError('Chỉ số mới không được nhỏ hơn chỉ số cũ.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await billingApi.createTerminationInvoice(contract.id, {
        discountAmount: Number(discountAmount) || 0,
        note: note.trim() || null,
        meterReadings
      });
      onCreated();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo hóa đơn.'));
    } finally {
      setSubmitting(false);
    }
  }

  function updateReading(serviceTypeId: string, patch: Partial<ReadingDraft>) {
    setReadings(current => ({
      ...current,
      [serviceTypeId]: {
        ...(current[serviceTypeId] ?? { previousReading: 0, currentReading: 0 }),
        ...patch
      }
    }));
  }

  async function handleMeterImage(serviceTypeId: string, file?: File) {
    if (!file || !preview) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      setError('Chỉ hỗ trợ ảnh đồng hồ định dạng JPG hoặc PNG.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Dung lượng ảnh đồng hồ không được vượt quá 5MB.');
      return;
    }

    setUploadingServiceId(serviceTypeId);
    setError(null);
    try {
      const response = await billingApi.readMeterImage({
        contractId: contract.id,
        serviceTypeId,
        billingPeriodStart: preview.billingPeriodStart,
        file
      });
      updateReading(serviceTypeId, {
        currentReading: response.data.reading,
        aiReading: response.data.reading,
        aiRawText: response.data.rawText,
        proofMediaAssetId: response.data.proofMediaAssetId ?? null,
        proofImageUrl: response.data.proofImageUrl
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể đọc chỉ số từ ảnh. Vui lòng thử ảnh rõ hơn.'));
    } finally {
      setUploadingServiceId('');
    }
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content terminate-contract-modal">
        <div className="history-modal-header">
          <h2>{actionLabel}</h2>
          <button className="history-modal-close" onClick={onClose} type="button">&times;</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="history-modal-body">
            {loading ? (
              <p>Đang tải thông tin hóa đơn...</p>
            ) : error && !preview ? (
              <div style={{ color: '#991b1b', background: '#fee2e2', borderRadius: 8, padding: '10px 12px' }}>{error}</div>
            ) : preview ? (
              <FinalInvoicePreviewSection
                preview={preview}
                readings={readings}
                utilityPreview={utilityPreview}
                discountAmount={discountAmount}
                note={note}
                totalAmount={totalAmount}
                totalLabel={isFinalPeriod ? 'Tổng hóa đơn kỳ cuối' : 'Tổng hóa đơn'}
                blockReason={preview.canGenerate ? '' : preview.blockReason || 'Chưa thể tạo hóa đơn này.'}
                onChangeReading={updateReading}
                onReadMeterImage={handleMeterImage}
                uploadingServiceId={uploadingServiceId}
                onChangeDiscountAmount={setDiscountAmount}
                onChangeNote={setNote}
              />
            ) : null}
            {error && preview && (
              <div style={{ color: '#991b1b', marginTop: 12 }}>{error}</div>
            )}
          </div>
          <div className="history-modal-footer">
            <Button type="button" variant="outline" onClick={onClose} disabled={submitting}>Đóng</Button>
            <Button type="submit" disabled={loading || submitting || uploadingServiceId !== '' || !preview?.canGenerate}>
              {submitting ? 'Đang tạo...' : actionLabel}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function formatMonth(value: string) {
  const [year, month] = value.split('-');
  return month && year ? `${month}/${year}` : value;
}
