import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { landlordApi } from '../services/landlordApi';
import type { LandlordDashboardData } from '../types/landlord.types';
import './LandlordDashboardPage.css';

const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });

export default function LandlordDashboardPage() {
  const navigate = useNavigate();
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const [data, setData] = useState<LandlordDashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const loadDashboard = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const response = await landlordApi.getDashboard(month);
      setData(response.data ?? null);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải dữ liệu dashboard.'));
    } finally { setLoading(false); }
  }, [month]);

  useEffect(() => { void loadDashboard(); }, [loadDashboard]);

  if (loading && !data) return <DashboardSkeleton />;
  if (error && !data) return <main className="dashboard-main"><ErrorPanel message={error} retry={loadDashboard} /></main>;

  return (
    <main className="dashboard-main landlord-dashboard-page">
      <header className="dash-header">
        <div><p className="dash-kicker">TỔNG QUAN VẬN HÀNH</p><h1>Dashboard</h1><p>Theo dõi tình hình khu trọ và các công việc cần xử lý.</p></div>
        <label className="month-picker"><CalendarIcon /><span>Tháng</span><input type="month" value={month} max="2099-12" onChange={(event) => setMonth(event.target.value)} /></label>
      </header>

      {error && <div className="inline-error">{error}<button onClick={() => void loadDashboard()}>Thử lại</button></div>}
      {data?.totalRoomingHouses === 0 ? <EmptyDashboard onCreate={() => navigate(`${ROUTE_PATHS.LANDLORD.REGISTER}?mode=new`)} /> : data && <DashboardContent data={data} navigate={navigate} />}
    </main>
  );
}

function DashboardContent({ data, navigate }: { data: LandlordDashboardData; navigate: ReturnType<typeof useNavigate> }) {
  const kpis = [
    ['building', 'Tổng khu trọ', data.totalRoomingHouses, 'blue'], ['bed', 'Tổng phòng', data.totalRooms, 'indigo'],
    ['users', 'Đang thuê', data.occupiedRooms, 'green'], ['door', 'Phòng trống', data.availableRooms, 'orange'],
    ['wallet', 'Doanh thu tháng', money.format(data.monthlyRevenue), 'violet']
  ] as const;
  const summaries = [
    { icon: 'invoice', title: 'Tóm tắt hóa đơn', rows: [['Bản nháp', data.draftInvoices, 'slate'], ['Đã phát hành', data.issuedInvoices, 'blue'], ['Đã thanh toán', data.paidInvoices, 'green'], ['Quá hạn', data.overdueInvoices, 'red']] },
    { icon: 'contract', title: 'Tóm tắt hợp đồng', rows: [['Đang hiệu lực', data.activeContracts, 'green'], ['Sắp hết hạn', data.expiringContracts, 'orange'], ['Hết hạn', data.expiredContracts, 'red']] },
    { icon: 'request', title: 'Yêu cầu thuê', rows: [['Chờ duyệt', data.pendingRequests, 'orange'], ['Đã chấp nhận', data.acceptedRequests, 'green'], ['Từ chối', data.rejectedRequests, 'red']] },
    { icon: 'calendar', title: 'Lịch hẹn xem phòng', rows: [['Hôm nay', data.todayAppointments, 'blue'], ['Sắp tới', data.upcomingAppointments, 'violet'], ['Hoàn thành', data.completedAppointments, 'green']] }
  ];
  const alerts = [
    [data.overdueInvoices, 'hóa đơn quá hạn', `${ROUTE_PATHS.LANDLORD.INVOICES}?status=Overdue`, 'red'], [data.expiringContracts, 'hợp đồng sắp hết hạn', `${ROUTE_PATHS.LANDLORD.CONTRACTS}?filter=expiring`, 'orange'],
    [data.pendingRequests, 'yêu cầu thuê chờ duyệt', `${ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS}?status=Pending`, 'yellow'], [data.todayAppointments, 'lịch xem phòng hôm nay', `${ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS}?filter=today`, 'violet']
  ] as const;
  return <>
    <section className="kpi-grid">{kpis.map(([icon, label, value, tone]) => <article className={`kpi-card kpi-${tone}`} key={label}><Icon name={icon} /><div><span>{label}</span><strong>{value}</strong>{label === 'Phòng trống' && <small>{data.occupancyRate}% đã lấp đầy</small>}</div></article>)}</section>
    <section className="revenue-strip"><Icon name="chart" /><div><span>Tổng doanh thu đã thanh toán</span><strong>{money.format(data.totalRevenue)}</strong></div></section>
    <section className="dashboard-upper"><RevenueChart data={data.revenueChart} /><div className="summary-grid">{summaries.map((item) => <SummaryCard key={item.title} {...item} />)}</div></section>
    <section className="dashboard-lower">
      <div className="lower-stack"><Panel title="Cảnh báo cần xử lý" icon="warning"><div className="action-list">{alerts.map(([count, label, path, tone]) => <button key={label} onClick={() => navigate(path)}><span className={`alert-count ${tone}`}>{count}</span><span>{label}</span><Icon name="chevron" /></button>)}</div></Panel>
      <Panel title="Thao tác nhanh" icon="bolt"><div className="action-list quick-list"><button onClick={() => navigate(ROUTE_PATHS.LANDLORD.ROOMING_HOUSES)}><Icon name="meter" /><span>Ghi chỉ số điện/nước</span><Icon name="chevron" /></button><button onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)}><Icon name="invoice" /><span>Tạo hóa đơn</span><Icon name="chevron" /></button><button onClick={() => navigate(ROUTE_PATHS.LANDLORD.RENTAL_REQUESTS)}><Icon name="request" /><span>Xem yêu cầu thuê</span><Icon name="chevron" /></button><button onClick={() => navigate(ROUTE_PATHS.LANDLORD.VIEWING_APPOINTMENTS)}><Icon name="calendar" /><span>Xem lịch hẹn hôm nay</span><Icon name="chevron" /></button></div></Panel></div>
      <Panel title="Hóa đơn gần đây" icon="invoice" className="invoice-panel"><InvoiceTable data={data.latestInvoices} onOpen={(id) => navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(id))} /><button className="view-all" onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICES)}>Xem tất cả hóa đơn <Icon name="chevron" /></button></Panel>
    </section>
  </>;
}

function RevenueChart({ data }: { data: LandlordDashboardData['revenueChart'] }) {
  const max = Math.max(...data.map((item) => item.revenue), 1);
  return <Panel title="Biểu đồ doanh thu 6 tháng" icon="chart" className="chart-panel"><div className="chart-wrap"><div className="y-label">Triệu đồng</div><div className="bars">{data.map((item) => { const height = item.revenue === 0 ? 3 : Math.max(12, item.revenue / max * 100); return <div className="bar-column" key={item.month}><span>{item.revenue > 0 ? Math.round(item.revenue / 1_000_000) : 0}</span><div className="bar-track"><div className="bar" tabIndex={0} aria-label={`Doanh thu tháng ${item.month}: ${money.format(item.revenue)}`} data-tooltip={money.format(item.revenue)} style={{ height: `${height}%` }} /></div><small>Tháng {item.month.slice(0, 2)}</small></div>; })}</div></div></Panel>;
}

function SummaryCard({ icon, title, rows }: { icon: string; title: string; rows: (string | number)[][] }) { return <Panel title={title} icon={icon} className="summary-card"><ul>{rows.map(([label, value, tone]) => <li key={label}><span><i className={`dot ${tone}`} />{label}</span><strong>{value}</strong></li>)}</ul></Panel>; }
function Panel({ title, icon, className = '', children }: { title: string; icon: string; className?: string; children: React.ReactNode }) { return <article className={`dashboard-panel ${className}`}><h2><Icon name={icon} />{title}</h2>{children}</article>; }

function InvoiceTable({ data, onOpen }: { data: LandlordDashboardData['latestInvoices']; onOpen: (id: string) => void }) {
  if (!data.length) return <div className="table-empty">Chưa có hóa đơn trong hệ thống.</div>;
  const labels: Record<string, string> = { Draft: 'Bản nháp', Issued: 'Đã phát hành', Paid: 'Đã thanh toán', Overdue: 'Quá hạn', Cancelled: 'Đã hủy' };
  return <div className="invoice-table-wrap"><table><thead><tr><th>Mã hóa đơn</th><th>Phòng</th><th>Trạng thái</th><th>Số tiền</th><th>Hạn thanh toán</th><th /></tr></thead><tbody>{data.map((item) => <tr key={item.id}><td>{item.invoiceCode}</td><td>{item.roomName}</td><td><span className={`invoice-status status-${item.status.toLowerCase()}`}>{labels[item.status]}</span></td><td>{money.format(item.amount)}</td><td>{new Date(`${item.dueDate}T00:00:00`).toLocaleDateString('vi-VN')}</td><td><button aria-label={`Mở ${item.invoiceCode}`} onClick={() => onOpen(item.id)}>•••</button></td></tr>)}</tbody></table></div>;
}

function EmptyDashboard({ onCreate }: { onCreate: () => void }) { return <section className="empty-dashboard"><div><Icon name="building" /></div><h2>Bắt đầu với khu trọ đầu tiên</h2><p>Tạo khu trọ để dashboard có thể tổng hợp phòng, hợp đồng, hóa đơn và doanh thu của bạn.</p><button onClick={onCreate}>+ Tạo khu trọ</button></section>; }
function ErrorPanel({ message, retry }: { message: string; retry: () => void }) { return <section className="empty-dashboard error-dashboard"><div><Icon name="warning" /></div><h2>Không thể tải dashboard</h2><p>{message}</p><button onClick={retry}>Thử lại</button></section>; }
function DashboardSkeleton() { return <main className="dashboard-main landlord-dashboard-page"><div className="skeleton skeleton-title" /><div className="skeleton-grid">{Array.from({ length: 5 }, (_, i) => <div className="skeleton skeleton-card" key={i} />)}</div><div className="skeleton skeleton-chart" /></main>; }

function CalendarIcon() { return <Icon name="calendar" />; }
function Icon({ name }: { name: string }) {
  const paths: Record<string, React.ReactNode> = {
    building: <><path d="M4 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17"/><path d="M16 9h3a1 1 0 0 1 1 1v11M8 7h4M8 11h4M8 15h4M9 21v-3h3v3"/></>, bed: <><path d="M3 20v-8M21 20v-6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v2h18M7 12V8h5a2 2 0 0 1 2 2v2"/></>,
    users: <><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></>, door: <><path d="M4 21h16M6 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17"/><path d="M14 12h.01"/></>,
    wallet: <><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M16 13h4M2 9h20"/></>, chart: <><path d="M3 3v18h18"/><path d="m7 15 4-4 3 3 5-7"/></>, invoice: <><path d="M6 2h9l4 4v16H6z"/><path d="M14 2v5h5M9 12h7M9 16h7"/></>, contract: <><path d="M5 3h14v18H5zM9 8h6M9 12h6M9 16h4"/></>, request: <><circle cx="9" cy="7" r="4"/><path d="M2 21v-2a4 4 0 0 1 4-4h6M19 14v6M16 17h6"/></>, calendar: <><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 10h18"/></>, warning: <><path d="M12 3 2 21h20L12 3Z"/><path d="M12 9v5M12 18h.01"/></>, bolt: <path d="m13 2-9 12h8l-1 8 9-12h-8z"/>, meter: <><path d="M4 19a8 8 0 1 1 16 0"/><path d="m12 15 4-5M8 19h8"/></>, chevron: <path d="m9 18 6-6-6-6"/>
  };
  return <svg className="dash-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>;
}
