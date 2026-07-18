import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { toAssetUrl } from '../../../shared/api/assets';
import {
  addParticipants,
  closeConversation,
  getConversation,
  getMessages,
  leaveConversation,
  markConversationRead,
  removeParticipant,
  sendMessage,
  uploadChatImage,
  uploadChatFile,
  deleteChatMessage
} from '../api';
import type { ChatMessage, Conversation, SendChatMessageRequest } from '../types';
import { useChatHub } from '../useChatHub';
import { UserSearchModal, MemberPanel } from './ChatSubComponents';
import { MessageBubbleItem } from './MessageBubbleItem';
import './ChatWindow.css';

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

interface ChatWindowProps {
  conversationId: string;
  mode?: 'page' | 'bubble';
  onCloseBubble?: () => void;
  onMinimizeBubble?: () => void;
  onConversationUpdated?: (conversation: Conversation) => void;
  isActive?: boolean;
}

export function ChatWindow({
  conversationId,
  mode = 'page',
  onCloseBubble,
  onMinimizeBubble,
  onConversationUpdated,
  isActive = true
}: ChatWindowProps) {
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [activeConversation, setActiveConversation] = useState<Conversation | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [composer, setComposer] = useState('');
  const [imageUploading, setImageUploading] = useState(false);
  const [fileUploading, setFileUploading] = useState(false);
  const [showMembers, setShowMembers] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const listEndRef = useRef<HTMLDivElement | null>(null);
  const prevConversationIdRef = useRef<string | null>(null);

  const isLandlord = currentUser?.roles.includes('Landlord') ?? false;

  const upsertMessage = useCallback((message: ChatMessage) => {
    if (message.conversationId !== conversationId) return;

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
  }, [conversationId]);

  const handleIncomingMessage = useCallback((message: ChatMessage) => {
    upsertMessage(message);
    if (onConversationUpdated) {
      void getConversation(conversationId)
        .then(onConversationUpdated)
        .catch(() => undefined);
    }
  }, [upsertMessage, conversationId, onConversationUpdated]);

  const handleConversationUpdated = useCallback((conversation: Conversation) => {
    if (conversation.id === conversationId) {
      setActiveConversation(conversation);
    }
    if (onConversationUpdated) {
      onConversationUpdated(conversation);
    }
  }, [conversationId, onConversationUpdated]);

  const handleParticipantRemoved = useCallback((cId: string) => {
    if (cId === conversationId) {
      setActiveConversation(null);
      setMessages([]);
    }
  }, [conversationId]);

  const handleConversationClosed = useCallback((conversation: Conversation) => {
    if (conversation.id === conversationId) {
      setActiveConversation(conversation);
    }
  }, [conversationId]);

  const loadConversationDetails = useCallback(async () => {
    setMessagesLoading(true);
    try {
      let found: Conversation | null = null;
      try {
        found = await getConversation(conversationId);
      } catch (err) {
        // Fallback Conversation object
        found = {
          id: conversationId,
          type: 'Direct',
          title: 'Trò chuyện',
          createdByUserId: '',
          unreadCount: 0,
          isClosed: false,
          isCurrentUserOwner: false,
          hasCurrentUserLeft: false,
          participants: []
        };
      }
      setActiveConversation(found);

      const items = await getMessages(conversationId);
      setMessages(items.map(item => ({ ...item, status: 'sent' })));

      if (isActive) {
        await markConversationRead(conversationId);
        if (onConversationUpdated && found) {
          onConversationUpdated({ ...found, unreadCount: 0 });
        }
      } else {
        if (onConversationUpdated && found) {
          onConversationUpdated(found);
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không tải được lịch sử tin nhắn.');
    } finally {
      setMessagesLoading(false);
    }
  }, [conversationId, onConversationUpdated, isActive]);

  const handleMessageDeleted = useCallback((message: ChatMessage) => {
    if (message.conversationId !== conversationId) return;
    setMessages(prev => prev.map(item => item.id === message.id ? { ...item, ...message } : item));
  }, [conversationId]);

  const handleMessageCreated = useCallback((payload: { message: ChatMessage; conversation: Conversation }) => {
    if (payload.conversation.id === conversationId) {
      setActiveConversation(payload.conversation);
    }
    if (onConversationUpdated) {
      onConversationUpdated(payload.conversation);
    }

    if (payload.message.conversationId === conversationId) {
      upsertMessage(payload.message);
      if (payload.message.senderId !== currentUser?.userId && isActive) {
        void markConversationRead(conversationId)
          .then(c => {
            setActiveConversation(c);
            if (onConversationUpdated) onConversationUpdated(c);
          })
          .catch(() => undefined);
      }
    }
  }, [conversationId, upsertMessage, onConversationUpdated, currentUser, isActive]);

  const handleReconnected = useCallback(() => {
    void loadConversationDetails();
  }, [loadConversationDetails]);

  const hub = useChatHub({
    currentConversationId: conversationId,
    onMessage: handleIncomingMessage,
    onConversationUpdated: handleConversationUpdated,
    onParticipantRemoved: handleParticipantRemoved,
    onConversationClosed: handleConversationClosed,
    onMessageDeleted: handleMessageDeleted,
    onMessageCreated: handleMessageCreated,
    onReconnected: handleReconnected
  });

  useEffect(() => {
    void loadConversationDetails();
  }, [conversationId, loadConversationDetails]);

  useEffect(() => {
    if (!hub.isConnected) return;
    void hub.joinConversation(conversationId);
    return () => {
      void hub.leaveConversation(conversationId);
    };
  }, [conversationId, hub.isConnected, hub.joinConversation, hub.leaveConversation]);

  useEffect(() => {
    if (isActive && conversationId) {
      void markConversationRead(conversationId)
        .then(c => {
          setActiveConversation(c);
          if (onConversationUpdated) onConversationUpdated({ ...c, unreadCount: 0 });
        })
        .catch(() => undefined);
    }
  }, [isActive, conversationId, onConversationUpdated]);

  const scrollToBottom = useCallback((behavior: ScrollBehavior = 'smooth') => {
    listEndRef.current?.scrollIntoView({ behavior, block: 'end' });
  }, []);

  useEffect(() => {
    if (isActive) {
      const isNewConversation = prevConversationIdRef.current !== conversationId;
      prevConversationIdRef.current = conversationId;
      const behavior = isNewConversation ? 'auto' : 'smooth';

      scrollToBottom(behavior);
      const t1 = window.setTimeout(() => scrollToBottom(behavior), 100);
      const t2 = window.setTimeout(() => scrollToBottom(behavior), 300);
      return () => {
        window.clearTimeout(t1);
        window.clearTimeout(t2);
      };
    }
  }, [messages.length, conversationId, isActive, scrollToBottom]);

  async function handleSend(request: SendChatMessageRequest) {
    if (!currentUser) return;

    const clientMessageId = request.clientMessageId ?? crypto.randomUUID();
    const optimistic: ChatMessage = {
      id: clientMessageId,
      conversationId: conversationId,
      senderId: currentUser.userId,
      senderName: currentUser.displayName,
      messageType: request.messageType,
      content: request.content,
      mediaAssetId: request.mediaAssetId,
      imageUrl: request.imageUrl,
      fileUrl: request.fileUrl,
      fileName: request.fileName,
      fileContentType: request.fileContentType,
      fileSize: request.fileSize,
      clientMessageId,
      createdAt: new Date().toISOString(),
      status: 'sending'
    };

    setMessages(prev => [...prev, optimistic]);

    try {
      const payload = { ...request, clientMessageId };
      const saved = hub.isConnected
        ? await hub.sendHubMessage(conversationId, payload)
        : await sendMessage(conversationId, payload);

      upsertMessage(saved);
      setComposer('');
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
      const uploaded = await uploadChatImage(file);
      await handleSend({ messageType: 'Image', mediaAssetId: uploaded.mediaAssetId });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Tải ảnh thất bại.');
    } finally {
      setImageUploading(false);
    }
  }

  async function handleFileSelected(file: File | null) {
    if (!file) return;
    setFileUploading(true);
    try {
      const uploaded = await uploadChatFile(file);
      await handleSend({
        messageType: 'File',
        mediaAssetId: uploaded.mediaAssetId,
        fileName: uploaded.fileName,
        fileContentType: uploaded.contentType,
        fileSize: uploaded.size
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Tải tệp tin thất bại.');
    } finally {
      setFileUploading(false);
    }
  }

  async function handleDeleteMessage(messageId: string) {
    try {
      const deletedMessage = await deleteChatMessage(conversationId, messageId);
      setMessages(prev => prev.map(item => item.id === messageId ? { ...item, ...deletedMessage } : item));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Xóa tin nhắn thất bại.');
    }
  }

  async function handleLeaveGroup() {
    if (!activeConversation) return;
    const updated = await leaveConversation(activeConversation.id);
    setActiveConversation(updated);
    setMessages([]);
  }

  async function handleCloseGroup() {
    if (!activeConversation) return;
    const updated = await closeConversation(activeConversation.id);
    setActiveConversation(updated);
  }

  async function handleRemoveMember(userId: string) {
    if (!activeConversation) return;
    const updated = await removeParticipant(activeConversation.id, userId);
    setActiveConversation(updated);
  }

  async function handleAddMembers(userIds: string[]) {
    if (!activeConversation || userIds.length === 0) return;
    const updated = await addParticipants(activeConversation.id, userIds);
    setActiveConversation(updated);
  }

  if (!activeConversation) {
    return (
      <div className={`chat-main__empty chat-window--${mode}`}>
        {messagesLoading ? 'Đang tải tin nhắn...' : 'Chọn một cuộc trò chuyện để bắt đầu.'}
      </div>
    );
  }

  return (
    <section className={`chat-main chat-window--${mode}`}>
      <header className={`chat-main__header ${mode === 'bubble' ? 'bubble-header' : ''}`}>
        <div className="chat-main__title">
          <Avatar
            name={activeConversation.title}
            url={activeConversation.type === 'Group'
              ? activeConversation.avatarUrl
              : activeConversation.participants.find(p => p.userId !== currentUser?.userId)?.avatarUrl}
          />
          <div>
            <h2 className={mode === 'bubble' ? 'bubble-title' : ''}>{activeConversation.title}</h2>
            {mode !== 'bubble' && (
              <p>{activeConversation.type === 'Group' ? `${activeConversation.participants.filter(p => !p.leftAt).length} thành viên` : 'Tin nhắn riêng'}</p>
            )}
          </div>
        </div>
        <div className="chat-main__tools">
          {mode === 'bubble' ? (
            <div className="bubble-actions">
              <button
                type="button"
                className="bubble-action-btn detail"
                onClick={() => {
                  if (onCloseBubble) onCloseBubble();
                  navigate(`${ROUTE_PATHS.MESSAGES}?conversationId=${conversationId}`);
                }}
                title="Mở trong trang tin nhắn"
              >
                •••
              </button>
              <button
                type="button"
                className="bubble-action-btn minimize"
                onClick={onMinimizeBubble}
                title="Thu nhỏ"
              >
                —
              </button>
              <button
                type="button"
                className="bubble-action-btn close"
                onClick={onCloseBubble}
                title="Đóng chat"
              >
                ×
              </button>
            </div>
          ) : (
            <>
              {activeConversation.type === 'Group' && (
                <button type="button" onClick={() => setShowMembers(value => !value)}>Thành viên</button>
              )}
            </>
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
              <MessageBubbleItem
                key={message.id}
                message={message}
                mine={message.senderId === currentUser?.userId}
                onDelete={handleDeleteMessage}
              />
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
            <div className="chat-composer__bar">
              <label className="chat-image-button" title="Gửi ảnh">
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.webp,.gif,image/*"
                  disabled={imageUploading || fileUploading}
                  onChange={event => {
                    const file = event.target.files?.[0] ?? null;
                    event.currentTarget.value = '';
                    void handleImageSelected(file);
                  }}
                />
                {imageUploading ? (
                  <span className="chat-upload-spinner" />
                ) : (
                  <svg style={{ width: '18px', height: '18px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                    <circle cx="8.5" cy="8.5" r="1.5" />
                    <polyline points="21 15 16 10 5 21" />
                  </svg>
                )}
              </label>
              <label className="chat-file-button" title="Gửi tệp">
                <input
                  type="file"
                  accept="*"
                  disabled={imageUploading || fileUploading}
                  onChange={event => {
                    const file = event.target.files?.[0] ?? null;
                    event.currentTarget.value = '';
                    void handleFileSelected(file);
                  }}
                />
                {fileUploading ? (
                  <span className="chat-upload-spinner" />
                ) : (
                  <svg style={{ width: '18px', height: '18px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                    <line x1="16" y1="13" x2="8" y2="13" />
                    <line x1="16" y1="17" x2="8" y2="17" />
                    <polyline points="10 9 9 9 8 9" />
                  </svg>
                )}
              </label>
              <div className="chat-composer__input-container">
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
                  disabled={imageUploading || fileUploading}
                />
              </div>
              <button
                type="button"
                onClick={() => void handleTextSubmit()}
                disabled={!composer.trim() || imageUploading || fileUploading}
                className="chat-composer__send-btn"
                title="Gửi tin nhắn"
              >
                <svg style={{ width: '16px', height: '16px', marginLeft: '1px' }} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="22" y1="2" x2="11" y2="13" />
                  <polygon points="22 2 15 22 11 13 2 9 22 2" />
                </svg>
              </button>
            </div>
          </>
        )}
      </footer>

      {error && (
        <button className="chat-toast" type="button" onClick={() => setError(null)}>
          {error}
        </button>
      )}
    </section>
  );
}
