import { useEffect, useMemo, useState } from 'react';
import type { Dispatch, FormEvent, ReactNode, SetStateAction } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { billingApi } from '../api';
import type {
  BillingServiceCode,
  CreateMeterReadingRequest,
  CreateServicePriceRequest,
  GenerateInvoiceDraftRequest,
  Invoice,
  MeterReading,
  RoomBillingContext,
  ServicePrice
} from '../types';
import './BillingPages.css';

const serviceOptions: Array<{ code: BillingServiceCode; label: string; method: 'Metered' | 'Fixed'; unit: string }> = [
  { code: 'Electric', label: 'Điện', method: 'Metered', unit: 'kWh' },
  { code: 'Water', label: 'Nước', method: 'Metered', unit: 'm3' },
  { code: 'Wifi', label: 'Wifi', method: 'Fixed', unit: 'tháng' },
  { code: 'Trash', label: 'Rác', method: 'Fixed', unit: 'tháng' }
];

const invoiceStatuses = ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Overdue', 'Cancelled'];
const today = formatDateInput(new Date());
const defaultBillingMonth = getDefaultBillingMonth(new Date());
const monthStart = getMonthStart(defaultBillingMonth);
const monthEnd = getMonthEnd(defaultBillingMonth);

type LandlordTab = 'prices' | 'readings' | 'invoices' | 'create' | 'detail';

export default function LandlordBillingPage() {
  const { id, invoiceId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const query = new URLSearchParams(location.search);
  const queryRoomId = query.get('roomId') ?? '';
  const roomingHouseId = id ?? '';
  const tab = getTab(location.pathname, invoiceId);

  const [prices, setPrices] = useState<ServicePrice[]>([]);
  const [roomContext, setRoomContext] = useState<RoomBillingContext | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [createdReading, setCreatedReading] = useState<MeterReading | null>(null);
  const [loadingPrices, setLoadingPrices] = useState(false);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [search, setSearch] = useState('');
  const [confirmAction, setConfirmAction] = useState<{ title: string; body: string; onConfirm: () => void } | null>(null);

  const [priceForm, setPriceForm] = useState<CreateServicePriceRequest>({
    serviceCode: 'Electric',
    billingMethod: 'Metered',
    unitName: 'kWh',
    unitPrice: 4000,
    effectiveFrom: today,
    note: ''
  });

  const [readingForm, setReadingForm] = useState<CreateMeterReadingRequest>({
    roomId: '',
    contractId: '',
    serviceCode: 'Electric',
    billingPeriodStart: monthStart,
    billingPeriodEnd: monthEnd,
    previousReading: 0,
    currentReading: 0,
    proofImageObjectKey: ''
  });

  const [invoiceForm, setInvoiceForm] = useState<GenerateInvoiceDraftRequest>({
    contractId: '',
    billingPeriodStart: monthStart,
    billingPeriodEnd: monthEnd,
    discountAmount: 0,
    note: ''
  });

  const activePrices = useMemo(() => prices.filter((price) => price.isActive), [prices]);
  const priceHistory = useMemo(() => prices.filter((price) => !price.isActive), [prices]);
  const consumption = Number(readingForm.currentReading) - Number(readingForm.previousReading);
  const readingInvalid = consumption < 0;
  const readingDateError = getMeterReadingDateError(readingForm, roomContext);
  const previewTotals = useMemo(() => buildDraftPreview(activePrices, invoiceForm.discountAmount), [activePrices, invoiceForm.discountAmount]);

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
      const response = await billingApi.getServicePrices(targetRoomingHouseId);
      setPrices(response.data);
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
      setReadingForm((prev) => ({
        ...prev,
        roomId: response.data.roomId,
        contractId: response.data.contractId
      }));
      setInvoiceForm((prev) => ({
        ...prev,
        contractId: response.data.contractId
      }));
    } catch (err) {
      setRoomContext(null);
      setError(getApiErrorMessage(err, 'Phòng này chưa có hợp đồng đang hiệu lực để nhập chỉ số hoặc tạo hóa đơn.'));
    }
  }

  async function loadInvoices(nextSearch = search) {
    setLoadingInvoices(true);
    setError('');
    try {
      const response = await billingApi.getLandlordInvoices({ status: statusFilter, search: nextSearch });
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

  function selectService(code: BillingServiceCode) {
    const option = serviceOptions.find((item) => item.code === code);
    if (!option) {
      return;
    }

    setPriceForm((prev) => ({
      ...prev,
      serviceCode: option.code,
      billingMethod: option.method,
      unitName: option.unit
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
      setMessage(`Đã tạo giá mới cho ${getServiceLabel(response.data.serviceCode)}. Giá cũ đã được lưu vào lịch sử.`);
      await loadPrices();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tạo được bảng giá dịch vụ.'));
    } finally {
      setBusy('');
    }
  }

  async function handleCreateReading(event: FormEvent) {
    event.preventDefault();
    const dateError = getMeterReadingDateError(readingForm, roomContext);
    if (dateError) {
      setError(dateError);
      return;
    }

    if (readingInvalid) {
      setError('Chỉ số cuối không được nhỏ hơn chỉ số đầu.');
      return;
    }

    setBusy('reading');
    setError('');
    setMessage('');
    try {
      const response = await billingApi.createMeterReading({
        ...readingForm,
        previousReading: Number(readingForm.previousReading),
        currentReading: Number(readingForm.currentReading),
        proofImageObjectKey: readingForm.proofImageObjectKey?.trim() || null
      });
      setCreatedReading(response.data);
      setInvoiceForm((prev) => ({
        ...prev,
        contractId: response.data.contractId
      }));
      setMessage(`Đã ghi chỉ số ${getServiceLabel(response.data.serviceCode)}. Tiêu thụ: ${response.data.consumption}.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không ghi được chỉ số dịch vụ.'));
    } finally {
      setBusy('');
    }
  }

  async function handleGenerateDraft(event: FormEvent) {
    event.preventDefault();
    setBusy('invoice');
    setError('');
    setMessage('');
    try {
      const invoiceMonth = toMonthValue(invoiceForm.billingPeriodStart);
      const response = await billingApi.generateDraft({
        ...invoiceForm,
        billingPeriodStart: getMonthStart(invoiceMonth),
        billingPeriodEnd: getMonthEnd(invoiceMonth),
        discountAmount: Number(invoiceForm.discountAmount),
        note: invoiceForm.note?.trim() || null
      });
      setSelectedInvoice(response.data);
      setMessage(`Đã tạo hóa đơn nháp ${response.data.invoiceNo}.`);
      navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(response.data.id));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tạo được hóa đơn nháp.'));
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
    <div className="billing-shell">
      <aside className="billing-sidebar">
        <div>
          <span className="billing-kicker">Chủ trọ</span>
          <h1>Quản lý thu phí</h1>
        </div>
        <NavButton active={tab === 'prices'} onClick={() => navigate(roomingHouseId ? ROUTE_PATHS.LANDLORD.SERVICE_PRICES(roomingHouseId) : ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Giá dịch vụ
        </NavButton>
        <NavButton active={tab === 'readings'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.METER_READINGS)}>
          Chỉ số điện nước
        </NavButton>
        <NavButton active={tab === 'invoices'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)}>
          Hóa đơn
        </NavButton>
        <NavButton active={tab === 'create'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICE_CREATE)}>
          Tạo hóa đơn
        </NavButton>
        <button type="button" className="billing-nav" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Về bảng điều khiển
        </button>
      </aside>

      <main className="billing-main">
        <section className="billing-header">
          <div>
            <span className="billing-kicker">Nền tảng quản lý thuê trọ</span>
            <h2>{getTitle(tab)}</h2>
            <p>{getSubtitle(tab)}</p>
          </div>
          <div className="header-actions">
            {(tab === 'prices' || tab === 'create') && roomingHouseId && (
              <button type="button" className="billing-button secondary" onClick={() => void loadPrices()}>
                Tải lại giá
              </button>
            )}
            {(tab === 'invoices' || tab === 'detail') && (
              <button type="button" className="billing-button secondary" onClick={() => void loadInvoices()}>
                Tải lại hóa đơn
              </button>
            )}
          </div>
        </section>

        <NotificationStrip invoices={invoices} />
        {message && <div className="billing-alert success">{message}</div>}
        {error && <div className="billing-alert error">{error}</div>}

        {tab === 'prices' && (
          <ServicePricesSection
            prices={prices}
            activePrices={activePrices}
            priceHistory={priceHistory}
            loading={loadingPrices}
            priceForm={priceForm}
            busy={busy}
            onSelectService={selectService}
            onChangeForm={setPriceForm}
            onSubmit={handleCreatePrice}
          />
        )}

        {tab === 'readings' && (
          <MeterReadingsSection
            roomContext={roomContext}
            form={readingForm}
            busy={busy}
            consumption={consumption}
            invalid={readingInvalid}
            dateError={readingDateError}
            createdReading={createdReading}
            onChangeForm={setReadingForm}
            onSubmit={handleCreateReading}
          />
        )}

        {tab === 'invoices' && (
          <InvoiceListSection
            invoices={invoices}
            loading={loadingInvoices}
            statusFilter={statusFilter}
            search={search}
            onStatusChange={setStatusFilter}
            onSearchChange={setSearch}
            onSearch={() => void loadInvoices()}
            onOpen={(invoice) => navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(invoice.id))}
            onCreate={() => navigate(ROUTE_PATHS.LANDLORD.INVOICE_CREATE)}
          />
        )}

        {tab === 'create' && (
          <InvoiceCreateSection
            roomContext={roomContext}
            form={invoiceForm}
            busy={busy}
            previewTotals={previewTotals}
            onChangeForm={setInvoiceForm}
            onSubmit={handleGenerateDraft}
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
    </div>
  );
}

function ServicePricesSection({
  activePrices,
  priceHistory,
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
  loading: boolean;
  priceForm: CreateServicePriceRequest;
  busy: string;
  onSelectService: (code: BillingServiceCode) => void;
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
          <span className="count-pill">{activePrices.length}/4 đang áp dụng</span>
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
            <select value={priceForm.serviceCode} onChange={(event) => onSelectService(event.target.value as BillingServiceCode)}>
              {serviceOptions.map((service) => (
                <option key={service.code} value={service.code}>{service.label}</option>
              ))}
            </select>
          </label>
          <div className="form-row">
            <label>
              Cách tính
              <input value={getBillingMethodLabel(priceForm.billingMethod)} readOnly />
            </label>
            <label>
              Đơn vị
              <input value={priceForm.unitName} onChange={(event) => onChangeForm((prev) => ({ ...prev, unitName: event.target.value }))} />
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
                <span>{getServiceLabel(price.serviceCode)}</span><span>{formatMoney(price.unitPrice)}</span><span>{price.unitName}</span><span>{price.effectiveFrom}</span><span>{price.effectiveTo ?? 'Hiện tại'}</span>
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
              <select value={form.serviceCode} onChange={(event) => onChangeForm((prev) => ({ ...prev, serviceCode: event.target.value as 'Electric' | 'Water' }))}>
                <option value="Electric">Điện</option>
                <option value="Water">Nước</option>
              </select>
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
            <strong>{getServiceLabel(createdReading.serviceCode)}</strong>
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
        <strong>{getServiceLabel(price.serviceCode)}</strong>
        <span>{getBillingMethodLabel(price.billingMethod)} / {price.unitName}</span>
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

function buildDraftPreview(prices: ServicePrice[], discount: number) {
  const fixedServices = prices
    .filter((price) => price.serviceCode === 'Wifi' || price.serviceCode === 'Trash')
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
    return 'readings';
  }
  if (pathname.includes('/invoices/create')) {
    return 'create';
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

function getServiceLabel(code: BillingServiceCode | string) {
  const service = serviceOptions.find((item) => item.code === code);
  return service?.label ?? code;
}

function getBillingMethodLabel(method: string) {
  const labels: Record<string, string> = {
    Metered: 'Theo chỉ số',
    MeterBased: 'Theo chỉ số',
    Fixed: 'Cố định',
    PerMonth: 'Theo tháng',
    PerPerson: 'Theo người'
  };

  return labels[method] ?? method;
}

function getInvoiceStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Nháp',
    Issued: 'Đã phát hành',
    Paid: 'Đã thanh toán',
    Overdue: 'Quá hạn',
    Cancelled: 'Đã hủy',
    PartiallyPaid: 'Thanh toán một phần'
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
