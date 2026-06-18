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
      <section className="overview-band">
        <div className="overview-left">
          <p className="eyebrow">QUẢN LÝ</p>
          <h2>Lịch sử thuê trọ</h2>
          <p className="overview-description">Danh sách các hợp đồng thuê đã có hiệu lực mà bạn từng tham gia</p>
        </div>
      </section>

      <div className="history-filter-container">
        {statusFilters.map((filter) => (
          <button
            key={filter.value}
            className={`history-filter-btn ${statusFilter === filter.value ? 'active' : ''}`}
            onClick={() => setStatusFilter(filter.value)}
            type="button"
          >
            {filter.label}
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

        {!isLoading && !error && filteredContracts.map((contract) => (
          <div key={contract.id} className="history-card">
            <div className="history-card-header">
              <div className="history-card-title">
                <h3>Phòng {contract.roomNumber} - {contract.roomingHouseName}</h3>
              </div>
              <span className={`status-badge ${getStatusClass(contract.status)}`}>
                {formatStatus(contract.status)}
              </span>
            </div>

            <div className="history-card-body">
              <div className="history-role-badge">
                {formatRelation(contract)}
              </div>
              <div className="history-dates" style={{ marginTop: '12px', fontSize: '0.9rem', color: '#475569' }}>
                <p style={{ margin: '4px 0' }}>
                  <strong>Thời hạn:</strong> {formatDate(contract.startDate)} - {formatDate(contract.endDate)}
                </p>
                {contract.snapshotAtDate && (
                  <p style={{ margin: '4px 0' }}>
                    <strong>Dữ liệu tại:</strong> {formatDate(contract.snapshotAtDate)}
                  </p>
                )}
                <p style={{ margin: '4px 0' }}>
                  <strong>Tiền thuê:</strong> {formatCurrency(contract.monthlyRent)} / tháng
                </p>
              </div>
            </div>

            <div className="history-card-footer">
              <Button onClick={() => navigate(ROUTE_PATHS.ACCOUNT.RENTAL_HISTORY_DETAIL(contract.id))}>
                Xem chi tiết
              </Button>
            </div>
          </div>
        ))}

        {!isLoading && !error && filteredContracts.length === 0 && (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', padding: '40px', color: '#64748b' }}>
            Không có hợp đồng nào phù hợp với bộ lọc.
          </div>
        )}
      </div>
    </div>
  );
};

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
