import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { Button } from '../../../shared/components/ui/Button';
import { billingApi } from '../api';
import type { Invoice } from '../types';
import '../../home/pages/MePage.css';
import './BillingPages.css';

const tenantStatuses = ['Issued', 'PartiallyPaid', 'Paid', 'Overdue'];

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
  const [error, setError] = useState('');
  const [confirmPay, setConfirmPay] = useState<Invoice | null>(null);

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
    setMessage('');
    setError('');
    try {
      const response = await billingApi.payInvoice(invoice.id);
      setInvoices((prev) => prev.map((item) => item.id === invoice.id ? response.data : item));
      setSelectedId(response.data.id);
      setMessage(`Thanh toán thành công hóa đơn ${response.data.invoiceNo}. Trạng thái đã cập nhật Đã thanh toán.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Thanh toán hóa đơn thất bại.'));
    } finally {
      setBusy('');
      setConfirmPay(null);
    }
  }

  return (
    <>
      <section className="billing-header tenant-invoices-header">
        <div>
          <span className="billing-kicker">Hóa đơn hằng tháng</span>
          <h2>Theo dõi và thanh toán</h2>
          <p>Bạn chỉ nhìn thấy hóa đơn đã phát hành và có thể thanh toán các khoản còn nợ qua ví nội bộ.</p>
        </div>
        <button type="button" className="billing-button secondary" onClick={() => void loadInvoices()}>
          Tải lại
        </button>
      </section>

      <TenantNotification invoices={invoices} />
      {message && <div className="billing-alert success">{message}</div>}
      {error && <div className="billing-alert error">{error}</div>}

      <div className="filter-bar">
        <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
          <option value="">Tất cả trạng thái</option>
          {tenantStatuses.map((status) => <option key={status} value={status}>{getInvoiceStatusLabel(status)}</option>)}
        </select>
        <input type="month" value={periodFilter} onChange={(event) => setPeriodFilter(event.target.value)} />
      </div>

      {loading && invoices.length === 0 ? (
        <div className="billing-panel"><div className="state-block loading-state">Đang tải hóa đơn...</div></div>
      ) : filteredInvoices.length === 0 ? (
        <div className="billing-panel empty-state">
          <h3>Chưa có hóa đơn</h3>
          <p>Khi chủ trọ phát hành hóa đơn, danh sách sẽ hiển thị tại đây.</p>
        </div>
      ) : (
        <section className="tenant-layout">
          <div className="billing-panel invoice-list">
            {filteredInvoices.map((invoice) => (
              <button
                type="button"
                key={invoice.id}
                className={`invoice-list-item ${selectedInvoice?.id === invoice.id ? 'active' : ''}`}
                onClick={() => {
                  setSelectedId(invoice.id);
                  if (onOpenInvoice) {
                    onOpenInvoice(invoice.id);
                  } else {
                    navigate(`/me/invoices/${invoice.id}`);
                  }
                }}
              >
                <span>{invoice.invoiceNo}</span>
                <strong>{formatMoney(invoice.remainingAmount)}</strong>
                <small>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</small>
                <em className={`status-chip ${invoice.status.toLowerCase()}`}>{getInvoiceStatusLabel(invoice.status)}</em>
              </button>
            ))}
          </div>

          {selectedInvoice && (
            <div className="billing-panel invoice-detail">
              <div className="invoice-topline">
                <div>
                  <span className="billing-kicker">Chi tiết hóa đơn</span>
                  <h3>{selectedInvoice.invoiceNo}</h3>
                </div>
                <span className={`status-chip ${selectedInvoice.status.toLowerCase()}`}>{getInvoiceStatusLabel(selectedInvoice.status)}</span>
              </div>

              <div className="invoice-summary">
                <span>Hạn thanh toán <strong>{selectedInvoice.dueDate}</strong></span>
                <span>Tổng tiền <strong>{formatMoney(selectedInvoice.totalAmount)}</strong></span>
                <span>Đã thanh toán <strong>{formatMoney(selectedInvoice.paidAmount)}</strong></span>
                <span>Còn lại <strong>{formatMoney(selectedInvoice.remainingAmount)}</strong></span>
              </div>

              <div className="data-table">
                <div className="table-row table-head item-table-row">
                  <span>Hạng mục</span><span>Mô tả</span><span>Số lượng</span><span>Đơn giá</span><span>Thành tiền</span>
                </div>
                {selectedInvoice.items.map((item) => (
                  <div key={item.id} className="table-row item-table-row">
                    <span>{getInvoiceItemTypeLabel(item.itemType)}</span>
                    <span>{item.description}</span>
                    <span>{item.quantity}</span>
                    <span>{formatMoney(item.unitPrice)}</span>
                    <strong>{formatMoney(item.amount)}</strong>
                  </div>
                ))}
              </div>

              <button
                type="button"
                className="billing-button"
                disabled={!canPay(selectedInvoice) || busy === selectedInvoice.id}
                onClick={() => setConfirmPay(selectedInvoice)}
              >
                {busy === selectedInvoice.id ? 'Đang thanh toán...' : 'Thanh toán bằng ví'}
              </button>
            </div>
          )}
        </section>
      )}

      {confirmPay && (
        <div className="modal-backdrop">
          <div className="confirm-dialog">
            <h3>Xác nhận thanh toán</h3>
            <p>Thanh toán {formatMoney(confirmPay.remainingAmount)} từ ví nội bộ?</p>
            <div className="action-row">
              <button type="button" className="billing-button secondary" onClick={() => setConfirmPay(null)} disabled={Boolean(busy)}>Đóng</button>
              <button type="button" className="billing-button" onClick={() => void handlePay(confirmPay)} disabled={Boolean(busy)}>Thanh toán</button>
            </div>
          </div>
        </div>
      )}
    </>
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
            <Button type="button" variant="secondary" onClick={() => navigate(ROUTE_PATHS.LANDLORD.DASHBOARD)}>
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
                <img src={toAssetUrl(currentUser.avatarUrl)} alt="Avatar" className="avatar-image" />
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
                <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ME.PROFILE); }}>
                  Chỉnh sửa thông tin
                </button>
                {isAdmin && (
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.ADMIN.APPROVALS); }}>
                    Duyệt hồ sơ
                  </button>
                )}
                {isLandlord && (
                  <button className="dropdown-item" onClick={() => { setShowDropdown(false); navigate(ROUTE_PATHS.LANDLORD.DASHBOARD); }}>
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
    return (invoice.status === 'Issued' || invoice.status === 'PartiallyPaid') && diffDays >= 0 && diffDays <= 3;
  }).length;
  const payable = invoices.filter((invoice) => canPay(invoice)).length;

  if (dueSoon === 0 && payable === 0) {
    return null;
  }

  return (
    <div className="billing-alert info">
      {payable > 0 && <span>Bạn có {payable} hóa đơn cần thanh toán.</span>}
      {dueSoon > 0 && <span>{dueSoon} hóa đơn sắp đến hạn trong 3 ngày.</span>}
    </div>
  );
}

function canPay(invoice: Invoice) {
  return invoice.remainingAmount > 0 && (invoice.status === 'Issued' || invoice.status === 'PartiallyPaid' || invoice.status === 'Overdue');
}

function getInvoiceStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Draft: 'Nháp',
    Issued: 'Đã phát hành',
    PartiallyPaid: 'Thanh toán một phần',
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
    Service: 'Dịch vụ',
    Discount: 'Giảm trừ',
    Other: 'Khác'
  };

  return labels[itemType] ?? itemType;
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}
