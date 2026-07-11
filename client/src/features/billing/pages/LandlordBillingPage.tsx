import { useEffect, useMemo, useState } from 'react';
import type { Dispatch, FormEvent, ReactNode, SetStateAction } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { contractApi } from '../../contracts/api';
import { uploadImage } from '../../files/api';
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
      <section className="invoice-overview-band">
        <div className="overview-left">
          <p className="eyebrow">Quản lý</p>
          <h2>Hóa đơn cho thuê</h2>
          <p className="overview-description">Xem và lọc hóa đơn theo khu trọ, phòng và trạng thái thanh toán.</p>
        </div>

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
      </section>

      {/* ── Status Tabs ── */}
      <div className="invoice-tabs-container">
        <div className="invoice-tabs-wrapper">
          <InvoiceStatusTab status="all" activeStatus={statusFilter} onClick={onStatusChange}>Tất cả</InvoiceStatusTab>
          {invoiceStatuses.map((status) => (
            <InvoiceStatusTab key={status} status={status} activeStatus={statusFilter} onClick={onStatusChange}>
              {getInvoiceStatusLabel(status)}
            </InvoiceStatusTab>
          ))}
        </div>
      </div>

      {/* ── Invoice List ── */}
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
    </div>
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
  proofMediaAssetId?: string | null;
  proofImageUrl?: string | null;
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
  const [appendices, setAppendices] = useState<ContractAppendixResponse[]>([]);
  const [latestReadingByServiceType, setLatestReadingByServiceType] = useState<Record<string, LatestMeterReading>>({});
  const [readings, setReadings] = useState<Record<string, ReadingDraft>>({});
  const [discountAmount, setDiscountAmount] = useState(0);
  const [note, setNote] = useState('');
  const [loadingContracts, setLoadingContracts] = useState(true);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingProofServiceId, setUploadingProofServiceId] = useState<string | null>(null);
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
        const [priceResponse, appendixResponse, contextResponse] = await Promise.all([
          billingApi.getServicePrices(contract.roomingHouseId),
          contractApi.getAppendices(contract.id),
          billingApi.getRoomBillingContext(contract.roomId)
        ]);
        if (cancelled) return;

        setPrices(priceResponse.data);
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
              proofImageObjectKey: '',
              proofMediaAssetId: null,
              proofImageUrl: null
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
  const fixedPrices = useMemo(
    () => preview?.fixedServices ?? [],
    [preview]
  );
  const meteredPrices = useMemo(
    () => preview?.meteredServices ?? [],
    [preview]
  );
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
            proofImageObjectKey: '',
            proofMediaAssetId: null,
            proofImageUrl: null
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
        ...(current[serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '', proofMediaAssetId: null, proofImageUrl: null }),
        ...patch
      }
    }));
  }

  async function handleProofUpload(serviceTypeId: string, file: File) {
    setUploadingProofServiceId(serviceTypeId);
    setError('');
    try {
      const uploaded = await uploadImage(file, 'MeterReading');
      updateReading(serviceTypeId, {
        proofImageObjectKey: uploaded.objectKey,
        proofMediaAssetId: uploaded.mediaAssetId || null,
        proofImageUrl: uploaded.url || null
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải ảnh công tơ lên.'));
    } finally {
      setUploadingProofServiceId(null);
    }
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
      const draft = readings[price.serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '', proofMediaAssetId: null, proofImageUrl: null };
      const latestReading = latestReadingByServiceType[price.serviceTypeId];
      return {
        serviceTypeId: price.serviceTypeId,
        previousReading: latestReading ? null : Number(draft.previousReading),
        currentReading: Number(draft.currentReading),
        proofImageObjectKey: draft.proofImageObjectKey.trim() || null,
        proofMediaAssetId: draft.proofMediaAssetId || null
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
                        const draft = readings[price.serviceTypeId] ?? { previousReading: 0, currentReading: 0, proofImageObjectKey: '', proofMediaAssetId: null, proofImageUrl: null };
                        const latestReading = latestReadingByServiceType[price.serviceTypeId];
                        const previousReading = latestReading?.currentReading ?? Number(draft.previousReading);
                        const consumption = Math.max(0, Number(draft.currentReading) - previousReading);
                        const amount = Math.round(consumption * price.unitPrice);
                        const uploadedProofUrl = resolveMeterProofUrl(draft.proofImageUrl, draft.proofImageObjectKey);
                        const latestProofUrl = resolveMeterProofUrl(latestReading?.proofImageUrl, null);
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
                            <div className="invoice-create-grid" style={{ marginTop: '12px' }}>
                              <label className="invoice-create-field" style={{ gridColumn: '1 / -1' }}>
                                <span className="label">Ảnh công tơ kỳ này</span>
                                <input
                                  type="file"
                                  accept="image/png,image/jpeg,image/webp"
                                  onChange={(event) => {
                                    const file = event.target.files?.[0];
                                    if (file) {
                                      void handleProofUpload(price.serviceTypeId, file);
                                      event.target.value = '';
                                    }
                                  }}
                                  disabled={uploadingProofServiceId === price.serviceTypeId}
                                />
                              </label>
                              {uploadedProofUrl && (
                                <div className="invoice-create-field" style={{ gridColumn: '1 / -1' }}>
                                  <span className="label">Ảnh đã chọn</span>
                                  <a href={uploadedProofUrl} target="_blank" rel="noreferrer">
                                    Xem ảnh công tơ vừa tải lên
                                  </a>
                                </div>
                              )}
                              {latestProofUrl && (
                                <div className="invoice-create-field" style={{ gridColumn: '1 / -1' }}>
                                  <span className="label">Ảnh công tơ kỳ trước</span>
                                  <a href={latestProofUrl} target="_blank" rel="noreferrer">
                                    Xem ảnh công tơ gần nhất
                                  </a>
                                </div>
                              )}
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

function getTabIcon(status: string, active: boolean) {
  const color = active ? '#ffffff' : '#64748b';
  const props = { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: color, strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, className: 'invoice-tab-icon' };

  switch (status.toLowerCase()) {
    case 'all':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <circle cx="12" cy="12" r="3" fill={color} stroke="none" />
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
      className={`invoice-tab-btn${active ? ' active' : ''}`}
    >
      {getTabIcon(status, active)}
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
            <span>
              {item.description}
              {item.meterReadingProofImageUrl && (
                <>
                  <br />
                  <a href={toAssetUrl(item.meterReadingProofImageUrl)} target="_blank" rel="noreferrer">
                    Xem ảnh công tơ
                  </a>
                </>
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

function resolveMeterProofUrl(imageUrl?: string | null, objectKey?: string | null) {
  if (imageUrl?.trim()) {
    return toAssetUrl(imageUrl);
  }

  if (objectKey?.trim()) {
    return toAssetUrl(objectKey);
  }

  return '';
}
