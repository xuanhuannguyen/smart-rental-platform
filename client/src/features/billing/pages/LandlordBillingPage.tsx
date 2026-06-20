import { useEffect, useMemo, useState } from 'react';
import type { Dispatch, FormEvent, ReactNode, SetStateAction } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { contractApi } from '../../contracts/api';
import { billingApi } from '../api';
import type { ContractAppendixResponse, ContractHistoryItemResponse } from '../../contracts/types';
import type {
  BillingServiceType,
  CreateServicePriceRequest,
  FixedServicePreview,
  Invoice,
  LatestMeterReading,
  MeteredServicePreview,
  MeterReadingInput,
  PricingUnit,
  RoomBillingContext,
  RoomInvoicePreview,
  ServicePrice
} from '../types';
import './BillingPages.css';
import '../../landlord/pages/LandlordDashboardPage.css';
import '../../rental-history/pages/HistoryModals.css';

const invoiceStatuses = ['Draft', 'Issued', 'Paid', 'Overdue', 'Cancelled'];
const today = formatDateInput(new Date());
const defaultBillingMonth = getDefaultBillingMonth(new Date());
const monthStart = getMonthStart(defaultBillingMonth);
const monthEnd = getMonthEnd(defaultBillingMonth);

type LandlordTab = 'prices' | 'readings' | 'invoices' | 'create' | 'detail';

type CreateMeterReadingRequest = {
  roomId: string;
  contractId: string;
  serviceTypeId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  previousReading: number;
  currentReading: number;
  proofImageObjectKey?: string | null;
};

type MeterReading = {
  serviceTypeId: string;
  serviceName: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  previousReading: number;
  currentReading: number;
  consumption: number;
};

type GenerateInvoiceDraftRequest = {
  contractId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  discountAmount: number;
  note?: string | null;
};

export default function LandlordBillingPage() {
  const { id, invoiceId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const query = new URLSearchParams(location.search);
  const queryRoomId = query.get('roomId') ?? '';
  const roomingHouseId = id ?? '';
  const tab = getTab(location.pathname, invoiceId);

  const [prices, setPrices] = useState<ServicePrice[]>([]);
  const [preview, setPreview] = useState<RoomInvoicePreview | null>(null);
  const [serviceTypes, setServiceTypes] = useState<BillingServiceType[]>([]);
  const [roomContext, setRoomContext] = useState<RoomBillingContext | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [loadingPrices, setLoadingPrices] = useState(false);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selectedHouseId, setSelectedHouseId] = useState('');
  const [selectedRoomId, setSelectedRoomId] = useState('');
  const [isCreateInvoiceModalOpen, setIsCreateInvoiceModalOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ title: string; body: string; onConfirm: () => void } | null>(null);

  const [priceForm, setPriceForm] = useState<CreateServicePriceRequest>({
    serviceTypeId: '',
    pricingUnit: 'PerMonth',
    unitPrice: 4000,
    effectiveFrom: today,
    note: ''
  });

  const activePrices = useMemo(() => prices.filter((price) => price.isActive), [prices]);
  const priceHistory = useMemo(() => prices.filter((price) => !price.isActive), [prices]);
  const invoiceHouses = useMemo(() => {
    const map = new Map<string, string>();
    invoices.forEach((invoice) => {
      map.set(invoice.roomingHouseId, invoice.roomingHouseName);
    });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [invoices]);
  const invoiceRooms = useMemo(() => {
    if (!selectedHouseId) {
      return [];
    }

    const map = new Map<string, string>();
    invoices
      .filter((invoice) => invoice.roomingHouseId === selectedHouseId)
      .forEach((invoice) => {
        map.set(invoice.roomId, invoice.roomNumber);
      });

    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [invoices, selectedHouseId]);
  const filteredInvoices = useMemo(() => {
    return invoices.filter((invoice) => {
      if (selectedHouseId && invoice.roomingHouseId !== selectedHouseId) return false;
      if (selectedRoomId && invoice.roomId !== selectedRoomId) return false;
      return true;
    });
  }, [invoices, selectedHouseId, selectedRoomId]);

  useEffect(() => {
    if (roomingHouseId) {
      void loadPrices();
    }
  }, [roomingHouseId]);

  useEffect(() => {
    if (queryRoomId) {
      void loadRoomContext(queryRoomId);
    }
  }, [queryRoomId]);

  useEffect(() => {
    if (!roomingHouseId && roomContext?.roomingHouseId) {
      void loadPrices(roomContext.roomingHouseId);
    }
  }, [roomingHouseId, roomContext?.roomingHouseId]);

  useEffect(() => {
    if (tab === 'invoices' || tab === 'detail') {
      void loadInvoices();
    }
  }, [tab, statusFilter]);

  useEffect(() => {
    setSelectedRoomId('');
  }, [selectedHouseId]);

  useEffect(() => {
    if (invoiceId) {
      void loadInvoiceDetail(invoiceId);
    }
  }, [invoiceId]);

  async function loadPrices(targetRoomingHouseId = roomingHouseId) {
    if (!targetRoomingHouseId) {
      return;
    }

    setLoadingPrices(true);
    setError('');
    try {
      const [typeResponse, priceResponse] = await Promise.all([
        billingApi.getServiceTypes(),
        billingApi.getServicePrices(targetRoomingHouseId)
      ]);
      setServiceTypes(typeResponse.data);
      setPrices(priceResponse.data);
      setPriceForm((prev) => {
        if (prev.serviceTypeId || typeResponse.data.length === 0) {
          return prev;
        }

        const first = typeResponse.data[0];
        return {
          ...prev,
          serviceTypeId: first.id,
          pricingUnit: getDefaultPricingUnit(first)
        };
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tải được bảng giá dịch vụ.'));
    } finally {
      setLoadingPrices(false);
    }
  }

  async function loadRoomContext(roomId: string) {
    setError('');
    try {
      const response = await billingApi.getRoomBillingContext(roomId);
      setRoomContext(response.data);
    } catch (err) {
      setRoomContext(null);
      setError(getApiErrorMessage(err, 'Phòng này chưa có hợp đồng đang hiệu lực để nhập chỉ số hoặc tạo hóa đơn.'));
    }
  }

  async function loadInvoices() {
    setLoadingInvoices(true);
    setError('');
    try {
      const response = await billingApi.getLandlordInvoices({
        status: statusFilter === 'all' ? '' : statusFilter
      });
      setInvoices(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tải được danh sách hóa đơn.'));
    } finally {
      setLoadingInvoices(false);
    }
  }

  async function loadInvoiceDetail(targetId: string) {
    setLoadingInvoices(true);
    setError('');
    try {
      const response = await billingApi.getLandlordInvoice(targetId);
      setSelectedInvoice(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tải được chi tiết hóa đơn.'));
    } finally {
      setLoadingInvoices(false);
    }
  }

  function selectService(serviceTypeId: string) {
    const serviceType = serviceTypes.find((item) => item.id === serviceTypeId);
    if (!serviceType) {
      return;
    }

    setPriceForm((prev) => ({
      ...prev,
      serviceTypeId,
      pricingUnit: getDefaultPricingUnit(serviceType)
    }));
  }

  async function handleCreatePrice(event: FormEvent) {
    event.preventDefault();
    setBusy('price');
    setError('');
    setMessage('');
    try {
      const response = await billingApi.createServicePrice(roomingHouseId, {
        ...priceForm,
        unitPrice: Number(priceForm.unitPrice),
        note: priceForm.note?.trim() || null
      });
      setMessage(`Đã tạo giá mới cho ${response.data.serviceName}. Giá cũ đã được lưu vào lịch sử.`);
      await loadPrices();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tạo được bảng giá dịch vụ.'));
    } finally {
      setBusy('');
    }
  }

  async function issueInvoice(invoice: Invoice) {
    setBusy('issue');
    setError('');
    setMessage('');
    try {
      const response = await billingApi.issueInvoice(invoice.id);
      setSelectedInvoice(response.data);
      setInvoices((prev) => prev.map((item) => item.id === response.data.id ? response.data : item));
      setMessage(`Đã phát hành hóa đơn ${response.data.invoiceNo}. Người thuê có thể xem và thanh toán.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không phát hành được hóa đơn.'));
    } finally {
      setBusy('');
      setConfirmAction(null);
    }
  }

  async function cancelInvoice(invoice: Invoice) {
    setBusy('cancel');
    setError('');
    setMessage('');
    try {
      const response = await billingApi.cancelInvoice(invoice.id, 'Chủ trọ đã hủy hóa đơn');
      setSelectedInvoice(response.data);
      setInvoices((prev) => prev.map((item) => item.id === response.data.id ? response.data : item));
      setMessage(`Đã hủy hóa đơn ${response.data.invoiceNo}.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không hủy được hóa đơn.'));
    } finally {
      setBusy('');
      setConfirmAction(null);
    }
  }

  return (
    <div className="billing-shell" style={{ display: 'contents' }}>

      <main className="billing-main">
        {tab !== 'invoices' && (
          <section className="billing-header">
            <div>
              <span className="billing-kicker">Nền tảng quản lý thuê trọ</span>
              <h2>{getTitle(tab)}</h2>
              <p>{getSubtitle(tab)}</p>
            </div>
            <div className="header-actions">
              {tab === 'prices' && roomingHouseId && (
                <button type="button" className="billing-button secondary" onClick={() => void loadPrices()}>
                  Tải lại giá
                </button>
              )}
            </div>
          </section>
        )}

        {tab !== 'invoices' && <NotificationStrip invoices={invoices} />}
        {message && <div className="billing-alert success">{message}</div>}
        {error && <div className="billing-alert error">{error}</div>}

        {tab === 'prices' && (
          <ServicePricesSection
            prices={prices}
            activePrices={activePrices}
            priceHistory={priceHistory}
            serviceTypes={serviceTypes}
            loading={loadingPrices}
            priceForm={priceForm}
            busy={busy}
            onSelectService={selectService}
            onChangeForm={setPriceForm}
            onSubmit={handleCreatePrice}
          />
        )}

        {tab === 'invoices' && (
          <InvoiceListSection
            invoices={filteredInvoices}
            loading={loadingInvoices}
            statusFilter={statusFilter}
            houses={invoiceHouses}
            rooms={invoiceRooms}
            selectedHouseId={selectedHouseId}
            selectedRoomId={selectedRoomId}
            onStatusChange={setStatusFilter}
            onHouseChange={setSelectedHouseId}
            onRoomChange={setSelectedRoomId}
            onOpen={(invoice) => navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(invoice.id))}
            onCreate={() => setIsCreateInvoiceModalOpen(true)}
          />
        )}

        {tab === 'detail' && (
          <InvoiceDetailSection
            invoice={selectedInvoice}
            loading={loadingInvoices}
            busy={busy}
            onIssue={(invoice) => setConfirmAction({
              title: 'Phát hành hóa đơn?',
              body: 'Sau khi phát hành, người thuê sẽ thấy hóa đơn và có thể thanh toán bằng ví nội bộ.',
              onConfirm: () => void issueInvoice(invoice)
            })}
            onCancel={(invoice) => setConfirmAction({
              title: 'Hủy hóa đơn?',
              body: 'Hóa đơn chưa thanh toán sẽ chuyển sang trạng thái đã hủy và người thuê sẽ không thể thanh toán.',
              onConfirm: () => void cancelInvoice(invoice)
            })}
          />
        )}
      </main>

      {confirmAction && (
        <ConfirmDialog
          title={confirmAction.title}
          body={confirmAction.body}
          busy={Boolean(busy)}
          onCancel={() => setConfirmAction(null)}
          onConfirm={confirmAction.onConfirm}
        />
      )}

      {isCreateInvoiceModalOpen && (
        <CentralCreateInvoiceModal
          onClose={() => setIsCreateInvoiceModalOpen(false)}
          onCreated={(invoice) => {
            setIsCreateInvoiceModalOpen(false);
            setMessage(`Đã tạo hóa đơn nháp ${invoice.invoiceNo}.`);
            void loadInvoices();
          }}
        />
      )}
    </div>
  );
}

function ServicePricesSection({
  activePrices,
  priceHistory,
  serviceTypes,
  loading,
  priceForm,
  busy,
  onSelectService,
  onChangeForm,
  onSubmit
}: {
  prices: ServicePrice[];
  activePrices: ServicePrice[];
  priceHistory: ServicePrice[];
  serviceTypes: BillingServiceType[];
  loading: boolean;
  priceForm: CreateServicePriceRequest;
  busy: string;
  onSelectService: (serviceTypeId: string) => void;
  onChangeForm: Dispatch<SetStateAction<CreateServicePriceRequest>>;
  onSubmit: (event: FormEvent) => void;
}) {
  return (
    <section className="billing-grid">
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Hiện tại</span>
            <h3>Giá đang áp dụng</h3>
          </div>
          <span className="count-pill">{activePrices.length}/{serviceTypes.length || 0} đang áp dụng</span>
        </div>

        <div className="service-price-list">
          {loading ? (
            <LoadingBlock text="Đang tải bảng giá..." />
          ) : activePrices.length === 0 ? (
            <EmptyBlock title="Chưa có giá đang áp dụng" text="Hãy tạo giá cho Điện, Nước, Wifi và Rác." />
          ) : (
            activePrices.map((price) => <PriceRow key={price.id} price={price} />)
          )}
        </div>
      </section>

      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Giá mới</span>
            <h3>Tạo giá hiệu lực mới</h3>
          </div>
        </div>

        <form className="billing-form" onSubmit={onSubmit}>
          <label>
            Dịch vụ
            <select value={priceForm.serviceTypeId} onChange={(event) => onSelectService(event.target.value)}>
              {serviceTypes.map((service) => (
                <option key={service.id} value={service.id}>{service.name}</option>
              ))}
            </select>
          </label>
          <div className="form-row">
            <label>
              Cách tính
              <select
                value={priceForm.pricingUnit}
                onChange={(event) => onChangeForm((prev) => ({ ...prev, pricingUnit: event.target.value as PricingUnit }))}
              >
                {getPricingUnitOptions(serviceTypes.find((service) => service.id === priceForm.serviceTypeId)).map((unit) => (
                  <option key={unit} value={unit}>{getPricingUnitLabel(unit)}</option>
                ))}
              </select>
            </label>
            <label>
              Đơn vị
              <input value={getPricingUnitDisplayUnit(priceForm.pricingUnit ?? 'PerMonth', serviceTypes.find((service) => service.id === priceForm.serviceTypeId))} readOnly />
            </label>
          </div>
          <div className="form-row">
            <label>
              Đơn giá
              <input type="number" min="1" value={priceForm.unitPrice} onChange={(event) => onChangeForm((prev) => ({ ...prev, unitPrice: Number(event.target.value) }))} />
            </label>
            <label>
              Hiệu lực từ
              <input type="date" value={priceForm.effectiveFrom} onChange={(event) => onChangeForm((prev) => ({ ...prev, effectiveFrom: event.target.value }))} />
            </label>
          </div>
          <label>
            Ghi chú
            <input value={priceForm.note ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, note: event.target.value }))} placeholder="Lý do thay đổi giá" />
          </label>
          <button type="submit" className="billing-button" disabled={busy === 'price'}>
            {busy === 'price' ? 'Đang lưu...' : 'Tạo giá mới'}
          </button>
        </form>
      </section>

      <section className="billing-panel wide">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Lịch sử</span>
            <h3>Lịch sử giá</h3>
          </div>
        </div>
        {priceHistory.length === 0 ? (
          <EmptyBlock title="Chưa có lịch sử" text="Khi giá thay đổi, bản ghi cũ sẽ nằm ở đây để truy vết." />
        ) : (
          <div className="data-table">
            <div className="table-row table-head">
              <span>Dịch vụ</span><span>Giá</span><span>Đơn vị</span><span>Từ ngày</span><span>Đến ngày</span>
            </div>
            {priceHistory.map((price) => (
              <div key={price.id} className="table-row">
                <span>{price.serviceName}</span><span>{formatMoney(price.unitPrice)}</span><span>{price.displayUnitName}</span><span>{price.effectiveFrom}</span><span>{price.effectiveTo ?? 'Hiện tại'}</span>
              </div>
            ))}
          </div>
        )}
      </section>
    </section>
  );
}

function MeterReadingsSection({
  roomContext,
  form,
  busy,
  consumption,
  invalid,
  dateError,
  createdReading,
  onChangeForm,
  onSubmit
}: {
  roomContext: RoomBillingContext | null;
  form: CreateMeterReadingRequest;
  busy: string;
  consumption: number;
  invalid: boolean;
  dateError: string;
  createdReading: MeterReading | null;
  onChangeForm: Dispatch<SetStateAction<CreateMeterReadingRequest>>;
  onSubmit: (event: FormEvent) => void;
}) {
  const maxBillingDate = roomContext?.contractEndDate && roomContext.contractEndDate < today
    ? roomContext.contractEndDate
    : today;

  return (
    <section className="billing-grid">
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Dịch vụ theo chỉ số</span>
            <h3>Nhập chỉ số điện nước</h3>
          </div>
        </div>
        <form className="billing-form" onSubmit={onSubmit}>
          {roomContext && <RoomContextCard context={roomContext} showContract={false} />}
          <div className="form-row">
            <label>
              Dịch vụ
              <input value={form.serviceTypeId} onChange={(event) => onChangeForm((prev) => ({ ...prev, serviceTypeId: event.target.value }))} />
            </label>
            <label>
              Ảnh minh chứng
              <input value={form.proofImageObjectKey ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, proofImageObjectKey: event.target.value }))} placeholder="Không bắt buộc" />
            </label>
          </div>
          <div className="form-row">
            <label>
              Từ ngày
              <input
                type="date"
                min={roomContext?.contractStartDate}
                max={maxBillingDate}
                value={form.billingPeriodStart}
                onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodStart: event.target.value }))}
              />
            </label>
            <label>
              Đến ngày
              <input
                type="date"
                min={roomContext?.contractStartDate}
                max={maxBillingDate}
                value={form.billingPeriodEnd}
                onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodEnd: event.target.value }))}
              />
            </label>
          </div>
          <div className="form-row">
            <label>
              Chỉ số đầu
              <input type="number" min="0" value={form.previousReading} onChange={(event) => onChangeForm((prev) => ({ ...prev, previousReading: Number(event.target.value) }))} />
            </label>
            <label>
              Chỉ số cuối
              <input type="number" min="0" value={form.currentReading} onChange={(event) => onChangeForm((prev) => ({ ...prev, currentReading: Number(event.target.value) }))} />
            </label>
          </div>
          <div className={`computed-strip ${invalid || dateError ? 'invalid' : ''}`}>
            Tiêu thụ: <strong>{invalid ? 0 : consumption}</strong>
            {invalid && <span>Chỉ số cuối không được nhỏ hơn chỉ số đầu.</span>}
            {dateError && <span>{dateError}</span>}
          </div>
          <button type="submit" className="billing-button" disabled={busy === 'reading' || invalid || Boolean(dateError)}>
            {busy === 'reading' ? 'Đang ghi...' : 'Ghi chỉ số'}
          </button>
        </form>
      </section>

      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Kết quả</span>
            <h3>Bản ghi mới nhất</h3>
          </div>
        </div>
        {createdReading ? (
          <div className="result-box">
            <strong>{createdReading.serviceName}</strong>
            <span>{createdReading.billingPeriodStart} - {createdReading.billingPeriodEnd}</span>
            <span>{createdReading.previousReading} - {createdReading.currentReading}</span>
            <span>Tiêu thụ: {createdReading.consumption}</span>
          </div>
        ) : (
          <EmptyBlock title="Chưa ghi chỉ số" text="Sau khi lưu thành công, bản ghi mới sẽ hiển thị tại đây." />
        )}
      </section>
    </section>
  );
}

function InvoiceListSection({
  invoices,
  loading,
  statusFilter,
  houses,
  rooms,
  selectedHouseId,
  selectedRoomId,
  onStatusChange,
  onHouseChange,
  onRoomChange,
  onOpen,
  onCreate
}: {
  invoices: Invoice[];
  loading: boolean;
  statusFilter: string;
  houses: Array<{ id: string; name: string }>;
  rooms: Array<{ id: string; name: string }>;
  selectedHouseId: string;
  selectedRoomId: string;
  onStatusChange: (value: string) => void;
  onHouseChange: (value: string) => void;
  onRoomChange: (value: string) => void;
  onOpen: (invoice: Invoice) => void;
  onCreate: () => void;
}) {
  return (
    <>
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">Quản lý</p>
          <h2>Hóa đơn cho thuê</h2>
          <p className="overview-description">Xem và lọc hóa đơn theo khu trọ, phòng và trạng thái thanh toán.</p>
        </div>

        <div className="overview-right" style={{ flexDirection: 'column', alignItems: 'flex-end', gap: '0.5rem' }}>
          <div className="filter-group" style={{ display: 'flex', gap: '1rem', background: '#fff', padding: '0.75rem', borderRadius: '0.5rem', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', color: '#6b7280', marginBottom: '0.25rem' }}>Khu trọ</label>
              <select
                value={selectedHouseId}
                onChange={(event) => onHouseChange(event.target.value)}
                style={{ padding: '0.5rem', borderRadius: '0.25rem', border: '1px solid #d1d5db', outline: 'none' }}
              >
                <option value="">Tất cả khu trọ</option>
                {houses.map((house) => (
                  <option key={house.id} value={house.id}>{house.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', color: '#6b7280', marginBottom: '0.25rem' }}>Phòng</label>
              <select
                value={selectedRoomId}
                onChange={(event) => onRoomChange(event.target.value)}
                disabled={!selectedHouseId}
                style={{ padding: '0.5rem', borderRadius: '0.25rem', border: '1px solid #d1d5db', outline: 'none', background: !selectedHouseId ? '#f3f4f6' : '#fff' }}
              >
                <option value="">Tất cả phòng</option>
                {rooms.map((room) => (
                  <option key={room.id} value={room.id}>Phòng {room.name}</option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </section>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', margin: '0 0 16px 0', borderBottom: '1px solid #e5e7eb' }}>
        <div className="tabs" style={{ display: 'flex', alignItems: 'center', overflowX: 'auto', gap: '8px', flex: 1 }}>
          <InvoiceStatusTab status="all" activeStatus={statusFilter} onClick={onStatusChange}>Tất cả</InvoiceStatusTab>
          {invoiceStatuses.map((status) => (
            <InvoiceStatusTab key={status} status={status} activeStatus={statusFilter} onClick={onStatusChange}>
              {getInvoiceStatusLabel(status)}
            </InvoiceStatusTab>
          ))}
        </div>
        <button type="button" className="billing-button" onClick={onCreate} style={{ flexShrink: 0 }}>
          Tạo hóa đơn
        </button>
      </div>

      {loading ? (
        <div className="empty-panel">Đang tải dữ liệu hóa đơn...</div>
      ) : invoices.length === 0 ? (
        <div className="empty-panel">
          <h2>Không tìm thấy hóa đơn</h2>
          <p>Chưa có hóa đơn nào phù hợp với bộ lọc hiện tại.</p>
        </div>
      ) : (
        <section className="card-grid">
          {invoices.map((invoice) => (
            <div
              className="dashboard-card"
              key={invoice.id}
              onClick={() => onOpen(invoice)}
              style={{ textAlign: 'left', cursor: 'pointer' }}
            >
              <div className="card-body-content" style={{ padding: '1rem', width: '100%', boxSizing: 'border-box' }}>
                <div className="card-title-row" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.75rem' }}>
                  <h3 style={{ margin: 0, fontSize: '1.125rem' }}>{invoice.invoiceNo}</h3>
                  <span className={`status-chip ${invoice.status.toLowerCase()}`}>{getInvoiceStatusLabel(invoice.status)}</span>
                </div>

                <div className="card-location" style={{ marginTop: '0.5rem', color: '#6b7280', fontSize: '0.875rem', display: 'flex', alignItems: 'center' }}>
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '0.25rem', flexShrink: 0 }}>
                    <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
                    <polyline points="9 22 9 12 15 12 15 22"></polyline>
                  </svg>
                  <span>{invoice.roomingHouseName} - Phòng {invoice.roomNumber}</span>
                </div>

                <hr className="card-divider" style={{ margin: '1rem 0', borderColor: '#e5e7eb' }} />

                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem' }}>
                    <span style={{ color: '#6b7280' }}>Người thuê:</span>
                    <strong>{invoice.tenantName || invoice.tenantEmail}</strong>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem' }}>
                    <span style={{ color: '#6b7280' }}>Kỳ hóa đơn:</span>
                    <span>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem' }}>
                    <span style={{ color: '#6b7280' }}>Hạn thanh toán:</span>
                    <span>{invoice.dueDate}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem' }}>
                    <span style={{ color: '#6b7280' }}>Tổng tiền:</span>
                    <strong style={{ color: '#10b981' }}>{formatMoney(invoice.totalAmount)}</strong>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </section>
      )}
    </>
  );
}

type ActiveInvoiceContract = Pick<
  ContractHistoryItemResponse,
  'id' | 'roomId' | 'roomNumber' | 'roomingHouseId' | 'roomingHouseName' | 'mainTenantName' | 'startDate' | 'endDate' | 'monthlyRent' | 'paymentDay' | 'occupants'
>;

type ReadingDraft = {
  previousReading: number;
  currentReading: number;
  proofImageObjectKey: string;
};

function CentralCreateInvoiceModal({
  onClose,
  onCreated
}: {
  onClose: () => void;
  onCreated: (invoice: Invoice) => void;
}) {
  const [contracts, setContracts] = useState<ActiveInvoiceContract[]>([]);
  const [selectedHouseId, setSelectedHouseId] = useState('');
  const [selectedContractId, setSelectedContractId] = useState('');
  const [billingMonth, setBillingMonth] = useState('');
  const [prices, setPrices] = useState<ServicePrice[]>([]);
  const [preview, setPreview] = useState<RoomInvoicePreview | null>(null);
  const [serviceTypes, setServiceTypes] = useState<BillingServiceType[]>([]);
  const [appendices, setAppendices] = useState<ContractAppendixResponse[]>([]);
  const [existingInvoices, setExistingInvoices] = useState<Invoice[]>([]);
  const [latestReadingByServiceType, setLatestReadingByServiceType] = useState<Record<string, LatestMeterReading>>({});
  const [readings, setReadings] = useState<Record<string, ReadingDraft>>({});
  const [discountAmount, setDiscountAmount] = useState(0);
  const [note, setNote] = useState('');
  const [loadingContracts, setLoadingContracts] = useState(true);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const houses = useMemo(() => {
    const map = new Map<string, string>();
    contracts.forEach((contract) => {
      map.set(contract.roomingHouseId, contract.roomingHouseName);
    });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [contracts]);
  const rooms = useMemo(
    () => contracts.filter((contract) => contract.roomingHouseId === selectedHouseId),
    [contracts, selectedHouseId]
  );
  const selectedContract = useMemo(
    () => contracts.find((contract) => contract.id === selectedContractId) ?? null,
    [contracts, selectedContractId]
  );

  useEffect(() => {
    let cancelled = false;

    async function loadContracts() {
      setLoadingContracts(true);
      setError('');
      try {
        const response = await contractApi.getLandlordContracts();
        if (cancelled) return;
        setContracts((response.data ?? []).filter((contract) => contract.status === 'Active'));
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err, 'Không thể tải danh sách phòng đang có hợp đồng active.'));
        }
      } finally {
        if (!cancelled) {
          setLoadingContracts(false);
        }
      }
    }

    void loadContracts();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    setSelectedContractId('');
  }, [selectedHouseId]);

  useEffect(() => {
    if (!selectedContract) {
      setBillingMonth('');
      setPrices([]);
      setPreview(null);
      setAppendices([]);
      setExistingInvoices([]);
      setLatestReadingByServiceType({});
      setReadings({});
      return;
    }

    setBillingMonth(getCentralDefaultInvoiceMonth(selectedContract));
  }, [selectedContract]);

  useEffect(() => {
    if (!selectedContract) return;

    let cancelled = false;

    async function loadInvoiceContext(contract: ActiveInvoiceContract) {
      setLoadingDetails(true);
      setError('');
      try {
        const [priceResponse, invoiceResponse, appendixResponse, contextResponse] = await Promise.all([
          billingApi.getServicePrices(contract.roomingHouseId),
          billingApi.getLandlordInvoices({ contractId: contract.id }),
          contractApi.getAppendices(contract.id),
          billingApi.getRoomBillingContext(contract.roomId)
        ]);
        if (cancelled) return;

        setPrices(priceResponse.data);
        setExistingInvoices(invoiceResponse.data);
        setAppendices(appendixResponse.data ?? []);
        const latestReadings = contextResponse.data.latestReadingByServiceType ?? {};
        setLatestReadingByServiceType(latestReadings);

        const nextReadings: Record<string, ReadingDraft> = {};
        priceResponse.data
          .filter((price) => price.isActive && isCentralMeteredServicePrice(price))
          .forEach((price) => {
            const latestReading = latestReadings[price.serviceTypeId];
            nextReadings[price.serviceTypeId] = {
              previousReading: latestReading?.currentReading ?? 0,
              currentReading: latestReading?.currentReading ?? 0,
              proofImageObjectKey: ''
            };
          });
        setReadings(nextReadings);
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err, 'Không thể tải thông tin tạo hóa đơn cho phòng đã chọn.'));
        }
      } finally {
        if (!cancelled) {
          setLoadingDetails(false);
        }
      }
    }

    void loadInvoiceContext(selectedContract);

    return () => {
      cancelled = true;
    };
  }, [selectedContract]);

  const period = useMemo(
    () => selectedContract && billingMonth ? resolveCentralInvoicePeriodForContract(billingMonth, selectedContract) : null,
    [billingMonth, selectedContract]
  );
  const effectivePrices = useMemo(
    () => period ? getCentralEffectiveServicePrices(prices, period.start) : [],
    [prices, period]
  );
  const fixedPrices = useMemo(
    () => preview?.fixedServices ?? [],
    [preview]
  );
  const meteredPrices = useMemo(
    () => preview?.meteredServices ?? [],
    [preview]
  );
  const todayDateString = centralToDateOnlyString(getCentralTodayDateOnly());
  const periodIsFuture = period ? compareCentralDateOnly(period.end, todayDateString) > 0 : false;
  const periodValidationMessage = !selectedContract
    ? ''
    : !period
      ? 'Tháng hóa đơn không nằm trong thời hạn hợp đồng.'
      : '';
  const previewBlockReason = preview && !preview.canGenerate
    ? preview.blockReason || 'Chưa thể tạo hóa đơn cho kỳ này.'
    : '';
  useEffect(() => {
    if (!selectedContract || !period) {
      setPreview(null);
      return;
    }

    let cancelled = false;

    async function loadPreview() {
      setLoadingDetails(true);
      setError('');
      try {
        const response = await billingApi.getRoomInvoicePreview(selectedContract!.roomId, {
          billingPeriodStart: period!.start,
          billingPeriodEnd: period!.end
        });
        if (cancelled) return;

        setPreview(response.data);
        const latestReadings: Record<string, LatestMeterReading> = {};
        const nextReadings: Record<string, ReadingDraft> = {};
        response.data.meteredServices.forEach((service) => {
          if (service.latestReading) {
            latestReadings[service.serviceTypeId] = service.latestReading;
          }
          nextReadings[service.serviceTypeId] = {
            previousReading: service.latestReading?.currentReading ?? 0,
            currentReading: service.latestReading?.currentReading ?? 0,
            proofImageObjectKey: ''
          };
        });
        setLatestReadingByServiceType(latestReadings);
        setReadings(nextReadings);
      } catch (err) {
        if (!cancelled) {
          setPreview(null);
          setError(getApiErrorMessage(err, 'Không thể tải thông tin xem trước hóa đơn.'));
        }
      } finally {
        if (!cancelled) {
          setLoadingDetails(false);
        }
      }
    }

    void loadPreview();

    return () => {
      cancelled = true;
    };
  }, [selectedContract, period?.start, period?.end]);

  const resolvedMonthlyRent = selectedContract && period
    ? preview?.monthlyRent ?? resolveCentralMonthlyRentFromAppendices(selectedContract.monthlyRent, appendices, period.start)
    : selectedContract?.monthlyRent ?? 0;
  const occupantCount = fixedPrices[0]?.occupantCount ?? (selectedContract && period
    ? getCentralActiveOccupantCount(selectedContract, period)
    : 1);
  const rentPreview = preview?.rentAmount ?? (selectedContract && period ? calculateCentralPeriodAmount(resolvedMonthlyRent, period) : 0);
  const fixedTotal = preview?.fixedServiceAmount ?? 0;
  const utilityPreview = meteredPrices.reduce((sum, price) => {
    const draft = readings[price.serviceTypeId];
    if (!draft) return sum;
    const latestReading = price.latestReading;
    const previousReading = latestReading?.currentReading ?? Number(draft.previousReading);
    const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
    return sum + consumption * price.unitPrice;
  }, 0);
  const previewTotal = Math.max(0, rentPreview + fixedTotal + utilityPreview - discountAmount);

  function updateReading(serviceTypeId: string, patch: Partial<ReadingDraft>) {
    setReadings((current) => ({
      ...current,
      [serviceTypeId]: {
        ...(current[serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '' }),
        ...patch
      }
    }));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');

    if (!selectedContract) {
      setError('Vui lòng chọn phòng cần tạo hóa đơn.');
      return;
    }

    if (!period) {
      setError(periodValidationMessage || 'Kỳ hóa đơn không hợp lệ.');
      return;
    }

    if (previewBlockReason) {
      setError(previewBlockReason);
      return;
    }

    const meterReadings: MeterReadingInput[] = meteredPrices.map((price) => {
      const draft = readings[price.serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '' };
      const latestReading = latestReadingByServiceType[price.serviceTypeId];
      return {
        serviceTypeId: price.serviceTypeId,
        previousReading: latestReading ? null : Number(draft.previousReading),
        currentReading: Number(draft.currentReading),
        proofImageObjectKey: draft.proofImageObjectKey.trim() || null
      };
    });

    for (const reading of meterReadings) {
      const latestReading = latestReadingByServiceType[reading.serviceTypeId];
      const previousReading = latestReading?.currentReading ?? reading.previousReading;
      if (previousReading !== null && previousReading !== undefined && reading.currentReading < previousReading) {
        setError('Chỉ số mới không được nhỏ hơn chỉ số cũ.');
        return;
      }
    }

    setSubmitting(true);
    try {
      const response = await billingApi.generateWithReadings({
        contractId: selectedContract.id,
        billingPeriodStart: period.start,
        billingPeriodEnd: period.end,
        discountAmount: Number(discountAmount) || 0,
        note: note.trim() || null,
        meterReadings
      });
      onCreated(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo hóa đơn.'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content invoice-create-modal">
        <div className="history-modal-header">
          <h2>Tạo hóa đơn</h2>
          <button className="history-modal-close" onClick={onClose} type="button">&times;</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="history-modal-body">
            {error && <div className="billing-alert error" style={{ marginBottom: '12px' }}>{error}</div>}

            <div className="invoice-create-selector">
              <label className="invoice-create-field">
                <span className="label">Khu trọ</span>
                <select
                  value={selectedHouseId}
                  onChange={(event) => setSelectedHouseId(event.target.value)}
                  disabled={loadingContracts}
                >
                  <option value="">Chọn khu trọ</option>
                  {houses.map((house) => (
                    <option key={house.id} value={house.id}>{house.name}</option>
                  ))}
                </select>
              </label>
              <label className="invoice-create-field">
                <span className="label">Phòng</span>
                <select
                  value={selectedContractId}
                  onChange={(event) => setSelectedContractId(event.target.value)}
                  disabled={!selectedHouseId || loadingContracts}
                >
                  <option value="">Chọn phòng đang có hợp đồng active</option>
                  {rooms.map((contract) => (
                    <option key={contract.id} value={contract.id}>
                      Phòng {contract.roomNumber} - {contract.mainTenantName}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            {loadingContracts ? (
              <p>Đang tải danh sách phòng...</p>
            ) : contracts.length === 0 ? (
              <p>Chưa có phòng nào đang có hợp đồng active.</p>
            ) : !selectedContract ? (
              <div className="invoice-create-placeholder">Chọn khu trọ và phòng để xem thông tin tạo hóa đơn.</div>
            ) : loadingDetails ? (
              <p>Đang tải dữ liệu hóa đơn...</p>
            ) : (
              <div className="invoice-create-stack">
                {periodValidationMessage && <div className="billing-alert error" style={{ marginTop: '16px', marginBottom: '-4px' }}>{periodValidationMessage}</div>}
                {previewBlockReason && <div className="billing-alert error" style={{ marginTop: '16px', marginBottom: '-4px' }}>{previewBlockReason}</div>}
                <div className="invoice-create-grid">
                  <div className="invoice-create-field">
                    <span className="label">Phòng</span>
                    <span className="value">{selectedContract.roomNumber}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Người thuê</span>
                    <span className="value">{selectedContract.mainTenantName}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Tiền phòng</span>
                    <span className="value">{formatMoney(resolvedMonthlyRent)}</span>
                  </div>
                  <label className="invoice-create-field">
                    <span className="label">Kỳ hóa đơn</span>
                    <input type="month" value={billingMonth} onChange={(event) => setBillingMonth(event.target.value)} />
                  </label>
                  <div className="invoice-create-field">
                    <span className="label">Kỳ thực tế</span>
                    <span className="value">{period ? `${period.start} - ${period.end}` : '--'}</span>
                  </div>
                  <div className="invoice-create-field">
                    <span className="label">Tiền phòng kỳ này</span>
                    <span className="value">{formatMoney(rentPreview)}</span>
                  </div>
                </div>

                <section className="invoice-create-section">
                  <h3>Dịch vụ cố định</h3>
                  {fixedPrices.length === 0 ? (
                    <p className="invoice-create-empty">Chưa có dịch vụ cố định được cấu hình.</p>
                  ) : (
                    <div className="invoice-create-stack">
                      {fixedPrices.map((price) => (
                        <div key={price.serviceTypeId} className="invoice-create-line">
                          <span>{price.serviceName} / {price.displayUnitName}</span>
                          <strong>{formatCentralFixedPreviewLine(price)}</strong>
                        </div>
                      ))}
                    </div>
                  )}
                </section>

                <section className="invoice-create-section">
                  <h3>Chỉ số điện nước</h3>
                  {meteredPrices.length === 0 ? (
                    <p className="invoice-create-empty">Không có dịch vụ điện/nước nào đang cấu hình theo chỉ số.</p>
                  ) : (
                    <div className="invoice-create-meter-list">
                      {meteredPrices.map((price) => {
                        const draft = readings[price.serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '' };
                        const latestReading = latestReadingByServiceType[price.serviceTypeId];
                        const previousReading = latestReading?.currentReading ?? Number(draft.previousReading);
                        const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
                        const amount = Math.round(consumption * price.unitPrice);
                        return (
                          <div key={price.serviceTypeId} className="invoice-create-meter-card">
                            <strong>{price.serviceName} ({formatMoney(price.unitPrice)} / {price.meterUnitName})</strong>
                            <div className="invoice-create-grid">
                              {latestReading ? (
                                <div className="invoice-create-field">
                                  <span className="label">Chỉ số cũ</span>
                                  <strong>{latestReading.currentReading}</strong>
                                </div>
                              ) : (
                                <label className="invoice-create-field">
                                  <span className="label">Chỉ số cũ</span>
                                  <input
                                    type="number"
                                    min="0"
                                    value={draft.previousReading}
                                    onChange={(event) => updateReading(price.serviceTypeId, { previousReading: Number(event.target.value) })}
                                  />
                                </label>
                              )}
                              <label className="invoice-create-field">
                                <span className="label">Chỉ số mới</span>
                                <input
                                  type="number"
                                  min="0"
                                  value={draft.currentReading}
                                  onChange={(event) => updateReading(price.serviceTypeId, { currentReading: Number(event.target.value) })}
                                />
                              </label>
                              <div className="invoice-create-field invoice-create-total">
                                <span className="label">Tạm tính</span>
                                <strong>{formatNumber(consumption)} x {formatMoney(price.unitPrice)} = {formatMoney(amount)}</strong>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </section>

                <div className="invoice-create-summary">
                  <label className="invoice-create-field">
                    <span className="label">Giảm trừ</span>
                    <input type="number" min="0" value={discountAmount} onChange={(event) => setDiscountAmount(Number(event.target.value))} />
                  </label>
                  <label className="invoice-create-field">
                    <span className="label">Ghi chú</span>
                    <input value={note} onChange={(event) => setNote(event.target.value)} />
                  </label>
                  <div className="invoice-create-field invoice-create-total">
                    <span className="label">Tạm tính</span>
                    <span className="value">{formatMoney(previewTotal)}</span>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="history-modal-footer">
            <button type="button" className="billing-button secondary" onClick={onClose} disabled={submitting}>Đóng</button>
            <button type="submit" className="billing-button" disabled={loadingContracts || loadingDetails || submitting || !selectedContract || !period || !preview || Boolean(previewBlockReason)}>
              {submitting ? 'Đang tạo...' : 'Tạo hóa đơn nháp'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function InvoiceStatusTab({
  status,
  activeStatus,
  onClick,
  children
}: {
  status: string;
  activeStatus: string;
  onClick: (value: string) => void;
  children: ReactNode;
}) {
  const active = activeStatus === status;

  return (
    <button
      type="button"
      onClick={() => onClick(status)}
      style={{ padding: '12px 16px', background: 'none', border: 'none', borderBottom: active ? '2px solid #2563eb' : '2px solid transparent', color: active ? '#2563eb' : '#6b7280', fontWeight: active ? 600 : 500, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 0.2s' }}
    >
      {children}
    </button>
  );
}

function LegacyInvoiceListSection({
  invoices,
  loading,
  statusFilter,
  search,
  onStatusChange,
  onSearchChange,
  onSearch,
  onOpen,
  onCreate
}: {
  invoices: Invoice[];
  loading: boolean;
  statusFilter: string;
  search: string;
  onStatusChange: (value: string) => void;
  onSearchChange: (value: string) => void;
  onSearch: () => void;
  onOpen: (invoice: Invoice) => void;
  onCreate: () => void;
}) {
  return (
    <section className="billing-panel">
      <div className="panel-heading">
        <div>
          <span className="billing-kicker">Hóa đơn</span>
          <h3>Danh sách hóa đơn</h3>
        </div>
        <button type="button" className="billing-button" onClick={onCreate}>Tạo hóa đơn</button>
      </div>
      <div className="filter-bar">
        <select value={statusFilter} onChange={(event) => onStatusChange(event.target.value)}>
          <option value="">Tất cả trạng thái</option>
          {invoiceStatuses.map((status) => <option key={status} value={status}>{getInvoiceStatusLabel(status)}</option>)}
        </select>
        <input value={search} onChange={(event) => onSearchChange(event.target.value)} placeholder="Tìm theo mã hóa đơn, phòng, người thuê..." />
        <button type="button" className="billing-button secondary" onClick={onSearch}>Tìm</button>
      </div>
      {loading ? (
        <LoadingBlock text="Đang tải hóa đơn..." />
      ) : invoices.length === 0 ? (
        <EmptyBlock title="Chưa có hóa đơn" text="Tạo hóa đơn nháp cho phòng có hợp đồng đang hiệu lực để bắt đầu thu phí." />
      ) : (
        <div className="data-table">
          <div className="table-row table-head invoice-table-row">
            <span>Mã hóa đơn</span><span>Phòng</span><span>Người thuê</span><span>Kỳ</span><span>Tổng tiền</span><span>Hạn thanh toán</span><span>Trạng thái</span><span></span>
          </div>
          {invoices.map((invoice) => (
            <div key={invoice.id} className="table-row invoice-table-row">
              <span>{invoice.invoiceNo}</span>
              <span>Phòng {invoice.roomNumber}</span>
              <span>{invoice.tenantName || invoice.tenantEmail}</span>
              <span>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</span>
              <strong>{formatMoney(invoice.totalAmount)}</strong>
              <span>{invoice.dueDate}</span>
              <span className={`status-chip ${invoice.status.toLowerCase()}`}>{getInvoiceStatusLabel(invoice.status)}</span>
              <button type="button" className="link-button" onClick={() => onOpen(invoice)}>Chi tiết</button>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function InvoiceCreateSection({
  roomContext,
  form,
  busy,
  previewTotals,
  onChangeForm,
  onSubmit
}: {
  roomContext: RoomBillingContext | null;
  form: GenerateInvoiceDraftRequest;
  busy: string;
  previewTotals: { fixedServices: number; discount: number; estimatedTotal: number };
  onChangeForm: Dispatch<SetStateAction<GenerateInvoiceDraftRequest>>;
  onSubmit: (event: FormEvent) => void;
}) {
  return (
    <section className="billing-grid">
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Hóa đơn nháp</span>
            <h3>Tạo hóa đơn nháp</h3>
          </div>
        </div>
        <form className="billing-form" onSubmit={onSubmit}>
          {roomContext && <RoomContextCard context={roomContext} showContract={false} />}
          <div className="form-row">
            <label>
              Từ ngày
              <input
                type="month"
                value={toMonthValue(form.billingPeriodStart)}
                onChange={(event) => {
                  const range = getMonthRange(event.target.value);
                  onChangeForm((prev) => ({
                    ...prev,
                    billingPeriodStart: range.start,
                    billingPeriodEnd: range.end
                  }));
                }}
              />
            </label>
            <label>
              Đến ngày
              <input type="date" value={form.billingPeriodEnd} readOnly />
            </label>
          </div>
          <label>
            Giảm trừ
            <input type="number" min="0" value={form.discountAmount} onChange={(event) => onChangeForm((prev) => ({ ...prev, discountAmount: Number(event.target.value) }))} />
          </label>
          <label>
            Ghi chú
            <input value={form.note ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, note: event.target.value }))} placeholder="Hóa đơn tháng này" />
          </label>
          <button type="submit" className="billing-button" disabled={busy === 'invoice'}>
            {busy === 'invoice' ? 'Đang tạo...' : 'Tạo hóa đơn nháp'}
          </button>
        </form>
      </section>
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Xem trước</span>
            <h3>Kiểm tra trước khi tạo</h3>
          </div>
        </div>
        <div className="check-list">
          <span>Phòng phải đang có người thuê.</span>
          <span>Dịch vụ điện và nước cần có bản ghi chỉ số cùng kỳ.</span>
          <span>Giá dịch vụ sẽ được chốt vào từng hạng mục hóa đơn.</span>
          <span>Hệ thống không cho tạo trùng hóa đơn cùng hợp đồng và cùng kỳ.</span>
        </div>
        <div className="invoice-summary single">
          <span>Dịch vụ cố định <strong>{formatMoney(previewTotals.fixedServices)}</strong></span>
          <span>Giảm trừ <strong>{formatMoney(previewTotals.discount)}</strong></span>
          <span>Tạm tính dịch vụ cố định <strong>{formatMoney(previewTotals.estimatedTotal)}</strong></span>
        </div>
      </section>
    </section>
  );
}

function InvoiceDetailSection({
  invoice,
  loading,
  busy,
  onIssue,
  onCancel
}: {
  invoice: Invoice | null;
  loading: boolean;
  busy: string;
  onIssue: (invoice: Invoice) => void;
  onCancel: (invoice: Invoice) => void;
}) {
  if (loading) {
    return <section className="billing-panel"><LoadingBlock text="Đang tải chi tiết hóa đơn..." /></section>;
  }

  if (!invoice) {
    return <section className="billing-panel"><EmptyBlock title="Không có hóa đơn" text="Chọn một hóa đơn từ danh sách để xem chi tiết." /></section>;
  }

  return (
    <section className="billing-panel invoice-detail">
      <div className="invoice-topline">
        <div>
          <span className="billing-kicker">Chi tiết hóa đơn</span>
          <h3>{invoice.invoiceNo}</h3>
        </div>
        <span className={`status-chip ${invoice.status.toLowerCase()}`}>{getInvoiceStatusLabel(invoice.status)}</span>
      </div>
      <div className="invoice-summary">
        <span>Tiền phòng <strong>{formatMoney(invoice.rentAmount)}</strong></span>
        <span>Điện nước <strong>{formatMoney(invoice.utilityAmount)}</strong></span>
        <span>Dịch vụ cố định <strong>{formatMoney(invoice.serviceAmount)}</strong></span>
        <span>Tổng cộng <strong>{formatMoney(invoice.totalAmount)}</strong></span>
      </div>
      <div className="data-table">
        <div className="table-row table-head item-table-row">
          <span>Hạng mục</span><span>Mô tả</span><span>Số lượng</span><span>Đơn giá chốt</span><span>Thành tiền</span>
        </div>
        {invoice.items.map((item) => (
          <div key={item.id} className="table-row item-table-row">
            <span>{getInvoiceItemTypeLabel(item.itemType)}</span>
            <span>{item.description}</span>
            <span>{item.quantity}</span>
            <span>{formatMoney(item.unitPrice)}</span>
            <strong>{formatMoney(item.amount)}</strong>
          </div>
        ))}
      </div>
      <div className="action-row">
        <button type="button" className="billing-button" disabled={invoice.status !== 'Draft' || busy === 'issue'} onClick={() => onIssue(invoice)}>
          {busy === 'issue' ? 'Đang phát hành...' : 'Phát hành'}
        </button>
        <button type="button" className="billing-button danger" disabled={invoice.status === 'Paid' || invoice.status === 'Cancelled' || busy === 'cancel'} onClick={() => onCancel(invoice)}>
          {busy === 'cancel' ? 'Đang hủy...' : 'Hủy hóa đơn'}
        </button>
      </div>
    </section>
  );
}

function NotificationStrip({ invoices }: { invoices: Invoice[] }) {
  const issuedCount = invoices.filter((invoice) => invoice.status === 'Issued').length;
  const dueSoon = invoices.filter((invoice) => {
    const dueTime = new Date(invoice.dueDate).getTime();
    const diffDays = Math.ceil((dueTime - Date.now()) / 86400000);
    return invoice.status === 'Issued' && diffDays >= 0 && diffDays <= 3;
  }).length;

  if (issuedCount === 0 && dueSoon === 0) {
    return null;
  }

  return (
    <div className="billing-alert info">
      {issuedCount > 0 && <span>{issuedCount} hóa đơn đang chờ thanh toán.</span>}
      {dueSoon > 0 && <span>{dueSoon} hóa đơn sắp đến hạn trong 3 ngày.</span>}
    </div>
  );
}

function RoomContextCard({ context, showContract = true }: { context: RoomBillingContext; showContract?: boolean }) {
  return (
    <div className="room-context-card">
      <div>
        <span className="billing-kicker">Tên phòng</span>
        <strong>Phòng {context.roomNumber}</strong>
      </div>
      <div>
        <span>Chủ phòng</span>
        <strong>{context.tenantName || context.tenantEmail}</strong>
      </div>
      {showContract && (
        <div>
          <span>Hợp đồng</span>
          <strong>{context.contractNumber}</strong>
        </div>
      )}
      <div>
        <span>Tiền phòng</span>
        <strong>{formatMoney(context.monthlyRent)}</strong>
      </div>
    </div>
  );
}

function PriceRow({ price }: { price: ServicePrice }) {
  return (
    <div className="price-row">
      <div>
        <strong>{price.serviceName}</strong>
        <span>{getPricingUnitLabel(price.pricingUnit)} / {price.displayUnitName}</span>
      </div>
      <div>
        <strong>{formatMoney(price.unitPrice)}</strong>
        <span>{price.effectiveFrom} - {price.effectiveTo ?? 'Hiện tại'}</span>
      </div>
    </div>
  );
}

function ConfirmDialog({ title, body, busy, onCancel, onConfirm }: { title: string; body: string; busy: boolean; onCancel: () => void; onConfirm: () => void }) {
  return (
    <div className="modal-backdrop">
      <div className="confirm-dialog">
        <h3>{title}</h3>
        <p>{body}</p>
        <div className="action-row">
          <button type="button" className="billing-button secondary" onClick={onCancel} disabled={busy}>Đóng</button>
          <button type="button" className="billing-button" onClick={onConfirm} disabled={busy}>Xác nhận</button>
        </div>
      </div>
    </div>
  );
}

function NavButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return <button type="button" className={`billing-nav ${active ? 'active' : ''}`} onClick={onClick}>{children}</button>;
}

function LoadingBlock({ text }: { text: string }) {
  return <div className="state-block loading-state">{text}</div>;
}

function EmptyBlock({ title, text }: { title: string; text: string }) {
  return <div className="state-block empty-state"><h3>{title}</h3><p>{text}</p></div>;
}

type CentralResolvedInvoicePeriod = {
  start: string;
  end: string;
  billableDays: number;
  daysInMonth: number;
  isFullMonth: boolean;
};

function normalizeCentralPricingUnit(unit: PricingUnit | string) {
  if (unit === 'Metered' || unit === 'MeterBased') {
    return 'MeterReading';
  }

  if (unit === 'Fixed' || unit === 'PerMonth') {
    return 'PerMonth';
  }

  if (unit === 'PerPerson' || unit === 'PerPersonPerMonth') {
    return 'PerPersonPerMonth';
  }

  return unit;
}

function isCentralMeteredServicePrice(price: ServicePrice) {
  return normalizeCentralPricingUnit(price.pricingUnit) === 'MeterReading';
}

function calculateCentralFixedServiceAmount(
  price: ServicePrice,
  period: CentralResolvedInvoicePeriod,
  occupantCount: number
) {
  const unitCount = normalizeCentralPricingUnit(price.pricingUnit) === 'PerPersonPerMonth'
    ? occupantCount
    : 1;

  return Math.round(price.unitPrice * unitCount * getCentralPeriodQuantity(period));
}

function formatCentralFixedServicePreview(
  price: ServicePrice,
  period: CentralResolvedInvoicePeriod | null,
  occupantCount: number
) {
  if (!period) {
    return formatMoney(0);
  }

  const amount = calculateCentralFixedServiceAmount(price, period, occupantCount);

  if (normalizeCentralPricingUnit(price.pricingUnit) !== 'PerPersonPerMonth') {
    return formatMoney(amount);
  }

  const unitAmount = Math.round(price.unitPrice * getCentralPeriodQuantity(period));
  return `${formatMoney(unitAmount)} x ${occupantCount} = ${formatMoney(amount)}`;
}

function formatCentralFixedPreviewLine(price: FixedServicePreview) {
  if (normalizeCentralPricingUnit(price.pricingUnit) !== 'PerPersonPerMonth') {
    return formatMoney(price.amount);
  }

  const unitAmount = price.occupantCount > 0
    ? Math.round(price.amount / price.occupantCount)
    : price.unitPrice;

  return `${formatMoney(unitAmount)} x ${price.occupantCount} = ${formatMoney(price.amount)}`;
}

function getCentralPeriodQuantity(period: CentralResolvedInvoicePeriod) {
  return period.isFullMonth ? 1 : period.billableDays / period.daysInMonth;
}

function getCentralActiveOccupantCount(contract: ActiveInvoiceContract, period: CentralResolvedInvoicePeriod) {
  const count = contract.occupants.filter((occupant) =>
    occupant.status === 'Active' &&
    occupant.moveInDate <= period.end &&
    (!occupant.moveOutDate || occupant.moveOutDate >= period.start)
  ).length;

  return Math.max(count, 1);
}

function getCentralEffectiveServicePrices(prices: ServicePrice[], effectiveOn: string) {
  const latestByServiceType = new Map<string, ServicePrice>();

  prices
    .filter((price) => price.effectiveFrom <= effectiveOn && (!price.effectiveTo || price.effectiveTo >= effectiveOn))
    .sort((left, right) => right.effectiveFrom.localeCompare(left.effectiveFrom))
    .forEach((price) => {
      if (!latestByServiceType.has(price.serviceTypeId)) {
        latestByServiceType.set(price.serviceTypeId, price);
      }
    });

  return Array.from(latestByServiceType.values());
}

function resolveCentralMonthlyRentFromAppendices(
  baseMonthlyRent: number,
  appendices: ContractAppendixResponse[],
  effectiveOn: string
) {
  const rentChanges = appendices
    .filter((appendix) => appendix.status === 'Active')
    .flatMap((appendix) =>
      appendix.changes
        .filter((change) =>
          change.changeType === 'Update' &&
          change.targetType === 'Contract' &&
          change.fieldName?.toLowerCase() === 'monthlyrent'
        )
        .map((change) => ({
          effectiveDate: appendix.effectiveDate,
          oldValue: change.oldValue,
          newValue: change.newValue
        }))
    )
    .sort((left, right) => left.effectiveDate.localeCompare(right.effectiveDate));

  if (rentChanges.length === 0) {
    return baseMonthlyRent;
  }

  const latestAppliedChange = [...rentChanges]
    .filter((change) => change.effectiveDate <= effectiveOn)
    .sort((left, right) => right.effectiveDate.localeCompare(left.effectiveDate))[0];
  const appliedRent = parseAppendixMoney(latestAppliedChange?.newValue);
  if (appliedRent !== null) {
    return appliedRent;
  }

  const firstChange = rentChanges[0];
  const oldRent = effectiveOn < firstChange.effectiveDate
    ? parseAppendixMoney(firstChange.oldValue)
    : null;

  return oldRent ?? baseMonthlyRent;
}

function parseAppendixMoney(value?: string | null) {
  if (!value?.trim()) {
    return null;
  }

  const parsed = Number.parseFloat(value.trim().replace(/^"|"$/g, '').replace(/,/g, ''));
  return Number.isFinite(parsed) ? parsed : null;
}

function getCentralDefaultInvoiceMonth(contract: ActiveInvoiceContract) {
  const today = getCentralTodayDateOnly();
  const contractStart = parseCentralDateOnly(contract.startDate);
  let cursor = new Date(today.getFullYear(), today.getMonth(), 1);

  for (let index = 0; index < 240; index += 1) {
    const monthValue = centralToMonthValue(cursor);
    const period = resolveCentralInvoicePeriodForContract(monthValue, contract);
    if (period && compareCentralDateOnly(period.end, centralToDateOnlyString(today)) <= 0) {
      return monthValue;
    }

    cursor = new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1);
  }

  return centralToMonthValue(contractStart);
}

function resolveCentralInvoicePeriodForContract(monthValue: string, contract: ActiveInvoiceContract): CentralResolvedInvoicePeriod | null {
  const [year, month] = monthValue.split('-').map(Number);
  if (!year || !month) {
    return null;
  }

  const monthStart = new Date(year, month - 1, 1);
  const monthEnd = new Date(year, month, 0);
  const contractStart = parseCentralDateOnly(contract.startDate);
  const contractEnd = parseCentralDateOnly(contract.endDate);
  const startDate = compareCentralDates(contractStart, monthStart) > 0 ? contractStart : monthStart;
  const endDate = compareCentralDates(contractEnd, monthEnd) < 0 ? contractEnd : monthEnd;

  if (compareCentralDates(startDate, endDate) > 0) {
    return null;
  }

  const billableDays = countCentralInclusiveDays(startDate, endDate);
  const daysInMonth = countCentralInclusiveDays(monthStart, monthEnd);

  return {
    start: centralToDateOnlyString(startDate),
    end: centralToDateOnlyString(endDate),
    billableDays,
    daysInMonth,
    isFullMonth: compareCentralDates(startDate, monthStart) === 0 && compareCentralDates(endDate, monthEnd) === 0
  };
}

function calculateCentralPeriodAmount(monthlyAmount: number, period: CentralResolvedInvoicePeriod) {
  if (period.isFullMonth) {
    return monthlyAmount;
  }

  return Math.round(monthlyAmount * period.billableDays / period.daysInMonth);
}

function getCentralTodayDateOnly() {
  const today = new Date();
  return new Date(today.getFullYear(), today.getMonth(), today.getDate());
}

function parseCentralDateOnly(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function centralToDateOnlyString(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function centralToMonthValue(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

function compareCentralDateOnly(left: string, right: string) {
  return compareCentralDates(parseCentralDateOnly(left), parseCentralDateOnly(right));
}

function compareCentralDates(left: Date, right: Date) {
  return getCentralDayNumber(left) - getCentralDayNumber(right);
}

function countCentralInclusiveDays(start: Date, end: Date) {
  return getCentralDayNumber(end) - getCentralDayNumber(start) + 1;
}

function getCentralDayNumber(date: Date) {
  return Math.floor(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()) / 86400000);
}

function buildDraftPreview(prices: ServicePrice[], discount: number) {
  const fixedServices = prices
    .filter((price) => normalizeCentralPricingUnit(price.pricingUnit) !== 'MeterReading')
    .reduce((sum, price) => sum + price.unitPrice, 0);

  return {
    fixedServices,
    discount: Number(discount) || 0,
    estimatedTotal: Math.max(0, fixedServices - (Number(discount) || 0))
  };
}

function getMeterReadingDateError(form: CreateMeterReadingRequest, roomContext: RoomBillingContext | null) {
  if (!roomContext) {
    return 'Vui lòng chọn phòng có hợp đồng đang hiệu lực trước khi ghi chỉ số.';
  }

  if (!form.billingPeriodStart || !form.billingPeriodEnd) {
    return 'Vui lòng nhập đầy đủ từ ngày và đến ngày.';
  }

  if (form.billingPeriodEnd < form.billingPeriodStart) {
    return 'Đến ngày phải lớn hơn hoặc bằng từ ngày.';
  }

  if (form.billingPeriodStart > today || form.billingPeriodEnd > today) {
    return 'Kỳ ghi chỉ số không được nằm trong tương lai.';
  }

  if (form.billingPeriodStart < roomContext.contractStartDate || form.billingPeriodEnd > roomContext.contractEndDate) {
    return `Kỳ ghi chỉ số phải nằm trong thời hạn hợp đồng (${roomContext.contractStartDate} - ${roomContext.contractEndDate}).`;
  }

  return '';
}

function formatDateInput(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function getDefaultBillingMonth(date: Date) {
  const lastDayOfCurrentMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
  const billingDate = date.getDate() === lastDayOfCurrentMonth
    ? date
    : new Date(date.getFullYear(), date.getMonth() - 1, 1);

  return `${billingDate.getFullYear()}-${String(billingDate.getMonth() + 1).padStart(2, '0')}`;
}

function getMonthRange(monthValue: string) {
  const [year, month] = monthValue.split('-').map(Number);
  const paddedMonth = String(month).padStart(2, '0');
  const start = `${year}-${paddedMonth}-01`;
  const endDay = new Date(year, month, 0).getDate();
  const end = `${year}-${paddedMonth}-${String(endDay).padStart(2, '0')}`;

  return { start, end };
}

function getMonthStart(monthValue: string) {
  return getMonthRange(monthValue).start;
}

function getMonthEnd(monthValue: string) {
  return getMonthRange(monthValue).end;
}

function toMonthValue(dateValue: string) {
  return dateValue.slice(0, 7) || defaultBillingMonth;
}

function getTab(pathname: string, invoiceId?: string): LandlordTab {
  if (invoiceId) {
    return 'detail';
  }
  if (pathname.includes('/service-prices') || pathname.includes('/billing')) {
    return 'prices';
  }
  if (pathname.includes('/meter-readings')) {
    return 'invoices';
  }
  if (pathname.includes('/invoices/create')) {
    return 'invoices';
  }
  return 'invoices';
}

function getTitle(tab: LandlordTab) {
  const titles: Record<LandlordTab, string> = {
    prices: 'Cấu hình giá dịch vụ',
    readings: 'Nhập chỉ số điện nước',
    invoices: 'Quản lý hóa đơn',
    create: 'Tạo hóa đơn nháp',
    detail: 'Chi tiết hóa đơn'
  };
  return titles[tab];
}

function getSubtitle(tab: LandlordTab) {
  const subtitles: Record<LandlordTab, string> = {
    prices: 'Giá được cấu hình theo khu trọ và lưu lịch sử hiệu lực.',
    readings: 'Chỉ nhập điện và nước cho phòng có hợp đồng đang hiệu lực.',
    invoices: 'Lọc theo trạng thái và tìm kiếm theo mã hóa đơn.',
    create: 'Tạo hóa đơn nháp từ tiền phòng, chỉ số dịch vụ và giá đã chốt.',
    detail: 'Kiểm tra hạng mục hóa đơn, phát hành hoặc hủy hóa đơn chưa thanh toán.'
  };
  return subtitles[tab];
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    maximumFractionDigits: 2
  }).format(value);
}

function getDefaultPricingUnit(serviceType: BillingServiceType): PricingUnit {
  return serviceType.supportsMeterReading ? 'MeterReading' : 'PerMonth';
}

function getPricingUnitOptions(serviceType?: BillingServiceType): PricingUnit[] {
  if (!serviceType) {
    return ['PerMonth', 'PerPersonPerMonth'];
  }

  return serviceType.supportsMeterReading
    ? ['MeterReading', 'PerMonth', 'PerPersonPerMonth']
    : ['PerMonth', 'PerPersonPerMonth'];
}

function getPricingUnitDisplayUnit(unit: PricingUnit | string, serviceType?: BillingServiceType) {
  const normalized = normalizeCentralPricingUnit(unit);
  if (normalized === 'MeterReading') {
    return serviceType?.meterUnitName ?? '';
  }

  if (normalized === 'PerPersonPerMonth') {
    return 'người/tháng';
  }

  return 'tháng';
}

function getPricingUnitLabel(unit: string) {
  const normalized = normalizeCentralPricingUnit(unit);
  const labels: Record<string, string> = {
    MeterReading: 'Theo chỉ số',
    PerMonth: 'Theo tháng',
    PerPersonPerMonth: 'Theo người/tháng'
  };

  return labels[normalized] ?? unit;
}

function getInvoiceStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Nháp',
    Issued: 'Đã phát hành',
    Paid: 'Đã thanh toán',
    Overdue: 'Quá hạn',
    Cancelled: 'Đã hủy'
  };

  return labels[status] ?? status;
}

function getInvoiceItemTypeLabel(itemType: string) {
  const labels: Record<string, string> = {
    Rent: 'Tiền phòng',
    Electricity: 'Tiền điện',
    Water: 'Tiền nước',
    Wifi: 'Wifi',
    Garbage: 'Rác',
    Service: 'Dịch vụ',
    Discount: 'Giảm trừ',
    Other: 'Khác'
  };

  return labels[itemType] ?? itemType;
}
