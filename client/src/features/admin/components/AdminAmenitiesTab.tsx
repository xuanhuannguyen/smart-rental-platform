import React, { useEffect, useState } from 'react';
import { adminCatalogApi } from '../services/adminCatalogApi';
import type { AdminAmenityResponse, PagedResult } from '../types/adminCatalog.types';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { getApiErrorMessage } from '../../../shared/api/apiError';

export function AdminAmenitiesTab() {
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [data, setData] = useState<PagedResult<AdminAmenityResponse> | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);

  // Inline Editing State
  const [editingRowId, setEditingRowId] = useState<number | null>(null);
  const [editingFormData, setEditingFormData] = useState({ name: '', iconCode: '', scope: 'Both' });
  const [isSubmittingEdit, setIsSubmittingEdit] = useState(false);

  // Create Form State
  const [isCreating, setIsCreating] = useState(false);
  const [createFormData, setCreateFormData] = useState({ name: '', iconCode: '', scope: 'Both' });
  const [isSubmittingCreate, setIsSubmittingCreate] = useState(false);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      const result = await adminCatalogApi.getAmenities(page, 20);
      setData(result.data);
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách tiện ích'));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [page]);

  const handleToggleStatus = async (id: number) => {
    try {
      await adminCatalogApi.toggleAmenityStatus(id);
      void loadData();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra'), type: 'error' });
    }
  };

  const startEdit = (item: AdminAmenityResponse) => {
    setIsCreating(false);
    setEditingRowId(item.id);
    setEditingFormData({ name: item.name, iconCode: item.iconCode || '', scope: item.scope });
  };

  const cancelEdit = () => {
    setEditingRowId(null);
    setEditingFormData({ name: '', iconCode: '', scope: 'Both' });
  };

  const saveEdit = async (id: number) => {
    setIsSubmittingEdit(true);
    try {
      await adminCatalogApi.updateAmenity(id, {
        name: editingFormData.name,
        iconCode: editingFormData.iconCode,
        scope: editingFormData.scope
      });
      void loadData();
      setEditingRowId(null);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu tiện ích'), type: 'error' });
    } finally {
      setIsSubmittingEdit(false);
    }
  };

  const startCreate = () => {
    setEditingRowId(null);
    setIsCreating(true);
    setCreateFormData({ name: '', iconCode: '', scope: 'Both' });
  };

  const cancelCreate = () => {
    setIsCreating(false);
  };

  const saveCreate = async () => {
    if (!createFormData.name) {
      setToast({ message: "Vui lòng điền đủ tên tiện ích", type: 'error' });
      return;
    }
    setIsSubmittingCreate(true);
    try {
      await adminCatalogApi.createAmenity(createFormData);
      void loadData();
      setIsCreating(false);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu tiện ích mới'), type: 'error' });
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
          + Thêm tiện ích
        </button>
      </div>

      <div className="admin-card">
        <div className="table-responsive">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Tên Tiện Ích</th>
                <th>Phạm Vi</th>
                <th>Icon Code</th>
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
                        <select 
                          className="admin-table-input"
                          value={editingFormData.scope}
                          onChange={e => setEditingFormData({...editingFormData, scope: e.target.value})}
                        >
                          <option value="Both">Cả 2</option>
                          <option value="House">Khu trọ</option>
                          <option value="Room">Phòng</option>
                        </select>
                      </td>
                      <td>
                        <input 
                          type="text" 
                          className="admin-table-input"
                          value={editingFormData.iconCode}
                          onChange={e => setEditingFormData({...editingFormData, iconCode: e.target.value})}
                        />
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
                      <td>{item.scope === 'House' ? 'Khu trọ' : item.scope === 'Room' ? 'Phòng' : 'Cả 2'}</td>
                      <td><span title={item.iconCode} style={{ fontSize: '1.25rem' }}>{item.iconCode || '-'}</span></td>
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
                    Không có tiện ích nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        <div className="admin-pagination">
          <span className="pagination-info">
            Hiển thị tối đa 20 dòng · Tổng cộng {data?.totalItems || 0} tiện ích
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
              <h3 className="admin-modal-title">Thêm mới Tiện ích</h3>
            </div>
            <div className="admin-modal-body">
              <div className="admin-form-group">
                <label className="admin-form-label">Tên tiện ích</label>
                <input 
                  type="text" 
                  placeholder="Nhập tên tiện ích"
                  className="admin-form-input"
                  value={createFormData.name}
                  onChange={e => setCreateFormData({...createFormData, name: e.target.value})}
                />
              </div>
              <div className="admin-form-group">
                <label className="admin-form-label">Phạm vi</label>
                <select 
                  className="admin-form-input"
                  value={createFormData.scope}
                  onChange={e => setCreateFormData({...createFormData, scope: e.target.value})}
                >
                  <option value="Both">Cả 2</option>
                  <option value="House">Khu trọ</option>
                  <option value="Room">Phòng</option>
                </select>
              </div>
              <div className="admin-form-group">
                <label className="admin-form-label">Icon Code</label>
                <input 
                  type="text" 
                  placeholder="vd: fa-solid fa-home"
                  className="admin-form-input"
                  value={createFormData.iconCode}
                  onChange={e => setCreateFormData({...createFormData, iconCode: e.target.value})}
                />
              </div>
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
