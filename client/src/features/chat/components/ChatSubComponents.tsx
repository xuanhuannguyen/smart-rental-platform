import { useEffect, useState } from 'react';
import { toAssetUrl } from '../../../shared/api/assets';
import { searchChatUsers } from '../api';
import type { ChatUser, Conversation } from '../types';

function Avatar({ name, url }: { name: string; url?: string | null }) {
  if (url) {
    return <img className="chat-avatar" src={toAssetUrl(url)} alt={name} />;
  }
  return <div className="chat-avatar chat-avatar--placeholder">{name.trim().charAt(0).toUpperCase() || 'U'}</div>;
}

interface UserSearchModalProps {
  title: string;
  submitLabel: string;
  onClose: () => void;
  onSubmit: (userIds: string[], groupName?: string) => Promise<void>;
  multiSelect?: boolean;
  showGroupName?: boolean;
}

export function UserSearchModal({ title, submitLabel, onClose, onSubmit, multiSelect, showGroupName }: UserSearchModalProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<ChatUser[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [groupName, setGroupName] = useState('');

  useEffect(() => {
    if (query.trim().length < 2) {
      setResults([]);
      return;
    }

    const handle = window.setTimeout(() => {
      void searchChatUsers(query, null).then(setResults).catch(() => setResults([]));
    }, 300);
    return () => window.clearTimeout(handle);
  }, [query]);

  function toggle(userId: string) {
    if (!multiSelect) {
      setSelectedIds([userId]);
      return;
    }
    setSelectedIds(prev =>
      prev.includes(userId) ? prev.filter(id => id !== userId) : [...prev, userId]
    );
  }

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal">
        <header>
          <h2>{title}</h2>
          <button type="button" onClick={onClose}>Đóng</button>
        </header>
        {showGroupName && (
          <input
            value={groupName}
            onChange={e => setGroupName(e.target.value)}
            placeholder="Tên nhóm (bắt buộc)"
            style={{ marginBottom: 8 }}
          />
        )}
        <input
          value={query}
          onChange={event => setQuery(event.target.value)}
          placeholder="Tìm bằng email hoặc tên..."
        />
        <div className="chat-user-results">
          {results.map(user => (
            <button
              key={user.userId}
              type="button"
              onClick={() => toggle(user.userId)}
              className={selectedIds.includes(user.userId) ? 'selected' : ''}
            >
              <Avatar name={user.displayName} url={user.avatarUrl} />
              <span>
                <strong>{user.displayName}</strong>
                <small>{user.email}{user.contextLabel ? ` · ${user.contextLabel}` : ''}</small>
              </span>
            </button>
          ))}
        </div>
        <footer>
          <span>{selectedIds.length} đã chọn</span>
          <button
            type="button"
            disabled={selectedIds.length === 0 || (showGroupName ? false : false)}
            onClick={() => void onSubmit(selectedIds, showGroupName ? groupName || undefined : undefined)}
          >
            {submitLabel}
          </button>
        </footer>
      </section>
    </div>
  );
}

export function MemberPanel({
  conversation,
  currentUserId,
  onLeave,
  onClose,
  onRemove,
  onAdd
}: {
  conversation: Conversation;
  currentUserId: string;
  onLeave: () => Promise<void>;
  onClose: () => Promise<void>;
  onRemove: (userId: string) => Promise<void>;
  onAdd: (userIds: string[]) => Promise<void>;
}) {
  const [showAdd, setShowAdd] = useState(false);

  return (
    <aside className="member-panel">
      <header>
        <h3>Thành viên</h3>
        {conversation.isCurrentUserOwner && (
          <button type="button" onClick={() => setShowAdd(true)}>Thêm</button>
        )}
      </header>
      <div className="member-list">
        {conversation.participants.map(participant => (
          <div key={participant.userId} className={participant.leftAt ? 'left' : ''}>
            <Avatar name={participant.displayName} url={participant.avatarUrl} />
            <span>
              <strong>{participant.displayName}</strong>
              <small>{participant.role}{participant.leftAt ? ' · Đã rời' : ''}</small>
            </span>
            {conversation.isCurrentUserOwner && participant.userId !== currentUserId && !participant.leftAt && (
              <button type="button" onClick={() => void onRemove(participant.userId)}>Xóa</button>
            )}
          </div>
        ))}
      </div>
      <footer>
        {conversation.isCurrentUserOwner ? (
          <button type="button" onClick={() => void onClose()} disabled={conversation.isClosed}>Đóng nhóm</button>
        ) : (
          <button type="button" onClick={() => void onLeave()}>Rời nhóm</button>
        )}
      </footer>
      {showAdd && (
        <UserSearchModal
          title="Thêm thành viên"
          submitLabel="Thêm"
          multiSelect
          onClose={() => setShowAdd(false)}
          onSubmit={async userIds => {
            await onAdd(userIds);
            setShowAdd(false);
          }}
        />
      )}
    </aside>
  );
}
