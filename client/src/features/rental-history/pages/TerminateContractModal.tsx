import React, { useEffect, useMemo, useState } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { contractApi } from '../../contracts/api';
import type { ContractDetailResponse, ContractHistoryItemResponse } from '../../contracts/types';
import { billingApi } from '../../billing/api';
import type { FixedServicePreview, LatestMeterReading, MeterReadingInput, RoomInvoicePreview } from '../../billing/types';
import './HistoryModals.css';

interface Props {
  contract: ContractHistoryItemResponse | ContractDetailResponse;
  onClose: () => void;
  onTerminated?: (contract: ContractDetailResponse) => void;
  terminationActor?: 'Tenant' | 'Landlord';
}

export interface ReadingDraft {
  previousReading: number;
  currentReading: number;
}

export const TerminateContractModal: React.FC<Props> = ({
  contract,
  onClose,
  onTerminated,
  terminationActor = 'Tenant'
}) => {
  const [reason, setReason] = useState('');
  const [landlordTerminationType, setLandlordTerminationType] = useState<'LandlordUnilateral' | 'MutualAgreement' | 'NormalExpiration'>('LandlordUnilateral');
  const [damageFee, setDamageFee] = useState('0');
  const [createFinalInvoice, setCreateFinalInvoice] = useState(false);
  const [finalInvoicePreview, setFinalInvoicePreview] = useState<RoomInvoicePreview | null>(null);
  const [finalInvoiceReadings, setFinalInvoiceReadings] = useState<Record<string, ReadingDraft>>({});
  const [finalInvoiceDiscountAmount, setFinalInvoiceDiscountAmount] = useState(0);
  const [finalInvoiceNote, setFinalInvoiceNote] = useState('');
  const [loadingFinalInvoicePreview, setLoadingFinalInvoicePreview] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const today = useMemo(() => getTodayDateOnly(), []);
  const isNormalExpiration = terminationActor === 'Landlord' && landlordTerminationType === 'NormalExpiration';
  const terminationEffectiveDate = isNormalExpiration ? contract.endDate : today;
  const isBeforeContractStart = today < contract.startDate;
  const normalExpirationBlocked = isNormalExpiration && today < contract.endDate;
  const canCreateFinalInvoice = terminationActor === 'Landlord' && !isBeforeContractStart;

  const finalInvoiceBlockReason = createFinalInvoice && finalInvoicePreview && !finalInvoicePreview.canGenerate
    ? finalInvoicePreview.blockReason || 'Chưa thể tạo hóa đơn kỳ cuối.'
    : '';
  const latestReadingByServiceType = useMemo(
    () => Object.fromEntries(
      (finalInvoicePreview?.meteredServices ?? [])
        .filter((service) => service.latestReading)
        .map((service) => [service.serviceTypeId, service.latestReading as LatestMeterReading])
    ),
    [finalInvoicePreview]
  );
  const utilityPreview = useMemo(
    () => (finalInvoicePreview?.meteredServices ?? []).reduce((sum, service) => {
      const draft = finalInvoiceReadings[service.serviceTypeId];
      if (!draft) {
        return sum;
      }

      const previousReading = service.latestReading?.currentReading ?? Number(draft.previousReading);
      const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
      return sum + consumption * service.unitPrice;
    }, 0),
    [finalInvoicePreview, finalInvoiceReadings]
  );
  const finalInvoiceTotal = Math.max(
    0,
    (finalInvoicePreview?.rentAmount ?? 0) +
    (finalInvoicePreview?.fixedServiceAmount ?? 0) +
    utilityPreview -
    finalInvoiceDiscountAmount
  );

  useEffect(() => {
    if (!canCreateFinalInvoice || !createFinalInvoice) {
      setFinalInvoicePreview(null);
      setFinalInvoiceReadings({});
      setPreviewError(null);
      return;
    }

    let cancelled = false;

    async function loadFinalInvoicePreview() {
      setLoadingFinalInvoicePreview(true);
      setPreviewError(null);
      try {
        const response = await billingApi.getRoomInvoicePreview(contract.roomId, {
          billingPeriodStart: terminationEffectiveDate,
          billingPeriodEnd: terminationEffectiveDate
        });
        if (cancelled) {
          return;
        }

        setFinalInvoicePreview(response.data);
        const nextReadings: Record<string, ReadingDraft> = {};
        response.data.meteredServices.forEach((service) => {
          nextReadings[service.serviceTypeId] = {
            previousReading: service.latestReading?.currentReading ?? 0,
            currentReading: service.latestReading?.currentReading ?? 0
          };
        });
        setFinalInvoiceReadings(nextReadings);
      } catch (err) {
        if (!cancelled) {
          setFinalInvoicePreview(null);
          setFinalInvoiceReadings({});
          setPreviewError(getApiErrorMessage(err, 'Không thể tải thông tin hóa đơn kỳ cuối.'));
        }
      } finally {
        if (!cancelled) {
          setLoadingFinalInvoicePreview(false);
        }
      }
    }

    void loadFinalInvoicePreview();

    return () => {
      cancelled = true;
    };
  }, [canCreateFinalInvoice, createFinalInvoice, terminationEffectiveDate, contract.roomId]);

  useEffect(() => {
    if (!canCreateFinalInvoice && createFinalInvoice) {
      setCreateFinalInvoice(false);
    }
  }, [canCreateFinalInvoice, createFinalInvoice]);

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

      if (normalExpirationBlocked) {
        setError(`Hợp đồng còn hạn đến ngày ${formatDateVi(contract.endDate)} nên không thể đáo hạn.`);
        return;
      }

      if (createFinalInvoice) {
        if (!finalInvoicePreview) {
          setError(previewError || 'Vui lòng chờ tải thông tin hóa đơn kỳ cuối.');
          return;
        }

        if (finalInvoiceBlockReason) {
          setError(finalInvoiceBlockReason);
          return;
        }
      }

      const finalInvoiceMeterReadings = createFinalInvoice
        ? buildFinalInvoiceMeterReadings(finalInvoicePreview, finalInvoiceReadings, latestReadingByServiceType)
        : [];
      const invalidReading = finalInvoiceMeterReadings.find((reading) => {
        const service = finalInvoicePreview?.meteredServices.find((item) => item.serviceTypeId === reading.serviceTypeId);
        const previousReading = service?.latestReading?.currentReading ?? reading.previousReading;
        return previousReading !== null && previousReading !== undefined && reading.currentReading < previousReading;
      });

      if (invalidReading) {
        setError('Chỉ số mới không được nhỏ hơn chỉ số cũ.');
        return;
      }

      const response = await contractApi.terminateContract(contract.id, {
        terminationType,
        terminationDate: null,
        damageFee: terminationActor === 'Landlord' && terminationType !== 'LandlordUnilateral' && Number.isFinite(parsedDamageFee)
          ? Math.max(parsedDamageFee, 0)
          : 0,
        reason: `${reason.trim()} ${dateLabel}: ${formatDateVi(terminationEffectiveDate)}.`,
        createFinalInvoice,
        finalInvoiceDiscountAmount: createFinalInvoice ? Number(finalInvoiceDiscountAmount) || 0 : 0,
        finalInvoiceNote: createFinalInvoice ? finalInvoiceNote.trim() || null : null,
        finalInvoiceMeterReadings
      });

      onTerminated?.(response.data);
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể gửi yêu cầu chấm dứt hợp đồng. Vui lòng thử lại sau.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  function updateFinalInvoiceReading(serviceTypeId: string, patch: Partial<ReadingDraft>) {
    setFinalInvoiceReadings((current) => ({
      ...current,
      [serviceTypeId]: {
        ...(current[serviceTypeId] ?? { previousReading: 0, currentReading: 0 }),
        ...patch
      }
    }));
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content terminate-contract-modal">
        <div className="history-modal-header">
          <h2>Chấm dứt hợp đồng</h2>
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
              <div className="ui-input" style={{ background: '#f8fafc', display: 'flex', alignItems: 'center' }}>
                {formatDateVi(terminationEffectiveDate)}
              </div>
              {normalExpirationBlocked && (
                <p style={{ color: '#dc2626', margin: '8px 0 0', fontSize: '0.9rem' }}>
                  Hợp đồng còn hạn đến ngày {formatDateVi(contract.endDate)} nên không thể đáo hạn.
                </p>
              )}
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
            {terminationActor === 'Landlord' && isBeforeContractStart && (
              <section style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 16, marginBottom: 16, color: '#64748b', background: '#f8fafc' }}>
                Hợp đồng chưa đến ngày bắt đầu thuê nên không phát sinh hóa đơn kỳ cuối.
              </section>
            )}
            {canCreateFinalInvoice && (
              <section style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 16, marginBottom: 16 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 700 }}>
                  <input
                    type="checkbox"
                    checked={createFinalInvoice}
                    onChange={(event) => setCreateFinalInvoice(event.target.checked)}
                  />
                  Tạo hóa đơn kỳ cuối
                </label>

                {createFinalInvoice && (
                  <div style={{ marginTop: 16 }}>
                    {false ? (
                      <p style={{ color: '#6b7280', margin: 0 }}>Chọn ngày chấm dứt để xem trước hóa đơn kỳ cuối.</p>
                    ) : loadingFinalInvoicePreview ? (
                      <p style={{ color: '#6b7280', margin: 0 }}>Đang tải thông tin hóa đơn kỳ cuối...</p>
                    ) : previewError ? (
                      <p style={{ color: '#dc2626', margin: 0 }}>{previewError}</p>
                    ) : finalInvoicePreview ? (
                      <FinalInvoicePreviewSection
                        preview={finalInvoicePreview}
                        readings={finalInvoiceReadings}
                        utilityPreview={utilityPreview}
                        discountAmount={finalInvoiceDiscountAmount}
                        note={finalInvoiceNote}
                        totalAmount={finalInvoiceTotal}
                        blockReason={finalInvoiceBlockReason}
                        onChangeReading={updateFinalInvoiceReading}
                        onChangeDiscountAmount={setFinalInvoiceDiscountAmount}
                        onChangeNote={setFinalInvoiceNote}
                      />
                    ) : null}
                  </div>
                )}
              </section>
            )}
            {error && (
              <p style={{ color: '#dc2626', marginTop: '12px', fontSize: '0.95rem' }}>
                {error}
              </p>
            )}
          </div>
          <div className="history-modal-footer">
            <Button variant="outline" type="button" onClick={onClose} disabled={isSubmitting}>Hủy</Button>
            <Button
              variant="danger"
              type="submit"
              disabled={isSubmitting || loadingFinalInvoicePreview || normalExpirationBlocked || Boolean(finalInvoiceBlockReason)}
            >
              Xác nhận yêu cầu
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
};

export function FinalInvoicePreviewSection({
  preview,
  readings,
  utilityPreview,
  discountAmount,
  note,
  totalAmount,
  totalLabel = 'Tổng hóa đơn kỳ cuối',
  blockReason,
  onChangeReading,
  onChangeDiscountAmount,
  onChangeNote
}: {
  preview: RoomInvoicePreview;
  readings: Record<string, ReadingDraft>;
  utilityPreview: number;
  discountAmount: number;
  note: string;
  totalAmount: number;
  totalLabel?: string;
  blockReason: string;
  onChangeReading: (serviceTypeId: string, patch: Partial<ReadingDraft>) => void;
  onChangeDiscountAmount: (value: number) => void;
  onChangeNote: (value: string) => void;
}) {
  return (
    <div style={{ display: 'grid', gap: 14 }}>
      {blockReason && (
        <div style={{ color: '#991b1b', background: '#fee2e2', borderRadius: 8, padding: '10px 12px', fontSize: '0.92rem' }}>
          {blockReason}
        </div>
      )}

      <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))' }}>
        <PreviewBox label="Kỳ hóa đơn" value={`${formatDateVi(preview.billingPeriodStart)} - ${formatDateVi(preview.billingPeriodEnd)}`} />
        <PreviewBox label="Tiền phòng" value={formatMoney(preview.rentAmount)} />
        <PreviewBox label="Dịch vụ cố định" value={formatMoney(preview.fixedServiceAmount)} />
      </div>

      <div>
        <strong style={{ display: 'block', marginBottom: 8 }}>Dịch vụ cố định</strong>
        {preview.fixedServices.length === 0 ? (
          <p style={{ color: '#6b7280', margin: 0 }}>Không có dịch vụ cố định trong kỳ này.</p>
        ) : (
          <div style={{ display: 'grid', gap: 8 }}>
            {preview.fixedServices.map((service) => (
              <div key={service.serviceTypeId} style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
                <span>{service.serviceName} / {service.displayUnitName}</span>
                <strong>{formatFixedServiceLine(service)}</strong>
              </div>
            ))}
          </div>
        )}
      </div>

      <div>
        <strong style={{ display: 'block', marginBottom: 8 }}>Chỉ số điện nước</strong>
        {preview.meteredServices.length === 0 ? (
          <p style={{ color: '#6b7280', margin: 0 }}>Không có dịch vụ tính theo chỉ số trong kỳ này.</p>
        ) : (
          <div style={{ display: 'grid', gap: 10 }}>
            {preview.meteredServices.map((service) => {
              const draft = readings[service.serviceTypeId] ?? { previousReading: 0, currentReading: 0 };
              const previousReading = service.latestReading?.currentReading ?? Number(draft.previousReading);
              const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
              const amount = Math.round(consumption * service.unitPrice);

              return (
                <div key={service.serviceTypeId} style={{ border: '1px solid #e5e7eb', borderRadius: 8, padding: 12 }}>
                  <strong>{service.serviceName} ({formatMoney(service.unitPrice)} / {service.meterUnitName})</strong>
                  <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', marginTop: 10 }}>
                    {service.latestReading ? (
                      <PreviewBox label="Chỉ số cũ" value={String(service.latestReading.currentReading)} />
                    ) : (
                      <label className="history-form-group" style={{ marginBottom: 0 }}>
                        <span>Chỉ số cũ</span>
                        <input
                          type="number"
                          min="0"
                          className="ui-input"
                          value={draft.previousReading}
                          onChange={(event) => onChangeReading(service.serviceTypeId, { previousReading: Number(event.target.value) })}
                        />
                      </label>
                    )}
                    <label className="history-form-group" style={{ marginBottom: 0 }}>
                      <span>Chỉ số mới</span>
                      <input
                        type="number"
                        min="0"
                        className="ui-input"
                        value={draft.currentReading}
                        onChange={(event) => onChangeReading(service.serviceTypeId, { currentReading: Number(event.target.value) })}
                      />
                    </label>
                    <PreviewBox label="Tạm tính" value={`${formatNumber(consumption)} x ${formatMoney(service.unitPrice)} = ${formatMoney(amount)}`} />
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div style={{ display: 'grid', gap: 10, gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
        <label className="history-form-group" style={{ marginBottom: 0 }}>
          <span>Giảm trừ hóa đơn</span>
          <input
            type="number"
            min="0"
            className="ui-input"
            value={discountAmount}
            onChange={(event) => onChangeDiscountAmount(Number(event.target.value))}
          />
        </label>
        <label className="history-form-group" style={{ marginBottom: 0 }}>
          <span>Ghi chú hóa đơn</span>
          <input
            className="ui-input"
            value={note}
            onChange={(event) => onChangeNote(event.target.value)}
          />
        </label>
      </div>

      <div style={{ display: 'grid', gap: 8, borderTop: '1px solid #e5e7eb', paddingTop: 12 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span>Tiền điện/nước tạm tính</span>
          <strong>{formatMoney(utilityPreview)}</strong>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '1.05rem' }}>
          <span>{totalLabel}</span>
          <strong>{formatMoney(totalAmount)}</strong>
        </div>
      </div>
    </div>
  );
}

function PreviewBox({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ background: '#f8fafc', border: '1px solid #e5e7eb', borderRadius: 8, padding: '10px 12px' }}>
      <span style={{ display: 'block', color: '#64748b', fontSize: '0.82rem', marginBottom: 4 }}>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

export function buildFinalInvoiceMeterReadings(
  preview: RoomInvoicePreview | null,
  readings: Record<string, ReadingDraft>,
  latestReadingByServiceType: Record<string, LatestMeterReading>
): MeterReadingInput[] {
  if (!preview) {
    return [];
  }

  return preview.meteredServices.map((service) => {
    const draft = readings[service.serviceTypeId] ?? { previousReading: 0, currentReading: 0 };
    const latestReading = latestReadingByServiceType[service.serviceTypeId];
    return {
      serviceTypeId: service.serviceTypeId,
      previousReading: latestReading ? null : Number(draft.previousReading),
      currentReading: Number(draft.currentReading)
    };
  });
}

function formatFixedServiceLine(service: FixedServicePreview) {
  if (service.pricingUnit !== 'PerPersonPerMonth') {
    return formatMoney(service.amount);
  }

  const unitAmount = service.occupantCount > 0
    ? Math.round(service.amount / service.occupantCount)
    : service.unitPrice;

  return `${formatMoney(unitAmount)} x ${service.occupantCount} = ${formatMoney(service.amount)}`;
}

function formatMoney(value: number) {
  return `${formatNumber(Math.round(value))} đ`;
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('vi-VN').format(value);
}

function formatDateVi(value: string) {
  const [year, month, day] = value.split('-');
  if (!year || !month || !day) {
    return value;
  }

  return `${day}/${month}/${year}`;
}

function getTodayDateOnly() {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

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
