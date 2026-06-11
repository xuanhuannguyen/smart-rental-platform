import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { billingApi } from '../api';
import type { Invoice } from '../types';
import './BillingPages.css';

const tenantStatuses = ['Issued', 'PartiallyPaid', 'Paid', 'Overdue'];

export default function TenantInvoicesPage() {
  const navigate = useNavigate();
  const { invoiceId } = useParams();
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
      setError(getApiErrorMessage(err, 'Khong tai duoc danh sach hoa don.'));
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
      setError(getApiErrorMessage(err, 'Khong tai duoc chi tiet hoa don.'));
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
      setMessage(`Thanh toan thanh cong hoa don ${response.data.invoiceNo}. Trang thai da cap nhat Paid.`);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Thanh toan hoa don that bai.'));
    } finally {
      setBusy('');
      setConfirmPay(null);
    }
  }

  return (
    <div className="billing-shell tenant">
      <aside className="billing-sidebar">
        <div>
          <span className="billing-kicker">Tenant</span>
          <h1>Hoa don cua toi</h1>
        </div>
        <button type="button" className="billing-nav active">Danh sach hoa don</button>
        <button type="button" className="billing-nav" onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}>
          Ve trang chu
        </button>
      </aside>

      <main className="billing-main">
        <section className="billing-header">
          <div>
            <span className="billing-kicker">Monthly invoices</span>
            <h2>Theo doi va thanh toan</h2>
            <p>Tenant chi thay hoa don tu Issued tro di va thanh toan qua vi noi bo.</p>
          </div>
          <button type="button" className="billing-button secondary" onClick={() => void loadInvoices()}>
            Tai lai
          </button>
        </section>

        <TenantNotification invoices={invoices} />
        {message && <div className="billing-alert success">{message}</div>}
        {error && <div className="billing-alert error">{error}</div>}

        <div className="filter-bar">
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">Tat ca trang thai</option>
            {tenantStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
          </select>
          <input type="month" value={periodFilter} onChange={(event) => setPeriodFilter(event.target.value)} />
        </div>

        {loading && invoices.length === 0 ? (
          <div className="billing-panel"><div className="state-block loading-state">Dang tai hoa don...</div></div>
        ) : filteredInvoices.length === 0 ? (
          <div className="billing-panel empty-state">
            <h3>Chua co hoa don</h3>
            <p>Khi chu tro phat hanh hoa don, danh sach se hien thi tai day.</p>
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
                    navigate(`/me/invoices/${invoice.id}`);
                  }}
                >
                  <span>{invoice.invoiceNo}</span>
                  <strong>{formatMoney(invoice.remainingAmount)}</strong>
                  <small>{invoice.billingPeriodStart} - {invoice.billingPeriodEnd}</small>
                  <em className={`status-chip ${invoice.status.toLowerCase()}`}>{invoice.status}</em>
                </button>
              ))}
            </div>

            {selectedInvoice && (
              <div className="billing-panel invoice-detail">
                <div className="invoice-topline">
                  <div>
                    <span className="billing-kicker">Invoice detail</span>
                    <h3>{selectedInvoice.invoiceNo}</h3>
                  </div>
                  <span className={`status-chip ${selectedInvoice.status.toLowerCase()}`}>{selectedInvoice.status}</span>
                </div>

                <div className="invoice-summary">
                  <span>Due date <strong>{selectedInvoice.dueDate}</strong></span>
                  <span>Total <strong>{formatMoney(selectedInvoice.totalAmount)}</strong></span>
                  <span>Paid <strong>{formatMoney(selectedInvoice.paidAmount)}</strong></span>
                  <span>Remaining <strong>{formatMoney(selectedInvoice.remainingAmount)}</strong></span>
                </div>

                <div className="data-table">
                  <div className="table-row table-head item-table-row">
                    <span>Hang muc</span><span>Mo ta</span><span>So luong</span><span>Don gia</span><span>Thanh tien</span>
                  </div>
                  {selectedInvoice.items.map((item) => (
                    <div key={item.id} className="table-row item-table-row">
                      <span>{item.itemType}</span>
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
                  {busy === selectedInvoice.id ? 'Dang thanh toan...' : 'Thanh toan bang vi'}
                </button>
              </div>
            )}
          </section>
        )}
      </main>

      {confirmPay && (
        <div className="modal-backdrop">
          <div className="confirm-dialog">
            <h3>Xac nhan thanh toan</h3>
            <p>Thanh toan {formatMoney(confirmPay.remainingAmount)} tu vi noi bo?</p>
            <div className="action-row">
              <button type="button" className="billing-button secondary" onClick={() => setConfirmPay(null)} disabled={Boolean(busy)}>Dong</button>
              <button type="button" className="billing-button" onClick={() => void handlePay(confirmPay)} disabled={Boolean(busy)}>Thanh toan</button>
            </div>
          </div>
        </div>
      )}
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
      {payable > 0 && <span>Ban co {payable} hoa don can thanh toan.</span>}
      {dueSoon > 0 && <span>{dueSoon} hoa don sap den han trong 3 ngay.</span>}
    </div>
  );
}

function canPay(invoice: Invoice) {
  return invoice.remainingAmount > 0 && (invoice.status === 'Issued' || invoice.status === 'PartiallyPaid' || invoice.status === 'Overdue');
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0
  }).format(value);
}
