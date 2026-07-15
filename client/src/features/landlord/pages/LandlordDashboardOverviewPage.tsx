import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { formatDateVi } from '../../../shared/utils/format';
import { landlordDashboardApi } from '../services/landlordDashboardApi';
import type { LandlordDashboard, LandlordDashboardInvoice } from '../types/dashboard.types';
import './LandlordDashboardPage.css';
import './LandlordDashboardOverviewPage.css';

export default function LandlordDashboardOverviewPage() {
  const navigate = useNavigate();
  const [selectedMonth, setSelectedMonth] = useState(getCurrentMonthInputValue());
  const [dashboard, setDashboard] = useState<LandlordDashboard | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    async function loadDashboard() {
      setLoading(true);
      setError('');

      try {
        const [year, month] = selectedMonth.split('-').map(Number);
        const response = await landlordDashboardApi.getDashboard({ year, month });
        setDashboard(response.data);
      } catch (err) {
        setError(getApiErrorMessage(err, 'Không thể tải dashboard chủ trọ.'));
      } finally {
        setLoading(false);
      }
    }

    void loadDashboard();
  }, [selectedMonth]);

  const maxRevenue = useMemo(() => {
    return Math.max(...(dashboard?.revenueSeries.map((item) => item.revenue) ?? [0]), 1);
  }, [dashboard?.revenueSeries]);

  const monthLabel = useMemo(() => {
    const [year, month] = selectedMonth.split('-').map(Number);
    const date = new Date(year, month - 1, 1);
    return date.toLocaleDateString('vi-VN', { month: 'long', year: 'numeric' });
  }, [selectedMonth]);
  const currentMonthValue = getCurrentMonthInputValue();
  const canGoNextMonth = selectedMonth < currentMonthValue;

  if (loading) {
    return (
      <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải dashboard...</div>
        </main>
      </div>
    );
  }

  if (error || !dashboard) {
    return (
      <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <Alert type="error">{error || 'Không có dữ liệu dashboard.'}</Alert>
        </main>
      </div>
    );
  }

  const { overview } = dashboard;

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main landlord-pr-dashboard">
        <header className="pr-dashboard-topbar">
          <div>
            <span className="pr-kicker">Tổng quan vận hành</span>
            <h2>Dashboard</h2>
            <p>Theo dõi tình hình khu trọ và các công việc cần xử lý.</p>
          </div>
          <div className="pr-month-filter" aria-label="Tháng thống kê">
            <button
              type="button"
              className="pr-month-step"
              onClick={() => setSelectedMonth(addMonthsToInputValue(selectedMonth, -1))}
              aria-label="Xem tháng trước"
              title="Tháng trước"
            >
              <ChevronLeftIcon />
            </button>
            <label className="pr-month-picker">
              <CalendarIcon />
              <span>Tháng</span>
              <strong>{monthLabel}</strong>
              <input
                type="month"
                value={selectedMonth}
                max={currentMonthValue}
                onChange={(event) => setSelectedMonth(event.target.value || currentMonthValue)}
              />
            </label>
            <button
              type="button"
              className="pr-month-step"
              onClick={() => setSelectedMonth(addMonthsToInputValue(selectedMonth, 1))}
              disabled={!canGoNextMonth}
              aria-label="Xem tháng sau"
              title="Tháng sau"
            >
              <ChevronIcon />
            </button>
          </div>
        </header>

        <section className="pr-stat-grid">
          <MetricCard icon={<BuildingIcon />} label="Tổng khu trọ" value={overview.roomingHouseCount} tone="blue" />
          <MetricCard icon={<BedIcon />} label="Tổng phòng" value={overview.totalRoomCount} tone="indigo" />
          <MetricCard icon={<UsersIcon />} label="Đang thuê" value={overview.occupiedRoomCount} tone="green" />
          <MetricCard
            icon={<DoorIcon />}
            label="Phòng trống"
            value={overview.availableRoomCount}
            helper={`${overview.occupancyRate}% đã lấp đầy`}
            tone="orange"
          />
          <MetricCard icon={<WalletIcon />} label="Doanh thu tháng này" value={formatMoney(overview.currentMonthRevenue)} tone="purple" />
          <MetricCard
            icon={<TrendIcon />}
            label="Tổng doanh thu đã thanh toán"
            value={formatMoney(overview.totalPaidRevenue)}
            tone="mint"
          />
        </section>

        <section className="pr-main-grid">
          <article className="pr-panel pr-chart-panel">
            <PanelTitle icon={<ChartIcon />} title="Biểu đồ doanh thu 6 tháng" />
            <div className="pr-chart-unit">Triệu đồng</div>
            <div className="pr-chart">
              {dashboard.revenueSeries.map((point) => (
                <div className="pr-chart-column" key={point.period}>
                  <span className="pr-chart-value">{point.revenue > 0 ? formatMoney(point.revenue) : '0'}</span>
                  <div className="pr-chart-track">
                    <div
                      className="pr-chart-bar"
                      style={{ height: `${Math.max(point.revenue / maxRevenue * 100, point.revenue > 0 ? 6 : 0)}%` }}
                    />
                  </div>
                  <small>{formatPeriod(point.period)}</small>
                </div>
              ))}
            </div>
          </article>

          <div className="pr-summary-grid">
            <SummaryCard
              icon={<FileIcon />}
              title="Tóm tắt hóa đơn"
              onDetail={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)}
              items={[
                ['Bản nháp', overview.draftInvoiceCount, 'muted'],
                ['Đã phát hành', overview.issuedInvoiceCount, 'blue'],
                ['Đã thanh toán', overview.paidInvoiceCount, 'green'],
                ['Quá hạn', overview.overdueInvoiceCount, 'red']
              ]}
            />
            <SummaryCard
              icon={<FileIcon />}
              title="Tóm tắt hợp đồng"
              onDetail={() => navigate(ROUTE_PATHS.LANDLORD.CONTRACTS)}
              items={[
                ['Đang hiệu lực', overview.activeContractCount, 'green'],
                ['Sắp hết hạn', overview.expiringContractCount, 'amber'],
                ['Hết hạn', overview.expiredContractCount, 'red']
              ]}
            />
            <SummaryCard
              icon={<UsersIcon />}
              title="Yêu cầu thuê"
              onDetail={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}
              items={[
                ['Chờ duyệt', overview.pendingRentalRequestCount, 'amber'],
                ['Đã chấp nhận', overview.acceptedRentalRequestCount, 'green'],
                ['Từ chối', overview.rejectedRentalRequestCount, 'red']
              ]}
            />
            <SummaryCard
              icon={<CalendarIcon />}
              title="Lịch hẹn xem phòng"
              onDetail={() => navigate(ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS)}
              items={[
                ['Hôm nay', overview.todayAppointmentCount, 'blue'],
                ['Sắp tới', overview.upcomingAppointmentCount, 'violet'],
                ['Hoàn thành', overview.completedAppointmentCount, 'green']
              ]}
            />
          </div>
        </section>

        <section className="pr-bottom-grid">
          <article className="pr-panel">
            <PanelTitle icon={<WarningIcon />} title="Cảnh báo cần xử lý" />
            <ActionList
              items={[
                ['Hóa đơn quá hạn', overview.overdueInvoiceCount, 'red', () => navigate(ROUTE_PATHS.LANDLORD.INVOICES)],
                ['Hợp đồng sắp hết hạn', overview.expiringContractCount, 'orange', () => navigate(ROUTE_PATHS.LANDLORD.CONTRACTS)],
                ['Yêu cầu thuê chờ duyệt', overview.pendingRentalRequestCount, 'yellow', () => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)],
                ['Lịch xem phòng hôm nay', overview.todayAppointmentCount, 'violet', () => navigate(ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS)]
              ]}
            />
          </article>

          <article className="pr-panel pr-invoice-panel">
            <PanelTitle icon={<FileIcon />} title="Hóa đơn gần đây" />
            <InvoiceTable invoices={dashboard.recentInvoices} onViewAll={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)} />
          </article>
        </section>
      </main>
    </div>
  );
}

function MetricCard({
  icon,
  label,
  value,
  helper,
  tone,
  wide
}: {
  icon?: ReactNode;
  label: string;
  value: ReactNode;
  helper?: string;
  tone: string;
  wide?: boolean;
}) {
  return (
    <article className={`pr-metric-card ${tone}${wide ? ' wide' : ''}`}>
      {icon && <span className="pr-metric-icon">{icon}</span>}
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        {helper && <small>{helper}</small>}
      </div>
    </article>
  );
}

function PanelTitle({ icon, title }: { icon: ReactNode; title: string }) {
  return (
    <div className="pr-panel-title">
      {icon}
      <h3>{title}</h3>
    </div>
  );
}

function SummaryCard({
  icon,
  title,
  items,
  onDetail
}: {
  icon: ReactNode;
  title: string;
  items: [string, number, string][];
  onDetail: () => void;
}) {
  return (
    <article className="pr-panel pr-summary-card">
      <div className="pr-summary-title-row">
        <PanelTitle icon={icon} title={title} />
        <button type="button" className="pr-detail-button" onClick={onDetail}>
          Chi tiết <ChevronIcon />
        </button>
      </div>
      <div className="pr-summary-list">
        {items.map(([label, value, tone]) => (
          <div className="pr-summary-row" key={label}>
            <span className={`pr-dot ${tone}`} />
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
    </article>
  );
}

function ActionList({
  items
}: {
  items: [string, number | null, string, () => void][];
}) {
  return (
    <div className="pr-action-list">
      {items.map(([label, value, tone, onClick]) => (
        <button type="button" className="pr-action-row" key={label} onClick={onClick}>
          {value !== null && <strong className={`pr-action-count ${tone}`}>{value}</strong>}
          <span>{label}</span>
          <ChevronIcon />
        </button>
      ))}
    </div>
  );
}

function InvoiceTable({ invoices, onViewAll }: { invoices: LandlordDashboardInvoice[]; onViewAll: () => void }) {
  if (invoices.length === 0) {
    return <div className="pr-empty">Chưa có hóa đơn gần đây.</div>;
  }

  return (
    <>
      <div className="pr-invoice-table">
        <div className="pr-invoice-head">
          <span>Mã hóa đơn</span>
          <span>Phòng</span>
          <span>Trạng thái</span>
          <span>Số tiền</span>
          <span>Hạn thanh toán</span>
          <span />
        </div>
        {invoices.map((invoice) => (
          <div className="pr-invoice-row" key={invoice.id}>
            <span>{invoice.invoiceNo}</span>
            <span>{invoice.roomNumber}</span>
            <span><StatusBadge status={invoice.status} /></span>
            <strong>{formatMoney(invoice.totalAmount)}</strong>
            <span>{formatDateVi(invoice.dueDate)}</span>
            <span className="pr-more">...</span>
          </div>
        ))}
      </div>
      <button type="button" className="pr-view-all" onClick={onViewAll}>Xem tất cả hóa đơn <ChevronIcon /></button>
    </>
  );
}

function StatusBadge({ status }: { status: string }) {
  return <span className={`pr-status ${status.toLowerCase()}`}>{formatInvoiceStatus(status)}</span>;
}

function formatMoney(value: number) {
  return `${value.toLocaleString('vi-VN')} đ`;
}

function getCurrentMonthInputValue() {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

function addMonthsToInputValue(value: string, offset: number) {
  const [year, month] = value.split('-').map(Number);
  const date = new Date(year, month - 1 + offset, 1);
  const current = getCurrentMonthInputValue();
  const nextValue = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
  return nextValue > current ? current : nextValue;
}

function formatPeriod(period: string) {
  const [month] = period.split('/');
  return `Tháng ${Number(month).toString().padStart(2, '0')}`;
}

function formatInvoiceStatus(status: string) {
  const normalized = status.toLowerCase();
  if (normalized === 'draft') return 'Bản nháp';
  if (normalized === 'issued') return 'Đã phát hành';
  if (normalized === 'paid') return 'Đã thanh toán';
  if (normalized === 'overdue') return 'Quá hạn';
  if (normalized === 'cancelled') return 'Đã hủy';
  return status;
}

function BuildingIcon() { return <svg viewBox="0 0 24 24"><path d="M4 21V5a2 2 0 0 1 2-2h8v18" /><path d="M14 8h4a2 2 0 0 1 2 2v11" /><path d="M8 7h2M8 11h2M8 15h2M17 13h.01M17 17h.01" /></svg>; }
function BedIcon() { return <svg viewBox="0 0 24 24"><path d="M3 12V5" /><path d="M21 12v7" /><path d="M3 19v-7h18" /><path d="M7 12V9a2 2 0 0 1 2-2h8a4 4 0 0 1 4 4v1" /></svg>; }
function UsersIcon() { return <svg viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M22 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" /></svg>; }
function DoorIcon() { return <svg viewBox="0 0 24 24"><path d="M4 21h16" /><path d="M6 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17" /><path d="M14 12h.01" /></svg>; }
function WalletIcon() { return <svg viewBox="0 0 24 24"><path d="M20 7H5a2 2 0 0 1 0-4h12" /><path d="M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1V8a1 1 0 0 0-1-1Z" /><path d="M16 14h.01" /></svg>; }
function TrendIcon() { return <svg viewBox="0 0 24 24"><path d="m3 17 6-6 4 4 8-8" /><path d="M14 7h7v7" /></svg>; }
function ChartIcon() { return <svg viewBox="0 0 24 24"><path d="M3 3v18h18" /><path d="m7 15 4-4 3 3 5-7" /></svg>; }
function FileIcon() { return <svg viewBox="0 0 24 24"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" /><path d="M14 2v6h6" /><path d="M8 13h8M8 17h5" /></svg>; }
function CalendarIcon() { return <svg viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="2" /><path d="M16 2v4M8 2v4M3 10h18" /></svg>; }
function WarningIcon() { return <svg viewBox="0 0 24 24"><path d="m12 3 10 18H2L12 3Z" /><path d="M12 9v4M12 17h.01" /></svg>; }
function ChevronLeftIcon() { return <svg viewBox="0 0 24 24"><path d="m15 18-6-6 6-6" /></svg>; }
function ChevronIcon() { return <svg viewBox="0 0 24 24"><path d="m9 18 6-6-6-6" /></svg>; }
