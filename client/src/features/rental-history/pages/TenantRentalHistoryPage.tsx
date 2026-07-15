import React, { useEffect, useMemo, useState } from 'react';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { Card, CardMetaRow, type CardStatusTone } from '../../../shared/components/ui/Card';
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

function getContractStatusTone(status: string): CardStatusTone {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Cancelled':
      return 'danger';
    case 'Expired':
      return 'neutral';
    default:
      return 'neutral';
  }
}

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
      <PageHeader
        icon={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"></circle>
              <polyline points="12 6 12 12 16 14"></polyline>
            </svg>
          </div>
        }
        eyebrow="QUẢN LÝ"
        title="Lịch sử thuê trọ"
        description="Danh sách các hợp đồng thuê đã có hiệu lực mà bạn từng tham gia"
      />

      <div className="history-content-wrapper">
        <Tabs
          className="attached-bottom"
          variant="segmented-secondary"
          activeId={statusFilter}
          onChange={(filter) => setStatusFilter(filter as HistoryStatusFilter)}
          items={statusFilters.map((filter) => ({
            id: filter.value,
            label: filter.label,
            icon: getFilterIcon(filter.value),
          }))}
        />

        <section className="tab-attached-panel tab-attached-panel--cards">
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

          {!isLoading && !error && filteredContracts.map((contract) => (
            <Card
              key={contract.id}
              title={`Phòng ${contract.roomNumber} - ${contract.roomingHouseName}`}
              status={formatContractStatus(contract.status)}
              statusTone={getContractStatusTone(contract.status)}
              bodyColumns={2}
              actionItems={[
                {
                  label: 'Xem chi tiết',
                  onClick: () => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY_DETAIL(contract.id)),
                  icon: (
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                  ),
                },
              ]}
            >
              <CardMetaRow
                label="Vai trò:"
                value={formatRelation(contract)}
                icon={
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                    <circle cx="12" cy="7" r="4" />
                  </svg>
                }
              />
              <CardMetaRow
                label="Thời hạn thuê:"
                value={`${formatDate(contract.startDate)} - ${formatDate(contract.endDate)}`}
                icon={
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                    <line x1="16" y1="2" x2="16" y2="6" />
                    <line x1="8" y1="2" x2="8" y2="6" />
                    <line x1="3" y1="10" x2="21" y2="10" />
                  </svg>
                }
              />
              {contract.snapshotAtDate && (
                <CardMetaRow
                  label="Dữ liệu tại:"
                  value={formatDate(contract.snapshotAtDate)}
                  icon={
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="12" r="10" />
                      <polyline points="12 6 12 12 16 14" />
                    </svg>
                  }
                />
              )}
              <CardMetaRow
                label="Tiền thuê:"
                value={`${formatCurrency(contract.monthlyRent)} đ/tháng`}
                valueClassName="tenant-history-price"
                icon={
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="12" y1="1" x2="12" y2="23" />
                    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                  </svg>
                }
              />
            </Card>
          ))}

          {!isLoading && !error && filteredContracts.length === 0 && (
            <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#64748b' }}>
              Không có hợp đồng nào phù hợp với bộ lọc.
            </div>
          )}
        </div>
      </section>
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

function formatContractStatus(status: string) {
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
