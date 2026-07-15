import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { contractApi } from '../api';
import type { ContractHistoryItemResponse } from '../types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Tabs } from '../../../shared/components/ui/Tabs';
import { PageHeader } from '../../../shared/components/ui/PageHeader';
import { Card, CardMetaRow, type CardStatusTone } from '../../../shared/components/ui/Card';
import { formatDateVi, formatMoneyString } from '../../../shared/utils/format';
import { formatStatus } from '../../../shared/utils/status';
import '../../landlord/pages/LandlordDashboardPage.css';
import './LandlordContractsPage.css';

function getContractStatusTabIcon(status: string) {
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

  switch (status) {
    case 'Active':
      return (
        <svg {...props}>
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          <path d="m9 12 2 2 4-4" />
        </svg>
      );
    case 'Expired':
      return (
        <svg {...props}>
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      );
    case 'Cancelled':
      return (
        <svg {...props}>
          <circle cx="12" cy="12" r="10" />
          <line x1="4.93" y1="4.93" x2="19.07" y2="19.07" />
        </svg>
      );
    default:
      return (
        <svg {...props}>
          <line x1="8" y1="6" x2="21" y2="6" />
          <line x1="8" y1="12" x2="21" y2="12" />
          <line x1="8" y1="18" x2="21" y2="18" />
          <line x1="3" y1="6" x2="3.01" y2="6" />
          <line x1="3" y1="12" x2="3.01" y2="12" />
          <line x1="3" y1="18" x2="3.01" y2="18" />
        </svg>
      );
  }
}

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

export default function LandlordContractsPage() {
  const navigate = useNavigate();
  const [contracts, setContracts] = useState<ContractHistoryItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [selectedHouseId, setSelectedHouseId] = useState<string>('');
  const [selectedRoomId, setSelectedRoomId] = useState<string>('');
  const [selectedStatus, setSelectedStatus] = useState<string>('all');

  useEffect(() => {
    async function loadData() {
      setLoading(true);
      setMessage('');
      try {
        const res = await contractApi.getLandlordContracts();
        if (res.success && res.data) {
          setContracts(res.data);
        } else {
          setMessage(res.message || 'Lỗi tải danh sách hợp đồng.');
        }
      } catch (err) {
        setMessage(getApiErrorMessage(err, 'Không thể tải danh sách hợp đồng.'));
      } finally {
        setLoading(false);
      }
    }

    void loadData();
  }, []);

  const houses = useMemo(() => {
    const map = new Map<string, string>();
    contracts.forEach((contract) => {
      map.set(contract.roomingHouseId, contract.roomingHouseName);
    });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [contracts]);

  const roomsForSelectedHouse = useMemo(() => {
    if (!selectedHouseId) return [];
    const map = new Map<string, string>();
    contracts
      .filter((contract) => contract.roomingHouseId === selectedHouseId)
      .forEach((contract) => {
        map.set(contract.roomId, contract.roomNumber);
      });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [contracts, selectedHouseId]);

  useEffect(() => {
    setSelectedRoomId('');
  }, [selectedHouseId]);

  const filteredContracts = useMemo(() => {
    return contracts.filter((contract) => {
      if (!['Active', 'Expired', 'Cancelled'].includes(contract.status)) return false;
      if (selectedStatus !== 'all' && contract.status !== selectedStatus) return false;
      if (selectedHouseId && contract.roomingHouseId !== selectedHouseId) return false;
      if (selectedRoomId && contract.roomId !== selectedRoomId) return false;
      return true;
    });
  }, [contracts, selectedStatus, selectedHouseId, selectedRoomId]);

  if (loading) {
    return (
      <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
        <main className="dashboard-main">
          <div className="empty-panel">Đang tải dữ liệu hợp đồng...</div>
        </main>
      </div>
    );
  }

  return (
    <div className="landlord-dashboard-page" style={{ display: 'contents' }}>
      <main className="dashboard-main">
        <PageHeader
          icon={
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
            </svg>
          }
          eyebrow="Quản lý"
          title="Hợp đồng cho thuê"
          description="Xem danh sách các hợp đồng đang có hiệu lực, đã hết hạn hoặc đã hủy."
          rightContent={
            <div className="overview-right landlord-contract-filters">
              <div className="landlord-contract-filter-group">
                <div>
                  <label className="landlord-contract-filter-label">Khu trọ</label>
                  <select
                    value={selectedHouseId}
                    onChange={(event) => setSelectedHouseId(event.target.value)}
                    className="landlord-contract-filter-select"
                  >
                    <option value="">Tất cả khu trọ</option>
                    {houses.map((house) => (
                      <option key={house.id} value={house.id}>{house.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="landlord-contract-filter-label">Phòng</label>
                  <select
                    value={selectedRoomId}
                    onChange={(event) => setSelectedRoomId(event.target.value)}
                    disabled={!selectedHouseId}
                    className="landlord-contract-filter-select"
                  >
                    <option value="">Tất cả phòng</option>
                    {roomsForSelectedHouse.map((room) => (
                      <option key={room.id} value={room.id}>{room.name}</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          }
        />

        {message && <p className="dashboard-message">{message}</p>}

        <Tabs
          className="attached-bottom"
          variant="segmented-secondary"
          activeId={selectedStatus}
          onChange={setSelectedStatus}
          items={[
            { id: 'all', label: 'Tất cả', icon: getContractStatusTabIcon('all') },
            { id: 'Active', label: formatStatus('Active'), icon: getContractStatusTabIcon('Active') },
            { id: 'Expired', label: formatStatus('Expired'), icon: getContractStatusTabIcon('Expired') },
            { id: 'Cancelled', label: formatStatus('Cancelled'), icon: getContractStatusTabIcon('Cancelled') },
          ]}
        />

        <section className="tab-attached-panel tab-attached-panel--cards">
          {filteredContracts.length === 0 ? (
            <div className="empty-panel">
              <h2>Không tìm thấy hợp đồng</h2>
              <p>Chưa có hợp đồng nào phù hợp với bộ lọc hiện tại.</p>
            </div>
          ) : (
            <div className="landlord-contract-list">
              {filteredContracts.map((contract) => (
                <Card
                  key={contract.id}
                  title={`Phòng ${contract.roomNumber} - ${contract.roomingHouseName}`}
                  status={formatStatus(contract.status)}
                  statusTone={getContractStatusTone(contract.status)}
                  bodyColumns={2}
                  actionItems={[
                    {
                      label: 'Xem chi tiết',
                      onClick: () => navigate(ROUTE_PATHS.LANDLORD.CONTRACT_DETAIL(contract.id)),
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
                    label="Mã HĐ:"
                    value={contract.contractNumber}
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                        <polyline points="14 2 14 8 20 8" />
                      </svg>
                    }
                  />
                  <CardMetaRow
                    label="Đại diện thuê:"
                    value={contract.mainTenantName}
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                        <circle cx="12" cy="7" r="4" />
                      </svg>
                    }
                  />
                  <CardMetaRow
                    label="Tiền thuê/tháng:"
                    value={`${formatMoneyString(contract.monthlyRent)} đ`}
                    valueClassName="landlord-contract-price"
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <line x1="12" y1="1" x2="12" y2="23" />
                        <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                      </svg>
                    }
                  />
                  <CardMetaRow
                    label="Kỳ hạn:"
                    value={`${formatDateVi(contract.startDate)} - ${formatDateVi(contract.endDate)}`}
                    icon={
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                        <line x1="16" y1="2" x2="16" y2="6" />
                        <line x1="8" y1="2" x2="8" y2="6" />
                        <line x1="3" y1="10" x2="21" y2="10" />
                      </svg>
                    }
                  />

                  {contract.isAwaitingFinalInvoice && (
                    <div className="landlord-contract-notice">
                      Chờ hóa đơn kỳ cuối
                    </div>
                  )}
                </Card>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
