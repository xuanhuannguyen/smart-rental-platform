import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { toAssetUrl } from '../../../shared/api/assets';
import {
  addParticipants,
  closeConversation,
  createDirectConversation,
  createGroupConversation,
  getConversations,
  getMessages,
  getQuickContacts,
  leaveConversation,
  markConversationRead,
  removeParticipant,
  searchChatUsers,
  sendMessage,
  uploadChatImage
} from '../api';
import type { ChatMessage, ChatUser, Conversation, SendChatMessageRequest } from '../types';
import { useChatHub } from '../useChatHub';
import './ChatPage.css';

const quickEmojis = ['👍', '❤️', '😊', '🙏', '👌', '🔥', '🎉', '✅'];

function formatTime(value?: string | null) {
  if (!value) return '';
  return new Date(value).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

function Avatar({ name, url }: { name: string; url?: string | null }) {
  if (url) {
    return <img className="chat-avatar" src={toAssetUrl(url)} alt={name} />;
  }

  return <div className="chat-avatar chat-avatar--placeholder">{name.trim().charAt(0).toUpperCase() || 'U'}</div>;
}

export function ChatPage() {
  const { currentUser } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeId, setActiveId] = useState<string | null>(searchParams.get('conversationId'));
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [composer, setComposer] = useState('');
  const [imageUploading, setImageUploading] = useState(false);
  const [showDirectModal, setShowDirectModal] = useState(false);
  const [showGroupModal, setShowGroupModal] = useState(false);
  const [showMembers, setShowMembers] = useState(false);
  const listEndRef = useRef<HTMLDivElement | null>(null);

  const activeConversation = useMemo(
    () => conversations.find(item => item.id === activeId) ?? null,
    [conversations, activeId]
  );
  const isLandlord = currentUser?.roles.includes('Landlord') ?? false;

  const upsertConversation = useCallback((conversation: Conversation) => {
    setConversations(prev => {
      const exists = prev.some(item => item.id === conversation.id);
      const next = exists
        ? prev.map(item => (item.id === conversation.id ? { ...item, ...conversation } : item))
        : [conversation, ...prev];
      return next.sort((a, b) => {
        const left = new Date(a.lastMessageAt ?? '1970-01-01').getTime();
        const right = new Date(b.lastMessageAt ?? '1970-01-01').getTime();
        return right - left;
      });
    });
  }, []);

  const upsertMessage = useCallback((message: ChatMessage) => {
    if (message.conversationId !== activeId) return;

    setMessages(prev => {
      const normalized = { ...message, status: 'sent' as const };
      if (prev.some(item => item.id === message.id)) {
        return prev.map(item => item.id === message.id ? normalized : item);
      }
      if (message.clientMessageId) {
        const replaced = prev.map(item => item.clientMessageId === message.clientMessageId ? normalized : item);
        if (replaced.some(item => item.id === message.id)) return replaced;
      }
      return [...prev, normalized];
    });
  }, [activeId]);

  const handleIncomingMessage = useCallback((message: ChatMessage) => {
    upsertMessage(message);
  }, [upsertMessage]);

  const handleConversationUpdated = useCallback((conversation: Conversation) => {
    upsertConversation(conversation);
  }, [upsertConversation]);

  const handleParticipantRemoved = useCallback((conversationId: string) => {
    setConversations(prev => prev.filter(item => item.id !== conversationId || item.id !== activeId));
    if (conversationId === activeId) {
      setActiveId(null);
      setMessages([]);
    }
    void loadConversations();
  }, [activeId]);

  const handleConversationClosed = useCallback((conversation: Conversation) => {
    upsertConversation(conversation);
  }, [upsertConversation]);

  const hub = useChatHub({
    currentConversationId: activeId,
    onMessage: handleIncomingMessage,
    onConversationUpdated: handleConversationUpdated,
    onParticipantRemoved: handleParticipantRemoved,
    onConversationClosed: handleConversationClosed
  });

  const loadConversations = useCallback(async () => {
    setLoading(true);
    try {
      const items = await getConversations();
      setConversations(items);
      if (!activeId && items.length > 0) {
        setActiveId(items[0].id);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không tải được danh sách tin nhắn.');
    } finally {
      setLoading(false);
    }
  }, [activeId]);

  useEffect(() => {
    void loadConversations();
  }, [loadConversations]);

  useEffect(() => {
    if (!activeId) return;
    setSearchParams({ conversationId: activeId });
    setMessagesLoading(true);
    void getMessages(activeId)
      .then(items => {
        setMessages(items.map(item => ({ ...item, status: 'sent' })));
        return hub.joinConversation(activeId);
      })
      .then(() => markConversationRead(activeId))
      .then(upsertConversation)
      .catch(err => setError(err instanceof Error ? err.message : 'Không tải được lịch sử tin nhắn.'))
      .finally(() => setMessagesLoading(false));

    return () => {
      void hub.leaveConversation(activeId);
    };
  }, [activeId, hub.joinConversation, hub.leaveConversation, setSearchParams, upsertConversation]);

  useEffect(() => {
    listEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages.length, activeId]);

  async function handleSend(request: SendChatMessageRequest) {
    if (!activeId || !currentUser) return;

    const clientMessageId = request.clientMessageId ?? crypto.randomUUID();
    const optimistic: ChatMessage = {
      id: clientMessageId,
      conversationId: activeId,
      senderId: currentUser.userId,
      senderName: currentUser.displayName,
      messageType: request.messageType,
      content: request.content,
      imageUrl: request.imageUrl,
      clientMessageId,
      createdAt: new Date().toISOString(),
      status: 'sending'
    };

    setMessages(prev => [...prev, optimistic]);

    try {
      const payload = { ...request, clientMessageId };
      const saved = hub.isConnected
        ? await hub.sendHubMessage(activeId, payload)
        : await sendMessage(activeId, payload);

      upsertMessage(saved);
      setComposer('');
      await loadConversations();
    } catch (err) {
      setMessages(prev => prev.map(item => item.clientMessageId === clientMessageId ? { ...item, status: 'error' } : item));
      setError(err instanceof Error ? err.message : 'Gửi tin nhắn thất bại.');
    }
  }

  async function handleTextSubmit() {
    const value = composer.trim();
    if (!value) return;
    await handleSend({ messageType: 'Text', content: value });
  }

  async function handleImageSelected(file: File | null) {
    if (!file) return;
    setImageUploading(true);
    try {
      const imageUrl = await uploadChatImage(file);
      await handleSend({ messageType: 'Image', imageUrl });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Tải ảnh thất bại.');
    } finally {
      setImageUploading(false);
    }
  }

  async function handleLeaveGroup() {
    if (!activeConversation) return;
    const updated = await leaveConversation(activeConversation.id);
    upsertConversation(updated);
    setActiveId(null);
    setMessages([]);
  }

  async function handleCloseGroup() {
    if (!activeConversation) return;
    const updated = await closeConversation(activeConversation.id);
    upsertConversation(updated);
  }

  async function handleRemoveMember(userId: string) {
    if (!activeConversation) return;
    const updated = await removeParticipant(activeConversation.id, userId);
    upsertConversation(updated);
  }

  async function handleAddMembers(userIds: string[]) {
    if (!activeConversation || userIds.length === 0) return;
    const updated = await addParticipants(activeConversation.id, userIds);
    upsertConversation(updated);
  }

  return (
    <main className="chat-page">
      <section className="chat-shell">
        <aside className="chat-inbox">
          <header className="chat-inbox__header">
            <div>
              <h1>Tin nhắn</h1>
              <p>{hub.isConnected ? 'Đang kết nối realtime' : 'Đang dùng chế độ tải lại'}</p>
            </div>
            <div className="chat-inbox__actions">
              <button type="button" onClick={() => setShowDirectModal(true)}>1-1</button>
              {isLandlord && <button type="button" onClick={() => setShowGroupModal(true)}>Nhóm</button>}
            </div>
          </header>

          {loading ? (
            <div className="chat-empty">Đang tải...</div>
          ) : conversations.length === 0 ? (
            <div className="chat-empty">Chưa có cuộc trò chuyện.</div>
          ) : (
            <div className="chat-conversation-list">
              {conversations.map(item => (
                <button
                  key={item.id}
                  type="button"
                  className={`chat-conversation ${item.id === activeId ? 'active' : ''}`}
                  onClick={() => setActiveId(item.id)}
                >
                  <Avatar name={item.title} url={item.participants.find(p => p.userId !== currentUser?.userId)?.avatarUrl} />
                  <span className="chat-conversation__body">
                    <span className="chat-conversation__title">{item.title}</span>
                    <span className="chat-conversation__preview">{item.lastMessagePreview || (item.type === 'Group' ? 'Nhóm chat' : 'Tin nhắn riêng')}</span>
                  </span>
                  <span className="chat-conversation__meta">
                    <span>{formatTime(item.lastMessageAt)}</span>
                    {item.unreadCount > 0 && <strong>{item.unreadCount}</strong>}
                  </span>
                </button>
              ))}
            </div>
          )}
        </aside>

        <section className="chat-main">
          {activeConversation ? (
            <>
              <header className="chat-main__header">
                <div className="chat-main__title">
                  <Avatar name={activeConversation.title} url={activeConversation.participants.find(p => p.userId !== currentUser?.userId)?.avatarUrl} />
                  <div>
                    <h2>{activeConversation.title}</h2>
                    <p>{activeConversation.type === 'Group' ? `${activeConversation.participants.filter(p => !p.leftAt).length} thành viên` : 'Tin nhắn riêng'}</p>
                  </div>
                </div>
                <div className="chat-main__tools">
                  {activeConversation.type === 'Group' && (
                    <button type="button" onClick={() => setShowMembers(value => !value)}>Thành viên</button>
                  )}
                </div>
              </header>

              <div className="chat-content">
                <div className="chat-messages">
                  {messagesLoading ? (
                    <div className="chat-empty">Đang tải tin nhắn...</div>
                  ) : messages.length === 0 ? (
                    <div className="chat-empty">Bắt đầu cuộc trò chuyện.</div>
                  ) : (
                    messages.map(message => (
                      <MessageBubble key={message.id} message={message} mine={message.senderId === currentUser?.userId} />
                    ))
                  )}
                  <div ref={listEndRef} />
                </div>

                {showMembers && activeConversation.type === 'Group' && (
                  <MemberPanel
                    conversation={activeConversation}
                    currentUserId={currentUser?.userId ?? ''}
                    onLeave={handleLeaveGroup}
                    onClose={handleCloseGroup}
                    onRemove={handleRemoveMember}
                    onAdd={handleAddMembers}
                  />
                )}
              </div>

              <footer className="chat-composer">
                {activeConversation.isClosed || activeConversation.hasCurrentUserLeft ? (
                  <div className="chat-composer__disabled">Cuộc trò chuyện đã đóng hoặc bạn đã rời nhóm.</div>
                ) : (
                  <>
                    <div className="chat-emoji-row">
                      {quickEmojis.map(icon => (
                        <button key={icon} type="button" onClick={() => handleSend({ messageType: 'Icon', content: icon })}>{icon}</button>
                      ))}
                    </div>
                    <div className="chat-composer__bar">
                      <label className="chat-image-button">
                        <input
                          type="file"
                          accept=".jpg,.jpeg,.png,.webp,.gif,image/*"
                          disabled={imageUploading}
                          onChange={event => {
                            const file = event.target.files?.[0] ?? null;
                            event.currentTarget.value = '';
                            void handleImageSelected(file);
                          }}
                        />
                        {imageUploading ? '...' : 'Ảnh'}
                      </label>
                      <textarea
                        value={composer}
                        onChange={event => setComposer(event.target.value)}
                        placeholder="Nhập tin nhắn..."
                        rows={1}
                        onKeyDown={event => {
                          if (event.key === 'Enter' && !event.shiftKey) {
                            event.preventDefault();
                            void handleTextSubmit();
                          }
                        }}
                      />
                      <button type="button" onClick={() => void handleTextSubmit()} disabled={!composer.trim()}>Gửi</button>
                    </div>
                  </>
                )}
              </footer>
            </>
          ) : (
            <div className="chat-main__empty">Chọn một cuộc trò chuyện để bắt đầu.</div>
          )}
        </section>
      </section>

      {error && (
        <button className="chat-toast" type="button" onClick={() => setError(null)}>
          {error}
        </button>
      )}

      {showDirectModal && (
        <UserSearchModal
          title="Tạo chat 1-1"
          submitLabel="Bắt đầu"
          onClose={() => setShowDirectModal(false)}
          onSubmit={async users => {
            if (users[0]) {
              const conversation = await createDirectConversation(users[0].userId);
              upsertConversation(conversation);
              setActiveId(conversation.id);
            }
            setShowDirectModal(false);
          }}
          single
        />
      )}

      {showGroupModal && (
        <CreateGroupModal
          onClose={() => setShowGroupModal(false)}
          onSubmit={async (title, users) => {
            const conversation = await createGroupConversation(title, users.map(user => user.userId));
            upsertConversation(conversation);
            setActiveId(conversation.id);
            setShowGroupModal(false);
          }}
        />
      )}
    </main>
  );
}

function MessageBubble({ message, mine }: { message: ChatMessage; mine: boolean }) {
  return (
    <div className={`message-row ${mine ? 'mine' : ''}`}>
      <div className={`message-bubble message-bubble--${message.messageType.toLowerCase()}`}>
        {!mine && <span className="message-sender">{message.senderName}</span>}
        {message.messageType === 'Image' && message.imageUrl ? (
          <img src={toAssetUrl(message.imageUrl)} alt="Ảnh chat" />
        ) : (
          <span>{message.content}</span>
        )}
        <small>{formatTime(message.createdAt)} {message.status === 'sending' ? '• Đang gửi' : message.status === 'error' ? '• Lỗi' : ''}</small>
      </div>
    </div>
  );
}

function UserSearchModal({
  title,
  submitLabel,
  onClose,
  onSubmit,
  single
}: {
  title: string;
  submitLabel: string;
  onClose: () => void;
  onSubmit: (users: ChatUser[]) => Promise<void>;
  single?: boolean;
}) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<ChatUser[]>([]);
  const [selected, setSelected] = useState<ChatUser[]>([]);

  useEffect(() => {
    if (query.trim().length < 3) {
      setResults([]);
      return;
    }

    const handle = window.setTimeout(() => {
      void searchChatUsers(query).then(setResults).catch(() => setResults([]));
    }, 250);
    return () => window.clearTimeout(handle);
  }, [query]);

  function toggle(user: ChatUser) {
    if (single) {
      setSelected([user]);
      return;
    }
    setSelected(prev => prev.some(item => item.userId === user.userId)
      ? prev.filter(item => item.userId !== user.userId)
      : [...prev, user]);
  }

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal">
        <header>
          <h2>{title}</h2>
          <button type="button" onClick={onClose}>Đóng</button>
        </header>
        <input value={query} onChange={event => setQuery(event.target.value)} placeholder="Tìm bằng email..." />
        <div className="chat-user-results">
          {results.map(user => (
            <button key={user.userId} type="button" onClick={() => toggle(user)} className={selected.some(item => item.userId === user.userId) ? 'selected' : ''}>
              <Avatar name={user.displayName} url={user.avatarUrl} />
              <span>
                <strong>{user.displayName}</strong>
                <small>{user.email}{user.contextLabel ? ` • ${user.contextLabel}` : ''}</small>
              </span>
            </button>
          ))}
        </div>
        <footer>
          <span>{selected.length} đã chọn</span>
          <button type="button" disabled={selected.length === 0} onClick={() => void onSubmit(selected)}>{submitLabel}</button>
        </footer>
      </section>
    </div>
  );
}

function CreateGroupModal({ onClose, onSubmit }: { onClose: () => void; onSubmit: (title: string, users: ChatUser[]) => Promise<void> }) {
  const [title, setTitle] = useState('');
  const [quickContacts, setQuickContacts] = useState<ChatUser[]>([]);
  const [selected, setSelected] = useState<ChatUser[]>([]);
  const [email, setEmail] = useState('');
  const [searchResults, setSearchResults] = useState<ChatUser[]>([]);

  useEffect(() => {
    void getQuickContacts().then(setQuickContacts).catch(() => setQuickContacts([]));
  }, []);

  useEffect(() => {
    if (email.trim().length < 3) {
      setSearchResults([]);
      return;
    }

    const handle = window.setTimeout(() => {
      void searchChatUsers(email).then(setSearchResults).catch(() => setSearchResults([]));
    }, 250);
    return () => window.clearTimeout(handle);
  }, [email]);

  function toggle(user: ChatUser) {
    setSelected(prev => prev.some(item => item.userId === user.userId)
      ? prev.filter(item => item.userId !== user.userId)
      : [...prev, user]);
  }

  const combined = [...quickContacts, ...searchResults].filter((user, index, arr) => arr.findIndex(item => item.userId === user.userId) === index);

  return (
    <div className="chat-modal-backdrop">
      <section className="chat-modal chat-modal--wide">
        <header>
          <h2>Tạo nhóm chat</h2>
          <button type="button" onClick={onClose}>Đóng</button>
        </header>
        <input value={title} onChange={event => setTitle(event.target.value)} placeholder="Tên nhóm" />
        <input value={email} onChange={event => setEmail(event.target.value)} placeholder="Tìm thêm bằng email..." />
        <div className="chat-user-results">
          {combined.map(user => (
            <button key={user.userId} type="button" onClick={() => toggle(user)} className={selected.some(item => item.userId === user.userId) ? 'selected' : ''}>
              <Avatar name={user.displayName} url={user.avatarUrl} />
              <span>
                <strong>{user.displayName}</strong>
                <small>{user.email}{user.contextLabel ? ` • ${user.contextLabel}` : ''}</small>
              </span>
            </button>
          ))}
        </div>
        <footer>
          <span>{selected.length} thành viên</span>
          <button type="button" onClick={() => void onSubmit(title || 'Nhóm trò chuyện', selected)}>Tạo nhóm</button>
        </footer>
      </section>
    </div>
  );
}

function MemberPanel({
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
        {conversation.isCurrentUserOwner && <button type="button" onClick={() => setShowAdd(true)}>Thêm</button>}
      </header>
      <div className="member-list">
        {conversation.participants.map(participant => (
          <div key={participant.userId} className={participant.leftAt ? 'left' : ''}>
            <Avatar name={participant.displayName} url={participant.avatarUrl} />
            <span>
              <strong>{participant.displayName}</strong>
              <small>{participant.role}{participant.leftAt ? ' • Đã rời' : ''}</small>
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
          onClose={() => setShowAdd(false)}
          onSubmit={async users => {
            await onAdd(users.map(user => user.userId));
            setShowAdd(false);
          }}
        />
      )}
    </aside>
  );
}
