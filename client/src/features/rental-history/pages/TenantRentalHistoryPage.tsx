import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../../../shared/components/ui/Button';
import { contractApi } from '../../contracts/api';
import type { ContractHistoryItemResponse } from '../../contracts/types';
import './TenantRentalHistoryPage.css';

type HistoryStatusFilter = 'All' | 'Active' | 'Expired' | 'Cancelled';

const statusFilters: Array<{ value: HistoryStatusFilter; label: string }> = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Active', label: 'Đang hiệu lực' },
  { value: 'Expired', label: 'Đã hết hạn' },
  { value: 'Cancelled', label: 'Đã chấm dứt' }
];

export const TenantRentalHistoryPage: React.FC = () => {
  const navigate = useNavigate();
  const [contracts, setContracts] = useState<ContractHistoryItemResponse[]>([]);
  const [statusFilter, setStatusFilter] = useState<HistoryStatusFilter>('All');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    async function loadContracts() {
      try {
        setIsLoading(true);
        setError(null);
        const response = await contractApi.getMyHistory();
        if (isMounted) {
          setContracts(response.data ?? []);
        }
      } catch {
        if (isMounted) {
          setError('Không thể tải lịch sử thuê. Vui lòng thử lại sau.');
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void loadContracts();

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredContracts = useMemo(() => {
    if (statusFilter === 'All') {
      return contracts;
    }

    return contracts.filter((contract) => contract.status === statusFilter);
  }, [contracts, statusFilter]);

  return (
    <div className="tenant-history-page">
      <div className="invoice-overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
          <h2>Lịch sử thuê trọ</h2>
          <p className="overview-description">Danh sách các hợp đồng thuê đã có hiệu lực mà bạn từng tham gia</p>
        </div>
      </div>

      <div className="history-filter-container">
        {statusFilters.map((filter) => (
          <button
            key={filter.value}
            className={`history-filter-btn ${statusFilter === filter.value ? 'active' : ''}`}
            onClick={() => setStatusFilter(filter.value)}
            type="button"
          >
            {getFilterIcon(filter.value)}
            <span>{filter.label}</span>
          </button>
        ))}
      </div>

      <div className="history-grid">
        {isLoading && (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#64748b' }}>
            Đang tải dữ liệu...
          </div>
        )}

        {!isLoading && error && (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#b91c1c' }}>
            {error}
          </div>
        )}

        {!isLoading && !error && filteredContracts.map((contract) => {
          const statusClass = contract.status.toLowerCase();
          return (
            <div key={contract.id} className={`history-card status-${statusClass}`}>
              <div className="history-card-header">
                <div className="history-card-title">
                  <div className="history-house-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
                      <polyline points="9 22 9 12 15 12 15 22"></polyline>
                    </svg>
                  </div>
                  <div className="history-card-title-text">
                    <h3>Phòng {contract.roomNumber} - {contract.roomingHouseName}</h3>
                  </div>
                </div>
                <span className={`status-badge ${getStatusClass(contract.status)}`}>
                  {formatStatus(contract.status)}
                </span>
              </div>

              <div className="history-card-body">
                <div className="history-role-badge">
                  <svg className="history-role-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                    <circle cx="12" cy="7" r="4"></circle>
                  </svg>
                  <span>{formatRelation(contract)}</span>
                </div>
                
                <div className="history-info-block">
                  <div className="history-info-item">
                    <svg className="history-info-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                      <line x1="16" y1="2" x2="16" y2="6"></line>
                      <line x1="8" y1="2" x2="8" y2="6"></line>
                      <line x1="3" y1="10" x2="21" y2="10"></line>
                    </svg>
                    <span>Thời hạn thuê: <strong>{formatDate(contract.startDate)} - {formatDate(contract.endDate)}</strong></span>
                  </div>
                  
                  {contract.snapshotAtDate && (
                    <div className="history-info-item">
                      <svg className="history-info-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"></circle>
                        <polyline points="12 6 12 12 16 14"></polyline>
                      </svg>
                      <span>Dữ liệu tại: <strong>{formatDate(contract.snapshotAtDate)}</strong></span>
                    </div>
                  )}
                  
                  <div className="history-info-item">
                    <svg className="history-info-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="12" y1="1" x2="12" y2="23"></line>
                      <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
                    </svg>
                    <span>Tiền thuê: <strong>{formatCurrency(contract.monthlyRent)} đ / tháng</strong></span>
                  </div>
                </div>
              </div>

              <div className="history-card-footer">
                <Button onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY_DETAIL(contract.id))}>
                  <svg className="button-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                    <circle cx="12" cy="12" r="3"></circle>
                  </svg>
                  <span>Xem chi tiết</span>
                </Button>
              </div>
            </div>
          );
        })}

        {!isLoading && !error && filteredContracts.length === 0 && (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#64748b' }}>
            Không có hợp đồng nào phù hợp với bộ lọc.
          </div>
        )}
      </div>
    </div>
  );
};

function getFilterIcon(filter: HistoryStatusFilter) {
  switch (filter) {
    case 'All':
      return (
        <svg className="history-filter-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="3" width="7" height="7"></rect>
          <rect x="14" y="3" width="7" height="7"></rect>
          <rect x="14" y="14" width="7" height="7"></rect>
          <rect x="3" y="14" width="7" height="7"></rect>
        </svg>
      );
    case 'Active':
      return (
        <svg className="history-filter-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
        </svg>
      );
    case 'Expired':
      return (
        <svg className="history-filter-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
          <line x1="16" y1="2" x2="16" y2="6"></line>
          <line x1="8" y1="2" x2="8" y2="6"></line>
          <line x1="3" y1="10" x2="21" y2="10"></line>
        </svg>
      );
    case 'Cancelled':
      return (
        <svg className="history-filter-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line>
        </svg>
      );
  }
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
