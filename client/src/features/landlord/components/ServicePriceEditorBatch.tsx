import { type FormEvent } from 'react';
import type { BillingServiceType, ServicePrice, CreateServicePriceRequest, PricingUnit } from '../../billing/types';
import { formatMoneyString, parseMoneyString } from '../../../shared/utils/format';

interface ServicePriceEditorBatchProps {
  serviceTypes: BillingServiceType[];
  prices: ServicePrice[];
  activePrices: ServicePrice[];
  priceHistory: ServicePrice[];
  loading: boolean;
  drafts: CreateServicePriceRequest[];
  note: string;
  actionLoading: boolean;
  effectiveFromDate: string;
  onChangeDraft: (serviceTypeId: string, patch: Partial<CreateServicePriceRequest>) => void;
  onChangeNote: (note: string) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onReload: () => void;
}

export function ServicePriceEditorBatch({
  serviceTypes,
  activePrices,
  priceHistory,
  loading,
  drafts,
  note,
  actionLoading,
  effectiveFromDate,
  onChangeDraft,
  onChangeNote,
  onSubmit,
  onReload,
}: ServicePriceEditorBatchProps) {
  const activeByServiceTypeId = new Map(activePrices.map((price) => [price.serviceTypeId, price]));
  const historyGroups = buildServicePriceHistoryGroups(priceHistory, serviceTypes);

  return (
    <div className="subtab-card">
      <div className="subtab-header" style={{ borderBottom: '1px solid #e2e8f0', paddingBottom: '16px', marginBottom: '20px' }}>
        <div className="subtab-header-icon">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="12" y1="1" x2="12" y2="23" />
            <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
          </svg>
        </div>
        <div style={{ flex: 1 }}>
          <h4>Bảng giá dịch vụ</h4>
          <p>Cấu hình đơn giá các loại dịch vụ tiện ích đi kèm (Điện, nước, internet... của khu trọ).</p>
        </div>
        <button type="button" className="secondary-action" onClick={onReload} disabled={loading} style={{ minHeight: '38px', padding: '0 16px', borderRadius: '8px', border: '1px solid #cbd5e1', font: 'inherit', fontWeight: 600, cursor: 'pointer', background: '#ffffff', color: '#475569' }}>
          Hoàn tác
        </button>
      </div>

      <form className="service-price-batch" onSubmit={onSubmit}>
        <div className="service-price-batch-table">
          <div className="service-price-batch-row service-price-batch-row--head">
            <span>Dịch vụ</span>
            <span>Giá hiện tại</span>
            <span>Cách tính phí</span>
            <span>Giá mới (VND)</span>
            <span>Từ ngày</span>
            <span></span>
          </div>
          {serviceTypes.map((serviceType) => {
            const currentPrice = activeByServiceTypeId.get(serviceType.id);
            const defaultPricingUnit = getDefaultServicePricingUnit(serviceType);
            const draft = drafts.find((item) => item.serviceTypeId === serviceType.id) ?? {
              serviceTypeId: serviceType.id,
              pricingUnit: defaultPricingUnit,
              unitName: getServicePricingUnitDisplayUnit(defaultPricingUnit, serviceType),
              unitPrice: 0,
              effectiveFrom: effectiveFromDate,
            };
            const methods = getServicePricingUnitOptions(serviceType);
            const serviceSlug = getServiceTypeSlug(serviceType);

            return (
              <div className="service-price-batch-row" key={serviceType.id}>
                <div className="service-price-service">
                  <span className={`service-price-icon service-price-icon--${serviceSlug}`}>
                    {getServiceIconSvg(serviceSlug)}
                  </span>
                  <div className="service-name-container">
                    <strong>{serviceType.name}</strong>
                    {!currentPrice && <span>Chưa cấu hình</span>}
                  </div>
                </div>
                <div className="service-price-current">
                  {currentPrice ? (
                    <div className="current-price-display">
                      <strong>{formatMoneyString(currentPrice.unitPrice)} đ</strong>
                      <span>/ {currentPrice.unitName}</span>
                    </div>
                  ) : (
                    <span className="current-price-empty">&mdash;</span>
                  )}
                </div>
                <div className="service-price-methods">
                  {methods.length === 1 ? (
                    <span className="service-price-method-static">{getPricingUnitLabel(methods[0])}</span>
                  ) : (
                    <div className="service-price-segmented">
                      {methods.map((method) => (
                        <button
                          type="button"
                          key={method}
                          className={normalizePricingUnitForDisplay(draft.pricingUnit ?? 'PerMonth') === method ? 'active' : ''}
                          onClick={() => onChangeDraft(serviceType.id, {
                            pricingUnit: method,
                            unitName: getServicePricingUnitDisplayUnit(method, serviceType),
                          })}
                        >
                          {getPricingUnitShortLabel(method)}
                        </button>
                      ))}
                    </div>
                  )}
                  <small>{getServicePricingUnitHint(draft.pricingUnit ?? 'PerMonth', serviceType)}</small>
                </div>
                <div className="service-price-new-wrapper">
                  <div className="price-input-wrapper">
                    <input
                      type="text"
                      placeholder="Nhập giá mới"
                      value={draft.unitPrice === 0 ? '' : formatMoneyString(draft.unitPrice)}
                      onChange={(event) => onChangeDraft(serviceType.id, { unitPrice: parseMoneyString(event.target.value) })}
                    />
                    <span className="price-suffix">đ</span>
                  </div>
                </div>
                <div className="service-price-date-wrapper">
                  <div className="date-input-wrapper">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                      <line x1="16" y1="2" x2="16" y2="6" />
                      <line x1="8" y1="2" x2="8" y2="6" />
                      <line x1="3" y1="10" x2="21" y2="10" />
                    </svg>
                    <input type="text" value={formatDateDisplay(effectiveFromDate)} readOnly />
                  </div>
                </div>
                <div className="service-price-action">
                  <button type="button" className="service-price-more-btn">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="12" r="1.5" />
                      <circle cx="12" cy="5" r="1.5" />
                      <circle cx="12" cy="19" r="1.5" />
                    </svg>
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        <div className="service-price-batch-actions-wrapper">
          <div className="service-price-note-wrapper">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="note-info-icon">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="16" x2="12" y2="12" />
              <line x1="12" y1="8" x2="12.01" y2="8" />
            </svg>
            <input
              value={note}
              onChange={(event) => onChangeNote(event.target.value)}
              placeholder="Ghi chú chung (ví dụ: điều chỉnh theo biểu giá tháng 10)"
            />
          </div>
          <button type="submit" className="primary-action-btn" disabled={actionLoading || serviceTypes.length === 0 || drafts.some((draft) => Number(draft.unitPrice) < 0)}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '6px' }}>
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
              <polyline points="17 21 17 13 7 13 7 21" />
              <polyline points="7 3 7 8 15 8" />
            </svg>
            {actionLoading ? 'Đang lưu...' : 'Lưu tất cả'}
          </button>
        </div>
      </form>

      <section className="service-price-panel service-price-history" style={{ marginTop: '30px', borderTop: '1px solid #e2e8f0', paddingTop: '20px' }}>
        <div className="section-heading" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <h4 style={{ margin: 0, fontSize: '15px', fontWeight: 600, color: '#1e293b' }}>Lịch sử thay đổi giá</h4>
          <button type="button" className="view-history-detail-btn" style={{ background: 'none', border: 'none', color: '#2563eb', fontWeight: 600, fontSize: '14px', cursor: 'pointer' }}>Xem chi tiết &gt;</button>
        </div>
        {historyGroups.length === 0 ? (
          <div className="empty-history-card">
            <svg width="120" height="90" viewBox="0 0 120 90" fill="none" xmlns="http://www.w3.org/2000/svg">
              <circle cx="60" cy="45" r="30" fill="#eff6ff" />
              <rect x="48" y="22" width="24" height="32" rx="3" fill="#ffffff" stroke="#cbd5e1" strokeWidth="1.5" />
              <rect x="54" y="18" width="12" height="6" rx="2" fill="#94a3b8" />
              <line x1="53" y1="28" x2="67" y2="28" stroke="#cbd5e1" strokeWidth="1.5" strokeLinecap="round" />
              <line x1="53" y1="34" x2="67" y2="34" stroke="#cbd5e1" strokeWidth="1.5" strokeLinecap="round" />
              <line x1="53" y1="40" x2="61" y2="40" stroke="#cbd5e1" strokeWidth="1.5" strokeLinecap="round" />
              <circle cx="70" cy="52" r="10" fill="#ffffff" stroke="#3b82f6" strokeWidth="1.5" />
              <path d="M70 48v4h3" stroke="#3b82f6" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            <p>Chưa có lịch sử thay đổi giá.</p>
          </div>
        ) : (
          <div className="service-price-history-groups">
            {historyGroups.map((group, index) => (
              <details className="service-price-history-group" key={group.key} open={index === 0}>
                <summary style={{ display: 'flex', alignItems: 'center', gap: '12px', padding: '12px 18px', background: '#f8fafc', cursor: 'pointer', fontWeight: 600, color: '#334155', borderBottom: '1px solid #e2e8f0' }}>
                  <span className="history-dot-row" style={{ display: 'flex', gap: '4px' }}>
                    {group.serviceTypeIds.map((serviceTypeId) => (
                      <i key={serviceTypeId} className={`history-dot history-dot--${getHistoryServiceTypeSlug(serviceTypeId, serviceTypes)}`} style={{ display: 'inline-block', width: '8px', height: '8px', borderRadius: '50%', background: '#3b82f6' }} />
                    ))}
                  </span>
                  <strong style={{ flex: 1 }}>{group.label}</strong>
                  <span style={{ fontSize: '13px', color: '#64748b', fontWeight: 400 }}>{group.items.length} dịch vụ</span>
                </summary>
                <div className="service-price-table service-price-table--monthly">
                  <div className="service-price-row service-price-row--head">
                    <span>Dịch vụ</span>
                    <span>Đơn giá</span>
                    <span>Đơn vị</span>
                    <span>Cách tính</span>
                    <span>Từ ngày</span>
                    <span>Đến ngày</span>
                  </div>
                  {group.items.map((price) => {
                    const svcName = serviceTypes.find((serviceType) => serviceType.id === price.serviceTypeId)?.name ?? price.serviceName;
                    return (
                      <div className="service-price-row" key={price.id}>
                        <span>{svcName}</span>
                        <span>{formatMoneyString(price.unitPrice)} VND</span>
                        <span>{formatUnitNameVietnamese(price.unitName, svcName)}</span>
                        <span>{getPricingUnitLabel(price.pricingUnit)}</span>
                        <span>{formatDateDMY(price.effectiveFrom)}</span>
                        <span>{price.effectiveTo ? formatDateDMY(price.effectiveTo) : 'Đang áp dụng'}</span>
                      </div>
                    );
                  })}
                </div>
              </details>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function formatUnitNameVietnamese(unitName: string, serviceName: string) {
  const name = unitName || '';
  if (name === 'MeterReading' || name === 'MeterBased' || name === 'Metered') {
    const sName = serviceName.toLowerCase();
    if (sName.includes('nước') || sName.includes('nuoc')) {
      return 'm³';
    }
    return 'số điện';
  }
  if (name === 'PerMonth' || name === 'Fixed') {
    return 'phòng/tháng';
  }
  if (name === 'PerPerson' || name === 'PerPersonPerMonth') {
    return 'người/tháng';
  }
  return name;
}

function formatDateDMY(dateStr: string) {
  if (!dateStr) return '';
  const cleanDateStr = dateStr.slice(0, 10);
  const parts = cleanDateStr.split('-');
  if (parts.length === 3) {
    return `${parts[2]}/${parts[1]}/${parts[0]}`;
  }
  return dateStr;
}

function formatDateDisplay(dateStr: string) {
  if (!dateStr) return '';
  const parts = dateStr.split('-');
  if (parts.length === 3) {
    return `${parts[1]}/${parts[2]}/${parts[0]}`;
  }
  return dateStr;
}

function getServiceIconSvg(serviceSlug: string) {
  switch (serviceSlug) {
    case 'dien':
    case 'electric':
    case 'electricity':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>
        </svg>
      );
    case 'internet':
    case 'wifi':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M5 12.55a11 11 0 0 1 14.08 0"/>
          <path d="M1.42 9a16 16 0 0 1 21.16 0"/>
          <path d="M8.53 16.11a6 6 0 0 1 6.95 0"/>
          <line x1="12" y1="20" x2="12.01" y2="20" strokeWidth="3"/>
        </svg>
      );
    case 'nuoc':
    case 'water':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z"/>
        </svg>
      );
    case 'rac':
    case 'trash':
    case 'waste':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polyline points="3 6 5 6 21 6"/>
          <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
          <line x1="10" y1="11" x2="10" y2="17"/>
          <line x1="14" y1="11" x2="14" y2="17"/>
        </svg>
      );
    default:
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="10"/>
          <line x1="12" y1="16" x2="12" y2="12"/>
          <line x1="12" y1="8" x2="12.01" y2="8"/>
        </svg>
      );
  }
}

function buildServicePriceHistoryGroups(prices: ServicePrice[], serviceTypes: BillingServiceType[] = []) {
  const formatter = new Intl.DateTimeFormat('vi-VN', { month: 'long', year: 'numeric' });
  const groups = new Map<string, ServicePrice[]>();
  const serviceTypeOrder = new Map(serviceTypes.map((serviceType, index) => [serviceType.id, index]));

  for (const price of prices) {
    const key = price.effectiveFrom.slice(0, 7);
    const items = groups.get(key) ?? [];
    groups.set(key, [...items, price]);
  }

  return Array.from(groups.entries())
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([key, items]) => {
      const latestByService = new Map<string, ServicePrice>();
      for (const item of [...items].sort((a, b) =>
        b.effectiveFrom.localeCompare(a.effectiveFrom) ||
        b.updatedAt.localeCompare(a.updatedAt)
      )) {
        if (!latestByService.has(item.serviceTypeId)) {
          latestByService.set(item.serviceTypeId, item);
        }
      }

      const sortedItems = Array.from(latestByService.values())
        .sort((a, b) =>
          (serviceTypeOrder.get(a.serviceTypeId) ?? Number.MAX_SAFE_INTEGER) -
          (serviceTypeOrder.get(b.serviceTypeId) ?? Number.MAX_SAFE_INTEGER) ||
          a.serviceName.localeCompare(b.serviceName, 'vi')
        );
      const date = new Date(`${key}-01T00:00:00`);

      return {
        key,
        label: formatter.format(date),
        items: sortedItems,
        serviceTypeIds: sortedItems.map((item) => item.serviceTypeId),
      };
    });
}

function normalizePricingUnitForDisplay(method: PricingUnit | string): PricingUnit {
  if (method === 'Metered' || method === 'MeterReading') {
    return 'MeterBased';
  }

  if (method === 'Fixed') {
    return 'PerMonth';
  }

  if (method === 'PerPersonPerMonth') {
    return 'PerPerson';
  }

  return method as PricingUnit;
}

export function normalizePricingUnit(unit: PricingUnit | string): PricingUnit {
  const normalized = normalizePricingUnitForDisplay(unit);

  if (normalized === 'MeterBased') {
    return 'MeterReading';
  }

  if (normalized === 'PerPerson') {
    return 'PerPersonPerMonth';
  }

  if (normalized === 'PerMonth') {
    return 'PerMonth';
  }

  return normalized;
}

export function getDefaultServicePricingUnit(serviceType: BillingServiceType): PricingUnit {
  return serviceType.supportsMeterReading ? 'MeterReading' : 'PerMonth';
}

function getServicePricingUnitOptions(serviceType: BillingServiceType): PricingUnit[] {
  return serviceType.supportsMeterReading
    ? ['MeterBased', 'PerMonth', 'PerPerson']
    : ['PerMonth', 'PerPerson'];
}

export function getServicePricingUnitDisplayUnit(unit: PricingUnit | string, serviceType: BillingServiceType) {
  const normalized = normalizePricingUnitForDisplay(unit);

  if (normalized === 'PerPerson') {
    return 'người/tháng';
  }

  if (normalized === 'PerMonth') {
    return 'tháng';
  }

  return serviceType.meterUnitName ?? '';
}

function getServicePricingUnitHint(unit: PricingUnit | string, serviceType: BillingServiceType) {
  const normalized = normalizePricingUnitForDisplay(unit);

  if (normalized === 'PerPerson') {
    return 'VND / người / tháng';
  }

  if (normalized === 'PerMonth') {
    return 'VND / phòng / tháng';
  }

  return serviceType.meterUnitName ? `VND / ${serviceType.meterUnitName}` : 'VND / chỉ số';
}

function getServiceTypeSlug(serviceType: BillingServiceType) {
  return serviceType.name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '') || 'service';
}

function getHistoryServiceTypeSlug(serviceTypeId: string, serviceTypes: BillingServiceType[]) {
  const serviceType = serviceTypes.find((item) => item.id === serviceTypeId);
  return serviceType ? getServiceTypeSlug(serviceType) : 'service';
}

function getPricingUnitLabel(method: PricingUnit | string) {
  const normalized = normalizePricingUnitForDisplay(method);
  const labels: Record<PricingUnit, string> = {
    Metered: 'Theo chỉ số',
    MeterBased: 'Theo chỉ số',
    MeterReading: 'Theo chỉ số',
    Fixed: 'Theo tháng',
    PerMonth: 'Theo tháng',
    PerPerson: 'Theo người/tháng',
    PerPersonPerMonth: 'Theo người/tháng',
  };

  return labels[normalized];
}

function getPricingUnitShortLabel(method: PricingUnit | string) {
  const normalized = normalizePricingUnitForDisplay(method);
  const labels: Record<PricingUnit, string> = {
    Metered: 'Chỉ số',
    MeterBased: 'Chỉ số',
    MeterReading: 'Chỉ số',
    Fixed: 'Tháng',
    PerMonth: 'Tháng',
    PerPerson: 'Người/tháng',
    PerPersonPerMonth: 'Người/tháng',
  };

  return labels[normalized];
}
