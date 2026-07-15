import { Alert } from '../../../shared/components/ui/Alert';
import { useEffect, useMemo, useState } from 'react';
import type { Dispatch, FormEvent, ReactNode, SetStateAction } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { Toast } from '../../../shared/components/ui/Toast';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { contractApi } from '../../contracts/api';
import { billingApi } from '../api';
import type { ContractAppendixResponse, ContractHistoryItemResponse } from '../../contracts/types';
import type {
  BillingServiceType,
  BulkInvoiceResult,
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
const today = centralToDateOnlyString(getCentralTodayDateOnly());

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
  const [preview, setPreview] = useState<RoomInvoicePreview | null>(null);
  const [serviceTypes, setServiceTypes] = useState<BillingServiceType[]>([]);
  const [roomContext, setRoomContext] = useState<RoomBillingContext | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [loadingPrices, setLoadingPrices] = useState(false);
  const [loadingInvoices, setLoadingInvoices] = useState(false);
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [error, setError] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
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
        status: statusFilter
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
    setToast(null);
    try {
      const response = await billingApi.createServicePrice(roomingHouseId, {
        ...priceForm,
        unitPrice: Number(priceForm.unitPrice),
        note: priceForm.note?.trim() || null
      });
      setToast({ message: `Đã tạo giá mới cho ${response.data.serviceName}. Giá cũ đã được lưu vào lịch sử.`, type: 'success' });
      await loadPrices();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tạo được bảng giá dịch vụ.'), type: 'error' });
    } finally {
      setBusy('');
    }
  }

  async function issueInvoice(invoice: Invoice) {
    setBusy('issue');
    setError('');
    setToast(null);
    try {
      const response = await billingApi.issueInvoice(invoice.id);
      const updatedInvoice = response.data;
      setSelectedInvoice(updatedInvoice);
      setInvoices((prev) => {
        if (statusFilter !== '' && updatedInvoice.status !== statusFilter) {
          return prev.filter((item) => item.id !== updatedInvoice.id);
        }
        return prev.map((item) => item.id === updatedInvoice.id ? updatedInvoice : item);
      });
      setToast({
        message: `Đã phát hành hóa đơn ${updatedInvoice.invoiceNo} thành công. Người thuê có thể xem và thanh toán.`,
        type: 'success'
      });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không phát hành được hóa đơn.'), type: 'error' });
    } finally {
      setBusy('');
      setConfirmAction(null);
    }
  }

  async function cancelInvoice(invoice: Invoice) {
    setBusy('cancel');
    setError('');
    setToast(null);
    try {
      const response = await billingApi.cancelInvoice(invoice.id, 'Chủ trọ đã hủy hóa đơn');
      setSelectedInvoice(response.data);
      setInvoices((prev) => prev.map((item) => item.id === response.data.id ? response.data : item));
      setMessage(`Đã hủy hóa đơn ${response.data.invoiceNo}.`);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không hủy được hóa đơn.'), type: 'error' });
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
        {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
        {error && <Alert type="error">{error}</Alert>}

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
            <EmptyBlock title="Chưa có giá đang áp dụng" text="Hãy tạo giá cho các dịch vụ của khu trọ." />
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
    <div className="landlord-billing-page">
      {/* ── Overview Band ── */}
      <PageHeader
        icon={
          <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
          </svg>
        }
        eyebrow="Quản lý"
        title="Hóa đơn cho thuê"
        description="Xem và lọc hóa đơn theo khu trọ, phòng và trạng thái thanh toán."
        rightContent={
          <div className="invoice-overview-right">
          <div className="invoice-filter-group">
            <div className="invoice-filter-item">
              <label>Khu trọ</label>
              <select
                value={selectedHouseId}
                onChange={(event) => onHouseChange(event.target.value)}
                className="invoice-filter-select"
              >
                <option value="">Tất cả khu trọ</option>
                {houses.map((house) => (
                  <option key={house.id} value={house.id}>{house.name}</option>
                ))}
              </select>
            </div>
            <div className="invoice-filter-item">
              <label>Phòng</label>
              <select
                value={selectedRoomId}
                onChange={(event) => onRoomChange(event.target.value)}
                disabled={!selectedHouseId}
                className="invoice-filter-select"
              >
                <option value="">Tất cả phòng</option>
                {rooms.map((room) => (
                  <option key={room.id} value={room.id}>Phòng {room.name}</option>
                ))}
              </select>
            </div>
          </div>

          <button type="button" className="invoice-create-btn" onClick={onCreate}>
            <svg className="create-plus-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Tạo hóa đơn
          </button>
        </div>
        }
      />


      {/* ── Status Tabs ── */}
      <div className="invoice-list-wrapper">
        <Tabs
          className="attached-bottom"
        variant="segmented-secondary"
        activeId={statusFilter}
        onChange={onStatusChange}
        items={[
          { id: '', label: 'Tất cả', icon: getTabIcon('') },
          ...invoiceStatuses.map((status) => ({
            id: status,
            label: getInvoiceStatusLabel(status),
            icon: getTabIcon(status),
          })),
        ]}
      />

      {/* ── Invoice List ── */}
      <section className="tab-attached-panel tab-attached-panel--cards">
        {loading ? (
          <div className="empty-panel">Đang tải dữ liệu hóa đơn...</div>
        ) : invoices.length === 0 ? (
          <div className="empty-panel">
            <h2>Không tìm thấy hóa đơn</h2>
            <p>Chưa có hóa đơn nào phù hợp với bộ lọc hiện tại.</p>
          </div>
        ) : (
          <section className="invoice-grid">
            {invoices.map((invoice) => (
              <div
                className={`invoice-card status-${invoice.status.toLowerCase()}`}
                key={invoice.id}
                onClick={() => onOpen(invoice)}
              >
                {/* Card Header */}
                <div className="invoice-card-header">
                  <div className="invoice-no-box">
                    <svg className="invoice-file-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                      <polyline points="14 2 14 8 20 8" />
                      <line x1="16" y1="13" x2="8" y2="13" />
                      <line x1="16" y1="17" x2="8" y2="17" />
                    </svg>
                    <h3>{invoice.invoiceNo}</h3>
                  </div>
                  <span className={`invoice-status-badge ${invoice.status.toLowerCase()}`}>
                    {getInvoiceStatusLabel(invoice.status)}
                  </span>
                </div>

                {/* Card Body */}
                <div className="invoice-card-body">
                  <div className="house-info">
                    <svg className="info-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                      <polyline points="9 22 9 12 15 12 15 22" />
                    </svg>
                    <span>{invoice.roomingHouseName}</span>
                    <span className="room-badge">Phòng {invoice.roomNumber}</span>
                  </div>

                  <ul className="invoice-details-list">
                    <li className="invoice-detail-row">
                      <span className="label">Người thuê</span>
                      <span className="value tenant-name">{invoice.tenantName || invoice.tenantEmail}</span>
                    </li>
                    <li className="invoice-detail-row">
                      <span className="label">Kỳ hóa đơn</span>
                      <span className="value">{invoice.billingPeriodStart} – {invoice.billingPeriodEnd}</span>
                    </li>
                    <li className="invoice-detail-row">
                      <span className="label">Hạn thanh toán</span>
                      <span className="value due-date">{invoice.dueDate}</span>
                    </li>
                  </ul>

                  {/* Card Footer */}
                  <div className="invoice-card-footer">
                    <span className="amount-label">Tổng tiền</span>
                    <p className={`amount-value ${invoice.status.toLowerCase()}`}>
                      {formatMoney(invoice.totalAmount)}
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </section>
        )}
      </section>
      </div>
    </div>
  );
}

type ActiveInvoiceContract = Pick<
  ContractHistoryItemResponse,
  'id' | 'roomId' | 'roomNumber' | 'roomingHouseId' | 'roomingHouseName' | 'mainTenantName' | 'startDate' | 'endDate' | 'monthlyRent' | 'paymentDay' | 'occupants'
>;

type ReadingDraft = {
  previousReading: number;
  hasPreviousReading: boolean;
  currentReading: number;
  hasCurrentReading: boolean;
  proofMediaAssetId?: string | null;
  proofImageUrl: string;
  aiReading: number | null;
  aiRawText: string;
};

const emptyReadingDraft: ReadingDraft = {
  previousReading: 0,
  hasPreviousReading: false,
  currentReading: 0,
  hasCurrentReading: false,
  proofMediaAssetId: null,
  proofImageUrl: '',
  aiReading: null,
  aiRawText: ''
};

type BulkRoomDraft = {
  contract: ActiveInvoiceContract;
  preview: RoomInvoicePreview | null;
  selected: boolean;
  expanded: boolean;
  loadError: string;
  readings: Record<string, ReadingDraft>;
  discountAmount: number;
  note: string;
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
  const [billingMonth, setBillingMonth] = useState('');
  const [rooms, setRooms] = useState<Record<string, BulkRoomDraft>>({});
  const [loadingContracts, setLoadingContracts] = useState(true);
  const [loadingRooms, setLoadingRooms] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingKey, setUploadingKey] = useState('');
  const [error, setError] = useState('');
  const [result, setResult] = useState<BulkInvoiceResult | null>(null);
  const [publishInvoiceIds, setPublishInvoiceIds] = useState<Set<string>>(new Set());
  const [publishing, setPublishing] = useState(false);
  const [hiddenRoomCount, setHiddenRoomCount] = useState(0);

  const houses = useMemo(() => {
    const map = new Map<string, string>();
    contracts.forEach((contract) => map.set(contract.roomingHouseId, contract.roomingHouseName));
    return Array.from(map, ([id, name]) => ({ id, name }));
  }, [contracts]);
  const houseContracts = useMemo(
    () => contracts.filter((contract) => contract.roomingHouseId === selectedHouseId),
    [contracts, selectedHouseId]
  );
  const roomList = useMemo(() => Object.values(rooms), [rooms]);
  const selectedRooms = roomList.filter((room) => room.selected);
  const readyRooms = selectedRooms.filter(isBulkRoomReady);

  useEffect(() => {
    let cancelled = false;
    async function loadContracts() {
      setLoadingContracts(true);
      try {
        const response = await contractApi.getLandlordContracts();
        if (!cancelled) {
          setContracts((response.data ?? []).filter((contract) => contract.status === 'Active'));
        }
      } catch (err) {
        if (!cancelled) setError(getApiErrorMessage(err, 'Không thể tải danh sách hợp đồng Active.'));
      } finally {
        if (!cancelled) setLoadingContracts(false);
      }
    }
    void loadContracts();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    setRooms({});
    setHiddenRoomCount(0);
    setResult(null);
    setPublishInvoiceIds(new Set());
    setError('');
  }, [selectedHouseId, billingMonth]);

  async function loadHouseRooms() {
    if (!selectedHouseId) {
      setError('Vui lòng chọn khu trọ.');
      return;
    }
    if (!billingMonth) {
      setError('Vui lòng chọn kỳ hóa đơn.');
      return;
    }
    if (houseContracts.length === 0) {
      setError('Khu trọ không có phòng đang thuê với hợp đồng Active.');
      return;
    }

    setLoadingRooms(true);
    setError('');
    const monthStart = `${billingMonth}-01`;
    const [year, month] = billingMonth.split('-').map(Number);
    const monthEnd = `${billingMonth}-${String(new Date(year, month, 0).getDate()).padStart(2, '0')}`;

    const contractsInPeriod = houseContracts.filter((contract) =>
      Boolean(resolveCentralInvoicePeriodForContract(billingMonth, contract))
    );

    let existingInvoices: Invoice[] = [];
    try {
      const response = await billingApi.getLandlordInvoices();
      existingInvoices = response.data ?? [];
    } catch {
      // Preview validation remains the safety net if existing invoices cannot be loaded.
    }

    const visibleContracts = contractsInPeriod.filter((contract) => {
      const period = resolveCentralInvoicePeriodForContract(billingMonth, contract);
      if (!period) return false;
      return !existingInvoices.some((invoice) =>
        invoice.contractId === contract.id &&
        invoice.status.toLowerCase() !== 'cancelled' &&
        invoice.billingPeriodStart === period.start &&
        invoice.billingPeriodEnd === period.end
      );
    });

    setHiddenRoomCount(houseContracts.length - visibleContracts.length);

    const loaded = await Promise.all(visibleContracts.map(async (contract) => {
      const period = resolveCentralInvoicePeriodForContract(billingMonth, contract);
      if (!period) return null;
      try {
        const response = await billingApi.getRoomInvoicePreview(contract.roomId, {
          billingPeriodStart: period.start || monthStart,
          billingPeriodEnd: period.end || monthEnd
        });
        return [contract.id, buildBulkRoomDraft(contract, response.data, '')] as const;
      } catch (err) {
        return [contract.id, buildBulkRoomDraft(contract, null, getApiErrorMessage(err, 'Không thể tải dữ liệu phòng.'))] as const;
      }
    }));

    setRooms(Object.fromEntries(loaded.filter((entry): entry is readonly [string, BulkRoomDraft] => entry !== null)));
    setLoadingRooms(false);
  }

  function patchRoom(contractId: string, patch: Partial<BulkRoomDraft>) {
    setRooms((current) => ({
      ...current,
      [contractId]: { ...current[contractId], ...patch }
    }));
  }

  function patchReading(contractId: string, serviceTypeId: string, patch: Partial<ReadingDraft>) {
    setRooms((current) => {
      const room = current[contractId];
      return {
        ...current,
        [contractId]: {
          ...room,
          readings: {
            ...room.readings,
            [serviceTypeId]: { ...(room.readings[serviceTypeId] ?? emptyReadingDraft), ...patch }
          }
        }
      };
    });
  }

  async function uploadMeterImage(room: BulkRoomDraft, serviceTypeId: string, file?: File) {
    if (!file || !room.preview) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      setError('Chỉ hỗ trợ ảnh JPG hoặc PNG.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Ảnh đồng hồ không được vượt quá 5MB.');
      return;
    }
    const key = `${room.contract.id}:${serviceTypeId}`;
    setUploadingKey(key);
    setError('');
    try {
      const response = await billingApi.readMeterImage({
        contractId: room.contract.id,
        serviceTypeId,
        billingPeriodStart: room.preview.billingPeriodStart,
        file
      });
      patchReading(room.contract.id, serviceTypeId, {
        currentReading: response.data.reading,
        hasCurrentReading: true,
        aiReading: response.data.reading,
        aiRawText: response.data.rawText,
        proofMediaAssetId: response.data.proofMediaAssetId ?? null,
        proofImageUrl: response.data.proofImageUrl
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể đọc chỉ số từ ảnh.'));
    } finally {
      setUploadingKey('');
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');
    if (!selectedHouseId || !billingMonth) {
      setError('Vui lòng chọn đầy đủ khu trọ và kỳ hóa đơn.');
      return;
    }
    if (selectedRooms.length === 0) {
      setError('Vui lòng chọn ít nhất một phòng; các phòng không chọn sẽ được bỏ qua.');
      return;
    }
    if (readyRooms.length !== selectedRooms.length) {
      setError('Một hoặc nhiều phòng đang thiếu dữ liệu. Hãy hoàn tất chỉ số hoặc bỏ chọn phòng đó.');
      return;
    }

    const [year, month] = billingMonth.split('-').map(Number);
    const billingPeriodStart = `${billingMonth}-01`;
    const billingPeriodEnd = `${billingMonth}-${String(new Date(year, month, 0).getDate()).padStart(2, '0')}`;
    setSubmitting(true);
    try {
      const response = await billingApi.generateBulk({
        roomingHouseId: selectedHouseId,
        billingPeriodStart,
        billingPeriodEnd,
        rooms: selectedRooms.map((room) => ({
          contractId: room.contract.id,
          discountAmount: Number(room.discountAmount) || 0,
          note: room.note.trim() || null,
          meterReadings: (room.preview?.meteredServices ?? []).map((service) => {
            const draft = room.readings[service.serviceTypeId] ?? emptyReadingDraft;
            return {
              serviceTypeId: service.serviceTypeId,
              previousReading: service.latestReading ? null : draft.previousReading,
              currentReading: Number(draft.currentReading),
              proofMediaAssetId: draft.proofMediaAssetId || null,
              aiReading: draft.aiReading,
              aiRawText: draft.aiRawText || null
            };
          })
        }))
      });
      setResult(response.data);
      setPublishInvoiceIds(new Set(
        response.data.rooms
          .filter((room) => room.status === 'Created' && room.invoice)
          .map((room) => room.invoice!.id)
      ));
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tạo hóa đơn hàng loạt.'));
    } finally {
      setSubmitting(false);
    }
  }

  function finish() {
    const invoice = result?.rooms.find((room) => room.invoice)?.invoice;
    if (invoice) onCreated(invoice);
    else onClose();
  }

  function togglePublishInvoice(invoiceId: string) {
    setPublishInvoiceIds((current) => {
      const next = new Set(current);
      if (next.has(invoiceId)) next.delete(invoiceId);
      else next.add(invoiceId);
      return next;
    });
  }

  async function publishSelectedInvoices() {
    if (!result || publishInvoiceIds.size === 0) {
      finish();
      return;
    }

    setPublishing(true);
    setError('');
    const ids = Array.from(publishInvoiceIds);
    const responses = await Promise.allSettled(ids.map((id) => billingApi.issueInvoice(id)));
    const issuedById = new Map<string, Invoice>();
    const failedIds: string[] = [];
    responses.forEach((response, index) => {
      if (response.status === 'fulfilled') issuedById.set(ids[index], response.value.data);
      else failedIds.push(ids[index]);
    });

    setResult((current) => current ? {
      ...current,
      rooms: current.rooms.map((room) => room.invoice && issuedById.has(room.invoice.id)
        ? { ...room, invoice: issuedById.get(room.invoice.id), message: 'Đã phát hành hóa đơn.' }
        : room)
    } : current);
    setPublishInvoiceIds(new Set(failedIds));
    setPublishing(false);

    if (failedIds.length > 0) {
      setError(`${failedIds.length} hóa đơn chưa phát hành được. Các hóa đơn còn lại đã phát hành thành công.`);
      return;
    }
    finish();
  }

  return (
    <div className="history-modal-overlay">
      <div className="history-modal-content bulk-invoice-modal">
        <header className="history-modal-header bulk-invoice-header">
          <div><span className="bulk-kicker">Lập hóa đơn theo khu trọ</span><h2>Tạo hóa đơn nhanh</h2></div>
          <button className="history-modal-close" onClick={onClose} type="button" aria-label="Đóng">&times;</button>
        </header>

        <form onSubmit={handleSubmit}>
          <div className="history-modal-body bulk-invoice-body">
            <ol className="bulk-stepper" aria-label="Quy trình tạo hóa đơn">
              {['Chọn kỳ', 'Nhập chỉ số', 'Xác nhận', 'Kiểm tra nháp', 'Phát hành'].map((label, index) => (
                <li key={label} className={result ? (index < 4 ? 'done' : '') : roomList.length ? (index < 2 ? 'active' : '') : (index === 0 ? 'active' : '')}>
                  <span>{index + 1}</span><small>{label}</small>
                </li>
              ))}
            </ol>

            {error && <div className="billing-alert error bulk-alert">{error}</div>}

            {result ? (
              <section className="bulk-result-panel">
                <div className="bulk-draft-heading">
                  <div><strong>Kiểm tra hóa đơn nháp</strong><span>Hóa đơn được tick sẽ phát hành ngay. Bỏ tick để giữ lại bản nháp.</span></div>
                  <button type="button" onClick={() => setPublishInvoiceIds(new Set(
                    result.rooms.flatMap((room) => room.invoice?.status === 'Draft' ? [room.invoice.id] : [])
                  ))}>Chọn tất cả bản nháp</button>
                </div>
                <div className="bulk-result-list bulk-draft-list">
                  {result.rooms.map((room) => room.invoice ? (
                    <article key={room.contractId} className={`bulk-draft-card${publishInvoiceIds.has(room.invoice.id) ? ' selected' : ''}`}>
                      <label className="bulk-draft-select">
                        <input type="checkbox" checked={publishInvoiceIds.has(room.invoice.id)} disabled={room.invoice.status !== 'Draft' || publishing} onChange={() => togglePublishInvoice(room.invoice!.id)} />
                        <span aria-hidden="true" />
                      </label>
                      <div className="bulk-draft-identity"><strong>Phòng {room.roomNumber}</strong><span>{room.invoice.tenantName} · {formatCentralDate(room.invoice.billingPeriodStart)} – {formatCentralDate(room.invoice.billingPeriodEnd)}</span></div>
                      <em className={`invoice-status ${room.invoice.status.toLowerCase()}`}>{room.invoice.status === 'Draft' ? 'Bản nháp' : 'Đã phát hành'}</em>
                      <div className="bulk-draft-money">
                        <span><small>Tiền phòng</small><strong>{formatMoney(room.invoice.rentAmount)}</strong></span>
                        <span><small>Điện nước</small><strong>{formatMoney(room.invoice.utilityAmount)}</strong></span>
                        <span><small>Dịch vụ khác</small><strong>{formatMoney(room.invoice.serviceAmount)}</strong></span>
                        <span><small>Giảm trừ</small><strong>− {formatMoney(room.invoice.discountAmount)}</strong></span>
                        <span className="total"><small>Tổng thanh toán</small><strong>{formatMoney(room.invoice.totalAmount)}</strong></span>
                      </div>
                      {room.invoice.items.length > 0 && (
                        <div className="bulk-draft-items">
                          {room.invoice.items.map((item) => (
                            <span key={item.id}><span><strong>{item.serviceName || item.description}</strong><small>{item.quantity} × {formatMoney(item.unitPrice)}</small></span><strong>{formatMoney(item.amount)}</strong></span>
                          ))}
                        </div>
                      )}
                    </article>
                  ) : null)}
                </div>
                <p className="bulk-next-step"><strong>{publishInvoiceIds.size} hóa đơn sẽ phát hành ngay.</strong> Các hóa đơn không được chọn vẫn nằm trong danh sách nháp để bạn kiểm tra và phát hành sau.</p>
              </section>
            ) : (
              <>
                <section className="bulk-controls">
                  <label><span>Khu trọ</span><select value={selectedHouseId} onChange={(event) => setSelectedHouseId(event.target.value)} disabled={loadingContracts}><option value="">Chọn khu trọ</option>{houses.map((house) => <option key={house.id} value={house.id}>{house.name}</option>)}</select></label>
                  <label><span>Tháng / năm</span><input type="month" value={billingMonth} onChange={(event) => setBillingMonth(event.target.value)} /></label>
                  <button type="button" className="billing-button" onClick={() => void loadHouseRooms()} disabled={!selectedHouseId || !billingMonth || loadingRooms}>{loadingRooms ? 'Đang kiểm tra...' : 'Kiểm tra các phòng'}</button>
                </section>

                {loadingContracts ? <div className="bulk-empty">Đang tải danh sách hợp đồng...</div> : roomList.length === 0 ? (
                  <div className="bulk-empty">
                    <strong>{hiddenRoomCount > 0 ? 'Không có phòng cần tạo hóa đơn' : 'Chọn khu trọ và kỳ hóa đơn'}</strong>
                    <span>{hiddenRoomCount > 0 ? `${hiddenRoomCount} phòng đã được ẩn vì kỳ này đã có hóa đơn hoặc nằm ngoài thời hạn hợp đồng.` : 'Hệ thống sẽ tìm tất cả hợp đồng Active, giá dịch vụ và tình trạng hóa đơn của từng phòng.'}</span>
                  </div>
                ) : (
                  <section className="bulk-room-section">
                    <div className="bulk-room-toolbar">
                      <div><strong>{selectedRooms.length}/{roomList.length} phòng được chọn</strong><span>{readyRooms.length} phòng đã đủ dữ liệu{hiddenRoomCount > 0 ? ` · Đã ẩn ${hiddenRoomCount} phòng không thuộc kỳ này` : ''}</span></div>
                      <div><button type="button" onClick={() => setRooms((current) => Object.fromEntries(Object.entries(current).map(([id, room]) => [id, { ...room, selected: Boolean(room.preview?.canGenerate) && !room.loadError }]))) }>Chọn phòng hợp lệ</button><button type="button" onClick={() => setRooms((current) => Object.fromEntries(Object.entries(current).map(([id, room]) => [id, { ...room, selected: false }]))) }>Bỏ chọn tất cả</button></div>
                    </div>
                    <div className="bulk-room-list">
                      {roomList.map((room) => <BulkInvoiceRoomCard key={room.contract.id} room={room} uploadingKey={uploadingKey} onPatch={(patch) => patchRoom(room.contract.id, patch)} onPatchReading={(serviceId, patch) => patchReading(room.contract.id, serviceId, patch)} onUpload={(serviceId, file) => void uploadMeterImage(room, serviceId, file)} />)}
                    </div>
                  </section>
                )}
              </>
            )}
          </div>

          <footer className="history-modal-footer bulk-invoice-footer">
            {result ? <><button type="button" className="billing-button secondary" onClick={finish} disabled={publishing}>Để tất cả ở bản nháp</button><button type="button" className="billing-button" onClick={() => void publishSelectedInvoices()} disabled={publishing}>{publishing ? 'Đang phát hành...' : publishInvoiceIds.size > 0 ? `Phát hành ${publishInvoiceIds.size} hóa đơn` : 'Hoàn tất và giữ bản nháp'}</button></> : <><button type="button" className="billing-button secondary" onClick={onClose}>Đóng</button><button type="submit" className="billing-button" disabled={submitting || loadingRooms || selectedRooms.length === 0}>{submitting ? 'Đang tạo...' : `Tạo ${selectedRooms.length} hóa đơn nháp`}</button></>}
          </footer>
        </form>
      </div>
    </div>
  );
}

function buildBulkRoomDraft(contract: ActiveInvoiceContract, preview: RoomInvoicePreview | null, loadError: string): BulkRoomDraft {
  const readings: Record<string, ReadingDraft> = {};
  preview?.meteredServices.forEach((service) => {
    readings[service.serviceTypeId] = {
      ...emptyReadingDraft,
      previousReading: service.latestReading?.currentReading ?? 0,
      hasPreviousReading: Boolean(service.latestReading),
      currentReading: service.latestReading?.currentReading ?? 0,
      hasCurrentReading: false
    };
  });
  return {
    contract,
    preview,
    selected: Boolean(preview?.canGenerate) && !loadError,
    expanded: Boolean(preview?.canGenerate) && !loadError,
    loadError,
    readings,
    discountAmount: 0,
    note: ''
  };
}

function getBulkRoomIssue(room: BulkRoomDraft): string {
  if (room.loadError) return room.loadError;
  if (!room.preview) return 'Chưa tải được dữ liệu hóa đơn.';
  if (!room.preview.canGenerate) return room.preview.blockReason || 'Phòng chưa đủ điều kiện tạo hóa đơn.';
  for (const service of room.preview.meteredServices) {
    const draft = room.readings[service.serviceTypeId] ?? emptyReadingDraft;
    const previous = service.latestReading?.currentReading ?? draft.previousReading;
    if (service.requiresPreviousReading && !draft.hasPreviousReading) {
      return `Vui lòng nhập chỉ số đầu kỳ ${service.serviceName}.`;
    }
    if (!draft.hasCurrentReading) {
      return `Chưa nhập chỉ số ${service.serviceName}.`;
    }
    if (Number(draft.currentReading) < previous) {
      return `Chỉ số ${service.serviceName} thấp hơn kỳ trước.`;
    }
  }
  return '';
}

function isBulkRoomReady(room: BulkRoomDraft): boolean {
  return !getBulkRoomIssue(room);
}

function BulkInvoiceRoomCard({
  room,
  uploadingKey,
  onPatch,
  onPatchReading,
  onUpload
}: {
  room: BulkRoomDraft;
  uploadingKey: string;
  onPatch: (patch: Partial<BulkRoomDraft>) => void;
  onPatchReading: (serviceTypeId: string, patch: Partial<ReadingDraft>) => void;
  onUpload: (serviceTypeId: string, file?: File) => void;
}) {
  const [meterImagePreview, setMeterImagePreview] = useState<{ src: string; alt: string } | null>(null);
  const issue = getBulkRoomIssue(room);
  const ready = !issue;
  const preview = room.preview;
  const utilityAmount = (preview?.meteredServices ?? []).reduce((sum, service) => {
    const draft = room.readings[service.serviceTypeId] ?? emptyReadingDraft;
    const previous = service.latestReading?.currentReading ?? draft.previousReading;
    return sum + Math.max(0, Number(draft.currentReading) - previous) * service.unitPrice;
  }, 0);
  const total = Math.max(0, (preview?.rentAmount ?? 0) + (preview?.fixedServiceAmount ?? 0) + utilityAmount - room.discountAmount);

  return (
    <article className={`bulk-room-card${room.selected ? ' selected' : ''}${issue ? ' has-issue' : ' ready'}`}>
      <div className="bulk-room-card-head">
        <label className="bulk-room-check">
          <input type="checkbox" checked={room.selected} onChange={(event) => onPatch({ selected: event.target.checked })} />
          <span aria-hidden="true" />
        </label>
        <button type="button" className="bulk-room-identity" onClick={() => onPatch({ expanded: !room.expanded })}>
          <strong>Phòng {room.contract.roomNumber}</strong>
          <span>{room.contract.mainTenantName} · {room.contract.occupants.length || 1} người ở</span>
        </button>
        <div className="bulk-room-money"><small>Tạm tính</small><strong>{formatMoney(total)}</strong></div>
        <span className={`bulk-status ${ready ? 'ready' : 'issue'}`}>{ready ? 'Đủ dữ liệu' : room.preview?.canGenerate ? 'Cần nhập chỉ số' : 'Không thể tạo'}</span>
        <button type="button" className="bulk-expand" onClick={() => onPatch({ expanded: !room.expanded })} aria-label={room.expanded ? 'Thu gọn' : 'Mở chi tiết'}>{room.expanded ? '−' : '+'}</button>
      </div>

      {issue && <div className="bulk-room-issue">{issue} {!room.selected && <span>Phòng này sẽ được bỏ qua.</span>}</div>}

      {room.expanded && preview && (
        <div className="bulk-room-detail">
          <div className="bulk-room-breakdown">
            <span><small>Tiền phòng</small><strong>{formatMoney(preview.rentAmount)}</strong></span>
            <span><small>Dịch vụ khác</small><strong>{formatMoney(preview.fixedServiceAmount)}</strong></span>
            <span><small>Điện nước</small><strong>{formatMoney(utilityAmount)}</strong></span>
            <label><small>Giảm trừ</small><input type="number" min="0" value={room.discountAmount} onChange={(event) => onPatch({ discountAmount: Number(event.target.value) })} /></label>
          </div>

          {preview.fixedServices.length > 0 && (
            <div className="bulk-service-detail">
              <div><strong>Dịch vụ theo hợp đồng</strong><span>Đã tự động tính vào hóa đơn</span></div>
              <ul>
                {preview.fixedServices.map((service) => (
                  <li key={service.serviceTypeId}>
                    <span><strong>{service.serviceName}</strong><small>{service.quantity} × {formatMoney(service.unitPrice)} / {service.displayUnitName}</small></span>
                    <strong>{formatMoney(service.amount)}</strong>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {preview.meteredServices.length > 0 && (
            <div className="bulk-meter-grid">
              {preview.meteredServices.map((service) => {
                const draft = room.readings[service.serviceTypeId] ?? emptyReadingDraft;
                const previous = service.latestReading?.currentReading ?? draft.previousReading;
                const lower = draft.hasCurrentReading && draft.currentReading < previous;
                const uploadKey = `${room.contract.id}:${service.serviceTypeId}`;
                return (
                  <section key={service.serviceTypeId} className={`bulk-meter-item${lower ? ' invalid' : ''}`}>
                    <div className="bulk-meter-title"><strong>{service.serviceName}</strong><span>{formatMoney(service.unitPrice)} / {service.meterUnitName}</span></div>
                    <div className="bulk-meter-values">
                      {service.requiresPreviousReading ? (
                        <label className="bulk-first-reading"><small>Chỉ số đầu kỳ <em>Nhập lần đầu</em></small><input type="number" min="0" value={draft.hasPreviousReading ? draft.previousReading : ''} placeholder="Nhập chỉ số khi bắt đầu ở" onChange={(event) => onPatchReading(service.serviceTypeId, { previousReading: Number(event.target.value), hasPreviousReading: event.target.value !== '' })} /></label>
                      ) : (
                        <span><small>Chỉ số cũ <em>Từ kỳ trước</em></small><strong>{previous}</strong></span>
                      )}
                      <span><small>AI đọc <em>Tùy chọn</em></small><strong>{draft.aiReading ?? '--'}</strong></span>
                      <label><small>Chỉ số mới</small><input type="number" min="0" value={draft.hasCurrentReading ? draft.currentReading : ''} placeholder={`Từ ${previous}`} aria-invalid={lower} onChange={(event) => onPatchReading(service.serviceTypeId, { currentReading: Number(event.target.value), hasCurrentReading: event.target.value !== '' })} /></label>
                    </div>
                    <div className="bulk-meter-proof">
                      {draft.proofImageUrl && (
                        <button
                          type="button"
                          className="bulk-meter-thumbnail"
                          onClick={(event) => {
                            const resolvedImageUrl = event.currentTarget.querySelector('img')?.src;
                            if (resolvedImageUrl) {
                              setMeterImagePreview({
                                src: resolvedImageUrl,
                                alt: `Đồng hồ ${service.serviceName} phòng ${room.contract.roomNumber}`
                              });
                            }
                          }}
                          aria-label={`Xem ảnh đồng hồ ${service.serviceName} kích thước lớn`}
                        >
                          <PrivateMediaImage source={draft.proofImageUrl} alt={`Đồng hồ ${service.serviceName} phòng ${room.contract.roomNumber}`} />
                          <span>Xem ảnh</span>
                        </button>
                      )}
                      <div className="bulk-meter-upload-copy"><strong>Ảnh đồng hồ <em>Không bắt buộc</em></strong><span>Tải ảnh để AI đọc tự động, hoặc nhập chỉ số trực tiếp ở trên.</span></div>
                      <label>{uploadingKey === uploadKey ? 'AI đang đọc...' : draft.proofImageUrl ? 'Thay ảnh' : 'Chọn ảnh'}<input type="file" accept="image/jpeg,image/png" disabled={Boolean(uploadingKey)} onChange={(event) => { onUpload(service.serviceTypeId, event.target.files?.[0]); event.currentTarget.value = ''; }} /></label>
                    </div>
                    {lower && <small className="bulk-meter-error">Chỉ số mới phải từ {previous} {service.meterUnitName} trở lên. Ảnh vẫn được giữ để sửa.</small>}
                  </section>
                );
              })}
            </div>
          )}

          <label className="bulk-room-note"><span>Ghi chú riêng cho phòng</span><input value={room.note} onChange={(event) => onPatch({ note: event.target.value })} placeholder="Ví dụ: giảm tiền do sửa chữa..." /></label>
        </div>
      )}

      {meterImagePreview && (
        <div className="meter-image-lightbox" role="dialog" aria-modal="true" aria-label="Xem ảnh đồng hồ" onClick={() => setMeterImagePreview(null)}>
          <div className="meter-image-lightbox-content" onClick={(event) => event.stopPropagation()}>
            <div><strong>Ảnh đồng hồ</strong><span>Phòng {room.contract.roomNumber}</span></div>
            <button type="button" onClick={() => setMeterImagePreview(null)} aria-label="Đóng ảnh">×</button>
            <img src={meterImagePreview.src} alt={meterImagePreview.alt} />
          </div>
        </div>
      )}
    </article>
  );
}

function getTabIcon(status: string) {
  const props = { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, className: 'invoice-tab-icon' };

  switch (status.toLowerCase()) {
    case '':
    case 'all':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <circle cx="12" cy="12" r="3" fill="currentColor" stroke="none" />
        </svg>
      );
    case 'draft':
      return (
        <svg {...props}>
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
        </svg>
      );
    case 'issued':
    case 'sent':
    case 'paid':
      return (
        <svg {...props}>
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
          <polyline points="22 4 12 14.01 9 11.01" />
        </svg>
      );
    case 'overdue':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="8" x2="12" y2="12" />
          <line x1="12" y1="16" x2="12.01" y2="16" />
        </svg>
      );
    case 'cancelled':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
      );
    default:
      return null;
  }
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
  const [meterImagePreview, setMeterImagePreview] = useState<{ src: string; title: string; subtitle: string } | null>(null);

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
            <span className="tenant-invoice-item-desc">
              <span>{item.description}</span>
              {item.meterReadingProofImageUrl && (
                <button
                  type="button"
                  className="tenant-meter-proof-button"
                  onClick={() => setMeterImagePreview({
                    src: item.meterReadingProofImageUrl!,
                    title: getMeterReadingProofLabel(item.serviceName, item.description),
                    subtitle: item.description
                  })}
                >
                  {getMeterReadingProofLabel(item.serviceName, item.description)}
                </button>
              )}
            </span>
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

      {meterImagePreview && (
        <div className="meter-image-lightbox" role="dialog" aria-modal="true" aria-label={meterImagePreview.title} onClick={() => setMeterImagePreview(null)}>
          <div className="meter-image-lightbox-content" onClick={(event) => event.stopPropagation()}>
            <div>
              <strong>{meterImagePreview.title}</strong>
              <span>{meterImagePreview.subtitle}</span>
            </div>
            <button type="button" onClick={() => setMeterImagePreview(null)} aria-label="Đóng ảnh chỉ số">×</button>
            <PrivateMediaImage source={meterImagePreview.src} alt={meterImagePreview.title} />
          </div>
        </div>
      )}
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
    Service: 'Dịch vụ',
    Discount: 'Giảm trừ',
    Other: 'Khác'
  };

  return labels[itemType] ?? itemType;
}

function getMeterReadingProofLabel(serviceName?: string | null, description?: string | null) {
  const text = `${serviceName ?? ''} ${description ?? ''}`.toLowerCase();
  if (text.includes('điện') || text.includes('dien')) {
    return 'Xem chỉ số điện';
  }

  if (text.includes('nước') || text.includes('nuoc')) {
    return 'Xem chỉ số nước';
  }

  return 'Xem ảnh chỉ số';
}

function formatCentralDate(value: string) {
  const [year, month, day] = value.slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}
