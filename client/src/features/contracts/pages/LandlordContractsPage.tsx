import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { contractApi } from '../api';
import type { ContractHistoryItemResponse } from '../types';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { formatDateVi, formatMoneyString } from '../../../shared/utils/format';
import { formatStatus, getStatusToneClass } from '../../../shared/utils/status';
import '../../landlord/pages/LandlordDashboardPage.css';

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

  // Compute filters from data
  const houses = useMemo(() => {
    const map = new Map<string, string>();
    contracts.forEach(c => {
      map.set(c.roomingHouseId, c.roomingHouseName);
    });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [contracts]);

  const roomsForSelectedHouse = useMemo(() => {
    if (!selectedHouseId) return [];
    const map = new Map<string, string>();
    contracts
      .filter(c => c.roomingHouseId === selectedHouseId)
      .forEach(c => {
        map.set(c.roomId, c.roomNumber);
      });
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [contracts, selectedHouseId]);

  // Reset room selection when house changes
  useEffect(() => {
    setSelectedRoomId('');
  }, [selectedHouseId]);

  const filteredContracts = useMemo(() => {
    return contracts.filter(c => {
      // Chỉ hiển thị các trạng thái từ active trở đi
      if (!['Active', 'Expired', 'Cancelled'].includes(c.status)) return false;

      if (selectedStatus !== 'all' && c.status !== selectedStatus) return false;
      if (selectedHouseId && c.roomingHouseId !== selectedHouseId) return false;
      if (selectedRoomId && c.roomId !== selectedRoomId) return false;
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
        <section className="overview-band">
          <div className="overview-left">
            <p className="eyebrow">Quản lý</p>
            <h2>Hợp đồng cho thuê</h2>
            <p className="overview-description">Xem danh sách các hợp đồng đang có hiệu lực, đã hết hạn hoặc đã hủy.</p>
          </div>

          <div className="overview-right" style={{ flexDirection: 'column', alignItems: 'flex-end', gap: '0.5rem' }}>
             <div className="filter-group" style={{ display: 'flex', gap: '1rem', background: '#fff', padding: '0.75rem', borderRadius: '0.5rem', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
               <div>
                 <label style={{ display: 'block', fontSize: '0.85rem', color: '#6b7280', marginBottom: '0.25rem' }}>Khu trọ</label>
                 <select 
                   value={selectedHouseId} 
                   onChange={e => setSelectedHouseId(e.target.value)}
                   style={{ padding: '0.5rem', borderRadius: '0.25rem', border: '1px solid #d1d5db', outline: 'none' }}
                 >
                   <option value="">Tất cả khu trọ</option>
                   {houses.map(h => (
                     <option key={h.id} value={h.id}>{h.name}</option>
                   ))}
                 </select>
               </div>
               <div>
                 <label style={{ display: 'block', fontSize: '0.85rem', color: '#6b7280', marginBottom: '0.25rem' }}>Phòng</label>
                 <select 
                   value={selectedRoomId} 
                   onChange={e => setSelectedRoomId(e.target.value)}
                   disabled={!selectedHouseId}
                   style={{ padding: '0.5rem', borderRadius: '0.25rem', border: '1px solid #d1d5db', outline: 'none', background: !selectedHouseId ? '#f3f4f6' : '#fff' }}
                 >
                   <option value="">Tất cả phòng</option>
                   {roomsForSelectedHouse.map(r => (
                     <option key={r.id} value={r.id}>{r.name}</option>
                   ))}
                 </select>
               </div>
             </div>
          </div>
        </section>

        {message && <p className="dashboard-message">{message}</p>}

        <div className="tabs" style={{ display: 'flex', alignItems: 'center', margin: '0 0 16px 0', borderBottom: '1px solid #e5e7eb', overflowX: 'auto', gap: '8px' }}>
          <button 
            onClick={() => setSelectedStatus('all')} 
            style={{ padding: '12px 16px', background: 'none', border: 'none', borderBottom: selectedStatus === 'all' ? '2px solid #2563eb' : '2px solid transparent', color: selectedStatus === 'all' ? '#2563eb' : '#6b7280', fontWeight: selectedStatus === 'all' ? 600 : 500, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 0.2s' }}
          >
            Tất cả
          </button>
          <button 
            onClick={() => setSelectedStatus('Active')} 
            style={{ padding: '12px 16px', background: 'none', border: 'none', borderBottom: selectedStatus === 'Active' ? '2px solid #2563eb' : '2px solid transparent', color: selectedStatus === 'Active' ? '#2563eb' : '#6b7280', fontWeight: selectedStatus === 'Active' ? 600 : 500, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 0.2s' }}
          >
            Đang hiệu lực
          </button>
          <button 
            onClick={() => setSelectedStatus('Expired')} 
            style={{ padding: '12px 16px', background: 'none', border: 'none', borderBottom: selectedStatus === 'Expired' ? '2px solid #2563eb' : '2px solid transparent', color: selectedStatus === 'Expired' ? '#2563eb' : '#6b7280', fontWeight: selectedStatus === 'Expired' ? 600 : 500, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 0.2s' }}
          >
            Đã hết hạn
          </button>
          <button 
            onClick={() => setSelectedStatus('Cancelled')} 
            style={{ padding: '12px 16px', background: 'none', border: 'none', borderBottom: selectedStatus === 'Cancelled' ? '2px solid #2563eb' : '2px solid transparent', color: selectedStatus === 'Cancelled' ? '#2563eb' : '#6b7280', fontWeight: selectedStatus === 'Cancelled' ? 600 : 500, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 0.2s' }}
          >
            Đã hủy
          </button>
        </div>

        <section className="card-grid">
          {filteredContracts.length === 0 ? (
            <div className="empty-panel">
              <h2>Không tìm thấy hợp đồng</h2>
              <p>Chưa có hợp đồng nào phù hợp với bộ lọc hiện tại.</p>
            </div>
          ) : (
            filteredContracts.map((contract) => (
              <div
                className="dashboard-card"
                key={contract.id}
                onClick={() => navigate(ROUTE_PATHS.LANDLORD.CONTRACT_DETAIL(contract.id))}
                style={{ textAlign: 'left', cursor: 'pointer' }}
              >
                <div className="card-body-content" style={{ padding: '1rem', width: '100%', boxSizing: 'border-box' }}>
                  <div className="card-title-row" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <h3 style={{ margin: 0, fontSize: '1.125rem' }}>Phòng {contract.roomNumber}</h3>
                    <span className={`status-pill ${getStatusToneClass(contract.status)}`}>
                      {formatStatus(contract.status)}
                    </span>
                  </div>

                  {contract.isAwaitingFinalInvoice && (
                    <div style={{ marginTop: '0.6rem' }}>
                      <span className="status-pill status-warning">Chờ hóa đơn kỳ cuối</span>
                    </div>
                  )}

                  <div className="card-location" style={{ marginTop: '0.5rem', color: '#6b7280', fontSize: '0.875rem', display: 'flex', alignItems: 'center' }}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginRight: '0.25rem', flexShrink: 0 }}>
                      <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
                      <polyline points="9 22 9 12 15 12 15 22"></polyline>
                    </svg>
                    <span>{contract.roomingHouseName}</span>
                  </div>

                  <hr className="card-divider" style={{ margin: '1rem 0', borderColor: '#e5e7eb' }} />

                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', fontSize: '0.875rem' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: '#6b7280' }}>Mã HĐ:</span>
                      <strong>{contract.contractNumber}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: '#6b7280' }}>Đại diện thuê:</span>
                      <strong>{contract.mainTenantName}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: '#6b7280' }}>Tiền thuê/tháng:</span>
                      <strong style={{ color: '#10b981' }}>{formatMoneyString(contract.monthlyRent)} đ</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: '#6b7280' }}>Kỳ hạn:</span>
                      <span>
                        {formatDateVi(contract.startDate)} - {formatDateVi(contract.endDate)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            ))
          )}
        </section>
      </main>
    </div>
  );
}
