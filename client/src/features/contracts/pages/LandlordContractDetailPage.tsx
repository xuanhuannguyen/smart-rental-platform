import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Button } from '../../../shared/components/ui/Button';
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
import { AppendixFileActions } from '../components/AppendixFileActions';
import { openContractFileForView } from '../fileAccess';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { formatDateVi, formatMoneyString } from '../../../shared/utils/format';
import { formatStatus, getStatusToneClass } from '../../../shared/utils/status';
import { useAuth } from '../../../app/providers/AuthProvider';
import { TerminateContractModal } from '../../rental-history/pages/TerminateContractModal';
import { LandlordCreateAppendixModalV2 } from '../components/LandlordCreateAppendixModalV2';
import { CreateTerminationInvoiceModal } from '../components/CreateTerminationInvoiceModal';
import { AppendixPreviewModal } from '../../rental-history/components/AppendixPreviewModal';
import '../../landlord/pages/LandlordDashboardPage.css';
import '../../rental-history/pages/TenantRentalHistoryDetailPage.css';

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

  useEffect(() => {
    if (id) {
      loadContractDetail(id);
      loadAppendices(id);
    }
  }, [id]);

  async function loadContractDetail(contractId: string) {
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
  }

  async function loadAppendices(contractId: string) {
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
  }

  async function resolveRawContractFile(contractId: string): Promise<ContractFileResponse> {
    const filesResponse = await contractApi.getContractFiles(contractId);
    let files = filesResponse.data;
    if (files.length === 0) {
      await contractApi.generateContractFile(contractId);
      const updatedFilesResponse = await contractApi.getContractFiles(contractId);
      files = updatedFilesResponse.data;
    }
    const rawFile = files.find((f) => f.fileVariant === 'Raw');
    if (rawFile) return rawFile;

    const maskedFile = files.find((f) => f.fileVariant === 'Masked');
    if (maskedFile) return maskedFile;

    throw new Error('Chưa có file hợp đồng nào được tạo.');
  }

  async function openContractFile(mode: 'view' | 'download') {
    if (!contract) return;
    setIsFileActionLoading(true);
    setContractActionError(null);

    try {
      const file = await resolveRawContractFile(contract.id);
      if (mode === 'view') {
        await openContractFileForView(contract.id, file);
        return;
      }

      const blob = await contractApi.downloadContractFile(contract.id, file.id);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${contract.contractNumber}-${file.fileVariant.toLowerCase()}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      setTimeout(() => window.URL.revokeObjectURL(url), 1000);
    } catch (err) {
      setContractActionError(getApiErrorMessage(err, 'Không thể tải file hợp đồng.'));
    } finally {
      setIsFileActionLoading(false);
    }
  }

  function getAppendixStatusClass(status: ContractAppendixStatus) {
    switch (status) {
      case 'Active': return 'status-badge--success';
      case 'PendingSignature': return 'status-badge--info';
      case 'TenantRevisionRequested':
      case 'LandlordRevisionRequested': return 'status-badge--warning';
      case 'Rejected':
      case 'Cancelled': return 'status-badge--danger';
      default: return 'status-badge--neutral';
    }
  }

  const visibleAppendices = appendices && currentUser
    ? appendices.filter((appendix) => shouldShowAppendixToCurrentUser(appendix, currentUser.userId))
    : null;
  const hasBlockingAppendix = appendices?.some(isBlockingAppendix) ?? false;

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        <section className="overview-band" style={{ marginBottom: '20px' }}>
          <div className="overview-header-title-area" style={{ display: 'flex', alignItems: 'flex-start', gap: '16px' }}>
            <button
              type="button"
              className="back-icon-btn"
              onClick={() => navigate(ROUTE_PATHS.LANDLORD.CONTRACTS)}
              title="Quay về danh sách hợp đồng"
              style={{ marginTop: '4px' }}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="19" y1="12" x2="5" y2="12" />
                <polyline points="12 19 5 12 12 5" />
              </svg>
            </button>
            <div className="overview-left">
              <p className="eyebrow">Thông tin</p>
              <h2 style={{ display: 'flex', alignItems: 'center', gap: '12px', margin: 0 }}>
                Chi tiết hợp đồng
              </h2>
              {contract && (
                <p className="overview-description" style={{ marginTop: '4px' }}>
                  Mã HĐ: {contract.contractNumber}
                </p>
              )}
            </div>
          </div>

          <div className="overview-right">
            {contract && (
              <div className="overview-stats" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '12px' }}>
                <span className={`status-pill ${getStatusToneClass(contract.status)}`} style={{ padding: '6px 16px', fontSize: '14px', fontWeight: 'bold', border: '1px solid currentColor' }}>
                  {formatStatus(contract.status)}
                </span>
                
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', justifyContent: 'flex-end' }}>
                  <Button variant="outline" onClick={() => openContractFile('download')} disabled={isFileActionLoading}>
                    Tải PDF
                  </Button>
                  <Button variant="outline" onClick={() => openContractFile('view')} disabled={isFileActionLoading}>
                    Xem hợp đồng
                  </Button>
                  
                  {contract.isAwaitingFinalInvoice && (
                    <Button onClick={() => setIsTerminationInvoiceModalOpen(true)}>
                      Tạo hóa đơn kỳ cuối
                    </Button>
                  )}

                  {contract.status === 'Active' && (
                    <Button variant="danger" onClick={() => setIsTerminateModalOpen(true)}>
                      Chấm dứt hợp đồng
                    </Button>
                  )}
                </div>
                {contractActionError && (
                  <div style={{ color: '#b91c1c', fontSize: '0.875rem' }}>
                    {contractActionError}
                  </div>
                )}
              </div>
            )}
          </div>
        </section>

        {loading ? (
          <div className="empty-panel">Đang tải thông tin hợp đồng...</div>
        ) : error || !contract ? (
          <div className="empty-panel" style={{ color: '#ef4444' }}>
            {error || 'Không tìm thấy hợp đồng.'}
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
            {/* Thông tin chung */}
            <div className="dashboard-card" style={{ padding: '24px', cursor: 'default' }}>
              <h2 style={{ margin: '0 0 16px 0', fontSize: '1.25rem' }}>Thông tin chung</h2>
              
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', fontSize: '0.9375rem' }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Khu trọ / Phòng</span>
                  <strong>{contract.roomingHouseName} - Phòng {contract.roomNumber}</strong>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Đại diện thuê</span>
                  <strong>{contract.mainTenantName}</strong>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Kỳ hạn hợp đồng</span>
                  <strong>{formatDateVi(contract.startDate)} - {formatDateVi(contract.endDate)}</strong>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Ngày chấm dứt</span>
                  <strong>
                    {contract.status === 'Cancelled' || contract.status === 'Expired'
                      ? contract.terminationDate
                        ? formatDateVi(contract.terminationDate)
                        : 'Chưa xác định'
                      : 'Chưa xác định'}
                  </strong>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Tiền thuê hàng tháng</span>
                  <strong style={{ color: '#10b981' }}>{formatMoneyString(contract.monthlyRent)} đ</strong>
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                  <span style={{ color: '#6b7280' }}>Tiền cọc</span>
                  <strong>{formatMoneyString(contract.depositAmount)} đ</strong>
                </div>
              </div>
            </div>

            {/* Phụ lục hợp đồng */}
            <div className="dashboard-card" style={{ padding: '24px', cursor: 'default' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h2 style={{ margin: 0, fontSize: '1.25rem' }}>Danh sách phụ lục</h2>
                {contract.status === 'Active' && (
                  <Button
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
                  <div style={{ padding: '20px', color: '#64748b', textAlign: 'center' }}>Đang tải danh sách phụ lục...</div>
                ) : visibleAppendices.length === 0 ? (
                  <div style={{ textAlign: 'center', padding: '20px', color: '#64748b', background: '#f8fafc', borderRadius: '8px' }}>
                    Hợp đồng này chưa có phụ lục nào.
                  </div>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    {visibleAppendices.map((appendix) => (
                      <div key={appendix.id} className="appendix-item" style={{ border: '1px solid #e2e8f0', padding: '16px', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <div className="appendix-info">
                          <h4 style={{ margin: '0 0 8px 0', fontSize: '1rem' }}>Phụ lục số {appendix.appendixNumber}</h4>
                          <div style={{ color: '#475569', fontSize: '0.875rem' }}>
                            <strong>Ngày hiệu lực:</strong> {formatDateVi(appendix.effectiveDate)}
                          </div>
                          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '12px' }}>
                            {appendix.status === 'TenantRevisionRequested' && contract.status === 'Active' && (
                              <Button variant="outline" onClick={() => setEditingAppendix(appendix)}>
                                Sửa phụ lục
                              </Button>
                            )}
                            {canLandlordOpenAppendixForSigning(appendix) && (
                              <Button variant="outline" onClick={() => setSigningAppendixId(appendix.id)}>
                                Xem chi tiết
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
                          <span className={`status-badge ${getAppendixStatusClass(appendix.status)}`} style={{ padding: '6px 12px', fontSize: '0.85rem' }}>
                            {formatAppendixStatus(appendix, 'Landlord')}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </main>

      {isTerminateModalOpen && contract && (
        <TerminateContractModal
          contract={contract}
          terminationActor="Landlord"
          onClose={() => setIsTerminateModalOpen(false)}
          onTerminated={() => {
            setIsTerminateModalOpen(false);
            loadContractDetail(contract.id);
          }}
        />
      )}

      {isTerminationInvoiceModalOpen && contract && (
        <CreateTerminationInvoiceModal
          contract={contract}
          onClose={() => setIsTerminationInvoiceModalOpen(false)}
          onCreated={() => {
            setIsTerminationInvoiceModalOpen(false);
            loadContractDetail(contract.id);
          }}
        />
      )}

      {isAppendixModalOpen && contract && (
        <LandlordCreateAppendixModalV2
          contract={contract}
          onClose={() => setIsAppendixModalOpen(false)}
          onCreated={() => {
            setIsAppendixModalOpen(false);
            loadAppendices(contract.id);
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
            loadAppendices(contract.id);
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
            loadAppendices(contract.id);
          }}
        />
      )}
    </div>
  );
}
