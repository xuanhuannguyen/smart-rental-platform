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
  { code: 'Electric', label: 'Dien', method: 'Metered', unit: 'kWh' },
  { code: 'Water', label: 'Nuoc', method: 'Metered', unit: 'm3' },
  { code: 'Wifi', label: 'Wifi', method: 'Fixed', unit: 'thang' },
  { code: 'Trash', label: 'Rac', method: 'Fixed', unit: 'thang' }
];

const invoiceStatuses = ['Draft', 'Issued', 'Paid', 'Overdue', 'Cancelled'];
const today = new Date().toISOString().slice(0, 10);
const monthStart = today.slice(0, 8) + '01';

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
    billingPeriodEnd: today,
    previousReading: 0,
    currentReading: 0,
    proofImageObjectKey: ''
  });

  const [invoiceForm, setInvoiceForm] = useState<GenerateInvoiceDraftRequest>({
    contractId: '',
    billingPeriodStart: monthStart,
    billingPeriodEnd: today,
    discountAmount: 0,
    note: ''
  });

  const activePrices = useMemo(() => prices.filter((price) => price.isActive), [prices]);
  const priceHistory = useMemo(() => prices.filter((price) => !price.isActive), [prices]);
  const consumption = Number(readingForm.currentReading) - Number(readingForm.previousReading);
  const readingInvalid = consumption < 0;
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
      setError(getApiErrorMessage(err, 'Khong tai duoc bang gia dich vu.'));
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
      setError(getApiErrorMessage(err, 'Phong nay chua co hop dong Active de nhap chi so hoac tao hoa don.'));
    }
  }

  async function loadInvoices(nextSearch = search) {
    setLoadingInvoices(true);
    setError('');
    try {
      const response = await billingApi.getLandlordInvoices({ status: statusFilter, search: nextSearch });
      setInvoices(response.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong tai duoc danh sach hoa don.'));
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
      setError(getApiErrorMessage(err, 'Khong tai duoc chi tiet hoa don.'));
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
      setMessage(`Da tao gia moi cho ${response.data.serviceName}. Gia cu duoc dong lich su.`);
      await loadPrices();
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong tao duoc bang gia dich vu.'));
    } finally {
      setBusy('');
    }
  }

  async function handleCreateReading(event: FormEvent) {
    event.preventDefault();
    if (readingInvalid) {
      setError('Chi so cuoi khong duoc nho hon chi so dau.');
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
        contractId: response.data.contractId,
        billingPeriodStart: response.data.billingPeriodStart,
        billingPeriodEnd: response.data.billingPeriodEnd
      }));
      setMessage(`Da ghi chi so ${response.data.serviceCode}. Tieu thu: ${response.data.consumption}.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong ghi duoc chi so dich vu.'));
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
      const response = await billingApi.generateDraft({
        ...invoiceForm,
        discountAmount: Number(invoiceForm.discountAmount),
        note: invoiceForm.note?.trim() || null
      });
      setSelectedInvoice(response.data);
      setMessage(`Da tao hoa don Draft ${response.data.invoiceNo}.`);
      navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(response.data.id));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong tao duoc hoa don Draft.'));
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
      setMessage(`Da phat hanh hoa don ${response.data.invoiceNo}. Tenant co the xem va thanh toan.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong phat hanh duoc hoa don.'));
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
      const response = await billingApi.cancelInvoice(invoice.id, 'Cancelled by landlord');
      setSelectedInvoice(response.data);
      setInvoices((prev) => prev.map((item) => item.id === response.data.id ? response.data : item));
      setMessage(`Da huy hoa don ${response.data.invoiceNo}.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Khong huy duoc hoa don.'));
    } finally {
      setBusy('');
      setConfirmAction(null);
    }
  }

  return (
    <div className="billing-shell">
      <aside className="billing-sidebar">
        <div>
          <span className="billing-kicker">Landlord</span>
          <h1>Billing Center</h1>
        </div>
        <NavButton active={tab === 'prices'} onClick={() => navigate(roomingHouseId ? ROUTE_PATHS.LANDLORD.SERVICE_PRICES(roomingHouseId) : ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Gia dich vu
        </NavButton>
        <NavButton active={tab === 'readings'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.METER_READINGS)}>
          Chi so dien nuoc
        </NavButton>
        <NavButton active={tab === 'invoices'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)}>
          Hoa don
        </NavButton>
        <NavButton active={tab === 'create'} onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICE_CREATE)}>
          Tao hoa don
        </NavButton>
        <button type="button" className="billing-nav" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
          Ve dashboard
        </button>
      </aside>

      <main className="billing-main">
        <section className="billing-header">
          <div>
            <span className="billing-kicker">Smart Rental Platform</span>
            <h2>{getTitle(tab)}</h2>
            <p>{getSubtitle(tab)}</p>
          </div>
          <div className="header-actions">
            {(tab === 'prices' || tab === 'create') && roomingHouseId && (
              <button type="button" className="billing-button secondary" onClick={() => void loadPrices()}>
                Tai lai gia
              </button>
            )}
            {(tab === 'invoices' || tab === 'detail') && (
              <button type="button" className="billing-button secondary" onClick={() => void loadInvoices()}>
                Tai lai hoa don
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
              title: 'Phat hanh hoa don?',
              body: 'Sau khi phat hanh, tenant se nhin thay hoa don va co the thanh toan bang vi noi bo.',
              onConfirm: () => void issueInvoice(invoice)
            })}
            onCancel={(invoice) => setConfirmAction({
              title: 'Huy hoa don?',
              body: 'Hoa don chua thanh toan se chuyen sang Cancelled va tenant se khong thanh toan duoc.',
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
            <span className="billing-kicker">Current</span>
            <h3>Gia dang ap dung</h3>
          </div>
          <span className="count-pill">{activePrices.length}/4 active</span>
        </div>

        <div className="service-price-list">
          {loading ? (
            <LoadingBlock text="Dang tai bang gia..." />
          ) : activePrices.length === 0 ? (
            <EmptyBlock title="Chua co gia active" text="Hay tao gia cho Electric, Water, Wifi va Trash." />
          ) : (
            activePrices.map((price) => <PriceRow key={price.id} price={price} />)
          )}
        </div>
      </section>

      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">New price</span>
            <h3>Tao gia hieu luc moi</h3>
          </div>
        </div>

        <form className="billing-form" onSubmit={onSubmit}>
          <label>
            Dich vu
            <select value={priceForm.serviceCode} onChange={(event) => onSelectService(event.target.value as BillingServiceCode)}>
              {serviceOptions.map((service) => (
                <option key={service.code} value={service.code}>{service.label}</option>
              ))}
            </select>
          </label>
          <div className="form-row">
            <label>
              Cach tinh
              <input value={priceForm.billingMethod} readOnly />
            </label>
            <label>
              Don vi
              <input value={priceForm.unitName} onChange={(event) => onChangeForm((prev) => ({ ...prev, unitName: event.target.value }))} />
            </label>
          </div>
          <div className="form-row">
            <label>
              Don gia
              <input type="number" min="1" value={priceForm.unitPrice} onChange={(event) => onChangeForm((prev) => ({ ...prev, unitPrice: Number(event.target.value) }))} />
            </label>
            <label>
              Hieu luc tu
              <input type="date" value={priceForm.effectiveFrom} onChange={(event) => onChangeForm((prev) => ({ ...prev, effectiveFrom: event.target.value }))} />
            </label>
          </div>
          <label>
            Ghi chu
            <input value={priceForm.note ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, note: event.target.value }))} placeholder="Ly do thay doi gia" />
          </label>
          <button type="submit" className="billing-button" disabled={busy === 'price'}>
            {busy === 'price' ? 'Dang luu...' : 'Tao gia moi'}
          </button>
        </form>
      </section>

      <section className="billing-panel wide">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">History</span>
            <h3>Lich su gia</h3>
          </div>
        </div>
        {priceHistory.length === 0 ? (
          <EmptyBlock title="Chua co lich su" text="Khi gia thay doi, ban ghi cu se nam o day de truy vet." />
        ) : (
          <div className="data-table">
            <div className="table-row table-head">
              <span>Dich vu</span><span>Gia</span><span>Don vi</span><span>Tu ngay</span><span>Den ngay</span>
            </div>
            {priceHistory.map((price) => (
              <div key={price.id} className="table-row">
                <span>{price.serviceName}</span><span>{formatMoney(price.unitPrice)}</span><span>{price.unitName}</span><span>{price.effectiveFrom}</span><span>{price.effectiveTo ?? 'Now'}</span>
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
  createdReading,
  onChangeForm,
  onSubmit
}: {
  roomContext: RoomBillingContext | null;
  form: CreateMeterReadingRequest;
  busy: string;
  consumption: number;
  invalid: boolean;
  createdReading: MeterReading | null;
  onChangeForm: Dispatch<SetStateAction<CreateMeterReadingRequest>>;
  onSubmit: (event: FormEvent) => void;
}) {
  return (
    <section className="billing-grid">
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Metered only</span>
            <h3>Nhap chi so dien nuoc</h3>
          </div>
        </div>
        <form className="billing-form" onSubmit={onSubmit}>
          {roomContext && <RoomContextCard context={roomContext} />}
          <label>
            Room ID
            <input value={form.roomId} onChange={(event) => onChangeForm((prev) => ({ ...prev, roomId: event.target.value }))} placeholder="Phong co hop dong Active" readOnly={Boolean(roomContext)} required />
          </label>
          <label>
            Contract ID
            <input value={form.contractId} onChange={(event) => onChangeForm((prev) => ({ ...prev, contractId: event.target.value }))} placeholder="Hop dong Active" readOnly={Boolean(roomContext)} required />
          </label>
          <div className="form-row">
            <label>
              Dich vu
              <select value={form.serviceCode} onChange={(event) => onChangeForm((prev) => ({ ...prev, serviceCode: event.target.value as 'Electric' | 'Water' }))}>
                <option value="Electric">Dien</option>
                <option value="Water">Nuoc</option>
              </select>
            </label>
            <label>
              Anh minh chung
              <input value={form.proofImageObjectKey ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, proofImageObjectKey: event.target.value }))} placeholder="optional/object-key" />
            </label>
          </div>
          <div className="form-row">
            <label>
              Tu ngay
              <input type="date" value={form.billingPeriodStart} onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodStart: event.target.value }))} />
            </label>
            <label>
              Den ngay
              <input type="date" value={form.billingPeriodEnd} onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodEnd: event.target.value }))} />
            </label>
          </div>
          <div className="form-row">
            <label>
              Chi so dau
              <input type="number" min="0" value={form.previousReading} onChange={(event) => onChangeForm((prev) => ({ ...prev, previousReading: Number(event.target.value) }))} />
            </label>
            <label>
              Chi so cuoi
              <input type="number" min="0" value={form.currentReading} onChange={(event) => onChangeForm((prev) => ({ ...prev, currentReading: Number(event.target.value) }))} />
            </label>
          </div>
          <div className={`computed-strip ${invalid ? 'invalid' : ''}`}>
            Tieu thu: <strong>{invalid ? 0 : consumption}</strong>
            {invalid && <span>Chi so cuoi khong duoc nho hon chi so dau.</span>}
          </div>
          <button type="submit" className="billing-button" disabled={busy === 'reading' || invalid}>
            {busy === 'reading' ? 'Dang ghi...' : 'Ghi chi so'}
          </button>
        </form>
      </section>

      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Result</span>
            <h3>Ban ghi moi nhat</h3>
          </div>
        </div>
        {createdReading ? (
          <div className="result-box">
            <strong>{createdReading.serviceCode}</strong>
            <span>{createdReading.billingPeriodStart} - {createdReading.billingPeriodEnd}</span>
            <span>{createdReading.previousReading} - {createdReading.currentReading}</span>
            <span>Tieu thu: {createdReading.consumption}</span>
          </div>
        ) : (
          <EmptyBlock title="Chua ghi chi so" text="Sau khi luu thanh cong, ban ghi moi se hien thi tai day." />
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
          <span className="billing-kicker">Invoices</span>
          <h3>Danh sach hoa don</h3>
        </div>
        <button type="button" className="billing-button" onClick={onCreate}>Tao hoa don</button>
      </div>
      <div className="filter-bar">
        <select value={statusFilter} onChange={(event) => onStatusChange(event.target.value)}>
          <option value="">Tat ca trang thai</option>
          {invoiceStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
        <input value={search} onChange={(event) => onSearchChange(event.target.value)} placeholder="Tim invoice, room, tenant..." />
        <button type="button" className="billing-button secondary" onClick={onSearch}>Tim</button>
      </div>
      {loading ? (
        <LoadingBlock text="Dang tai hoa don..." />
      ) : invoices.length === 0 ? (
        <EmptyBlock title="Chua co hoa don" text="Tao Draft invoice cho hop dong Active de bat dau thu phi." />
      ) : (
        <div className="data-table">
          <div className="table-row table-head invoice-table-row">
            <span>Ma hoa don</span><span>Room</span><span>Tenant</span><span>Ky</span><span>Tong tien</span><span>Han</span><span>Trang thai</span><span></span>
          </div>
          {invoices.map((invoice) => (
            <div key={invoice.id} className="table-row invoice-table-row">
              <span>{invoice.invoiceNo}</span>
              <span>{shortId(invoice.roomId)}</span>
              <span>{shortId(invoice.tenantUserId)}</span>
              <span>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</span>
              <strong>{formatMoney(invoice.totalAmount)}</strong>
              <span>{invoice.dueDate}</span>
              <span className={`status-chip ${invoice.status.toLowerCase()}`}>{invoice.status}</span>
              <button type="button" className="link-button" onClick={() => onOpen(invoice)}>Chi tiet</button>
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
            <span className="billing-kicker">Draft</span>
            <h3>Tao hoa don nhap</h3>
          </div>
        </div>
        <form className="billing-form" onSubmit={onSubmit}>
          {roomContext && <RoomContextCard context={roomContext} />}
          <label>
            Contract ID
            <input value={form.contractId} onChange={(event) => onChangeForm((prev) => ({ ...prev, contractId: event.target.value }))} placeholder="Hop dong Active" readOnly={Boolean(roomContext)} required />
          </label>
          <div className="form-row">
            <label>
              Tu ngay
              <input type="date" value={form.billingPeriodStart} onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodStart: event.target.value }))} />
            </label>
            <label>
              Den ngay
              <input type="date" value={form.billingPeriodEnd} onChange={(event) => onChangeForm((prev) => ({ ...prev, billingPeriodEnd: event.target.value }))} />
            </label>
          </div>
          <label>
            Giam tru
            <input type="number" min="0" value={form.discountAmount} onChange={(event) => onChangeForm((prev) => ({ ...prev, discountAmount: Number(event.target.value) }))} />
          </label>
          <label>
            Ghi chu
            <input value={form.note ?? ''} onChange={(event) => onChangeForm((prev) => ({ ...prev, note: event.target.value }))} placeholder="Hoa don thang nay" />
          </label>
          <button type="submit" className="billing-button" disabled={busy === 'invoice'}>
            {busy === 'invoice' ? 'Dang tao...' : 'Tao Draft'}
          </button>
        </form>
      </section>
      <section className="billing-panel">
        <div className="panel-heading">
          <div>
            <span className="billing-kicker">Preview</span>
            <h3>Kiem tra truoc khi tao</h3>
          </div>
        </div>
        <div className="check-list">
          <span>Hop dong phai dang Active.</span>
          <span>Dien/Nuoc can co meter reading cung ky.</span>
          <span>Gia dich vu duoc snapshot vao invoice item.</span>
          <span>He thong chan duplicate invoice cung hop dong va ky.</span>
        </div>
        <div className="invoice-summary single">
          <span>Fixed services <strong>{formatMoney(previewTotals.fixedServices)}</strong></span>
          <span>Discount <strong>{formatMoney(previewTotals.discount)}</strong></span>
          <span>Estimated fixed subtotal <strong>{formatMoney(previewTotals.estimatedTotal)}</strong></span>
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
    return <section className="billing-panel"><LoadingBlock text="Dang tai chi tiet hoa don..." /></section>;
  }

  if (!invoice) {
    return <section className="billing-panel"><EmptyBlock title="Khong co hoa don" text="Chon mot hoa don tu danh sach de xem chi tiet." /></section>;
  }

  return (
    <section className="billing-panel invoice-detail">
      <div className="invoice-topline">
        <div>
          <span className="billing-kicker">Invoice detail</span>
          <h3>{invoice.invoiceNo}</h3>
        </div>
        <span className={`status-chip ${invoice.status.toLowerCase()}`}>{invoice.status}</span>
      </div>
      <div className="invoice-summary">
        <span>Rent <strong>{formatMoney(invoice.rentAmount)}</strong></span>
        <span>Utilities <strong>{formatMoney(invoice.utilityAmount)}</strong></span>
        <span>Fixed services <strong>{formatMoney(invoice.serviceAmount)}</strong></span>
        <span>Total <strong>{formatMoney(invoice.totalAmount)}</strong></span>
      </div>
      <div className="data-table">
        <div className="table-row table-head item-table-row">
          <span>Hang muc</span><span>Mo ta</span><span>So luong</span><span>Don gia snapshot</span><span>Thanh tien</span>
        </div>
        {invoice.items.map((item) => (
          <div key={item.id} className="table-row item-table-row">
            <span>{item.itemType}</span>
            <span>{item.description}</span>
            <span>{item.quantity}</span>
            <span>{formatMoney(item.unitPrice)}</span>
            <strong>{formatMoney(item.amount)}</strong>
          </div>
        ))}
      </div>
      <div className="action-row">
        <button type="button" className="billing-button" disabled={invoice.status !== 'Draft' || busy === 'issue'} onClick={() => onIssue(invoice)}>
          {busy === 'issue' ? 'Dang phat hanh...' : 'Phat hanh'}
        </button>
        <button type="button" className="billing-button danger" disabled={invoice.status === 'Paid' || invoice.status === 'Cancelled' || busy === 'cancel'} onClick={() => onCancel(invoice)}>
          {busy === 'cancel' ? 'Dang huy...' : 'Huy hoa don'}
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
      {issuedCount > 0 && <span>{issuedCount} hoa don dang cho thanh toan.</span>}
      {dueSoon > 0 && <span>{dueSoon} hoa don sap den han trong 3 ngay.</span>}
    </div>
  );
}

function RoomContextCard({ context }: { context: RoomBillingContext }) {
  return (
    <div className="room-context-card">
      <div>
        <span className="billing-kicker">Selected room</span>
        <strong>Phong {context.roomNumber}</strong>
      </div>
      <div>
        <span>Tenant</span>
        <strong>{context.tenantName || context.tenantEmail}</strong>
      </div>
      <div>
        <span>Hop dong</span>
        <strong>{context.contractNumber}</strong>
      </div>
      <div>
        <span>Tien phong</span>
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
        <span>{price.billingMethod} / {price.unitName}</span>
      </div>
      <div>
        <strong>{formatMoney(price.unitPrice)}</strong>
        <span>{price.effectiveFrom} - {price.effectiveTo ?? 'Now'}</span>
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
          <button type="button" className="billing-button secondary" onClick={onCancel} disabled={busy}>Dong</button>
          <button type="button" className="billing-button" onClick={onConfirm} disabled={busy}>Xac nhan</button>
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
    prices: 'Cau hinh gia dich vu',
    readings: 'Nhap chi so dien nuoc',
    invoices: 'Quan ly hoa don',
    create: 'Tao hoa don Draft',
    detail: 'Chi tiet hoa don'
  };
  return titles[tab];
}

function getSubtitle(tab: LandlordTab) {
  const subtitles: Record<LandlordTab, string> = {
    prices: 'Gia duoc cau hinh theo khu tro va luu lich su hieu luc.',
    readings: 'Chi nhap Electric va Water cho phong co hop dong Active.',
    invoices: 'Loc Draft, Issued, Paid, Overdue, Cancelled va tim theo ma hoa don.',
    create: 'Tao Draft bang tien phong, chi so dich vu va gia snapshot.',
    detail: 'Kiem tra invoice items, phat hanh hoac huy hoa don chua thanh toan.'
  };
  return subtitles[tab];
}

function shortId(value: string) {
  return value.slice(0, 8);
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}
