import React, { useEffect, useState } from 'react';
import { adminCatalogApi } from '../services/adminCatalogApi';
import type { AdminProvinceResponse, AdminWardResponse, PagedResult } from '../types/adminCatalog.types';
import { Button } from '../../../shared/components/ui/Button';
import { Toast } from '../../../shared/components/ui/Toast';
import { getApiErrorMessage } from '../../../shared/api/apiError';

export function AdminProvincesTab() {
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [subTab, setSubTab] = useState<'provinces' | 'wards'>('provinces');

  // Provinces State
  const [data, setData] = useState<PagedResult<AdminProvinceResponse> | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);

  // Wards State
  const [wardsData, setWardsData] = useState<PagedResult<AdminWardResponse> | null>(null);
  const [wardPage, setWardPage] = useState(1);
  const [selectedProvinceCode, setSelectedProvinceCode] = useState<string>('');
  const [allProvinces, setAllProvinces] = useState<AdminProvinceResponse[]>([]);

  // Inline Editing State
  const [editingRowCode, setEditingRowCode] = useState<string | null>(null);
  const [editingFormData, setEditingFormData] = useState({ name: '', type: '' });
  const [isSubmittingEdit, setIsSubmittingEdit] = useState(false);

  // Create Form State
  const [isCreating, setIsCreating] = useState(false);
  const [createFormData, setCreateFormData] = useState({ code: '', name: '', type: 'Province', provinceCode: '' });
  const [isSubmittingCreate, setIsSubmittingCreate] = useState(false);

  const loadProvinces = async () => {
    setIsLoading(true);
    setError('');
    try {
      const result = await adminCatalogApi.getProvinces(page, 20);
      setData(result.data);
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách tỉnh/thành'));
    } finally {
      setIsLoading(false);
    }
  };

  const loadAllProvincesForDropdown = async () => {
    try {
      const result = await adminCatalogApi.getProvinces(1, 100);
      setAllProvinces(result.data.items);
      if (result.data.items.length > 0 && !selectedProvinceCode) {
        setSelectedProvinceCode(result.data.items[0].code);
      }
    } catch (err) {
      // ignore
    }
  };

  const loadWards = async () => {
    if (!selectedProvinceCode) return;
    setIsLoading(true);
    setError('');
    try {
      const result = await adminCatalogApi.getWards(selectedProvinceCode, wardPage, 20);
      setWardsData(result.data);
    } catch (err: any) {
      setError(getApiErrorMessage(err, 'Không thể tải danh sách xã/phường'));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (subTab === 'provinces') {
      void loadProvinces();
    } else {
      void loadAllProvincesForDropdown();
    }
  }, [subTab, page]);

  useEffect(() => {
    if (subTab === 'wards' && selectedProvinceCode) {
      void loadWards();
    }
  }, [subTab, selectedProvinceCode, wardPage]);

  const handleToggleProvince = async (code: string) => {
    try {
      await adminCatalogApi.toggleProvinceStatus(code);
      void loadProvinces();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra'), type: 'error' });
    }
  };

  const handleToggleWard = async (code: string) => {
    try {
      await adminCatalogApi.toggleWardStatus(code);
      void loadWards();
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra'), type: 'error' });
    }
  };

  const startEdit = (item: any) => {
    setIsCreating(false);
    setEditingRowCode(item.code);
    setEditingFormData({ name: item.name, type: item.type });
  };

  const cancelEdit = () => {
    setEditingRowCode(null);
    setEditingFormData({ name: '', type: '' });
  };

  const saveEdit = async (code: string) => {
    setIsSubmittingEdit(true);
    try {
      if (subTab === 'provinces') {
        await adminCatalogApi.updateProvince(code, { name: editingFormData.name, type: editingFormData.type });
        void loadProvinces();
      } else {
        await adminCatalogApi.updateWard(code, { name: editingFormData.name, type: editingFormData.type });
        void loadWards();
      }
      setEditingRowCode(null);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu khu vực'), type: 'error' });
    } finally {
      setIsSubmittingEdit(false);
    }
  };

  const startCreate = () => {
    setEditingRowCode(null);
    setIsCreating(true);
    if (subTab === 'provinces') {
      setCreateFormData({ code: '', name: '', type: 'Province', provinceCode: '' });
    } else {
      setCreateFormData({ code: '', name: '', type: 'Ward', provinceCode: selectedProvinceCode });
    }
  };

  const cancelCreate = () => {
    setIsCreating(false);
  };

  const saveCreate = async () => {
    if (!createFormData.code || !createFormData.name) {
      setToast({ message: "Vui lòng điền đủ mã code và tên", type: 'error' });
      return;
    }
    setIsSubmittingCreate(true);
    try {
      if (subTab === 'provinces') {
        await adminCatalogApi.createProvince({ code: createFormData.code, name: createFormData.name, type: createFormData.type });
        void loadProvinces();
      } else {
        await adminCatalogApi.createWard({ code: createFormData.code, name: createFormData.name, type: createFormData.type, provinceCode: createFormData.provinceCode });
        void loadWards();
      }
      setIsCreating(false);
    } catch (err: any) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra khi lưu khu vực mới'), type: 'error' });
    } finally {
      setIsSubmittingCreate(false);
    }
  };

  const translateProvinceType = (type: string) => type === 'City' ? 'Thành phố' : type === 'Province' ? 'Tỉnh' : type;
  const translateWardType = (type: string) => type === 'Ward' ? 'Phường' : type === 'Commune' ? 'Xã' : type === 'SpecialAdministrativeRegion' ? 'Đặc khu' : type;

  return (
    <>
      <div className="admin-tabs">
        <button
          className={`admin-tab-btn ${subTab === 'provinces' ? 'active' : ''}`}
          onClick={() => {
            setSubTab('provinces');
            cancelEdit();
            cancelCreate();
          }}
        >
          Tỉnh / Thành phố
        </button>
        <button
          className={`admin-tab-btn ${subTab === 'wards' ? 'active' : ''}`}
          onClick={() => {
            setSubTab('wards');
            cancelEdit();
            cancelCreate();
          }}
        >
          Xã / Phường
        </button>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', width: '100%', marginBottom: '16px' }}>
        <div style={{ flex: 1 }}>
          {subTab === 'wards' && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '0.875rem', fontWeight: 500, color: '#374151' }}>Chọn tỉnh/thành:</span>
              <select
                value={selectedProvinceCode}
                onChange={(e) => {
                  setSelectedProvinceCode(e.target.value);
                  setWardPage(1);
                  cancelEdit();
                  cancelCreate();
                }}
                style={{ padding: '8px 12px', border: '1px solid #d1d5db', borderRadius: '6px', backgroundColor: '#ffffff', outline: 'none' }}
              >
                {allProvinces.map(p => (
                  <option key={p.code} value={p.code}>{p.name}</option>
                ))}
              </select>
            </div>
          )}
        </div>

        <div style={{ flexShrink: 0 }}>
          <button 
            type="button" 
            className="admin-table-action-btn" 
            onClick={startCreate}
            disabled={isCreating}
            style={{ fontWeight: 'bold' }}
          >
            + Thêm {subTab === 'provinces' ? 'Tỉnh/Thành' : 'Xã/Phường'}
          </button>
        </div>
      </div>

      {(isLoading && !data && subTab === 'provinces') || (isLoading && !wardsData && subTab === 'wards') ? (
        <div className="text-gray-500 p-4">Đang tải...</div>
      ) : null}
      
      {error && <div className="text-red-500 p-4">{error}</div>}

      <div className="admin-card">
        <div className="table-responsive">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã code</th>
                <th>Tên Khu Vực</th>
                <th>Phân Loại</th>
                <th>Trạng Thái</th>
                <th style={{ textAlign: 'right' }}>Thao Tác</th>
              </tr>
            </thead>
            <tbody>

              {subTab === 'provinces' ? (
                data?.items.map((item) => (
                  <tr key={item.code} className={editingRowCode === item.code ? 'bg-blue-50' : ''}>
                    {editingRowCode === item.code ? (
                      <>
                        <td>{item.code}</td>
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
                            value={editingFormData.type}
                            onChange={e => setEditingFormData({...editingFormData, type: e.target.value})}
                          >
                            <option value="Province">Tỉnh</option>
                            <option value="City">Thành phố</option>
                          </select>
                        </td>
                        <td>
                          <span className={`kyc-timeline-status ${item.isActive ? 'Approved' : 'Rejected'}`}>
                            {item.isActive ? 'Hoạt động' : 'Đã ẩn'}
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button 
                            type="button"
                            onClick={() => saveEdit(item.code)}
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
                        <td>{item.code}</td>
                        <td><strong>{item.name}</strong></td>
                        <td>{translateProvinceType(item.type)}</td>
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
                            onClick={() => handleToggleProvince(item.code)}
                            className="admin-table-action-btn"
                            style={{ backgroundColor: item.isActive ? '#ef4444' : '#10b981', color: '#ffffff' }}
                          >
                            {item.isActive ? 'Ẩn' : 'Hiện'}
                          </button>
                        </td>
                      </>
                    )}
                  </tr>
                ))
              ) : (
                wardsData?.items.map((item) => (
                  <tr key={item.code} className={editingRowCode === item.code ? 'bg-blue-50' : ''}>
                    {editingRowCode === item.code ? (
                      <>
                        <td>{item.code}</td>
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
                            value={editingFormData.type}
                            onChange={e => setEditingFormData({...editingFormData, type: e.target.value})}
                          >
                            <option value="Ward">Phường</option>
                            <option value="Commune">Xã</option>
                            <option value="SpecialAdministrativeRegion">Đặc khu</option>
                          </select>
                        </td>
                        <td>
                          <span className={`kyc-timeline-status ${item.isActive ? 'Approved' : 'Rejected'}`}>
                            {item.isActive ? 'Hoạt động' : 'Đã ẩn'}
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button 
                            type="button"
                            onClick={() => saveEdit(item.code)}
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
                        <td>{item.code}</td>
                        <td><strong>{item.name}</strong></td>
                        <td>{translateWardType(item.type)}</td>
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
                            onClick={() => handleToggleWard(item.code)}
                            className="admin-table-action-btn"
                            style={{ backgroundColor: item.isActive ? '#ef4444' : '#10b981', color: '#ffffff' }}
                          >
                            {item.isActive ? 'Ẩn' : 'Hiện'}
                          </button>
                        </td>
                      </>
                    )}
                  </tr>
                ))
              )}
              
              {!isCreating && (subTab === 'provinces' ? data?.items.length : wardsData?.items.length) === 0 && (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '32px' }}>
                    Không có khu vực nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        <div className="admin-pagination">
          <span className="pagination-info">
            Hiển thị tối đa 20 dòng · Tổng cộng {subTab === 'provinces' ? (data?.totalItems || 0) : (wardsData?.totalItems || 0)} khu vực
          </span>
          <div className="pagination-actions">
            <button
              type="button"
              className="pagination-btn"
              disabled={(subTab === 'provinces' ? page : wardPage) <= 1}
              onClick={() => subTab === 'provinces' ? setPage(page - 1) : setWardPage(wardPage - 1)}
            >
              Trước
            </button>
            <span className="pagination-current">
              Trang {subTab === 'provinces' ? page : wardPage} / {subTab === 'provinces' ? (data?.totalPages || 1) : (wardsData?.totalPages || 1)}
            </span>
            <button
              type="button"
              className="pagination-btn"
              disabled={subTab === 'provinces' ? (!data || page >= data.totalPages) : (!wardsData || wardPage >= wardsData.totalPages)}
              onClick={() => subTab === 'provinces' ? setPage(page + 1) : setWardPage(wardPage + 1)}
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
              <h3 className="admin-modal-title">Thêm mới {subTab === 'provinces' ? 'Tỉnh/Thành' : 'Xã/Phường'}</h3>
            </div>
            <div className="admin-modal-body">
              <div className="admin-form-group">
                <label className="admin-form-label">Mã code</label>
                <input 
                  type="text" 
                  placeholder="Nhập mã code"
                  className="admin-form-input"
                  value={createFormData.code}
                  onChange={e => setCreateFormData({...createFormData, code: e.target.value})}
                />
              </div>
              <div className="admin-form-group">
                <label className="admin-form-label">Tên {subTab === 'provinces' ? 'tỉnh/thành' : 'xã/phường'}</label>
                <input 
                  type="text" 
                  placeholder="Nhập tên"
                  className="admin-form-input"
                  value={createFormData.name}
                  onChange={e => setCreateFormData({...createFormData, name: e.target.value})}
                />
              </div>
              {subTab === 'wards' && (
                <div className="admin-form-group">
                  <label className="admin-form-label">Thuộc Tỉnh/Thành</label>
                  <select
                    className="admin-form-input"
                    value={createFormData.provinceCode}
                    onChange={e => setCreateFormData({...createFormData, provinceCode: e.target.value})}
                  >
                    <option value="">-- Chọn tỉnh/thành --</option>
                    {allProvinces.map(p => (
                      <option key={p.code} value={p.code}>{p.name}</option>
                    ))}
                  </select>
                </div>
              )}
              <div className="admin-form-group">
                <label className="admin-form-label">Phân loại</label>
                <select 
                  className="admin-form-input"
                  value={createFormData.type}
                  onChange={e => setCreateFormData({...createFormData, type: e.target.value})}
                >
                  {subTab === 'provinces' ? (
                    <>
                      <option value="Province">Tỉnh</option>
                      <option value="City">Thành phố</option>
                    </>
                  ) : (
                    <>
                      <option value="Ward">Phường</option>
                      <option value="Commune">Xã</option>
                      <option value="SpecialAdministrativeRegion">Đặc khu</option>
                    </>
                  )}
                </select>
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
