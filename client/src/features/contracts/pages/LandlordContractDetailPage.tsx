import { Alert } from '../../../shared/components/ui/Alert';
import React, { useCallback, useEffect, useState, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Button } from '../../../shared/components/ui/Button';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { contractApi } from '../api';
import type {
  ContractDetailResponse,
  ContractAppendixResponse,
  ContractAppendixStatus,
  ContractFileResponse
} from '../types';
import {
  canLandlordOpenAppendixForSigning,
  formatAppendixStatus,
  isBlockingAppendix,
  shouldShowAppendixToCurrentUser,
} from '../appendixRules';
import {
  findAccessibleAppendixFile,
  loadAccessibleContractFiles,
} from '../appendixFiles';
import { findAccessibleContractFile } from '../contractFiles';
import { AppendixFileActions } from '../components/AppendixFileActions';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { formatDateVi as formatDate, formatMoneyString as formatCurrency } from '../../../shared/utils/format';
import { useAuth } from '../../../app/providers/AuthProvider';
import { TerminateContractModal } from '../../rental-history/pages/TerminateContractModal';
import { LandlordCreateAppendixModalV2 } from '../components/LandlordCreateAppendixModalV2';
import { CreateTerminationInvoiceModal } from '../components/CreateTerminationInvoiceModal';
import { AppendixPreviewModal } from '../../rental-history/components/AppendixPreviewModal';
import { billingApi } from '../../billing/api';
import type { Invoice } from '../../billing/types';
import '../../rental-history/pages/TenantRentalHistoryDetailPage.css';

type Tab = 'contract' | 'occupants' | 'invoices';
type OccupantFilter = 'all' | 'active' | 'pending' | 'left';
const invoiceStatusTabs = ['', 'Issued', 'Paid', 'Overdue', 'Cancelled'];

function getContractTabIcon(tab: Tab) {
  const props = { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };

  if (tab === 'occupants') {
    return (
      <svg {...props}>
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
        <path d="M16 3.13a4 4 0 0 1 0 7.75" />
      </svg>
    );
  }

  if (tab === 'invoices') {
    return (
      <svg {...props}>
        <path d="M4 2h14l2 2v18l-3-2-3 2-3-2-3 2-4-2V2z" />
        <line x1="8" y1="8" x2="16" y2="8" />
        <line x1="8" y1="12" x2="16" y2="12" />
        <line x1="8" y1="16" x2="13" y2="16" />
      </svg>
    );
  }

  return (
    <svg {...props}>
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <polyline points="14 2 14 8 20 8" />
      <line x1="16" y1="13" x2="8" y2="13" />
      <line x1="16" y1="17" x2="8" y2="17" />
    </svg>
  );
}

function getInvoiceStatusTabIcon(status: string) {
  const props = { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const };

  switch (status) {
    case 'Issued':
      return <svg {...props}><circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" /></svg>;
    case 'Paid':
      return <svg {...props}><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" /><polyline points="22 4 12 14.01 9 11.01" /></svg>;
    case 'Overdue':
      return <svg {...props}><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" /><line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg>;
    case 'Cancelled':
      return <svg {...props}><circle cx="12" cy="12" r="10" /><line x1="15" y1="9" x2="9" y2="15" /><line x1="9" y1="9" x2="15" y2="15" /></svg>;
    default:
      return <svg {...props}><line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" /><line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" /><line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" /></svg>;
  }
}

export default function LandlordContractDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  // Appendix state
  const [appendices, setAppendices] = useState<ContractAppendixResponse[] | null>(null);
  const [isAppendixModalOpen, setIsAppendixModalOpen] = useState(false);
  const [editingAppendix, setEditingAppendix] = useState<ContractAppendixResponse | null>(null);
  const [signingAppendixId, setSigningAppendixId] = useState<string | null>(null);
  const [appendicesError, setAppendicesError] = useState<string | null>(null);
  const [accessibleContractFiles, setAccessibleContractFiles] = useState<ContractFileResponse[]>([]);
  const [appendixFilesError, setAppendixFilesError] = useState<string | null>(null);

  // Terminate state
  const [isTerminateModalOpen, setIsTerminateModalOpen] = useState(false);
  const [isTerminationInvoiceModalOpen, setIsTerminationInvoiceModalOpen] = useState(false);

  // File action state
  const [isFileActionLoading, setIsFileActionLoading] = useState(false);
  const [contractActionError, setContractActionError] = useState<string | null>(null);

  // UI state
  const [activeTab, setActiveTab] = useState<Tab>('contract');
  const [occupantFilter, setOccupantFilter] = useState<OccupantFilter>('all');

  // Invoice state
  const [contractInvoices, setContractInvoices] = useState<Invoice[]>([]);
  const [invoiceStatusFilter, setInvoiceStatusFilter] = useState('');
  const [invoicesLoading, setInvoicesLoading] = useState(false);
  const [invoiceError, setInvoiceError] = useState<string | null>(null);

  const loadContractDetail = useCallback(async (contractId: string) => {
    setLoading(true);
    setError('');
    try {
      const res = await contractApi.getContract(contractId);
      setContract(res.data);
    } catch (err) {
      setError(getApiErrorMessage(err, 'Không thể tải chi tiết hợp đồng.'));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAppendices = useCallback(async (contractId: string) => {
    setAppendicesError(null);
    setAppendixFilesError(null);
    setAccessibleContractFiles([]);
    try {
      const response = await contractApi.getAppendices(contractId);
      setAppendices(response.data);
    } catch (err) {
      setAppendices([]);
      setAppendicesError(getApiErrorMessage(err, 'Không thể tải danh sách phụ lục.'));
    }

    try {
      setAccessibleContractFiles(await loadAccessibleContractFiles(contractId));
    } catch (err) {
      setAccessibleContractFiles([]);
      setAppendixFilesError(getApiErrorMessage(err, 'Không thể tải danh sách file phụ lục đã ký.'));
    }
  }, []);

  useEffect(() => {
    if (id) {
      void loadContractDetail(id);
      void loadAppendices(id);
    }
  }, [id, loadContractDetail, loadAppendices]);

  const loadContractInvoices = useCallback(async () => {
    if (!contract) return;
    setInvoicesLoading(true);
    setInvoiceError(null);
    try {
      const response = await billingApi.getLandlordInvoices({
        contractId: contract.id,
        status: invoiceStatusFilter || undefined,
      });
      setContractInvoices(response.data);
    } catch (err) {
      setInvoiceError(getApiErrorMessage(err, 'Không thể tải danh sách hóa đơn.'));
      setContractInvoices([]);
    } finally {
      setInvoicesLoading(false);
    }
  }, [contract, invoiceStatusFilter]);

  useEffect(() => {
    if (activeTab === 'invoices') {
      void loadContractInvoices();
    }
  }, [activeTab, loadContractInvoices]);

  async function resolveRawContractFile(contractId: string): Promise<ContractFileResponse> {
    const filesResponse = await contractApi.getContractFiles(contractId);
    let files = filesResponse.data;
    if (files.length === 0) {
      await contractApi.generateContractFile(contractId);
      const updatedFilesResponse = await contractApi.getContractFiles(contractId);
      files = updatedFilesResponse.data;
    }
    const contractFile = findAccessibleContractFile(files);
    if (contractFile) return contractFile;

    throw new Error('Chưa có file hợp đồng nào được tạo.');
  }

  async function openContractFile(mode: 'view' | 'download') {
    if (!contract) return;
    setIsFileActionLoading(true);
    setContractActionError(null);

    try {
      const file = await resolveRawContractFile(contract.id);
      const blob = await contractApi.downloadContractFile(contract.id, file.id);

      const url = window.URL.createObjectURL(blob);
      if (mode === 'view') {
        window.open(url, '_blank', 'noopener,noreferrer');
      } else {
        const link = document.createElement('a');
        link.href = url;
        link.download = `${contract.contractNumber}-${file.purpose.toLowerCase()}.pdf`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      }
      setTimeout(() => window.URL.revokeObjectURL(url), 60000);
    } catch (err) {
      setContractActionError(getApiErrorMessage(err, 'Không thể tải file hợp đồng.'));
    } finally {
      setIsFileActionLoading(false);
    }
  }

  const visibleAppendices = appendices && currentUser
    ? appendices.filter((appendix) => shouldShowAppendixToCurrentUser(appendix, currentUser.userId))
    : null;
  const hasBlockingAppendix = appendices?.some(isBlockingAppendix) ?? false;
  const currentMainTenantUserId = useMemo(
    () => contract ? resolveCurrentMainTenantUserId(contract.mainTenantUserId, appendices) : '',
    [appendices, contract]
  );

  const occupants = contract?.occupants ? [...contract.occupants].sort((a, b) =>
    new Date(a.moveInDate).getTime() - new Date(b.moveInDate).getTime()
  ) : [];

  const filteredOccupants = occupants.filter((occupant) => {
    if (occupantFilter === 'active') return occupant.status === 'Active';
    if (occupantFilter === 'pending') return occupant.status === 'PendingMoveIn';
    if (occupantFilter === 'left') return occupant.status === 'MoveOut' || occupant.status === 'Voided';
    return true;
  });

  if (loading) return <div>Đang tải dữ liệu...</div>;
  if (error) return <div style={{ color: '#b91c1c' }}>{error}</div>;
  if (!contract) return <div>Không tìm thấy hợp đồng.</div>;

  return (
    <main className="dashboard-main">
      <div className="history-detail-page">
        <div className="contract-header-wrapper">
          <PageHeader
            className="page-header-band--flat-bottom"
            onBack={() => navigate(ROUTE_PATHS.LANDLORD.CONTRACTS)}
            icon={
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                  <line x1="16" y1="13" x2="8" y2="13" />
                  <line x1="16" y1="17" x2="8" y2="17" />
                  <polyline points="10 9 9 9 8 9" />
                </svg>
              </div>
            }
            eyebrow={
              <div style={{ display: 'flex', alignItems: 'center', gap: '4px', color: '#2563eb', fontSize: '11px', fontWeight: 600, textTransform: 'uppercase', marginBottom: '4px' }}>
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
                  <polyline points="9 22 9 12 15 12 15 22" />
                </svg>
                {contract.roomingHouseName}
              </div>
            }
            title={`Phòng ${contract.roomNumber}`}
            description={
              <div className="overview-description" style={{ display: 'flex', alignItems: 'center', gap: '6px', margin: 0, color: '#64748b' }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                  <line x1="16" y1="2" x2="16" y2="6" />
                  <line x1="8" y1="2" x2="8" y2="6" />
                  <line x1="3" y1="10" x2="21" y2="10" />
                </svg>
                Mã HĐ: <strong>{contract.contractNumber}</strong>
              </div>
            }
            rightContent={
              <div className="overview-right">
                <span className={`status-badge ${getStatusClass(contract.status)}`} style={{ fontSize: '1rem', padding: '8px 16px' }}>
                  {formatStatus(contract.status)}
                </span>
              </div>
            }
          />

          <Tabs
            className="attached-top"
            variant="segmented-primary"
            activeId={activeTab}
            onChange={(tab) => setActiveTab(tab as Tab)}
            items={[
              { id: 'contract', label: 'Thông tin hợp đồng', icon: getContractTabIcon('contract') },
              { id: 'occupants', label: 'Thông tin người ở', icon: getContractTabIcon('occupants') },
              { id: 'invoices', label: 'Hóa đơn', icon: getContractTabIcon('invoices') },
            ]}
          />
        </div>

        {activeTab === 'contract' && (
          <div className="history-detail-content" style={{ marginTop: '20px' }}>
            <div className="contract-info-grid">
              <div className="contract-info-block">
                <h3>Thông tin cơ bản</h3>
                <div className="contract-info-item">
                  <span className="label">Đại diện thuê:</span>
                  <span className="value">{contract.mainTenantName}</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Kỳ hạn hợp đồng:</span>
                  <span className="value">{formatDate(contract.startDate)} - {formatDate(contract.endDate)}</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Ngày chấm dứt:</span>
                  <span className="value">
                    {contract.status === 'Cancelled' || contract.status === 'Expired'
                      ? contract.terminationDate
                        ? formatDate(contract.terminationDate)
                        : 'Chưa xác định'
                      : 'Chưa xác định'}
                  </span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Số người ở tối đa:</span>
                  <span className="value">{contract.maxOccupants} người</span>
                </div>
              </div>
              <div className="contract-info-block">
                <h3>Thông tin tài chính</h3>
                <div className="contract-info-item">
                  <span className="label">Tiền thuê hằng tháng:</span>
                  <span className="value">{formatCurrency(contract.monthlyRent)} đ</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Tiền cọc:</span>
                  <span className="value">{formatCurrency(contract.depositAmount)} đ</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Ngày thanh toán hằng tháng:</span>
                  <span className="value">Ngày {contract.paymentDay}</span>
                </div>
              </div>
            </div>

            <div className="contract-actions">
              <Button variant="outline" onClick={() => void openContractFile('download')} disabled={isFileActionLoading}>Tải hợp đồng</Button>
              <Button variant="outline" onClick={() => void openContractFile('view')} disabled={isFileActionLoading}>Xem hợp đồng</Button>

              {contract.isAwaitingFinalInvoice && (
                <Button onClick={() => setIsTerminationInvoiceModalOpen(true)}>
                  Tạo hóa đơn kỳ cuối
                </Button>
              )}

              {contract.status === 'Active' && (
                <Button variant="danger" onClick={() => setIsTerminateModalOpen(true)}>Chấm dứt hợp đồng</Button>
              )}
            </div>

            {contractActionError && (
              <div style={{ color: '#b91c1c', marginTop: '12px' }}>
                {contractActionError}
              </div>
            )}
          </div>
        )}

        {activeTab === 'contract' && (
          <div className="history-detail-content" style={{ marginTop: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ margin: 0, fontSize: '1.25rem' }}>Danh sách phụ lục</h2>
              {contract.status === 'Active' && (
                <Button
                  style={{ whiteSpace: 'nowrap' }}
                  onClick={() => setIsAppendixModalOpen(true)}
                  disabled={hasBlockingAppendix}
                  title={hasBlockingAppendix ? 'Không thể tạo phụ lục mới khi đang có phụ lục chờ ký hoặc đang yêu cầu sửa.' : ''}
                >
                  Tạo phụ lục
                </Button>
              )}
            </div>
            {appendicesError && (
              <div style={{ color: '#b91c1c', marginBottom: '16px', padding: '12px', background: '#fef2f2', borderRadius: '8px' }}>
                {appendicesError}
              </div>
            )}
            {appendixFilesError && (
              <div style={{ color: '#b91c1c', marginBottom: '16px', padding: '12px', background: '#fef2f2', borderRadius: '8px' }}>
                {appendixFilesError}
              </div>
            )}

            <div className="appendices-list">
              {visibleAppendices === null ? (
                <div style={{ padding: '20px', color: '#64748b' }}>Đang tải danh sách phụ lục...</div>
              ) : visibleAppendices.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '20px', color: '#64748b', background: '#f8fafc', borderRadius: '8px' }}>Hợp đồng này chưa có phụ lục nào.</div>
              ) : (
                visibleAppendices.map((appendix) => (
                  <div key={appendix.id} className="appendix-item">
                    <div className="appendix-info">
                      <h4>Phụ lục số {appendix.appendixNumber}</h4>
                      <div className="appendix-dates" style={{ textAlign: 'left', marginTop: '4px' }}>
                        <div><strong>Ngày hiệu lực:</strong> {formatDate(appendix.effectiveDate)}</div>
                      </div>
                      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '8px' }}>
                        {appendix.status === 'TenantRevisionRequested' && contract.status === 'Active' && (
                          <Button
                            variant="outline"
                            style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                            onClick={() => setEditingAppendix(appendix)}
                          >
                            Sửa phụ lục
                          </Button>
                        )}
                        {canLandlordOpenAppendixForSigning(appendix) && (
                          <Button
                            style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                            onClick={() => setSigningAppendixId(appendix.id)}
                          >
                            Xem và ký phụ lục
                          </Button>
                        )}
                      </div>
                      <AppendixFileActions
                        contractId={contract.id}
                        contractNumber={contract.contractNumber}
                        appendix={appendix}
                        file={findAccessibleAppendixFile(accessibleContractFiles, appendix.id)}
                      />
                    </div>
                    <div className="appendix-status-badge" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '8px' }}>
                      <span className={`status-badge ${getAppendixStatusClass(appendix.status)}`} style={{ padding: '6px 16px', fontSize: '0.85rem' }}>
                        {formatAppendixStatus(appendix, 'Landlord')}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {activeTab === 'occupants' && (
          <div className="history-detail-secondary-section">
            <div className="contract-invoices-header history-detail-section-heading">
              <div>
                <h2>Thông tin người ở</h2>
                <p>Danh sách người ở của hợp đồng này.</p>
              </div>
            </div>
            <Tabs
              className="attached-bottom"
              variant="segmented-secondary"
              activeId={occupantFilter}
              onChange={(filter) => setOccupantFilter(filter as OccupantFilter)}
              items={[
                { id: 'all', label: `Tất cả (${occupants.length})`, icon: getOccupantFilterTabIcon('all') },
                { id: 'active', label: `Đang ở (${occupants.filter((occupant) => occupant.status === 'Active').length})`, icon: getOccupantFilterTabIcon('active') },
                { id: 'pending', label: `Chờ dọn vào (${occupants.filter((occupant) => occupant.status === 'PendingMoveIn').length})`, icon: getOccupantFilterTabIcon('pending') },
                { id: 'left', label: `Đã rời đi / Đã hủy (${occupants.filter((occupant) => occupant.status === 'MoveOut' || occupant.status === 'Voided').length})`, icon: getOccupantFilterTabIcon('left') },
              ]}
            />

          <div className="history-detail-content tab-attached-panel tab-attached-panel--compact">

            <div className="occupant-list">
              {filteredOccupants.map((occupant) => (
                <div key={occupant.id} className="occupant-item">
                  <div className="occupant-info">
                    <h4>
                      {occupant.fullName}
                      {' - '}
                      {formatOccupantRole(Boolean(occupant.userId && occupant.userId === currentMainTenantUserId))}
                    </h4>
                    <div className="occupant-role">Email: {formatOptionalContact(occupant.email)}</div>
                    <div className="occupant-role">Số điện thoại: {formatOptionalContact(occupant.phoneNumber)}</div>
                  </div>
                  <div className="occupant-dates">
                    <div style={{ marginBottom: '6px' }}>
                      <span className={`status-badge ${occupant.status === 'Active' ? 'active' : occupant.status === 'PendingMoveIn' ? 'warning' : 'terminated'}`} style={{ padding: '2px 8px', fontSize: '0.75rem' }}>
                        {occupant.status === 'Active' ? 'Đang ở' : occupant.status === 'PendingMoveIn' ? 'Chờ dọn vào' : occupant.status === 'Voided' ? 'Đã hủy' : 'Đã rời đi'}
                      </span>
                    </div>
                    <div><strong>Vào:</strong> {formatDate(occupant.moveInDate)}</div>
                    {occupant.moveOutDate && (
                      <div style={{ color: '#94a3b8' }}><strong>Rời đi:</strong> {formatDate(occupant.moveOutDate)}</div>
                    )}
                  </div>
                </div>
              ))}
              {filteredOccupants.length === 0 && (
                <div style={{ textAlign: 'center', padding: '20px', color: '#64748b' }}>Không có người ở nào khớp với bộ lọc.</div>
              )}
            </div>
          </div>
          </div>
        )}

        {activeTab === 'invoices' && (
          <div className="history-detail-secondary-section">
            <div className="contract-invoices-header history-detail-section-heading">
              <div>
                <h2>Hóa đơn hằng tháng</h2>
                <p>Danh sách hóa đơn của hợp đồng này.</p>
              </div>
            </div>

            <Tabs
              className="attached-bottom"
              variant="segmented-secondary"
              activeId={invoiceStatusFilter || 'all'}
              onChange={(status) => setInvoiceStatusFilter(status === 'all' ? '' : status)}
              items={invoiceStatusTabs.map((status) => ({
                id: status || 'all',
                label: status ? getInvoiceStatusLabel(status) : 'Tất cả',
                icon: getInvoiceStatusTabIcon(status),
              }))}
            />

            <div className="history-detail-content tab-attached-panel tab-attached-panel--compact">
            {invoiceError && <Alert type="error">{invoiceError}</Alert>}

            {invoicesLoading ? (
              <div className="coming-soon-placeholder">
                <p>Đang tải danh sách hóa đơn...</p>
              </div>
            ) : contractInvoices.length === 0 ? (
              <div className="coming-soon-placeholder">
                <h2>Chưa có hóa đơn</h2>
                <p>Chưa có hóa đơn nào phù hợp với bộ lọc hiện tại.</p>
              </div>
            ) : (
              <div className="contract-invoice-list">
                {contractInvoices.map((invoice) => (
                  <div
                    key={invoice.id}
                    className={`contract-invoice-card ${invoice.status === 'Cancelled' ? 'muted' : ''}`}
                  >
                    <div>
                      <div className="contract-invoice-title">
                        <strong>{invoice.invoiceNo}</strong>
                        <span className={`status-badge ${getInvoiceStatusClass(invoice.status)}`}>
                          {getInvoiceStatusLabel(invoice.status)}
                        </span>
                      </div>
                      <div className="contract-invoice-meta">
                        <span>Kỳ: {formatDate(invoice.billingPeriodStart)} - {formatDate(invoice.billingPeriodEnd)}</span>
                        <span>Hạn thanh toán: {formatDate(invoice.dueDate)}</span>
                        <span>Người đứng tên: {invoice.tenantName}</span>
                      </div>
                    </div>
                    <div className="contract-invoice-actions">
                      <strong>{formatCurrency(invoice.totalAmount)} đ</strong>
                      <Button
                        variant="outline"
                        onClick={() => navigate(ROUTE_PATHS.LANDLORD.INVOICE_DETAIL(invoice.id))}
                      >
                        Xem chi tiết
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
            </div>
          </div>
        )}

        {isTerminateModalOpen && contract && (
          <TerminateContractModal
            contract={contract as any} // TerminateContractModal expects ContractHistoryItemResponse, but only uses minimal fields which are compatible
            terminationActor="Landlord"
            onClose={() => setIsTerminateModalOpen(false)}
            onTerminated={() => {
              setIsTerminateModalOpen(false);
              void loadContractDetail(contract.id);
            }}
          />
        )}

        {isTerminationInvoiceModalOpen && contract && (
          <CreateTerminationInvoiceModal
            contract={contract}
            onClose={() => setIsTerminationInvoiceModalOpen(false)}
            onCreated={() => {
              setIsTerminationInvoiceModalOpen(false);
              void loadContractDetail(contract.id);
            }}
          />
        )}

        {isAppendixModalOpen && contract && (
          <LandlordCreateAppendixModalV2
            contract={contract}
            onClose={() => setIsAppendixModalOpen(false)}
            onCreated={() => {
              setIsAppendixModalOpen(false);
              void loadAppendices(contract.id);
            }}
          />
        )}

        {editingAppendix && contract && (
          <LandlordCreateAppendixModalV2
            contract={contract}
            appendix={editingAppendix}
            onClose={() => setEditingAppendix(null)}
            onCreated={() => {
              setEditingAppendix(null);
              void loadAppendices(contract.id);
            }}
          />
        )}

        {signingAppendixId && contract && (
          <AppendixPreviewModal
            contractId={contract.id}
            appendixId={signingAppendixId}
            isCreator={appendices?.find((appendix) => appendix.id === signingAppendixId)?.createdByUserId === currentUser?.userId}
            hasNoSignatures={appendices?.find((appendix) => appendix.id === signingAppendixId)?.signatures.length === 0}
            onClose={() => setSigningAppendixId(null)}
            onSuccess={() => {
              setSigningAppendixId(null);
              void loadAppendices(contract.id);
            }}
          />
        )}
      </div>
    </main>
  );
}

function getOccupantFilterTabIcon(filter: OccupantFilter) {
  const props = {
    width: 15,
    height: 15,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2.2,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
  };

  switch (filter) {
    case 'active':
      return (
        <svg {...props}>
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <polyline points="16 11 18 13 22 9" />
        </svg>
      );
    case 'pending':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      );
    case 'left':
      return (
        <svg {...props}>
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
          <polyline points="16 17 21 12 16 7" />
          <line x1="21" y1="12" x2="9" y2="12" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
          <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        </svg>
      );
  }
}

function formatOccupantRole(isMainTenant: boolean) {
  return isMainTenant ? 'Người thuê chính' : 'Người ở cùng';
}

function resolveCurrentMainTenantUserId(
  contractMainTenantUserId: string,
  appendices: ContractAppendixResponse[] | null
) {
  let currentMainTenantUserId = contractMainTenantUserId;

  const appliedAppendices = [...(appendices ?? [])]
    .filter((appendix) => appendix.status === 'Active' && appendix.appliedAt)
    .sort((left, right) => {
      const leftAppliedAt = getAppendixAppliedSortTime(left);
      const rightAppliedAt = getAppendixAppliedSortTime(right);

      if (leftAppliedAt !== rightAppliedAt) {
        return leftAppliedAt - rightAppliedAt;
      }

      return new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime();
    });

  for (const appendix of appliedAppendices) {
    const orderedChanges = [...appendix.changes].sort((left, right) => left.sortOrder - right.sortOrder);

    for (const change of orderedChanges) {
      if (
        change.targetType === 'Contract' &&
        change.changeType === 'Update' &&
        normalizeAppendixFieldName(change.fieldName) === 'maintenantuserid'
      ) {
        currentMainTenantUserId = extractAppendixUserId(change.newValue) ?? currentMainTenantUserId;
      }
    }
  }

  return currentMainTenantUserId;
}

function getAppendixAppliedSortTime(appendix: ContractAppendixResponse) {
  return new Date(appendix.appliedAt ?? appendix.activatedAt ?? appendix.updatedAt).getTime();
}

function normalizeAppendixFieldName(value?: string | null) {
  return value?.replace(/_/g, '').trim().toLowerCase() ?? '';
}

function extractAppendixUserId(value?: string | null) {
  if (!value?.trim()) return null;

  const trimmed = value.trim().replace(/^"|"$/g, '');
  if (isGuid(trimmed)) return trimmed;

  try {
    const parsed: unknown = JSON.parse(value);

    if (typeof parsed === 'string' && isGuid(parsed)) {
      return parsed;
    }

    if (parsed && typeof parsed === 'object' && 'userId' in parsed) {
      const userId = (parsed as { userId?: unknown }).userId;
      return typeof userId === 'string' && isGuid(userId) ? userId : null;
    }
  } catch {
    return null;
  }

  return null;
}

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

function formatOptionalContact(value?: string | null) {
  return value?.trim() ? value.trim() : 'Chưa xác định';
}

function formatStatus(status: string) {
  switch (status) {
    case 'Active':
      return 'Đang hiệu lực';
    case 'Expired':
      return 'Đã hết hạn';
    case 'Cancelled':
      return 'Đã chấm dứt';
    default:
      return status;
  }
}

function getStatusClass(status: string) {
  switch (status) {
    case 'Active':
      return 'active';
    case 'Expired':
      return 'completed';
    case 'Cancelled':
      return 'terminated';
    default:
      return '';
  }
}

function getInvoiceStatusLabel(status: string) {
  switch (status) {
    case 'Issued':
      return 'Đã phát hành';
    case 'Paid':
      return 'Đã thanh toán';
    case 'Overdue':
      return 'Quá hạn';
    case 'Cancelled':
      return 'Đã hủy';
    default:
      return status;
  }
}

function getInvoiceStatusClass(status: string) {
  switch (status) {
    case 'Issued':
      return 'completed';
    case 'Paid':
      return 'active';
    case 'Overdue':
      return 'terminated';
    case 'Cancelled':
      return 'terminated';
    default:
      return '';
  }
}

function getAppendixStatusClass(status: ContractAppendixStatus) {
  switch (status) {
    case 'Active': return 'active';
    case 'PendingSignature':
    case 'LandlordRevisionRequested':
    case 'TenantRevisionRequested': return 'pending';
    case 'Rejected':
    case 'Cancelled': return 'terminated';
    default: return '';
  }
}
