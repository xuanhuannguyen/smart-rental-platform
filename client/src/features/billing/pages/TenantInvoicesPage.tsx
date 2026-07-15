import { Alert } from '../../../shared/components/ui/Alert';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAvatarImageUrl } from '../../../shared/api/assets';
import { PrivateMediaImage } from '../../../shared/components/media/PrivateMediaImage';
import { Toast } from '../../../shared/components/ui/Toast';
import { Button } from '../../../shared/components/ui/Button';
import { billingApi } from '../api';
import type { Invoice, InvoiceItem } from '../types';
import { WalletPaymentConfirmModal } from '../../wallet/components/WalletPaymentConfirmModal';
import '../../home/pages/MePage.css';
import './BillingPages.css';

const tenantStatuses = ['Issued', 'Paid', 'Overdue', 'Cancelled'];

type TenantInvoicesPanelProps = {
  invoiceId?: string;
  onOpenInvoice?: (invoiceId: string) => void;
};

export function TenantInvoicesPanel({ invoiceId: controlledInvoiceId, onOpenInvoice }: TenantInvoicesPanelProps) {
  const navigate = useNavigate();
  const { invoiceId: routeInvoiceId } = useParams();
  const invoiceId = controlledInvoiceId ?? routeInvoiceId;
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [selectedId, setSelectedId] = useState(invoiceId ?? '');
  const [statusFilter, setStatusFilter] = useState('');
  const [periodFilter, setPeriodFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState('');
  const [message, setMessage] = useState('');
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [error, setError] = useState('');
  const [confirmPay, setConfirmPay] = useState<Invoice | null>(null);
  const [meterImagePreview, setMeterImagePreview] = useState<{ src: string; title: string; subtitle: string } | null>(null);

  useEffect(() => {
    void loadInvoices();
  }, []);

  useEffect(() => {
    if (invoiceId) {
      setSelectedId(invoiceId);
      void loadInvoiceDetail(invoiceId);
    }
  }, [invoiceId]);

  const filteredInvoices = useMemo(() => {
    return invoices.filter((invoice) => {
      const matchesStatus = !statusFilter || invoice.status === statusFilter;
      const matchesPeriod = !periodFilter || invoice.billingPeriodStart.startsWith(periodFilter) || invoice.billingPeriodEnd.startsWith(periodFilter);
      return matchesStatus && matchesPeriod;
    });
  }, [invoices, statusFilter, periodFilter]);

  const selectedInvoice = useMemo(() => {
    return invoices.find((invoice) => invoice.id === selectedId) ?? filteredInvoices[0] ?? null;
  }, [filteredInvoices, invoices, selectedId]);

  async function loadInvoices() {
    setLoading(true);
    setError('');
    try {
      const response = await billingApi.getMyInvoices();
      setInvoices(response.data);
      setSelectedId((current) => current || invoiceId || response.data[0]?.id || '');
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tải được danh sách hóa đơn.'));
    } finally {
      setLoading(false);
    }
  }

  async function loadInvoiceDetail(targetId: string) {
    setLoading(true);
    setError('');
    try {
      const response = await billingApi.getMyInvoice(targetId);
      setInvoices((prev) => {
        const exists = prev.some((invoice) => invoice.id === response.data.id);
        return exists ? prev.map((invoice) => invoice.id === response.data.id ? response.data : invoice) : [response.data, ...prev];
      });
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không tải được chi tiết hóa đơn.'));
    } finally {
      setLoading(false);
    }
  }

  async function handlePay(invoice: Invoice) {
    setBusy(invoice.id);
    setToast(null);
    setError('');
    try {
      const response = await billingApi.payInvoice(invoice.id);
      setInvoices((prev) => prev.map((item) => item.id === invoice.id ? response.data : item));
      setSelectedId(response.data.id);
      setToast({ message: `Thanh toán thành công hóa đơn ${response.data.invoiceNo}. Trạng thái đã cập nhật Đã thanh toán.`, type: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Thanh toán hóa đơn thất bại.'), type: 'error' });
    } finally {
      setBusy('');
      setConfirmPay(null);
    }
  }

  return (
    <>
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16c0 1.1.9 2 2 2h12a2 2 0 0 0 2-2V8l-6-6z" />
              <path d="M14 3v5h5M16 13H8M16 17H8M10 9H8" />
            </svg>
          </div>
        }
        eyebrow="Hóa đơn hằng tháng"
        title="Theo dõi &amp; Thanh toán"
        description="Xem các hóa đơn đã phát hành và có thể thanh toán các khoản còn nợ qua ví nội bộ."
        rightContent={
          <div className="invoice-filter-group" style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
            <div className="invoice-filter-item">
              <label>Trạng thái</label>
              <select className="invoice-filter-select" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
                <option value="">Tất cả trạng thái</option>
                {tenantStatuses.map((status) => <option key={status} value={status}>{getInvoiceStatusLabel(status)}</option>)}
              </select>
            </div>
            <div className="invoice-filter-item">
              <label>Kỳ hóa đơn</label>
              <input type="month" className="invoice-filter-select" value={periodFilter} onChange={(event) => setPeriodFilter(event.target.value)} />
            </div>
          </div>
        }
      />

      <TenantNotification invoices={invoices} />
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
      {error && <Alert type="error">{error}</Alert>}

      {loading && invoices.length === 0 ? (
        <div className="billing-panel"><div className="state-block loading-state">Đang tải hóa đơn...</div></div>
      ) : filteredInvoices.length === 0 ? (
        <div className="billing-panel empty-state">
          <h3>Chưa có hóa đơn</h3>
          <p>Khi chủ trọ phát hành hóa đơn, danh sách sẽ hiển thị tại đây.</p>
        </div>
      ) : (
        <section className="tenant-layout">
          <div className="billing-panel tenant-invoice-list">
            {filteredInvoices.map((invoice) => {
              const isSelected = selectedInvoice?.id === invoice.id;
              const statusClass = invoice.status.toLowerCase();
              return (
                <button
                  type="button"
                  key={invoice.id}
                  className={`tenant-invoice-card ${isSelected ? 'active' : ''} status-${statusClass}`}
                  onClick={() => {
                    setSelectedId(invoice.id);
                    if (onOpenInvoice) {
                      onOpenInvoice(invoice.id);
                    } else {
                      navigate(ROUTE_PATHS.ACCOUNT.INVOICE_DETAIL(invoice.id));
                    }
                  }}
                >
                  <div className="tenant-invoice-card-header">
                    <span className="invoice-no">{invoice.invoiceNo}</span>
                    <span className={`invoice-status-badge ${statusClass}`}>{getInvoiceStatusLabel(invoice.status)}</span>
                  </div>
                  <div className="tenant-invoice-card-body">
                    <div className="invoice-period">
                      <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="period-icon">
                        <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                        <line x1="16" y1="2" x2="16" y2="6"></line>
                        <line x1="8" y1="2" x2="8" y2="6"></line>
                        <line x1="3" y1="10" x2="21" y2="10"></line>
                      </svg>
                      <span>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</span>
                    </div>
                    <div className="invoice-amount-box">
                      <span className="amount-label">{invoice.status === 'Paid' ? 'Đã thanh toán' : 'Cần thanh toán'}</span>
                      <strong className="amount-value">{formatMoney(getPayableAmount(invoice))}</strong>
                    </div>
                  </div>
                </button>
              );
            })}
          </div>

          {selectedInvoice && (
            <div className="billing-panel tenant-invoice-detail">
              <div className="tenant-invoice-detail-header">
                <div className="header-title">
                  <div className="invoice-icon-box">
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                      <polyline points="14 2 14 8 20 8"></polyline>
                      <line x1="16" y1="13" x2="8" y2="13"></line>
                      <line x1="16" y1="17" x2="8" y2="17"></line>
                      <polyline points="10 9 9 9 8 9"></polyline>
                    </svg>
                  </div>
                  <div>
                    <h3>{selectedInvoice.invoiceNo}</h3>
                    <span className="billing-kicker">Chi tiết hóa đơn thanh toán</span>
                  </div>
                </div>
                <span className={`invoice-status-badge xl ${selectedInvoice.status.toLowerCase()}`}>
                  {getInvoiceStatusLabel(selectedInvoice.status)}
                </span>
              </div>

              <div className="tenant-invoice-stats-grid">
                <div className="stat-card">
                  <span className="stat-label">Hạn thanh toán</span>
                  <strong className="stat-value due-date">{selectedInvoice.dueDate}</strong>
                </div>
                <div className="stat-card">
                  <span className="stat-label">Tổng tiền</span>
                  <strong className="stat-value">{formatMoney(selectedInvoice.totalAmount)}</strong>
                </div>
                <div className="stat-card">
                  <span className="stat-label">Đã thanh toán</span>
                  <strong className="stat-value paid">{formatMoney(getPaidAmount(selectedInvoice))}</strong>
                </div>
                <div className="stat-card highlighted">
                  <span className="stat-label">Còn lại</span>
                  <strong className="stat-value payable">{formatMoney(getPayableAmount(selectedInvoice))}</strong>
                </div>
              </div>

              <div className="tenant-invoice-items-table">
                <div className="table-header">
                  <span className="col-type">Khoản mục</span>
                  <span className="col-desc">Mô tả</span>
                  <span className="col-qty">Số lượng</span>
                  <span className="col-price">Đơn giá</span>
                  <span className="col-amount">Thành tiền</span>
                </div>
                <div className="table-body">
                  {selectedInvoice.items.map((item) => (
                    <div key={item.id} className="table-row">
                      <span className="col-type" data-label="Khoản mục">
                        <span className={`item-type-tag ${item.itemType.toLowerCase()}`}>
                          {getInvoiceItemTypeLabel(item.itemType)}
                        </span>
                      </span>
                      <span className="col-desc tenant-invoice-item-desc" data-label="Mô tả">
                        <span>{item.description}</span>
                        {item.meterReadingProofImageUrl && (
                          <button
                            type="button"
                            className="tenant-meter-proof-button"
                            onClick={() => setMeterImagePreview({
                              src: item.meterReadingProofImageUrl!,
                              title: getMeterReadingButtonLabel(item),
                              subtitle: item.description
                            })}
                          >
                            {getMeterReadingButtonLabel(item)}
                          </button>
                        )}
                      </span>
                      <span className="col-qty" data-label="Số lượng">{item.quantity}</span>
                      <span className="col-price" data-label="Đơn giá">{formatMoney(item.unitPrice)}</span>
                      <strong className="col-amount" data-label="Thành tiền">{formatMoney(item.amount)}</strong>
                    </div>
                  ))}
                </div>
              </div>

              <div className="tenant-invoice-actions">
                <button
                  type="button"
                  className="tenant-pay-button"
                  disabled={!canPay(selectedInvoice) || busy === selectedInvoice.id}
                  onClick={() => setConfirmPay(selectedInvoice)}
                >
                  <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="pay-icon">
                    <rect x="2" y="5" width="20" height="14" rx="2" ry="2"></rect>
                    <line x1="2" y1="10" x2="22" y2="10"></line>
                  </svg>
                  {busy === selectedInvoice.id ? 'Đang thực hiện giao dịch...' : 'Thanh toán ngay bằng Ví nội bộ'}
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      <WalletPaymentConfirmModal
        isOpen={Boolean(confirmPay)}
        title="Xác nhận thanh toán"
        description={confirmPay ? `Thanh toán ${formatMoney(getPayableAmount(confirmPay))} từ ví nội bộ?` : undefined}
        amount={confirmPay ? getPayableAmount(confirmPay) : 0}
        confirmLabel="Thanh toán"
        isSubmitting={Boolean(busy)}
        onConfirm={() => {
          if (confirmPay) {
            void handlePay(confirmPay);
          }
        }}
        onClose={() => setConfirmPay(null)}
      />

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
    </>
  );
}

export function AccountTenantInvoicesPage() {
  const navigate = useNavigate();

  return (
    <div className="profile-invoices-section">
      <TenantInvoicesPanel onOpenInvoice={(invoiceId) => navigate(ROUTE_PATHS.ACCOUNT.INVOICE_DETAIL(invoiceId))} />
    </div>
  );
}

export default function TenantInvoicesPage() {
  const { currentUser, logout } = useAuth();
  const navigate = useNavigate();
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const isAdmin = currentUser?.roles.includes('Admin') || false;
  const isLandlord = currentUser?.roles.includes('Landlord') || false;

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  if (!currentUser) {
    return null;
  }

  const avatarInitials = currentUser.displayName
    ? currentUser.displayName.split(' ').map((name) => name[0]).join('').substring(0, 2).toUpperCase()
    : 'U';

  return (
    <div className="home-container">
      <header className="home-header">
        <div className="header-logo" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
          Smart Rental
        </div>
        <div className="header-auth" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          {isAdmin ? (
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.ADMIN.APPROVALS)}>
              Duyệt hồ sơ
            </Button>
          ) : isLandlord ? (
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}>
              Kênh chủ trọ
            </Button>
          ) : (
            <Button type="button" onClick={() => navigate(ROUTE_PATHS.LANDLORD.REGISTER)}>
              Đăng ký làm chủ trọ
            </Button>
          )}

          <div className="avatar-wrapper" ref={dropdownRef}>
            <button className="avatar-btn" onClick={() => setShowDropdown(!showDropdown)}>
              {currentUser.avatarUrl && currentUser.avatarUrl.trim() !== '' ? (
                <img src={toAvatarImageUrl(currentUser)} alt="Avatar" className="avatar-image" />
              ) : (
                <span className="avatar-initials">{avatarInitials}</span>
              )}
              <span className="avatar-name">{currentUser.displayName}</span>
            </button>
            {showDropdown && (
              <div className="avatar-dropdown">
                <div className="dropdown-info">
                  <strong>{currentUser.displayName}</strong>
                  <span>{currentUser.email}</span>
                </div>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.PROFILE); }}>
                  Chỉnh sửa thông tin
                </button>
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ACCOUNT.INVOICES); }}>
                  Hóa đơn của tôi
                </button>
                {isAdmin && (
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ADMIN.APPROVALS); }}>
                    Duyệt hồ sơ
                  </button>
                )}
                {isLandlord && (
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES); }}>
                    Kênh chủ trọ
                  </button>
                )}
                <button className="dropdown-item dropdown-item--danger" onClick={() => { setShowDropdown(false); logout(); }}>
                  Đăng xuất
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      <main className="tenant-invoices-page">
        <TenantInvoicesPanel />
      </main>

      <footer className="home-footer">
        <p>&copy; 2026 Smart Rental Platform. All rights reserved.</p>
      </footer>
    </div>
  );
}

function TenantNotification({ invoices }: { invoices: Invoice[] }) {
  const dueSoon = invoices.filter((invoice) => {
    const diffDays = Math.ceil((new Date(invoice.dueDate).getTime() - Date.now()) / 86400000);
    return invoice.status === 'Issued' && diffDays >= 0 && diffDays <= 3;
  }).length;
  const payable = invoices.filter((invoice) => canPay(invoice)).length;

  if (dueSoon === 0 && payable === 0) {
    return null;
  }

  return (
    <div className="tenant-billing-notification">
      <div className="notification-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <line x1="12" y1="17" x2="12.01" y2="17" />
        </svg>
      </div>
      <div className="notification-content">
        <h4>Chú ý thanh toán</h4>
        <p>
          {payable > 0 && <span>Bạn có <strong>{payable}</strong> hóa đơn cần thanh toán. </span>}
          {dueSoon > 0 && <span>Có <strong>{dueSoon}</strong> hóa đơn sẽ hết hạn trong vòng 3 ngày tới. Vui lòng thanh toán sớm.</span>}
        </p>
      </div>
    </div>
  );
}

function canPay(invoice: Invoice) {
  return invoice.totalAmount > 0 && (invoice.status === 'Issued' || invoice.status === 'Overdue');
}

function getPaidAmount(invoice: Invoice) {
  return invoice.status === 'Paid' ? invoice.totalAmount : 0;
}

function getPayableAmount(invoice: Invoice) {
  return invoice.status === 'Paid' ? 0 : invoice.totalAmount;
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

function getMeterReadingButtonLabel(item: InvoiceItem) {
  const text = `${item.serviceName ?? ''} ${item.description ?? ''}`.toLowerCase();
  if (text.includes('điện') || text.includes('dien')) {
    return 'Xem chỉ số điện';
  }

  if (text.includes('nước') || text.includes('nuoc')) {
    return 'Xem chỉ số nước';
  }

  return 'Xem ảnh chỉ số';
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}
