import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { toAssetUrl } from '../../../shared/api/assets';
import {
  addParticipants,
  clearConversation,
  closeConversation,
  createDirectConversation,
  createGroupConversation,
  getConversations,
  getMessages,
  leaveConversation,
  markConversationRead,
  removeParticipant,
  sendMessage,
  uploadChatFile,
  uploadChatImage,
  uploadChatAvatar,
  downloadChatFile,
  createJoinRequest,
  getJoinRequests,
  approveJoinRequest,
  rejectJoinRequest,
  updateApprovalSettings,
  updateParticipantRole,
  searchChatUsers,
  getConversationCounts,
  acceptContactRequest,
  rejectContactRequest,
  updateConversation
} from '../api';
import { ChatComposer } from '../components/ChatComposer';
import { CreateGroupModal, OwnerTransferModal, UserSearchModal } from '../components/ChatModals';
import { ChatDetailsPanel } from '../components/ChatDetailsPanel';
import { PrivateChatImage, usePrivateChatMediaObjectUrl } from '../components/PrivateChatImage';
import type { ChatMessage, Conversation, SendChatMessageRequest, ConversationJoinRequest, ChatParticipant, ChatUser } from '../types';
import { useChatHub } from '../useChatHub';
import './ChatPage.css';

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

function formatFileSize(bytes?: number | null): string {
  if (!bytes) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function ChatPage() {
  const { currentUser } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeId, setActiveId] = useState<string | null>(searchParams.get('conversationId'));
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [messagesError, setMessagesError] = useState<string | null>(null);
  const [composer, setComposer] = useState('');
  const [imageUploading, setImageUploading] = useState(false);
  const [fileUploading, setFileUploading] = useState(false);
  const [showGroupModal, setShowGroupModal] = useState(false);
  const [showMembers, setShowMembers] = useState(false);
  const [showOwnerTransfer, setShowOwnerTransfer] = useState(false);
  const [activeMenuId, setActiveMenuId] = useState<string | null>(null);
  const [showToolsDropdown, setShowToolsDropdown] = useState(false);
  const [showDetails, setShowDetails] = useState(false);
  const toolsDropdownRef = useRef<HTMLDivElement>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeTab, setActiveTab] = useState<'main' | 'pending'>('main');
  const [pendingCount, setPendingCount] = useState(0);
  const [mainUnreadCount, setMainUnreadCount] = useState(0);

  useEffect(() => {
    if (!showToolsDropdown) return;
    const clickOutside = (e: MouseEvent) => {
      if (toolsDropdownRef.current && !toolsDropdownRef.current.contains(e.target as Node)) {
        setShowToolsDropdown(false);
      }
    };
    document.addEventListener('mousedown', clickOutside);
    return () => document.removeEventListener('mousedown', clickOutside);
  }, [showToolsDropdown]);
  const [searchedUsers, setSearchedUsers] = useState<ChatUser[]>([]);
  const [searchingUsers, setSearchingUsers] = useState(false);
  const listEndRef = useRef<HTMLDivElement | null>(null);
  const syncedConversationParamRef = useRef<string | null>(searchParams.get('conversationId'));

  useEffect(() => {
    const trimmed = searchQuery.trim();
    if (trimmed.length < 3) {
      setSearchedUsers([]);
      return;
    }
    const delayDebounce = setTimeout(async () => {
      setSearchingUsers(true);
      try {
        const res = await searchChatUsers(trimmed, null);
        setSearchedUsers(res);
      } catch {
        setSearchedUsers([]);
      } finally {
        setSearchingUsers(false);
      }
    }, 400);

    return () => clearTimeout(delayDebounce);
  }, [searchQuery]);

  const handleStartChatWithUser = async (userId: string) => {
    try {
      const conv = await createDirectConversation(userId);
      upsertConversation(conv);
      selectConversation(conv.id);
      setSearchQuery('');
    } catch (err) {
      alert('Không thể tạo cuộc trò chuyện: ' + (err instanceof Error ? err.message : ''));
    }
  };

  const filteredConversations = useMemo(() => {
    const q = searchQuery.toLowerCase().trim();
    if (!q) return conversations;
    return conversations.filter(c => {
      const matchTitle = c.title.toLowerCase().includes(q);
      const matchPreview = c.lastMessagePreview?.toLowerCase().includes(q);
      const matchParticipant = c.participants.some(p => p.displayName.toLowerCase().includes(q) || p.email.toLowerCase().includes(q));
      return matchTitle || matchPreview || matchParticipant;
    });
  }, [conversations, searchQuery]);

  const activeConversation = useMemo(
    () => conversations.find(item => item.id === activeId) ?? null,
    [conversations, activeId]
  );
  const isLandlord = currentUser?.roles.includes('Landlord') ?? false;

  const selectConversation = useCallback((conversationId: string | null) => {
    syncedConversationParamRef.current = conversationId;
    setActiveMenuId(null);
    setActiveId(conversationId);
  }, []);

  useEffect(() => {
    const handleOutsideClick = () => setActiveMenuId(null);
    document.addEventListener('click', handleOutsideClick);
    return () => document.removeEventListener('click', handleOutsideClick);
  }, []);

  const activeIdRef = useRef(activeId);
  // Update ref synchronously during render so it's always current before effects run
  activeIdRef.current = activeId;

  // Message cache
  const [messageCache, setMessageCache] = useState<Record<string, ChatMessage[]>>({});
  const messageCacheRef = useRef(messageCache);
  messageCacheRef.current = messageCache;

  const updateMessagesAndCache = useCallback((convId: string, updater: (prev: ChatMessage[]) => ChatMessage[]) => {
    setMessages(prev => {
      const next = updater(prev);
      setMessageCache(cache => ({
        ...cache,
        [convId]: next
      }));
      return next;
    });
  }, []);

  const upsertConversation = useCallback((conversation: Conversation) => {
    setConversations(prev => {
      const exists = prev.some(item => item.id === conversation.id);
      let next;
      if (exists) {
        next = prev.map(item => {
          if (item.id === conversation.id) {
            const finalUnread = conversation.id === activeIdRef.current ? 0 : conversation.unreadCount;
            return { ...item, ...conversation, unreadCount: finalUnread };
          }
          return item;
        });
      } else {
        const finalUnread = conversation.id === activeIdRef.current ? 0 : conversation.unreadCount;
        next = [{ ...conversation, unreadCount: finalUnread }, ...prev];
      }
      return next.sort((a, b) => {
        const left = new Date(a.lastMessageAt ?? '1970-01-01').getTime();
        const right = new Date(b.lastMessageAt ?? '1970-01-01').getTime();
        return right - left;
      });
    });
  }, []);

  const upsertMessage = useCallback((message: ChatMessage) => {
    if (message.conversationId !== activeIdRef.current) return;

    updateMessagesAndCache(message.conversationId, prev => {
      const normalized = { ...message, status: 'sent' as const };
      const exists = prev.some(item => item.id === message.id || (message.clientMessageId && item.clientMessageId === message.clientMessageId));
      let next;
      if (exists) {
        next = prev.map(item => {
          if (item.id === message.id || (message.clientMessageId && item.clientMessageId === message.clientMessageId)) {
            return normalized;
          }
          return item;
        });
      } else {
        next = [...prev, normalized];
      }
      return next.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    });
  }, [updateMessagesAndCache]);

  const handleIncomingMessage = useCallback((message: ChatMessage) => {
    upsertMessage(message);
    if (message.conversationId === activeIdRef.current) {
      if (message.senderId !== currentUser?.userId) {
        void markConversationRead(activeIdRef.current)
          .then(upsertConversation)
          .catch(() => undefined);
      }
    }
  }, [upsertMessage, currentUser, upsertConversation]);

  const handleConversationUpdated = useCallback((conversation: Conversation) => {
    upsertConversation(conversation);
  }, [upsertConversation]);

  const handleParticipantRemoved = useCallback((conversationId: string) => {
    setMessageCache(prev => {
      const next = { ...prev };
      delete next[conversationId];
      return next;
    });
    setConversations(prev => prev.filter(item => item.id !== conversationId));
    if (conversationId === activeIdRef.current) {
      selectConversation(null);
      setMessages([]);
    }
  }, [selectConversation]);

  const handleConversationClosed = useCallback((conversation: Conversation) => {
    upsertConversation(conversation);
  }, [upsertConversation]);

  const handleMessageDeleted = useCallback((message: ChatMessage) => {
    if (message.conversationId !== activeIdRef.current) return;
    updateMessagesAndCache(message.conversationId, prev =>
      prev.map(item => item.id === message.id ? { ...item, ...message } : item)
    );
  }, [updateMessagesAndCache]);

  const reloadConversationsSilently = useCallback(async () => {
    try {
      const items = await getConversations(activeTab);
      setConversations(items);
      const counts = await getConversationCounts();
      setPendingCount(counts.pendingCount);
      setMainUnreadCount(counts.mainUnreadCount);
    } catch {
      // Fail silently
    }
  }, [activeTab]);

  const refreshConversationCounts = useCallback(async () => {
    try {
      const counts = await getConversationCounts();
      setPendingCount(counts.pendingCount);
      setMainUnreadCount(counts.mainUnreadCount);
    } catch {
      // Fail silently
    }
  }, []);

  const handleMessageCreated = useCallback((payload: { message: ChatMessage; conversation: Conversation }) => {
    upsertConversation(payload.conversation);
    void refreshConversationCounts();
    window.dispatchEvent(new CustomEvent('refresh-chat-list'));
    if (payload.message.conversationId === activeIdRef.current) {
      upsertMessage(payload.message);
      if (payload.message.senderId !== currentUser?.userId) {
        void markConversationRead(payload.message.conversationId)
          .then(conversation => {
            upsertConversation(conversation);
            void refreshConversationCounts();
            window.dispatchEvent(new CustomEvent('refresh-chat-list'));
          })
          .catch(() => undefined);
      }
    }
  }, [upsertConversation, upsertMessage, currentUser, refreshConversationCounts]);

  const handleReconnected = useCallback(() => {
    void reloadConversationsSilently();
  }, [reloadConversationsSilently]);

  const handleUnreadChanged = useCallback(() => {
    void reloadConversationsSilently();
    window.dispatchEvent(new CustomEvent('refresh-chat-list'));
  }, [reloadConversationsSilently]);

  const hub = useChatHub({
    currentConversationId: activeId,
    onMessage: handleIncomingMessage,
    onConversationUpdated: handleConversationUpdated,
    onParticipantRemoved: handleParticipantRemoved,
    onConversationClosed: handleConversationClosed,
    onMessageDeleted: handleMessageDeleted,
    onMessageCreated: handleMessageCreated,
    onUnreadCountUpdated: handleUnreadChanged,
    onMessageRead: handleUnreadChanged,
    onReconnected: handleReconnected
  });

  const loadConversations = useCallback(async () => {
    setLoading(true);
    try {
      const items = await getConversations(activeTab);
      try {
        const counts = await getConversationCounts();
        setPendingCount(counts.pendingCount);
        setMainUnreadCount(counts.mainUnreadCount);
      } catch {
        // fail silently
      }
      // Use ref to avoid stale closure – we only need the value, not a reactive dep
      const currentActiveId = activeIdRef.current;
      const urlId = new URLSearchParams(window.location.search).get('conversationId') || currentActiveId;

      setConversations(prev => {
        const merged = [...items];
        if (urlId) {
          const active = prev.find(c => c.id === urlId);
          if (active && !merged.some(c => c.id === urlId)) {
            merged.push(active);
          }
        }
        return merged;
      });

      // Only auto-select if no conversation is currently active
      if (!currentActiveId && !urlId) {
        const firstValid = items.find(c => c.lastMessageAt || c.lastMessagePreview || c.type === 'Group');
        selectConversation(firstValid?.id ?? null);
      } else if (urlId && !currentActiveId) {
        // Restore from URL on initial load
        selectConversation(urlId);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không tải được danh sách tin nhắn.');
    } finally {
      setLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectConversation, activeTab]); // Uses activeIdRef to avoid re-running on every conversation switch

  useEffect(() => {
    void loadConversations();
  }, [loadConversations]);

  const loadMessages = useCallback((conversationId: string) => {
    const cached = messageCacheRef.current[conversationId];
    if (cached) {
      // Instantly restore from cache for smooth switching
      setMessages(cached);
    } else {
      setMessagesLoading(true);
      setMessages([]);
    }
    setMessagesError(null);

    // Join conversation immediately
    void hub.joinConversation(conversationId).catch(() => undefined);

    void getMessages(conversationId)
      .then(items => {
        if (conversationId !== activeIdRef.current) return;
        const normalized = items.map(item => ({ ...item, status: 'sent' as const }));

        setMessageCache(prev => ({
          ...prev,
          [conversationId]: normalized
        }));
        setMessages(normalized);

        return markConversationRead(conversationId);
      })
      .then(conv => {
        if (conversationId !== activeIdRef.current) return;
        if (conv) upsertConversation(conv);
      })
      .catch(err => {
        if (conversationId !== activeIdRef.current) return;
        if (!cached) {
          setMessagesError(err instanceof Error ? err.message : 'Không tải được lịch sử tin nhắn.');
        }
      })
      .finally(() => {
        if (conversationId !== activeIdRef.current) return;
        setMessagesLoading(false);
      });
  }, [hub.joinConversation, upsertConversation]);

  // Sync activeId to url param & trigger messages load
  useEffect(() => {
    if (activeId) {
      syncedConversationParamRef.current = activeId;
      setSearchParams({ conversationId: activeId }, { replace: true });
      loadMessages(activeId);
    } else {
      syncedConversationParamRef.current = null;
      setSearchParams({}, { replace: true });
    }

    return () => {
      if (activeId) {
        void hub.leaveConversation(activeId).catch(() => undefined);
      }
    };
  }, [activeId, loadMessages, setSearchParams, hub.leaveConversation]);

  useEffect(() => {
    if (messagesLoading) return;
    const timer = setTimeout(() => {
      listEndRef.current?.scrollIntoView({ behavior: 'auto', block: 'end' });
    }, 100);
    return () => clearTimeout(timer);
  }, [messages, activeId, messagesLoading]);

  const handleAcceptContact = async (conversationId: string) => {
    try {
      const updated = await acceptContactRequest(conversationId);
      upsertConversation(updated);
      // Reload counts
      const counts = await getConversationCounts();
      setPendingCount(counts.pendingCount);
      setMainUnreadCount(counts.mainUnreadCount);
      // Switch active tab to main
      setActiveTab('main');
      selectConversation(conversationId);
    } catch (err) {
      alert('Không thể chấp nhận yêu cầu: ' + (err instanceof Error ? err.message : ''));
    }
  };

  const handleRejectContact = async (conversationId: string) => {
    if (!window.confirm('Bạn có chắc chắn muốn từ chối yêu cầu nhắn tin này không?')) return;
    try {
      await rejectContactRequest(conversationId);
      // Remove from sidebar list by setting activeId null and refreshing
      selectConversation(null);
      setConversations(prev => prev.filter(c => c.id !== conversationId));
      // Reload counts
      const counts = await getConversationCounts();
      setPendingCount(counts.pendingCount);
      setMainUnreadCount(counts.mainUnreadCount);
    } catch (err) {
      alert('Không thể từ chối yêu cầu: ' + (err instanceof Error ? err.message : ''));
    }
  };

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
      mediaAssetId: request.mediaAssetId,
      imageUrl: request.imageUrl,
      fileName: request.fileName,
      fileUrl: request.fileUrl,
      fileType: request.fileType,
      fileSize: request.fileSize,
      clientMessageId,
      createdAt: new Date().toISOString(),
      status: 'sending'
    };

    updateMessagesAndCache(activeId, prev => [...prev, optimistic]);

    try {
      const payload = { ...request, clientMessageId };
      const saved = hub.isConnected
        ? await hub.sendHubMessage(activeId, payload)
        : await sendMessage(activeId, payload);

      upsertMessage(saved);
      setComposer('');
      window.dispatchEvent(new CustomEvent('refresh-chat-list'));
    } catch (err) {
      updateMessagesAndCache(activeId, prev => prev.map(item => item.clientMessageId === clientMessageId ? { ...item, status: 'error' } : item));
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
      const meta = await uploadChatFile(file);
      await handleSend({
        messageType: 'File',
        mediaAssetId: meta.mediaAssetId,
        fileName: meta.fileName,
        fileContentType: meta.contentType,
        fileType: meta.contentType,
        fileSize: meta.size
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Tải tệp thất bại.');
    } finally {
      setFileUploading(false);
    }
  }

  async function handleClearConversation(targetId: string) {
    if (!targetId) return;
    if (!window.confirm('Bạn có chắc chắn muốn xóa lịch sử cuộc trò chuyện này?')) return;

    try {
      await clearConversation(targetId);
      
      setMessageCache(prev => {
        const next = { ...prev };
        delete next[targetId];
        return next;
      });

      setConversations(prev => {
        const nextList = prev.filter(c => c.id !== targetId);
        if (targetId === activeId) {
          selectConversation(nextList[0]?.id ?? null);
          if (nextList.length === 0) setMessages([]);
        }
        return nextList;
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể xóa lịch sử cuộc trò chuyện.');
    }
  }

  async function handleLeaveGroup() {
    if (!activeConversation) return;
    const currentParticipant = activeConversation.participants.find(
      participant => participant.userId === currentUser?.userId && !participant.leftAt
    );
    const isCurrentOwner = currentParticipant?.role === 'Owner' || currentParticipant?.role === 'Admin';
    const activeOwnerCount = activeConversation.participants.filter(
      participant => !participant.leftAt && (participant.role === 'Owner' || participant.role === 'Admin')
    ).length;

    if (isCurrentOwner && activeOwnerCount <= 1) {
      setShowOwnerTransfer(true);
      return;
    }

    try {
      const updated = await leaveConversation(activeConversation.id);
      upsertConversation(updated);
      selectConversation(null);
      setMessages([]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể rời nhóm.');
    }
  }

  async function handleTransferOwnerAndLeave(targetUserId: string) {
    if (!activeConversation) return;

    try {
      const promoted = await updateParticipantRole(activeConversation.id, targetUserId, 'Owner');
      upsertConversation(promoted);
      const updated = await leaveConversation(activeConversation.id);
      upsertConversation(updated);
      setShowOwnerTransfer(false);
      selectConversation(null);
      setMessages([]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Không thể trao quyền trưởng nhóm và rời nhóm.');
      throw err;
    }
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
          <header className="chat-inbox__header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px 20px', borderBottom: 'none' }}>
            <div>
              <h1 style={{ fontSize: '24px', fontWeight: 700, color: '#1e293b', margin: 0 }}>Tin nhắn</h1>
              <span style={{ fontSize: '12px', color: hub.isConnected ? '#10b981' : '#f59e0b', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '4px' }}>
                <span style={{ width: '6px', height: '6px', borderRadius: '50%', backgroundColor: hub.isConnected ? '#10b981' : '#f59e0b', display: 'inline-block' }}></span>
                {hub.isConnected ? 'Đang kết nối' : 'Đang tải lại'}
              </span>
            </div>
            {isLandlord && (
              <button
                type="button"
                onClick={() => setShowGroupModal(true)}
                title="Tạo nhóm chat mới"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: '36px',
                  height: '36px',
                  borderRadius: '50%',
                  border: 'none',
                  backgroundColor: '#f1f5f9',
                  color: '#475569',
                  cursor: 'pointer',
                  transition: 'all 0.2s',
                  outline: 'none',
                  padding: 0
                }}
                onMouseOver={e => {
                  e.currentTarget.style.backgroundColor = '#e2e8f0';
                  e.currentTarget.style.color = '#0f172a';
                }}
                onMouseOut={e => {
                  e.currentTarget.style.backgroundColor = '#f1f5f9';
                  e.currentTarget.style.color = '#475569';
                }}
              >
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <line x1="19" y1="8" x2="19" y2="14" />
                  <line x1="22" y1="11" x2="16" y2="11" />
                </svg>
              </button>
            )}
          </header>

          <div style={{ padding: '4px 16px 12px 16px', borderBottom: 'none' }}>
            <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
              <span style={{ position: 'absolute', left: '12px', color: '#94a3b8', display: 'flex', alignItems: 'center' }}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
              </span>
              <input
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Tìm kiếm cuộc trò chuyện hoặc email..."
                style={{
                  width: '100%',
                  padding: '8px 12px 8px 36px',
                  borderRadius: '20px',
                  border: '1px solid #e2e8f0',
                  fontSize: '14px',
                  backgroundColor: '#f8fafc',
                  outline: 'none',
                  transition: 'all 0.2s',
                  color: '#1e293b'
                }}
                onFocus={e => {
                  e.target.style.border = '1px solid #3b82f6';
                  e.target.style.backgroundColor = '#ffffff';
                  e.target.style.boxShadow = '0 0 0 3px rgba(59, 130, 246, 0.1)';
                }}
                onBlur={e => {
                  e.target.style.border = '1px solid #e2e8f0';
                  e.target.style.backgroundColor = '#f8fafc';
                  e.target.style.boxShadow = 'none';
                }}
              />
              {searchQuery && (
                <button
                  type="button"
                  onClick={() => setSearchQuery('')}
                  style={{
                    position: 'absolute',
                    right: '12px',
                    border: 'none',
                    background: 'none',
                    color: '#94a3b8',
                    cursor: 'pointer',
                    fontSize: '16px',
                    display: 'flex',
                    alignItems: 'center',
                    padding: 0
                  }}
                >
                  &times;
                </button>
              )}
            </div>
          </div>

          <div style={{
            display: 'flex',
            borderBottom: '1px solid #e2e8f0',
            padding: '0 16px',
            backgroundColor: '#f8fafc'
          }}>
            <button
              type="button"
              onClick={() => {
                setActiveTab('main');
                selectConversation(null);
              }}
              style={{
                flex: 1,
                padding: '12px 8px',
                border: 'none',
                background: 'none',
                fontSize: '0.9rem',
                fontWeight: activeTab === 'main' ? 600 : 500,
                color: activeTab === 'main' ? '#3b82f6' : '#64748b',
                borderBottom: activeTab === 'main' ? '2px solid #3b82f6' : '2px solid transparent',
                cursor: 'pointer',
                textAlign: 'center',
                transition: 'all 0.2s'
              }}
            >
              Hộp thư chính
              {mainUnreadCount > 0 && (
                <span style={{
                  backgroundColor: '#ef4444',
                  color: '#fff',
                  borderRadius: '10px',
                  padding: '2px 6px',
                  fontSize: '0.75rem',
                  fontWeight: 600,
                  minWidth: '18px',
                  textAlign: 'center',
                  marginLeft: '6px'
                }}>
                  {mainUnreadCount > 99 ? '99+' : mainUnreadCount}
                </span>
              )}
            </button>
            <button
              type="button"
              onClick={() => {
                setActiveTab('pending');
                selectConversation(null);
              }}
              style={{
                flex: 1,
                padding: '12px 8px',
                border: 'none',
                background: 'none',
                fontSize: '0.9rem',
                fontWeight: activeTab === 'pending' ? 600 : 500,
                color: activeTab === 'pending' ? '#3b82f6' : '#64748b',
                borderBottom: activeTab === 'pending' ? '2px solid #3b82f6' : '2px solid transparent',
                cursor: 'pointer',
                textAlign: 'center',
                transition: 'all 0.2s',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '6px'
              }}
            >
              Tin nhắn chờ
              {pendingCount > 0 && (
                <span style={{
                  backgroundColor: '#ef4444',
                  color: '#fff',
                  borderRadius: '10px',
                  padding: '2px 6px',
                  fontSize: '0.75rem',
                  fontWeight: 600,
                  minWidth: '18px',
                  textAlign: 'center'
                }}>
                  {pendingCount}
                </span>
              )}
            </button>
          </div>

          {loading ? (
            <div className="chat-empty">Đang tải...</div>
          ) : (
            <div className="chat-conversation-list" style={{ flex: 1, overflowY: 'auto' }}>
              {searchQuery.trim() !== '' && (
                <div style={{ padding: '8px 12px 4px 12px', fontSize: '11px', fontWeight: 600, color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  Cuộc trò chuyện
                </div>
              )}
              
              {filteredConversations.length === 0 && searchQuery.trim() === '' && (
                <div className="chat-empty">Chưa có cuộc trò chuyện.</div>
              )}
              
              {filteredConversations.map(item => (
                <div
                  key={item.id}
                  role="button"
                  tabIndex={0}
                  className={`chat-conversation ${item.id === activeId ? 'active' : ''}`}
                  onClick={() => selectConversation(item.id)}
                  onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      selectConversation(item.id);
                    }
                  }}
                >
                  <Avatar name={item.title} url={item.type === 'Group' ? item.avatarUrl : item.participants.find(p => p.userId !== currentUser?.userId)?.avatarUrl} />
                  <span className="chat-conversation__body">
                    <span className="chat-conversation__title">{item.title}</span>
                    <span className="chat-conversation__preview">{item.lastMessagePreview || (item.type === 'Group' ? 'Nhóm chat' : 'Tin nhắn riêng')}</span>
                  </span>
                  <span className="chat-conversation__meta">
                    <span>{formatTime(item.lastMessageAt)}</span>
                    {item.unreadCount > 0 && <strong>{item.unreadCount}</strong>}
                  </span>
                  <div className="chat-conversation__options">
                    <button
                      type="button"
                      className="chat-conversation__options-trigger"
                      onClick={(e) => {
                        e.stopPropagation();
                        setActiveMenuId(prev => prev === item.id ? null : item.id);
                      }}
                      title="Tùy chọn"
                    >
                      &#8942;
                    </button>
                    {activeMenuId === item.id && (
                      <div className="chat-conversation__menu" onClick={e => e.stopPropagation()}>
                        <button
                          type="button"
                          className="chat-conversation__menu-item"
                          onClick={(e) => {
                            e.stopPropagation();
                            setActiveMenuId(null);
                            void handleClearConversation(item.id);
                          }}
                        >
                          Xóa cuộc trò chuyện
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              ))}

              {searchQuery.trim() !== '' && (
                <>
                  <div style={{ padding: '12px 12px 4px 12px', fontSize: '11px', fontWeight: 600, color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.05em', borderTop: '1px solid #f1f5f9', marginTop: '8px' }}>
                    Người dùng
                  </div>
                  {searchingUsers ? (
                    <div style={{ padding: '12px', fontSize: '13px', color: '#64748b', textAlign: 'center' }}>Đang tìm kiếm...</div>
                  ) : searchedUsers.length === 0 ? (
                    <div style={{ padding: '12px', fontSize: '13px', color: '#64748b', textAlign: 'center', fontStyle: 'italic' }}>Không tìm thấy người dùng phù hợp.</div>
                  ) : (
                    searchedUsers.map(user => (
                      <div
                        key={user.userId}
                        role="button"
                        tabIndex={0}
                        className="chat-conversation"
                        onClick={() => void handleStartChatWithUser(user.userId)}
                        onKeyDown={e => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            void handleStartChatWithUser(user.userId);
                          }
                        }}
                      >
                        <Avatar name={user.displayName} url={user.avatarUrl} />
                        <span className="chat-conversation__body">
                          <span className="chat-conversation__title">{user.displayName}</span>
                          <span className="chat-conversation__preview" style={{ color: '#3b82f6' }}>{user.email} (Bắt đầu chat mới)</span>
                        </span>
                      </div>
                    ))
                  )}
                </>
              )}
            </div>
          )}

          <div style={{ padding: '12px 16px', borderTop: '1px solid #e2e8f0', backgroundColor: '#f8fafc' }}>
            <button
              type="button"
              onClick={() => navigate(ROUTE_PATHS.ME.ROOT)}
              style={{
                width: '100%',
                padding: '10px 16px',
                backgroundColor: '#3b82f6',
                color: '#fff',
                border: 'none',
                borderRadius: '8px',
                fontSize: '14px',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '8px',
                boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                transition: 'all 0.2s'
              }}
              onMouseOver={e => e.currentTarget.style.backgroundColor = '#2563eb'}
              onMouseOut={e => e.currentTarget.style.backgroundColor = '#3b82f6'}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="19" y1="12" x2="5" y2="12" />
                <polyline points="12 19 5 12 12 5" />
              </svg>
              Về trang chủ
            </button>
          </div>
        </aside>

        <section className="chat-main">
          {activeConversation ? (
            <>
              <header className="chat-main__header">
                <div className="chat-main__title">
                  <Avatar name={activeConversation.title} url={activeConversation.type === 'Group' ? activeConversation.avatarUrl : activeConversation.participants.find(p => p.userId !== currentUser?.userId)?.avatarUrl} />
                  <div>
                    <h2>{activeConversation.title}</h2>
                    <p>
                      {activeConversation.type === 'Group'
                        ? `${activeConversation.participants.filter(p => !p.leftAt).length} thành viên${activeConversation.roomingHouseName ? ` • ${activeConversation.roomingHouseName}` : ''}`
                        : 'Tin nhắn riêng'}
                    </p>
                  </div>
                </div>
                <div className="chat-main__tools">
                  <button
                    type="button"
                    onClick={() => {
                      if (activeConversation.type === 'Group') {
                        setShowMembers(!showMembers);
                        setShowDetails(false);
                      } else {
                        setShowDetails(!showDetails);
                        setShowMembers(false);
                      }
                    }}
                    style={{
                      width: '36px',
                      height: '36px',
                      borderRadius: '50%',
                      border: '1px solid #dce6f3',
                      backgroundColor: (activeConversation.type === 'Group' ? showMembers || showDetails : showDetails) ? '#eef5ff' : '#ffffff',
                      color: (activeConversation.type === 'Group' ? showMembers || showDetails : showDetails) ? '#3b82f6' : '#1e3a5f',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: '18px',
                      fontWeight: 'bold',
                      boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                      outline: 'none',
                      padding: 0
                    }}
                    title="Chi tiết cuộc trò chuyện"
                  >
                    ⋮
                  </button>
                </div>
              </header>

              <div className="chat-content">
                <div className="chat-messages">
                  {messagesLoading ? (
                    <div className="chat-empty">Đang tải tin nhắn...</div>
                  ) : messagesError ? (
                    <button type="button" className="chat-empty chat-empty--retry" onClick={() => loadMessages(activeConversation.id)}>
                      {messagesError}. Bấm để tải lại.
                    </button>
                  ) : messages.length === 0 ? (
                    <div className="chat-empty">Bắt đầu cuộc trò chuyện.</div>
                  ) : (
                    messages.map(message => (
                      <MessageBubble key={message.id} message={message} mine={message.senderId === currentUser?.userId} />
                    ))
                  )}
                  <div ref={listEndRef} />
                </div>
              </div>

              {showMembers && activeConversation.type === 'Group' && (
                <MemberPanel
                  conversation={activeConversation}
                  messages={messages}
                  currentUserId={currentUser?.userId ?? ''}
                  onLeave={handleLeaveGroup}
                  onClose={handleCloseGroup}
                  onRemove={handleRemoveMember}
                  onAdd={handleAddMembers}
                  onHide={() => setShowMembers(false)}
                  onUpdateConversation={upsertConversation}
                  onOpenDetails={() => {
                    setShowMembers(false);
                    setShowDetails(true);
                  }}
                />
              )}

              {showDetails && (
                <div className="chat-details-popover">
                  <button
                    type="button"
                    className="chat-popover-close"
                    onClick={() => {
                      setShowDetails(false);
                      if (activeConversation.type === 'Group') {
                        setShowMembers(true);
                      }
                    }}
                    aria-label="Đóng file và ảnh"
                  >
                    ×
                  </button>
                  <ChatDetailsPanel
                    messages={messages}
                    onClearHistory={() => void handleClearConversation(activeConversation.id)}
                  />
                </div>
              )}

              {activeConversation.inboxStatus === 'Pending' ? (
                activeConversation.createdByUserId === currentUser?.userId ? (
                  <div className="chat-approval-bar chat-approval-bar--waiting">
                    Đang chờ chủ trọ phê duyệt yêu cầu nhắn tin của bạn...
                  </div>
                ) : (
                  <div className="chat-approval-bar">
                    <div className="chat-approval-bar__text">
                      Người dùng này muốn gửi tin nhắn liên hệ về khu trọ <strong>{activeConversation.roomingHouseName || 'của bạn'}</strong>.
                    </div>
                    <div className="chat-approval-bar__actions">
                      <button
                        type="button"
                        onClick={() => void handleAcceptContact(activeConversation.id)}
                        className="chat-accept-btn"
                      >
                        Chấp nhận
                      </button>
                      <button
                        type="button"
                        onClick={() => void handleRejectContact(activeConversation.id)}
                        className="chat-reject-btn"
                      >
                        Từ chối
                      </button>
                    </div>
                  </div>
                )
              ) : (
                <ChatComposer
                  value={composer}
                  onChange={setComposer}
                  imageUploading={imageUploading}
                  fileUploading={fileUploading}
                  onSendText={() => void handleTextSubmit()}
                  onSendIcon={(icon: string) => void handleSend({ messageType: 'Icon', content: icon })}
                  onImageSelected={(file: File | null) => void handleImageSelected(file)}
                  onFileSelected={(file: File | null) => void handleFileSelected(file)}
                  onError={setError}
                  enterToSend
                  isClosed={activeConversation.isClosed}
                  hasLeft={activeConversation.hasCurrentUserLeft}
                />
              )}
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



      {showGroupModal && (
        <CreateGroupModal
          onClose={() => setShowGroupModal(false)}
          onSubmit={async (title: string, users: ChatUser[], roomingHouseId?: string | null, avatarMediaAssetId?: string | null) => {
            const conversation = await createGroupConversation(title, users.map(user => user.userId), roomingHouseId, avatarMediaAssetId);
            upsertConversation(conversation);
            selectConversation(conversation.id);
            setShowGroupModal(false);
          }}
        />
      )}

      {showOwnerTransfer && activeConversation && (
        <OwnerTransferModal
          currentUserId={currentUser?.userId ?? ''}
          participants={activeConversation.participants}
          onClose={() => setShowOwnerTransfer(false)}
          onSubmit={handleTransferOwnerAndLeave}
        />
      )}
    </main>
  );
}

function MessageBubble({ message, mine }: { message: ChatMessage; mine: boolean }) {
  const { currentUser } = useAuth();
  const imageMediaAssetId = message.messageType === 'Image' ? message.mediaAssetId : null;
  const { objectUrl: imageObjectUrl, error: imageLoadError } = usePrivateChatMediaObjectUrl(imageMediaAssetId);
  
  const handleDownload = async () => {
    if (!message.mediaAssetId) return;

    try {
      const blob = await downloadChatFile(message.mediaAssetId);
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = message.fileName || 'file';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    } catch {
      alert('Không thể tải tệp. Vui lòng thử lại.');
    }
  };

  if (message.messageType === 'System') {
    const isMine = message.senderId === currentUser?.userId;
    const displayName = isMine ? 'Bạn' : (message.senderName || 'Chủ trọ');
    const displayContent = message.content === 'đã tạo đoạn chat nhóm mới'
      ? `${displayName} đã tạo đoạn chat nhóm mới`
      : message.content;

    return (
      <div className="message-system-row">
        <div className="message-system-content">
          {displayContent}
        </div>
      </div>
    );
  }

  return (
    <div className={`message-row ${mine ? 'mine' : ''}`}>
      <div className={`message-bubble message-bubble--${message.messageType.toLowerCase()}`}>
        {!mine && <span className="message-sender">{message.senderName}</span>}
        {message.messageType === 'Image' && message.mediaAssetId ? (
          imageLoadError ? (
            <span className="chat-media-error">Không tải được ảnh.</span>
          ) : imageObjectUrl ? (
            <img
              src={imageObjectUrl}
              alt="Ảnh chat"
              onClick={() => window.open(imageObjectUrl, '_blank', 'noopener,noreferrer')}
            />
          ) : (
            <span className="chat-media-loading">Đang tải ảnh...</span>
          )
        ) : message.messageType === 'File' && message.mediaAssetId ? (
          <div className="message-file-card">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
            </svg>
            <div className="message-file-info">
              <span className="message-file-name">{message.fileName || 'Tệp đính kèm'}</span>
              <span className="message-file-meta">{message.fileType?.split('/').pop()?.toUpperCase() || 'FILE'}{message.fileSize ? ` • ${formatFileSize(message.fileSize)}` : ''}</span>
            </div>
            <button type="button" className="message-file-download" onClick={() => void handleDownload()} title="Tải xuống">
              ⬇
            </button>
          </div>
        ) : (
          <span>{message.content}</span>
        )}
        <small>{formatTime(message.createdAt)} {message.status === 'sending' ? '• Đang gửi' : message.status === 'error' ? '• Lỗi' : ''}</small>
      </div>
    </div>
  );
}

function MemberPanel({
  conversation,
  messages,
  currentUserId,
  onLeave,
  onClose,
  onRemove,
  onAdd,
  onHide,
  onUpdateConversation,
  onOpenDetails
}: {
  conversation: Conversation;
  messages: ChatMessage[];
  currentUserId: string;
  onLeave: () => Promise<void>;
  onClose: () => Promise<void>;
  onRemove: (userId: string) => Promise<void>;
  onAdd: (userIds: string[]) => Promise<void>;
  onHide: () => void;
  onUpdateConversation: (conversation: Conversation) => void;
  onOpenDetails: () => void;
}) {
  const [showAdd, setShowAdd] = useState(false);
  const [subTab, setSubTab] = useState<'members' | 'requests'>('members');
  const [joinRequests, setJoinRequests] = useState<ConversationJoinRequest[]>([]);
  const [requestsLoading, setRequestsLoading] = useState(false);
  const [approvalLoading, setApprovalLoading] = useState(false);

  const [newTitle, setNewTitle] = useState(conversation.title);
  const [updatingGroup, setUpdatingGroup] = useState(false);

  useEffect(() => {
    setNewTitle(conversation.title);
  }, [conversation.title]);

  const handleUpdateAvatar = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUpdatingGroup(true);
    try {
      const uploaded = await uploadChatAvatar(file);
      const updated = await updateConversation(conversation.id, undefined, uploaded.mediaAssetId);
      onUpdateConversation(updated);
    } catch (err) {
      alert('Cập nhật ảnh đại diện nhóm thất bại: ' + (err instanceof Error ? err.message : ''));
    } finally {
      setUpdatingGroup(false);
    }
  };

  const handleUpdateTitle = async () => {
    const trimmed = newTitle.trim();
    if (!trimmed || trimmed === conversation.title) return;
    setUpdatingGroup(true);
    try {
      const updated = await updateConversation(conversation.id, trimmed);
      onUpdateConversation(updated);
    } catch (err) {
      alert('Cập nhật tên nhóm thất bại: ' + (err instanceof Error ? err.message : ''));
    } finally {
      setUpdatingGroup(false);
    }
  };

  const mediaMessages = messages.filter((m) => m.messageType === 'Image' && m.mediaAssetId && !m.deletedAt);
  const fileMessages = messages.filter((m) => m.messageType === 'File' && m.mediaAssetId && !m.deletedAt);
  const [showMediaList, setShowMediaList] = useState(true);
  const [showFileList, setShowFileList] = useState(false);

  const downloadSharedFile = async (message: ChatMessage) => {
    if (!message.mediaAssetId) return;

    try {
      const blob = await downloadChatFile(message.mediaAssetId);
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = message.fileName || 'file';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    } catch {
      alert('Không thể tải tệp.');
    }
  };

  const handleClearAvatar = async () => {
    if (!window.confirm('Gỡ ảnh đại diện hiện tại của nhóm?')) return;

    setUpdatingGroup(true);
    try {
      const updated = await updateConversation(conversation.id, undefined, undefined, true);
      onUpdateConversation(updated);
    } catch (err) {
      alert('Gỡ ảnh đại diện nhóm thất bại: ' + (err instanceof Error ? err.message : ''));
    } finally {
      setUpdatingGroup(false);
    }
  };

  const isAdminOrOwner = conversation.isCurrentUserOwner;

  const loadRequests = useCallback(async () => {
    if (!isAdminOrOwner) return;
    setRequestsLoading(true);
    try {
      const list = await getJoinRequests(conversation.id);
      setJoinRequests(list);
    } catch {
      // Fail silently
    } finally {
      setRequestsLoading(false);
    }
  }, [conversation.id, isAdminOrOwner]);

  useEffect(() => {
    if (subTab === 'requests') {
      void loadRequests();
    }
  }, [subTab, loadRequests]);

  const handleToggleApproval = async (event: React.ChangeEvent<HTMLInputElement>) => {
    setApprovalLoading(true);
    try {
      const updated = await updateApprovalSettings(conversation.id, event.target.checked);
      onUpdateConversation(updated);
    } catch (err) {
      alert('Không thể cập nhật cài đặt duyệt: ' + (err instanceof Error ? err.message : ''));
    } finally {
      setApprovalLoading(false);
    }
  };

  const handleRoleChange = async (targetUserId: string, newRole: 'Owner' | 'Member') => {
    try {
      const updated = await updateParticipantRole(conversation.id, targetUserId, newRole);
      onUpdateConversation(updated);
    } catch (err) {
      alert('Đổi vai trò thất bại: ' + (err instanceof Error ? err.message : ''));
    }
  };

  const handleApprove = async (reqId: string) => {
    try {
      const updated = await approveJoinRequest(conversation.id, reqId);
      onUpdateConversation(updated);
      setJoinRequests(prev => prev.filter(r => r.id !== reqId));
    } catch (err) {
      alert('Duyệt thất bại: ' + (err instanceof Error ? err.message : ''));
    }
  };

  const handleReject = async (reqId: string) => {
    try {
      await rejectJoinRequest(conversation.id, reqId);
      setJoinRequests(prev => prev.filter(r => r.id !== reqId));
    } catch (err) {
      alert('Từ chối thất bại: ' + (err instanceof Error ? err.message : ''));
    }
  };

  const canRemove = (participant: ChatParticipant) => {
    if (participant.userId === currentUserId || participant.leftAt) return false;
    if (!conversation.isCurrentUserOwner) return false;
    return participant.role !== 'Owner' && participant.role !== 'Admin';
  };

  const getRoleLabel = (role: string) => {
    if (role === 'Owner' || role === 'Admin') return 'Trưởng nhóm';
    return 'Thành viên';
  };

  return (
    <aside className="member-panel">
      <header style={{ borderBottom: '1px solid #e2e8f0', paddingBottom: '8px', marginBottom: '12px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%', marginBottom: '8px' }}>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>Quản lý nhóm</h3>
          <div className="member-panel__header-actions">
            <button type="button" className="member-panel__close" onClick={onHide} aria-label="Đóng quản lý nhóm">⋮</button>
          </div>
        </div>
      </header>

      {isAdminOrOwner && (
        <div style={{ padding: '10px', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0', marginBottom: '12px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div style={{ position: 'relative', width: '40px', height: '40px', flexShrink: 0 }}>
              <Avatar name={conversation.title} url={conversation.avatarUrl} />
              <label style={{
                position: 'absolute',
                bottom: '-4px',
                right: '-4px',
                backgroundColor: '#3b82f6',
                color: '#fff',
                borderRadius: '50%',
                width: '18px',
                height: '18px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: '10px',
                cursor: 'pointer',
                boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
              }} title="Đổi ảnh đại diện">
                📷
                <input
                  type="file"
                  accept="image/*"
                  onChange={handleUpdateAvatar}
                  disabled={updatingGroup}
                  style={{ display: 'none' }}
                />
              </label>
            </div>
            <div style={{ flex: 1, minWidth: 0, display: 'flex', gap: '6px' }}>
              <input
                value={newTitle}
                onChange={e => setNewTitle(e.target.value)}
                disabled={updatingGroup}
                placeholder="Tên nhóm..."
                style={{
                  width: '100%',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  border: '1px solid #cbd5e1',
                  fontSize: '13px',
                  fontWeight: 600,
                  outline: 'none'
                }}
              />
              <button
                type="button"
                onClick={() => void handleUpdateTitle()}
                disabled={updatingGroup || newTitle.trim() === '' || newTitle.trim() === conversation.title}
                style={{
                  padding: '4px 8px',
                  backgroundColor: (updatingGroup || newTitle.trim() === '' || newTitle.trim() === conversation.title) ? '#cbd5e1' : '#3b82f6',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '4px',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: (updatingGroup || newTitle.trim() === '' || newTitle.trim() === conversation.title) ? 'not-allowed' : 'pointer'
                }}
              >
                Lưu
              </button>
            </div>
          </div>
          {(conversation.avatarMediaAssetId || conversation.avatarUrl) && (
            <button
              type="button"
              onClick={() => void handleClearAvatar()}
              disabled={updatingGroup}
              style={{
                alignSelf: 'flex-start',
                padding: '4px 8px',
                border: '1px solid #fecaca',
                borderRadius: '4px',
                backgroundColor: '#fff',
                color: '#dc2626',
                fontSize: '12px',
                fontWeight: 600,
                cursor: updatingGroup ? 'not-allowed' : 'pointer'
              }}
            >
              Gỡ ảnh đại diện
            </button>
          )}
        </div>
      )}

      {conversation.roomingHouseName && (
        <p className="member-panel__house" style={{ fontSize: '12px', color: '#64748b', margin: '0 0 12px 0' }}>
          Khu trọ: <strong>{conversation.roomingHouseName}</strong>
        </p>
      )}

      {isAdminOrOwner && (
        <>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '10px',
              padding: '10px',
              backgroundColor: '#f8fafc',
              borderRadius: '8px',
              marginBottom: '8px',
              fontSize: '13px'
            }}
          >
            <span style={{ fontWeight: 500, color: '#475569' }}>Yêu cầu duyệt thành viên</span>
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: '10px', flexShrink: 0 }}>
              <input
                type="checkbox"
                checked={conversation.requiresJoinApproval}
                onChange={handleToggleApproval}
                disabled={approvalLoading}
                style={{ cursor: 'pointer', width: '16px', height: '16px' }}
              />
              <button
                type="button"
                className="member-panel__add-button"
                onClick={() => setShowAdd(true)}
              >
                Thêm
              </button>
            </div>
          </div>

          <div className="member-panel__tab-row">
            <button
              type="button"
              onClick={() => setSubTab('members')}
              className={subTab === 'members' ? 'active' : ''}
            >
              Thành viên ({conversation.participants.filter(p => !p.leftAt).length})
            </button>
            <button
              type="button"
              onClick={() => setSubTab('requests')}
              className={subTab === 'requests' ? 'active' : ''}
            >
              Chờ duyệt
            </button>
            <button type="button" className="member-panel__media-button" onClick={onOpenDetails}>
              File & Ảnh
            </button>
          </div>
        </>
      )}

      {!isAdminOrOwner && (
        <div className="member-panel__tab-row member-panel__tab-row--single">
          <button type="button" className="member-panel__media-button" onClick={onOpenDetails}>
            File & Ảnh
          </button>
        </div>
      )}

      {subTab === 'members' ? (
        <>
          <div className="member-list" style={{ display: 'flex', flexDirection: 'column', gap: '8px', flex: 1, overflowY: 'auto' }}>
            {conversation.participants.map(participant => (
              <div
                key={participant.userId}
                className={participant.leftAt ? 'left' : ''}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '6px',
                  borderRadius: '6px',
                  backgroundColor: participant.leftAt ? '#f1f5f9' : 'transparent',
                  opacity: participant.leftAt ? 0.6 : 1
                }}
              >
                <Avatar name={participant.displayName} url={participant.avatarUrl} />
                <span style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
                  <strong style={{ fontSize: '13px', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                    {participant.displayName}
                  </strong>
                  <span style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                    <small
                      style={{
                        padding: '1px 4px',
                        borderRadius: '4px',
                        backgroundColor: participant.role === 'Owner' || participant.role === 'Admin' ? '#fef3c7' : '#f1f5f9',
                        color: participant.role === 'Owner' || participant.role === 'Admin' ? '#d97706' : '#475569',
                        fontSize: '10px',
                        fontWeight: 600
                      }}
                    >
                      {getRoleLabel(participant.role)}
                    </small>
                    {participant.leftAt && <small style={{ color: '#ef4444', fontSize: '10px' }}>• Đã rời</small>}
                  </span>
                </span>

                {conversation.isCurrentUserOwner && participant.userId !== currentUserId && !participant.leftAt && (
                  <select
                    value={participant.role === 'Admin' ? 'Owner' : participant.role}
                    onChange={e => handleRoleChange(participant.userId, e.target.value as 'Owner' | 'Member')}
                    style={{
                      padding: '2px 4px',
                      fontSize: '11px',
                      border: '1px solid #cbd5e1',
                      borderRadius: '4px',
                      backgroundColor: '#fff',
                      cursor: 'pointer'
                    }}
                  >
                    <option value="Member">Thành viên</option>
                    <option value="Owner">Trưởng nhóm</option>
                  </select>
                )}

                {canRemove(participant) && (
                  <button
                    type="button"
                    onClick={() => void onRemove(participant.userId)}
                    style={{
                      padding: '2px 6px',
                      backgroundColor: '#fee2e2',
                      color: '#ef4444',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Xóa
                  </button>
                )}
              </div>
            ))}
          </div>
        </>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', flex: 1, overflowY: 'auto' }}>
          {requestsLoading ? (
            <div style={{ textAlign: 'center', padding: '16px', color: '#64748b', fontSize: '13px' }}>Đang tải danh sách...</div>
          ) : joinRequests.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '24px', color: '#64748b', border: '1px dashed #cbd5e1', borderRadius: '8px', fontSize: '13px' }}>
              Không có yêu cầu chờ duyệt.
            </div>
          ) : (
            joinRequests.map(req => (
              <div
                key={req.id}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '6px',
                  padding: '8px',
                  border: '1px solid #e2e8f0',
                  borderRadius: '6px',
                  backgroundColor: '#fff'
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Avatar name={req.requesterDisplayName || req.displayName || 'User'} url={req.requesterAvatarUrl || req.avatarUrl} />
                  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
                    <strong style={{ fontSize: '13px', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                      {req.requesterDisplayName || req.displayName || 'User'}
                    </strong>
                    <small style={{ fontSize: '11px', color: '#64748b', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                      {req.requesterEmail || req.email || ''}
                    </small>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                  <button
                    type="button"
                    onClick={() => handleReject(req.id)}
                    style={{
                      padding: '4px 8px',
                      backgroundColor: '#f1f5f9',
                      color: '#475569',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Từ chối
                  </button>
                  <button
                    type="button"
                    onClick={() => handleApprove(req.id)}
                    style={{
                      padding: '4px 8px',
                      backgroundColor: '#dcfce7',
                      color: '#15803d',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    Duyệt
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* Media & Files Section */}
      <div
        style={{
          marginTop: '16px',
          border: '1px solid #dbeafe',
          borderRadius: '12px',
          backgroundColor: '#fff',
          padding: '12px',
          boxShadow: '0 8px 22px rgba(15, 23, 42, 0.04)'
        }}
      >
        <h4 style={{ margin: '0 0 8px 0', fontSize: '13px', fontWeight: 600, color: '#475569' }}>
          File & Ảnh đã gửi
        </h4>
        
        <div style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
          <button
            type="button"
            onClick={() => {
              setShowMediaList(true);
              setShowFileList(false);
            }}
            style={{
              flex: 1,
              padding: '4px',
              border: 'none',
              borderRadius: '4px',
              fontSize: '11px',
              fontWeight: 600,
              backgroundColor: showMediaList ? '#f1f5f9' : 'transparent',
              color: showMediaList ? '#1e293b' : '#64748b',
              cursor: 'pointer'
            }}
          >
            Ảnh ({mediaMessages.length})
          </button>
          <button
            type="button"
            onClick={() => {
              setShowMediaList(false);
              setShowFileList(true);
            }}
            style={{
              flex: 1,
              padding: '4px',
              border: 'none',
              borderRadius: '4px',
              fontSize: '11px',
              fontWeight: 600,
              backgroundColor: showFileList ? '#f1f5f9' : 'transparent',
              color: showFileList ? '#1e293b' : '#64748b',
              cursor: 'pointer'
            }}
          >
            Tệp ({fileMessages.length})
          </button>
        </div>

        {showMediaList && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '8px', maxHeight: '220px', overflowY: 'auto' }}>
            {mediaMessages.length === 0 ? (
              <div style={{ gridColumn: 'span 3', textAlign: 'center', padding: '12px', fontSize: '12px', color: '#94a3b8' }}>
                Không có ảnh
              </div>
            ) : (
              mediaMessages.map(m => (
                <PrivateChatImage
                  key={m.id}
                  mediaAssetId={m.mediaAssetId!}
                  alt="shared media"
                  openOnClick
                  style={{ width: '100%', height: '82px', objectFit: 'cover', borderRadius: '8px', cursor: 'pointer', border: '1px solid #e2e8f0' }}
                />
              ))
            )}
          </div>
        )}

        {showFileList && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: '220px', overflowY: 'auto' }}>
            {fileMessages.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '12px', fontSize: '12px', color: '#94a3b8' }}>
                Không có tệp
              </div>
            ) : (
              fileMessages.map(m => (
                <div
                  key={m.id}
                  onClick={() => void downloadSharedFile(m)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    cursor: 'pointer',
                    backgroundColor: '#fff'
                  }}
                >
                  <span style={{ fontSize: '16px' }}>📄</span>
                  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
                    <span style={{ fontSize: '12px', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap', color: '#334155' }}>
                      {m.fileName || 'Tài liệu'}
                    </span>
                    <span style={{ fontSize: '10px', color: '#64748b' }}>
                      {m.fileSize ? `${(m.fileSize / 1024).toFixed(1)} KB` : ''}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </div>

      <footer style={{ marginTop: '12px', borderTop: '1px solid #e2e8f0', paddingTop: '12px' }}>
        <button
          type="button"
          onClick={() => void onLeave()}
          style={{
            width: '100%',
            padding: '8px',
            backgroundColor: '#fee2e2',
            color: '#ef4444',
            border: 'none',
            borderRadius: '6px',
            fontSize: '13px',
            fontWeight: 600,
            cursor: 'pointer'
          }}
        >
          Rời nhóm
        </button>
      </footer>

      {showAdd && (
        <UserSearchModal
          title={conversation.requiresJoinApproval && !isAdminOrOwner ? "Gửi yêu cầu tham gia" : "Thêm thành viên"}
          submitLabel="Thêm"
          onClose={() => setShowAdd(false)}
          roomingHouseId={conversation.roomingHouseId}
          conversationId={conversation.id}
          requiresApproval={conversation.requiresJoinApproval}
          isAdminOrOwner={isAdminOrOwner}
          excludedUserIds={conversation.participants.filter(p => !p.leftAt).map(p => p.userId)}
          onSubmit={async (users: ChatUser[]) => {
            const requiresApproval = conversation.requiresJoinApproval;
            if (requiresApproval && !isAdminOrOwner) {
              for (const u of users) {
                await createJoinRequest(conversation.id, u.userId);
              }
              alert('Đã gửi yêu cầu phê duyệt mời thành viên tham gia.');
            } else {
              await onAdd(users.map((user: ChatUser) => user.userId));
            }
            setShowAdd(false);
          }}
        />
      )}
    </aside>
  );
}
