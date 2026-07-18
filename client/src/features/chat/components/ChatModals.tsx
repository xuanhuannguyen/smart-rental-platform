import { useEffect, useState } from 'react';
import { Avatar } from '../../../shared/components/Avatar';
import { getQuickContacts, searchChatUsers, getActiveTenantsByRoomingHouse, getEligibleMembers, getFilterRoomingHouses, uploadChatAvatar } from '../api';
import type { ChatUser, ChatFilterRoomingHouse, ChatParticipant } from '../types';
import { getMyRoomingHouses } from '../../rooming-houses/api';
import type { RoomingHouseSummary } from '../../rooming-houses/types';

interface UserSearchModalProps {
  title: string;
  submitLabel: string;
  onClose: () => void;
  onSubmit: (users: ChatUser[]) => Promise<void>;
  single?: boolean;
  roomingHouseId?: string | null;
  excludedUserIds?: string[];
  conversationId?: string | null;
  requiresApproval?: boolean;
  isAdminOrOwner?: boolean;
}

export function UserSearchModal({
  title,
  submitLabel,
  onClose,
  onSubmit,
  single,
  roomingHouseId,
  excludedUserIds = [],
  conversationId,
  requiresApproval = false,
  isAdminOrOwner = false
}: UserSearchModalProps) {
  const [tab, setTab] = useState<'quick' | 'search'>(conversationId && !roomingHouseId ? 'search' : 'quick');
  const [email, setEmail] = useState('');
  const [searchResults, setSearchResults] = useState<ChatUser[]>([]);
  const [selected, setSelected] = useState<ChatUser[]>([]);
  const [quickContacts, setQuickContacts] = useState<ChatUser[]>([]);
  const [filterHouses, setFilterHouses] = useState<ChatFilterRoomingHouse[]>([]);
  const [selectedHouseId, setSelectedHouseId] = useState<string>(roomingHouseId || '');
  const [loading, setLoading] = useState(false);
  const [searching, setSearching] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    getFilterRoomingHouses()
      .then(setFilterHouses)
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (tab !== 'quick') return;
    setLoading(true);
    const request = conversationId
      ? getEligibleMembers(conversationId, selectedHouseId || null)
      : (selectedHouseId
          ? getActiveTenantsByRoomingHouse(selectedHouseId)
          : getQuickContacts());

    request
      .then(setQuickContacts)
      .catch(() => setError('Không thể tải danh sách người dùng.'))
      .finally(() => setLoading(false));
  }, [selectedHouseId, conversationId, tab]);

  const handleSearch = async (val = email) => {
    const trimmed = val.trim();
    if (trimmed.length < 3) {
      return;
    }
    setSearching(true);
    try {
      const res = await searchChatUsers(trimmed, null); // Truyền null để không cần cùng khu trọ
      setSearchResults(res);
    } catch {
      setSearchResults([]);
    } finally {
      setSearching(false);
    }
  };

  useEffect(() => {
    if (tab !== 'search' || email.trim().length < 3) {
      setSearchResults([]);
      return;
    }

    const handle = window.setTimeout(() => {
      void handleSearch(email);
    }, 400);
    return () => window.clearTimeout(handle);
  }, [email, roomingHouseId, tab]);

  function toggle(user: ChatUser) {
    if (single) {
      setSelected([user]);
      return;
    }
    setSelected(prev =>
      prev.some(item => item.userId === user.userId)
        ? prev.filter(item => item.userId !== user.userId)
        : [...prev, user]
    );
  }

  function toggleAllVisible(users: ChatUser[]) {
    if (single) return;
    const allSelected = users.every(user => selected.some(item => item.userId === user.userId));
    setSelected(prev => {
      if (allSelected) {
        const visibleIds = new Set(users.map(user => user.userId));
        return prev.filter(user => !visibleIds.has(user.userId));
      }

      const next = [...prev];
      for (const user of users) {
        if (!next.some(item => item.userId === user.userId)) {
          next.push(user);
        }
      }
      return next;
    });
  }

  const excluded = new Set(excludedUserIds);
  const rawList = tab === 'quick' ? quickContacts : searchResults;
  const combined = rawList
    .filter(user => !excluded.has(user.userId))
    .filter((user, index, arr) => arr.findIndex(item => item.userId === user.userId) === index);

  const handleSubmit = async () => {
    if (selected.length === 0 || submitting) return;
    setSubmitting(true);
    setError('');
    try {
      await onSubmit(selected);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể thực hiện thao tác.');
    } finally {
      setSubmitting(false);
    }
  };

  const showApproval = requiresApproval && !isAdminOrOwner;
  const buttonLabel = submitting
    ? 'Đang xử lý...'
    : (showApproval ? 'Gửi yêu cầu tham gia' : submitLabel);

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal chat-modal--wide">
        <header>
          <h2>{title}</h2>
          <button type="button" onClick={onClose} disabled={submitting}>Đóng</button>
        </header>

        {conversationId && (
          <div className="chat-modal-tabs" style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <button
              type="button"
              onClick={() => setTab('quick')}
              style={{
                flex: 1,
                padding: '8px',
                border: 'none',
                borderRadius: '6px',
                backgroundColor: tab === 'quick' ? '#3b82f6' : '#f1f5f9',
                color: tab === 'quick' ? '#fff' : '#475569',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              Thêm nhanh từ khu trọ
            </button>
            <button
              type="button"
              onClick={() => setTab('search')}
              style={{
                flex: 1,
                padding: '8px',
                border: 'none',
                borderRadius: '6px',
                backgroundColor: tab === 'search' ? '#3b82f6' : '#f1f5f9',
                color: tab === 'search' ? '#fff' : '#475569',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              Tìm theo email
            </button>
          </div>
        )}

        {tab === 'quick' && conversationId && (
          <div style={{ marginBottom: '12px' }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#475569', marginBottom: '6px' }}>
              Lọc theo khu trọ:
            </label>
            <select
              value={selectedHouseId}
              onChange={e => setSelectedHouseId(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 12px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                backgroundColor: '#fff',
                fontSize: '14px',
                cursor: 'pointer'
              }}
            >
              <option value="">-- Tất cả liên hệ nhanh --</option>
              {filterHouses.map(house => (
                <option key={house.id} value={house.id}>
                  {house.name} ({house.address})
                </option>
              ))}
            </select>
            <p style={{ fontSize: '11px', color: '#64748b', margin: '4px 0 0 0', fontStyle: 'italic' }}>
              * Khu trọ chỉ dùng để lọc danh sách gợi ý nhanh, bạn vẫn có thể chuyển sang tab tìm kiếm email để mời bất kỳ ai.
            </p>
          </div>
        )}

        {tab === 'search' && (
          <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <input
              value={email}
              onChange={event => setEmail(event.target.value)}
              placeholder="Nhập email để tìm..."
              disabled={submitting}
              onKeyDown={event => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  void handleSearch();
                }
              }}
              style={{ flex: 1, padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
            />
            <button
              type="button"
              onClick={() => void handleSearch()}
              disabled={submitting || searching}
              style={{
                padding: '8px 16px',
                backgroundColor: '#3b82f6',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              {searching ? 'Đang tìm...' : 'Tìm kiếm'}
            </button>
          </div>
        )}

        {error && <p className="chat-modal__error">{error}</p>}
        <div className="chat-user-results">
          {loading || searching ? (
            <div className="chat-empty">Đang tìm kiếm...</div>
          ) : combined.length === 0 ? (
            <div className="chat-empty">
              {tab === 'quick'
                ? 'Không có thành viên mới phù hợp để thêm nhanh.'
                : (email.trim().length < 3
                    ? 'Nhập tối thiểu 3 ký tự email và nhấn Tìm kiếm.'
                    : 'Không tìm thấy người dùng phù hợp với email này.')}
            </div>
          ) : (
            <>
              {tab === 'quick' && !single && (
                <button
                  type="button"
                  className="chat-user-results__select-all"
                  onClick={() => toggleAllVisible(combined)}
                  disabled={submitting}
                >
                  {combined.every(user => selected.some(item => item.userId === user.userId))
                    ? 'Bỏ chọn tất cả'
                    : `Chọn tất cả (${combined.length})`}
                </button>
              )}
              {combined.map(user => (
                <button
                  key={user.userId}
                  type="button"
                  onClick={() => toggle(user)}
                  className={selected.some(item => item.userId === user.userId) ? 'selected' : ''}
                  disabled={submitting}
                >
                  <Avatar name={user.displayName} url={user.avatarUrl} />
                  <span>
                    <strong>{user.displayName}</strong>
                    <small>
                      {user.email}
                      {user.contextLabel ? ` • ${user.contextLabel}` : ''}
                    </small>
                  </span>
                </button>
              ))}
            </>
          )}
        </div>
        <footer>
          <span>{selected.length} người được chọn</span>
          <button
            type="button"
            onClick={() => void handleSubmit()}
            disabled={selected.length === 0 || submitting}
          >
            {buttonLabel}
          </button>
        </footer>
      </section>
    </div>
  );
}

interface CreateGroupModalProps {
  onClose: () => void;
  onSubmit: (title: string, users: ChatUser[], roomingHouseId?: string | null, avatarMediaAssetId?: string | null) => Promise<void>;
}

export function CreateGroupModal({ onClose, onSubmit }: CreateGroupModalProps) {
  const [title, setTitle] = useState('');
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const [tab, setTab] = useState<'quick' | 'search'>('quick');
  const [email, setEmail] = useState('');
  const [searchResults, setSearchResults] = useState<ChatUser[]>([]);
  const [selected, setSelected] = useState<ChatUser[]>([]);
  const [quickContacts, setQuickContacts] = useState<ChatUser[]>([]);
  const [filterHouses, setFilterHouses] = useState<ChatFilterRoomingHouse[]>([]);
  const [selectedHouseId, setSelectedHouseId] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [searching, setSearching] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    getFilterRoomingHouses()
      .then(setFilterHouses)
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (tab !== 'quick') return;
    setLoading(true);
    const request = selectedHouseId
      ? getActiveTenantsByRoomingHouse(selectedHouseId)
      : getQuickContacts();

    request
      .then(setQuickContacts)
      .catch(() => setError('Không thể tải danh sách gợi ý.'))
      .finally(() => setLoading(false));
  }, [selectedHouseId, tab]);

  const handleSearch = async (val = email) => {
    const trimmed = val.trim();
    if (trimmed.length < 3) {
      return;
    }
    setSearching(true);
    try {
      const res = await searchChatUsers(trimmed, null);
      setSearchResults(res);
    } catch {
      setSearchResults([]);
    } finally {
      setSearching(false);
    }
  };

  useEffect(() => {
    if (tab !== 'search' || email.trim().length < 3) {
      setSearchResults([]);
      return;
    }
    const timer = setTimeout(() => {
      void handleSearch(email);
    }, 400);
    return () => clearTimeout(timer);
  }, [email, tab]);

  function toggle(user: ChatUser) {
    setSelected(prev =>
      prev.some(item => item.userId === user.userId)
        ? prev.filter(item => item.userId !== user.userId)
        : [...prev, user]
    );
  }

  function toggleAllVisible(users: ChatUser[]) {
    const allSelected = users.every(user => selected.some(item => item.userId === user.userId));
    setSelected(prev => {
      if (allSelected) {
        const visibleIds = new Set(users.map(user => user.userId));
        return prev.filter(user => !visibleIds.has(user.userId));
      }

      const next = [...prev];
      for (const user of users) {
        if (!next.some(item => item.userId === user.userId)) {
          next.push(user);
        }
      }
      return next;
    });
  }

  const handleCreate = async () => {
    if (selected.length < 2) {
      alert('Nhóm chat phải có ít nhất 3 thành viên (bao gồm cả bạn). Vui lòng chọn ít nhất 2 thành viên khác.');
      return;
    }
    if (submitting) return;
    setSubmitting(true);
    try {
      let avatarMediaAssetId: string | null = null;
      if (avatarFile) {
        const uploaded = await uploadChatAvatar(avatarFile);
        avatarMediaAssetId = uploaded.mediaAssetId;
      }
      await onSubmit(title || 'Nhóm trò chuyện', selected, selectedHouseId || null, avatarMediaAssetId);
    } catch (err) {
      alert('Lỗi tạo nhóm: ' + (err instanceof Error ? err.message : ''));
    } finally {
      setSubmitting(false);
    }
  };

  const currentList = tab === 'quick' ? quickContacts : searchResults;

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal chat-modal--wide">
        <header>
          <h2>Tạo nhóm trò chuyện</h2>
          <button type="button" onClick={onClose} disabled={submitting}>Đóng</button>
        </header>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', margin: '16px 0 8px 0' }}>
          <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '6px' }}>
              <label style={{ fontSize: '13px', fontWeight: 600, color: '#475569' }}>
                Ảnh đại diện
              </label>
              <div style={{ position: 'relative', width: '56px', height: '56px' }}>
                {avatarPreview ? (
                  <img src={avatarPreview} alt="Avatar preview" style={{ width: '56px', height: '56px', borderRadius: '50%', objectFit: 'cover', border: '1px solid #cbd5e1' }} />
                ) : (
                  <div style={{ width: '56px', height: '56px', borderRadius: '50%', backgroundColor: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '20px', color: '#64748b', fontWeight: 'bold' }}>
                    👥
                  </div>
                )}
                <label style={{
                  position: 'absolute',
                  bottom: '-2px',
                  right: '-2px',
                  backgroundColor: '#3b82f6',
                  color: '#fff',
                  borderRadius: '50%',
                  width: '20px',
                  height: '20px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '11px',
                  cursor: 'pointer',
                  boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
                }} title="Chọn ảnh">
                  📷
                  <input
                    type="file"
                    accept="image/*"
                    onChange={e => {
                      const file = e.target.files?.[0];
                      if (file) {
                        setAvatarFile(file);
                        setAvatarPreview(URL.createObjectURL(file));
                      }
                    }}
                    disabled={submitting}
                    style={{ display: 'none' }}
                  />
                </label>
              </div>
            </div>
            <div style={{ flex: 1 }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#475569', marginBottom: '6px' }}>
                Tên nhóm chat
              </label>
              <input
                value={title}
                onChange={event => setTitle(event.target.value)}
                placeholder="Nhập tên nhóm trò chuyện..."
                disabled={submitting}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px', outline: 'none' }}
              />
            </div>
          </div>

          <div className="chat-modal-tabs" style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
            <button
              type="button"
              onClick={() => setTab('quick')}
              style={{
                flex: 1,
                padding: '8px',
                border: 'none',
                borderRadius: '6px',
                backgroundColor: tab === 'quick' ? '#3b82f6' : '#f1f5f9',
                color: tab === 'quick' ? '#fff' : '#475569',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              Thêm nhanh từ khu trọ
            </button>
            <button
              type="button"
              onClick={() => setTab('search')}
              style={{
                flex: 1,
                padding: '8px',
                border: 'none',
                borderRadius: '6px',
                backgroundColor: tab === 'search' ? '#3b82f6' : '#f1f5f9',
                color: tab === 'search' ? '#fff' : '#475569',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              Tìm theo email
            </button>
          </div>
        </div>

        {tab === 'quick' && (
          <div style={{ marginBottom: '12px' }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
              Lọc theo khu trọ (gợi ý nhanh):
            </label>
            <select
              value={selectedHouseId}
              onChange={e => setSelectedHouseId(e.target.value)}
              disabled={submitting}
              style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px', background: '#fff', cursor: 'pointer' }}
            >
              <option value="">-- Tất cả liên hệ nhanh --</option>
              {filterHouses.map(h => (
                <option key={h.id} value={h.id}>{h.name} • {h.address}</option>
              ))}
            </select>
          </div>
        )}

        {tab === 'search' && (
          <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <input
              value={email}
              onChange={event => setEmail(event.target.value)}
              placeholder="Nhập email để tìm..."
              disabled={submitting}
              onKeyDown={event => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  void handleSearch();
                }
              }}
              style={{ flex: 1, padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
            />
            <button
              type="button"
              onClick={() => void handleSearch()}
              disabled={submitting || searching}
              style={{
                padding: '8px 16px',
                backgroundColor: '#3b82f6',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              {searching ? 'Đang tìm...' : 'Tìm kiếm'}
            </button>
          </div>
        )}

        {error && (
          <div style={{ color: '#ef4444', fontSize: '13px', margin: '8px 0' }}>{error}</div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: '#475569', marginBottom: '6px' }}>
            {tab === 'quick' ? 'Gợi ý liên hệ nhanh' : 'Kết quả tìm kiếm'}
          </label>

          {loading ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#64748b' }}>Đang tải danh sách...</div>
          ) : currentList.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#64748b', border: '1px dashed #cbd5e1', borderRadius: '8px' }}>
              {tab === 'quick' ? 'Không có gợi ý nào.' : 'Không tìm thấy người dùng phù hợp.'}
            </div>
          ) : (
            <div className="chat-user-results" style={{ flex: 1, overflowY: 'auto', maxHeight: '240px' }}>
              {tab === 'quick' && (
                <button
                  type="button"
                  className="chat-user-results__select-all"
                  onClick={() => toggleAllVisible(currentList)}
                  disabled={submitting}
                >
                  {currentList.every(user => selected.some(item => item.userId === user.userId))
                    ? 'Bỏ chọn tất cả'
                    : `Chọn tất cả (${currentList.length})`}
                </button>
              )}
              {currentList.map(user => (
                <button
                  key={user.userId}
                  type="button"
                  onClick={() => toggle(user)}
                  className={selected.some(item => item.userId === user.userId) ? 'selected' : ''}
                  disabled={submitting}
                >
                  <Avatar name={user.displayName} url={user.avatarUrl} />
                  <span>
                    <strong>{user.displayName}</strong>
                    <small>
                      {user.email}
                      {user.contextLabel ? ` • ${user.contextLabel}` : ''}
                    </small>
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>

        {selected.length > 0 && (
          <div style={{ marginTop: '12px' }}>
            <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
              Đã chọn:
            </label>
            <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
              {selected.map(user => (
                <span
                  key={user.userId}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '3px 8px',
                    backgroundColor: '#e2e8f0',
                    borderRadius: '9999px',
                    fontSize: '12px',
                    color: '#334155'
                  }}
                >
                  {user.displayName}
                  <button
                    type="button"
                    onClick={() => toggle(user)}
                    style={{
                      border: 'none',
                      background: 'none',
                      color: '#64748b',
                      cursor: 'pointer',
                      fontSize: '12px',
                      padding: 0
                    }}
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
          </div>
        )}

        <footer style={{ marginTop: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: '13px', color: selected.length < 2 ? '#ef4444' : '#16a34a', fontWeight: 500 }}>
            {selected.length < 2
              ? `Cần chọn thêm ít nhất ${2 - selected.length} người khác để đủ 3 người (bao gồm bạn)`
              : `Đã chọn ${selected.length} người khác. Đủ điều kiện tạo nhóm.`}
          </span>
          <button
            type="button"
            onClick={() => void handleCreate()}
            disabled={selected.length < 2 || submitting}
            style={{
              padding: '8px 16px',
              backgroundColor: selected.length >= 2 ? '#3b82f6' : '#cbd5e1',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              cursor: selected.length >= 2 ? 'pointer' : 'not-allowed',
            }}
          >
            {submitting ? 'Đang tạo...' : 'Tạo nhóm'}
          </button>
        </footer>
      </section>
    </div>
  );
}

interface OwnerTransferModalProps {
  currentUserId: string;
  participants: ChatParticipant[];
  onClose: () => void;
  onSubmit: (targetUserId: string) => Promise<void>;
}

export function OwnerTransferModal({
  currentUserId,
  participants,
  onClose,
  onSubmit
}: OwnerTransferModalProps) {
  const [selectedUserId, setSelectedUserId] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const eligibleCandidates = participants.filter(
    p => p.userId !== currentUserId && !p.leftAt
  );

  const handleSubmit = async () => {
    if (!selectedUserId) {
      alert('Vui lòng chọn một thành viên để trao quyền trưởng nhóm.');
      return;
    }
    setSubmitting(true);
    try {
      await onSubmit(selectedUserId);
    } catch {
      // Error handled by caller
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal">
        <header>
          <h2>Trao quyền trưởng nhóm</h2>
          <button type="button" onClick={onClose} disabled={submitting}>Đóng</button>
        </header>

        <div style={{ margin: '16px 0' }}>
          <p style={{ fontSize: '14px', color: '#475569', marginBottom: '12px' }}>
            Bạn là trưởng nhóm duy nhất còn lại. Trước khi rời nhóm, bạn phải trao quyền trưởng nhóm cho một thành viên khác.
          </p>

          {eligibleCandidates.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#64748b', border: '1px dashed #cbd5e1', borderRadius: '8px' }}>
              Không có thành viên nào khác trong nhóm để trao quyền. Hãy thêm thành viên khác trước khi rời nhóm.
            </div>
          ) : (
            <div className="chat-user-results" style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: '240px', overflowY: 'auto' }}>
              {eligibleCandidates.map(user => (
                <button
                  key={user.userId}
                  type="button"
                  onClick={() => setSelectedUserId(user.userId)}
                  className={selectedUserId === user.userId ? 'selected' : ''}
                  disabled={submitting}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '12px',
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: '8px',
                    border: '1px solid ' + (selectedUserId === user.userId ? '#3b82f6' : '#cbd5e1'),
                    backgroundColor: selectedUserId === user.userId ? '#eff6ff' : '#fff',
                    textAlign: 'left',
                    cursor: 'pointer'
                  }}
                >
                  <Avatar name={user.displayName} url={user.avatarUrl} />
                  <div>
                    <strong style={{ display: 'block', fontSize: '14px', color: '#1e293b' }}>{user.displayName}</strong>
                    <span style={{ fontSize: '12px', color: '#64748b' }}>{user.email}</span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        <footer style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              backgroundColor: '#fff',
              cursor: 'pointer'
            }}
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={!selectedUserId || submitting}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              border: 'none',
              backgroundColor: selectedUserId ? '#3b82f6' : '#cbd5e1',
              color: '#fff',
              fontWeight: 600,
              cursor: selectedUserId ? 'pointer' : 'not-allowed'
            }}
          >
            {submitting ? 'Đang chuyển giao...' : 'Xác nhận & Rời nhóm'}
          </button>
        </footer>
      </section>
    </div>
  );
}
