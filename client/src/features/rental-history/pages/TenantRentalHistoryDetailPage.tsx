import React, { useCallback, useEffect, useState, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Button } from '../../../shared/components/ui/Button';
import { useAuth } from '../../../app/providers/AuthProvider';
import { contractApi } from '../../contracts/api';
import type {
  ContractAppendixResponse,
  ContractAppendixStatus,
  ContractFileResponse,
  ContractHistoryItemResponse,
} from '../../contracts/types';
import {
  canTenantOpenAppendixForSigning,
  formatAppendixStatus,
  isBlockingAppendix,
  shouldShowAppendixToCurrentUser,
} from '../../contracts/appendixRules';
import {
  findAccessibleAppendixFile,
  loadAccessibleContractFiles,
} from '../../contracts/appendixFiles';
import { AppendixFileActions } from '../../contracts/components/AppendixFileActions';
import { billingApi } from '../../billing/api';
import type { Invoice } from '../../billing/types';
import './TenantRentalHistoryDetailPage.css';
import { CreateAppendixModal } from './CreateAppendixModal';
import { TerminateContractModal } from './TerminateContractModal';
import { AppendixPreviewModal } from '../components/AppendixPreviewModal';

type Tab = 'occupants' | 'contract' | 'invoices' | 'issues' | 'appendices';
const invoiceStatusTabs = ['', 'Issued', 'Paid', 'Overdue', 'Cancelled'];

export const TenantRentalHistoryDetailPage: React.FC = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [contract, setContract] = useState<ContractHistoryItemResponse | null>(null);
  const [appendices, setAppendices] = useState<ContractAppendixResponse[] | null>(null);
  const [appendicesError, setAppendicesError] = useState<string | null>(null);
  const [accessibleContractFiles, setAccessibleContractFiles] = useState<ContractFileResponse[]>([]);
  const [appendixFilesError, setAppendixFilesError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<Tab>('contract');
  const [occupantFilter, setOccupantFilter] = useState<'all' | 'active' | 'left'>('all');
  const [isAppendixModalOpen, setIsAppendixModalOpen] = useState(false);
  const [editingAppendix, setEditingAppendix] = useState<ContractAppendixResponse | null>(null);
  const [isTerminateModalOpen, setIsTerminateModalOpen] = useState(false);
  const [signingAppendixId, setSigningAppendixId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isFileActionLoading, setIsFileActionLoading] = useState(false);
  const [contractInvoices, setContractInvoices] = useState<Invoice[]>([]);
  const [invoiceStatusFilter, setInvoiceStatusFilter] = useState('');
  const [invoicesLoading, setInvoicesLoading] = useState(false);
  const [invoiceActionBusy, setInvoiceActionBusy] = useState('');
  const [invoiceError, setInvoiceError] = useState<string | null>(null);
  const [invoiceMessage, setInvoiceMessage] = useState<string | null>(null);

  const visibleAppendices = useMemo(() => {
    if (!appendices || !currentUser) return null;
    return appendices.filter((appendix) => shouldShowAppendixToCurrentUser(appendix, currentUser.userId));
  }, [appendices, currentUser]);

  const loadContract = useCallback(async () => {
    if (!id) {
      setError('Không tìm thấy hợp đồng.');
      setContract(null);
      setAppendices(null);
      setAccessibleContractFiles([]);
      setAppendixFilesError(null);
      setIsLoading(false);
      return;
    }

    setError(null);
    const response = await contractApi.getMyHistory();
    const foundContract = response.data?.find((item) => item.id === id) ?? null;

    if (!foundContract) {
      setError('Không tìm thấy hợp đồng trong lịch sử thuê.');
      setContract(null);
      setAppendices(null);
      setAccessibleContractFiles([]);
      setAppendixFilesError(null);
      return;
    }

    setContract(foundContract);
    setAppendicesError(null);
    setAppendixFilesError(null);

    try {
      const appendicesRes = await contractApi.getAppendices(foundContract.id);
      setAppendices(appendicesRes.data);
    } catch (err: any) {
      console.error('Failed to load appendices:', err);
      setAppendicesError(err?.message || 'Không thể tải danh sách phụ lục.');
      setAppendices([]);
    }

    try {
      setAccessibleContractFiles(await loadAccessibleContractFiles(foundContract.id));
    } catch (err) {
      setAccessibleContractFiles([]);
      setAppendixFilesError(getApiErrorMessage(err, 'Không thể tải danh sách file phụ lục đã ký.'));
    }
  }, [id]);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      try {
        setIsLoading(true);
        await loadContract();
      } catch {
        if (isMounted) {
          setError('Không thể tải chi tiết lịch sử thuê. Vui lòng thử lại sau.');
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void load();

    return () => {
      isMounted = false;
    };
  }, [loadContract]);

  const loadContractInvoices = useCallback(async () => {
    if (!contract) return;

    setInvoicesLoading(true);
    setInvoiceError(null);
    try {
      const response = await billingApi.getMyContractInvoices(
        contract.id,
        invoiceStatusFilter ? { status: invoiceStatusFilter } : undefined
      );
      setContractInvoices(response.data);
    } catch (err) {
      setInvoiceError(getApiErrorMessage(err, 'Không thể tải danh sách hóa đơn.'));
      setContractInvoices([]);
    } finally {
      setInvoicesLoading(false);
    }
  }, [contract, invoiceStatusFilter]);

  useEffect(() => {
    if (activeTab !== 'invoices') return;
    void loadContractInvoices();
  }, [activeTab, loadContractInvoices]);

  if (isLoading) return <div>Đang tải dữ liệu...</div>;
  if (error) return <div>{error}</div>;
  if (!contract) return <div>Không tìm thấy hợp đồng.</div>;

  const occupants = [...contract.occupants].sort((a, b) =>
    new Date(a.moveInDate).getTime() - new Date(b.moveInDate).getTime()
  );

  const filteredOccupants = occupants.filter((occupant) => {
    if (occupantFilter === 'active') return occupant.status === 'Active';
    if (occupantFilter === 'left') return occupant.status !== 'Active' || occupant.moveOutDate !== null;
    return true;
  });

  const handleViewContract = async () => {
    await openContractFile('view');
  };

  const handleDownloadContract = async () => {
    await openContractFile('download');
  };

  const openContractFile = async (mode: 'view' | 'download') => {
    try {
      setActionError(null);
      setIsFileActionLoading(true);
      const file = await resolveVisibleContractFile(contract);
      const blob = await contractApi.downloadContractFile(contract.id, file.id);
      const url = URL.createObjectURL(blob);

      if (mode === 'view') {
        window.open(url, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        return;
      }

      const link = document.createElement('a');
      link.href = url;
      link.download = `${contract.contractNumber}-${file.fileVariant.toLowerCase()}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Không thể tải file hợp đồng.');
    } finally {
      setIsFileActionLoading(false);
    }
  };

  const handlePayInvoice = async (invoice: Invoice) => {
    setInvoiceActionBusy(invoice.id);
    setInvoiceError(null);
    setInvoiceMessage(null);
    try {
      const response = await billingApi.payInvoice(invoice.id);
      setContractInvoices((current) => current.map((item) => item.id === invoice.id ? response.data : item));
      setInvoiceMessage(`Đã thanh toán hóa đơn ${response.data.invoiceNo}.`);
    } catch (err) {
      setInvoiceError(getApiErrorMessage(err, 'Thanh toán hóa đơn thất bại.'));
    } finally {
      setInvoiceActionBusy('');
    }
  };

  return (
    <div className="history-detail-page">
      <div className="invoice-overview-band">
        <div className="overview-header-title-area">
          <button
            type="button"
            className="back-icon-btn"
            onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY)}
            title="Quay về danh sách lịch sử thuê"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="19" y1="12" x2="5" y2="12" />
              <polyline points="12 19 5 12 12 5" />
            </svg>
          </button>
          <div className="overview-left">
            <p className="eyebrow">{contract.roomingHouseName}</p>
            <h2>Phòng {contract.roomNumber}</h2>
            <p className="overview-description">
              Vai trò của bạn: <strong>{formatRelation(contract)}</strong>
            </p>
            {contract.snapshotAtDate && (
              <p className="overview-description">
                Dữ liệu hiển thị tại thời điểm: <strong>{formatDate(contract.snapshotAtDate)}</strong>
              </p>
            )}
          </div>
        </div>

        <div className="overview-right">
          <span className={`status-badge ${getStatusClass(contract.status)}`} style={{ fontSize: '1rem', padding: '8px 16px' }}>
            {formatStatus(contract.status)}
          </span>
        </div>
      </div>

      <div className="history-detail-tabs">
        <button className={`history-detail-tab ${activeTab === 'contract' ? 'active' : ''}`} onClick={() => setActiveTab('contract')}>
          Thông tin hợp đồng
        </button>
        <button className={`history-detail-tab ${activeTab === 'appendices' ? 'active' : ''}`} onClick={() => setActiveTab('appendices')}>
          Phụ lục hợp đồng
        </button>
        <button className={`history-detail-tab ${activeTab === 'occupants' ? 'active' : ''}`} onClick={() => setActiveTab('occupants')}>
          Thông tin người ở
        </button>
        <button className={`history-detail-tab ${activeTab === 'invoices' ? 'active' : ''}`} onClick={() => setActiveTab('invoices')}>
          Hóa đơn hằng tháng
        </button>
        <button className={`history-detail-tab ${activeTab === 'issues' ? 'active' : ''}`} onClick={() => setActiveTab('issues')}>
          Thông tin sự cố
        </button>
      </div>

      <div className="history-detail-content">
        {activeTab === 'occupants' && (
          <div>
            <div className="occupants-filter">
              <button
                className={`occupant-filter-btn ${occupantFilter === 'all' ? 'active' : ''}`}
                onClick={() => setOccupantFilter('all')}
              >
                Tất cả ({occupants.length})
              </button>
              <button
                className={`occupant-filter-btn ${occupantFilter === 'active' ? 'active' : ''}`}
                onClick={() => setOccupantFilter('active')}
              >
                Đang ở ({occupants.filter((occupant) => occupant.status === 'Active').length})
              </button>
              <button
                className={`occupant-filter-btn ${occupantFilter === 'left' ? 'active' : ''}`}
                onClick={() => setOccupantFilter('left')}
              >
                Đã rời đi ({occupants.filter((occupant) => occupant.status !== 'Active' || occupant.moveOutDate).length})
              </button>
            </div>

            <div className="occupant-list">
              {filteredOccupants.map((occupant) => (
                <div key={occupant.id} className="occupant-item">
                  <div className="occupant-info">
                    <h4>
                      {occupant.id === contract.currentUserOccupantId ? 'Bạn' : occupant.fullName}
                      {' - '}
                      {formatOccupantRole(occupant.userId === contract.mainTenantUserId)}
                      {occupant.id === contract.currentUserOccupantId && <span className="occupant-me-badge">Tôi</span>}
                    </h4>
                    <div className="occupant-role">Email: {formatOptionalContact(occupant.email)}</div>
                    <div className="occupant-role">Số điện thoại: {formatOptionalContact(occupant.phoneNumber)}</div>
                  </div>
                  <div className="occupant-dates">
                    <div style={{ marginBottom: '6px' }}>
                      <span className={`status-badge ${occupant.status === 'Active' ? 'active' : 'terminated'}`} style={{ padding: '2px 8px', fontSize: '0.75rem' }}>
                        {occupant.status === 'Active' ? 'Đang ở' : 'Đã rời đi'}
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
        )}

        {activeTab === 'contract' && (
          <div>
            <div className="contract-info-grid">
              <div className="contract-info-block">
                <h3>Thông tin cơ bản</h3>
                <div className="contract-info-item">
                  <span className="label">Người thuê chính:</span>
                  <span className="value">{contract.mainTenantName}</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Ngày bắt đầu:</span>
                  <span className="value">{formatDate(contract.startDate)}</span>
                </div>
                <div className="contract-info-item">
                  <span className="label">Ngày kết thúc:</span>
                  <span className="value">{formatDate(contract.endDate)}</span>
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
              {contract.canViewRawContract && (
                <>
                  <Button variant="outline" onClick={handleDownloadContract} disabled={isFileActionLoading}>Tải hợp đồng</Button>
                  <Button variant="outline" onClick={handleViewContract} disabled={isFileActionLoading}>Xem hợp đồng</Button>
                </>
              )}

              {!contract.canViewRawContract && contract.canViewMaskedContract && (
                <Button variant="outline" onClick={handleViewContract} disabled={isFileActionLoading}>
                  Xem hợp đồng (bản che thông tin)
                </Button>
              )}

              {contract.canTerminateContract && (
                <Button variant="danger" onClick={() => setIsTerminateModalOpen(true)}>Chấm dứt hợp đồng</Button>
              )}
            </div>

            {actionError && (
              <div style={{ color: '#b91c1c', marginTop: '12px' }}>
                {actionError}
              </div>
            )}
          </div>
        )}

        {activeTab === 'appendices' && (
          <div>
            <div className="appendices-header">
              <h3>Danh sách phụ lục hợp đồng</h3>
              {contract.canCreateAppendix && (
                  <Button 
                    style={{ whiteSpace: 'nowrap' }}
                    onClick={() => setIsAppendixModalOpen(true)}
                    disabled={appendices?.some(isBlockingAppendix)}
                    title={appendices?.some(isBlockingAppendix) ? 'Không thể tạo phụ lục mới khi đang có phụ lục chờ ký hoặc đang yêu cầu sửa.' : ''}
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
                      {(appendix.status === 'LandlordRevisionRequested' || canTenantOpenAppendixForSigning(appendix)) && (
                        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '8px' }}>
                          {appendix.status === 'LandlordRevisionRequested' && (
                            <Button
                              variant="outline"
                              style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                              onClick={() => setEditingAppendix(appendix)}
                            >
                              Sửa phụ lục
                            </Button>
                          )}
                          <Button 
                            style={{ padding: '6px 12px', fontSize: '0.85rem' }} 
                            onClick={() => setSigningAppendixId(appendix.id)}
                            disabled={!canTenantOpenAppendixForSigning(appendix)}
                            title={!canTenantOpenAppendixForSigning(appendix) ? 'Vui lòng sửa và lưu phụ lục trước khi ký.' : ''}
                          >
                            Xem và ký phụ lục
                          </Button>
                        </div>
                      )}
                      <AppendixFileActions
                        contractId={contract.id}
                        contractNumber={contract.contractNumber}
                        appendix={appendix}
                        file={findAccessibleAppendixFile(accessibleContractFiles, appendix.id)}
                      />
                    </div>
                    <div className="appendix-status-badge" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '8px' }}>
                      <span className={`status-badge ${getAppendixStatusClass(appendix.status)}`} style={{ padding: '6px 16px', fontSize: '0.85rem' }}>
                        {formatAppendixStatus(appendix, 'Tenant')}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {activeTab === 'invoices' && (
          <div className="contract-invoices-section">
            <div className="contract-invoices-header">
              <div>
                <h2>Hóa đơn hằng tháng</h2>
                <p>Danh sách hóa đơn thuộc hợp đồng này trong thời gian bạn ở phòng.</p>
              </div>
            </div>

            <div className="invoice-status-tabs">
              {invoiceStatusTabs.map((status) => (
                <button
                  key={status || 'all'}
                  type="button"
                  className={invoiceStatusFilter === status ? 'active' : ''}
                  onClick={() => setInvoiceStatusFilter(status)}
                >
                  {status ? getInvoiceStatusLabel(status) : 'Tất cả'}
                </button>
              ))}
            </div>

            {invoiceMessage && <div className="invoice-inline-message success">{invoiceMessage}</div>}
            {invoiceError && <div className="invoice-inline-message error">{invoiceError}</div>}

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
                {contractInvoices.map((invoice) => {
                  const canPayNow = invoice.status === 'Issued' && invoice.tenantUserId === currentUser?.userId;

                  return (
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
                        {canPayNow && (
                          <Button
                            onClick={() => void handlePayInvoice(invoice)}
                            disabled={invoiceActionBusy === invoice.id}
                          >
                            {invoiceActionBusy === invoice.id ? 'Đang thanh toán...' : 'Thanh toán ngay'}
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {activeTab === 'issues' && (
          <div className="coming-soon-placeholder">
            <h2>Thông tin sự cố</h2>
            <p>Tính năng đang được phát triển.</p>
          </div>
        )}
      </div>

      {isAppendixModalOpen && (
        <CreateAppendixModal
          contract={contract}
          onClose={() => setIsAppendixModalOpen(false)}
          onCreated={() => {
            void loadContract();
          }}
        />
      )}

      {editingAppendix && (
        <CreateAppendixModal
          contract={contract}
          appendix={editingAppendix}
          onClose={() => setEditingAppendix(null)}
          onCreated={() => {
            setEditingAppendix(null);
            void loadContract();
          }}
        />
      )}

      {isTerminateModalOpen && (
        <TerminateContractModal
          contract={contract}
          onClose={() => setIsTerminateModalOpen(false)}
          onTerminated={(updatedContract) => {
            setContract((current) => current ? {
              ...current,
              status: updatedContract.status,
              statusReason: updatedContract.statusReason,
              terminationDate: updatedContract.terminationDate,
              terminationType: updatedContract.terminationType,
              isAwaitingFinalInvoice: updatedContract.isAwaitingFinalInvoice
            } : current);
            setIsTerminateModalOpen(false);
          }}
        />
      )}

      {contract && signingAppendixId && (
        <AppendixPreviewModal
          contractId={contract.id}
          appendixId={signingAppendixId}
          isCreator={appendices?.find(a => a.id === signingAppendixId)?.createdByUserId === currentUser?.userId}
          hasNoSignatures={appendices?.find(a => a.id === signingAppendixId)?.signatures?.length === 0}
          onClose={() => setSigningAppendixId(null)}
          onSuccess={() => {
            setSigningAppendixId(null);
            void loadContract();
          }}
        />
      )}
    </div>
  );
};

async function resolveVisibleContractFile(contract: ContractHistoryItemResponse): Promise<ContractFileResponse> {
  const preferredVariant = contract.canViewRawContract ? 'Raw' : 'Masked';
  const response = await contractApi.getContractFiles(contract.id);
  let file = findContractFile(response.data ?? [], preferredVariant);

  if (!file && contract.canCreateAppendix) {
    await contractApi.generateContractFile(contract.id);
    const refreshedResponse = await contractApi.getContractFiles(contract.id);
    file = findContractFile(refreshedResponse.data ?? [], preferredVariant);
  }

  if (!file) {
    throw new Error('Chưa có file hợp đồng phù hợp với quyền xem của bạn.');
  }

  return file;
}

function findContractFile(files: ContractFileResponse[], fileVariant: string) {
  return files
    .filter((file) => !file.rentalContractAppendixId && file.fileVariant === fileVariant)
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
}

function formatOccupantRole(isMainTenant: boolean) {
  return isMainTenant ? 'Người thuê chính' : 'Người ở cùng';
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
    case 'TenantRevisionRequested': return 'completed';
    case 'Rejected':
    case 'Cancelled': return 'terminated';
    default: return '';
  }
}

function formatRelation(contract: ContractHistoryItemResponse) {
  switch (contract.currentUserRelation) {
    case 'CurrentMainTenant':
      return 'Người thuê chính';
    case 'FormerMainTenant':
      return 'Người thuê chính cũ';
    case 'CoTenant':
      return 'Người ở cùng';
    case 'FormerCoTenant':
    case 'FormerOccupant':
      return 'Người ở cùng đã rời đi';
    default:
      return contract.currentUserMoveOutDate ? 'Đã rời đi' : 'Đã từng tham gia';
  }
}

function formatDate(value?: string | null) {
  if (!value) {
    return '-';
  }

  return new Date(value).toLocaleDateString('vi-VN');
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('vi-VN').format(value);
}
