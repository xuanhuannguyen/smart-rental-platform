import React, { useEffect, useState } from 'react';
import { adminCatalogApi } from '../services/adminCatalogApi';
import type { AdminBillingServiceTypeResponse, PagedResult } from '../types/adminCatalog.types';
import { Toast } from '../../../shared/components/ui/Toast';
import { getApiErrorMessage } from '../../../shared/api/apiError';

export function AdminBillingServicesTab() {
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [data, setData] = useState<PagedResult<AdminBillingServiceTypeResponse> | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);

  // Inline Editing State
  const [editingRowId, setEditingRowId] = useState<number | null>(null);
  const [editingFormData, setEditingFormData] = useState({ name: '', meterUnitName: '', supportsMeterReading: false });
  const [isSubmittingEdit, setIsSubmittingEdit] = useState(false);

  // Create Form State
  const [isCreating, setIsCreating] = useState(false);
  const [createFormData, setCreateFormData] = useState({ name: '', meterUnitName: '', supportsMeterReading: false });
  const [isSubmittingCreate, setIsSubmittingCreate] = useState(false);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      const result = await adminCatalogApi.getBillingServiceTypes(page, 20);
      setData(result.data);
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách dịch vụ hóa đơn'));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [page]);

  const handleToggleStatus = async (id: number) => {
    try {
      await adminCatalogApi.toggleBillingServiceTypeStatus(id);
      void loadData();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra'), type: 'error' });
    }
  };

  const startEdit = (item: AdminBillingServiceTypeResponse) => {
    setIsCreating(false);
    setEditingRowId(item.id);
    setEditingFormData({ name: item.name, meterUnitName: item.meterUnitName || '', supportsMeterReading: item.supportsMeterReading });
  };

  const cancelEdit = () => {
    setEditingRowId(null);
    setEditingFormData({ name: '', meterUnitName: '', supportsMeterReading: false });
  };

  const saveEdit = async (id: number) => {
    setIsSubmittingEdit(true);
    try {
      await adminCatalogApi.updateBillingServiceType(id, {
        name: editingFormData.name,
        meterUnitName: editingFormData.meterUnitName,
        supportsMeterReading: editingFormData.supportsMeterReading
      });
      void loadData();
      setEditingRowId(null);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu dịch vụ'), type: 'error' });
    } finally {
      setIsSubmittingEdit(false);
    }
  };

  const startCreate = () => {
    setEditingRowId(null);
    setIsCreating(true);
    setCreateFormData({ name: '', meterUnitName: '', supportsMeterReading: false });
  };

  const cancelCreate = () => {
    setIsCreating(false);
  };

  const saveCreate = async () => {
    if (!createFormData.name) {
      setToast({ message: "Vui lòng điền tên dịch vụ", type: 'error' });
      return;
    }
    setIsSubmittingCreate(true);
    try {
      await adminCatalogApi.createBillingServiceType(createFormData);
      void loadData();
      setIsCreating(false);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu dịch vụ mới'), type: 'error' });
    } finally {
      setIsSubmittingCreate(false);
    }
  };

  if (isLoading && !data) return <div className="text-gray-500 p-4">Đang tải...</div>;
  if (error) return <div className="text-red-500 p-4">{error}</div>;

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', width: '100%', marginBottom: '16px' }}>
        <button 
          type="button" 
          className="admin-table-action-btn" 
          onClick={startCreate}
          disabled={isCreating}
          style={{ fontWeight: 'bold' }}
        >
          + Thêm dịch vụ
        </button>
      </div>

      <div className="admin-card">
        <div className="table-responsive">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Tên Dịch Vụ</th>
                <th>Đơn Vị Tính</th>
                <th>Hỗ Trợ Chốt Chỉ Số</th>
                <th>Trạng Thái</th>
                <th style={{ textAlign: 'right' }}>Thao Tác</th>
              </tr>
            </thead>
            <tbody>


              {data?.items.map((item) => (
                <tr key={item.id} className={editingRowId === item.id ? 'bg-blue-50' : ''}>
                  {editingRowId === item.id ? (
                    <>
                      <td>
                        <input 
                          type="text" 
                          className="admin-table-input"
                          value={editingFormData.name}
                          onChange={e => setEditingFormData({...editingFormData, name: e.target.value})}
                        />
                      </td>
                      <td>
                        <input 
                          type="text" 
                          className="admin-table-input"
                          value={editingFormData.meterUnitName}
                          onChange={e => setEditingFormData({...editingFormData, meterUnitName: e.target.value})}
                        />
                      </td>
                      <td>
                        <label className="flex items-center space-x-2">
                          <input 
                            type="checkbox" 
                            checked={editingFormData.supportsMeterReading}
                            onChange={e => setEditingFormData({...editingFormData, supportsMeterReading: e.target.checked})}
                          />
                          <span>Có đo chỉ số</span>
                        </label>
                      </td>
                      <td>
                        <span className={`kyc-timeline-status ${item.isActive ? 'Approved' : 'Rejected'}`}>
                          {item.isActive ? 'Hoạt động' : 'Đã ẩn'}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button 
                          type="button"
                          onClick={() => saveEdit(item.id)}
                          disabled={isSubmittingEdit}
                          className="admin-table-action-btn"
                          style={{ marginRight: '8px', backgroundColor: '#3b82f6', color: 'white' }}
                        >
                          {isSubmittingEdit ? '...' : 'Lưu'}
                        </button>
                        <button 
                          type="button"
                          onClick={cancelEdit}
                          className="admin-table-action-btn"
                          style={{ backgroundColor: '#9ca3af', color: '#ffffff' }}
                        >
                          Hủy
                        </button>
                      </td>
                    </>
                  ) : (
                    <>
                      <td><strong>{item.name}</strong></td>
                      <td>{item.meterUnitName || '-'}</td>
                      <td>
                        {item.supportsMeterReading ? (
                          <span className="text-blue-600 font-medium">Có</span>
                        ) : (
                          <span className="text-gray-500">Không</span>
                        )}
                      </td>
                      <td>
                        <span className={`kyc-timeline-status ${item.isActive ? 'Approved' : 'Rejected'}`}>
                          {item.isActive ? 'Hoạt động' : 'Đã ẩn'}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button 
                          type="button"
                          onClick={() => startEdit(item)}
                          className="admin-table-action-btn"
                          style={{ marginRight: '8px' }}
                        >
                          Sửa
                        </button>
                        <button 
                          type="button"
                          onClick={() => handleToggleStatus(item.id)}
                          className="admin-table-action-btn"
                          style={{ backgroundColor: item.isActive ? '#ef4444' : '#10b981', color: '#ffffff' }}
                        >
                          {item.isActive ? 'Ẩn' : 'Hiện'}
                        </button>
                      </td>
                    </>
                  )}
                </tr>
              ))}
              {!isCreating && data?.items.length === 0 && (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '32px' }}>
                    Không có dịch vụ nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        <div className="admin-pagination">
          <span className="pagination-info">
            Hiển thị tối đa 20 dòng · Tổng cộng {data?.totalItems || 0} dịch vụ
          </span>
          <div className="pagination-actions">
            <button
              type="button"
              className="pagination-btn"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              Trước
            </button>
            <span className="pagination-current">Trang {page} / {data?.totalPages || 1}</span>
            <button
              type="button"
              className="pagination-btn"
              disabled={!data || page >= data.totalPages}
              onClick={() => setPage(page + 1)}
            >
              Sau
            </button>
          </div>
        </div>
      </div>

      {isCreating && (
        <div className="admin-modal-overlay">
          <div className="admin-modal-container">
            <div className="admin-modal-header">
              <h3 className="admin-modal-title">Thêm mới Dịch vụ</h3>
            </div>
            <div className="admin-modal-body">
              <div className="admin-form-group">
                <label className="admin-form-label">Tên dịch vụ</label>
                <input 
                  type="text" 
                  placeholder="Nhập tên dịch vụ"
                  className="admin-form-input"
                  value={createFormData.name}
                  onChange={e => setCreateFormData({...createFormData, name: e.target.value})}
                />
              </div>
              <div className="admin-form-group">
                <label className="admin-form-label" style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                  <input 
                    type="checkbox" 
                    checked={createFormData.supportsMeterReading}
                    onChange={e => setCreateFormData({
                      ...createFormData, 
                      supportsMeterReading: e.target.checked,
                      meterUnitName: e.target.checked ? createFormData.meterUnitName : ''
                    })}
                  />
                  Hỗ trợ chốt chỉ số (đo công tơ)
                </label>
              </div>
              {createFormData.supportsMeterReading && (
                <div className="admin-form-group">
                  <label className="admin-form-label">Đơn vị tính</label>
                  <input 
                    type="text" 
                    placeholder="VD: kWh, m3, khối..."
                    className="admin-form-input"
                    value={createFormData.meterUnitName}
                    onChange={e => setCreateFormData({...createFormData, meterUnitName: e.target.value})}
                  />
                </div>
              )}
            </div>
            <div className="admin-modal-footer">
              <button 
                type="button"
                onClick={cancelCreate}
                className="admin-table-action-btn"
                style={{ backgroundColor: '#9ca3af', color: '#ffffff' }}
              >
                Hủy
              </button>
              <button 
                type="button"
                onClick={saveCreate}
                disabled={isSubmittingCreate}
                className="admin-table-action-btn"
                style={{ backgroundColor: '#3b82f6', color: 'white' }}
              >
                {isSubmittingCreate ? 'Đang lưu...' : 'Lưu'}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </>
  );
}
