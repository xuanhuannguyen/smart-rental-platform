import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { useAuth } from '../../../app/providers/AuthProvider';
import { getConversations } from '../../../features/chat/api';
import type { Conversation } from '../../../features/chat/types';
import { useChatHub } from '../../../features/chat/useChatHub';
import { toAssetUrl } from '../../../shared/api/assets';
import './MessageShortcut.css';

export function MessageShortcut() {
  const { currentUser } = useAuth();
  const navigate = useNavigate();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement | null>(null);

  const loadConversationsData = useCallback(async () => {
    try {
      const list = await getConversations();
      // Keep only first 5 recent conversations for the dropdown
      setConversations(list.slice(0, 5));
      setUnreadCount(list.reduce((total, item) => total + item.unreadCount, 0));
    } catch {
      setConversations([]);
      setUnreadCount(0);
    }
  }, []);

  const refreshChatList = useCallback(() => {
    if (!currentUser) return;
    void loadConversationsData();
  }, [currentUser, loadConversationsData]);

  useChatHub({
    currentConversationId: null,
    onMessage: refreshChatList,
    onConversationUpdated: refreshChatList,
    onParticipantRemoved: refreshChatList,
    onConversationClosed: refreshChatList,
    onMessageDeleted: refreshChatList,
    onUnreadCountUpdated: refreshChatList,
    onMessageRead: refreshChatList,
    onMessageCreated: refreshChatList,
    onReconnected: refreshChatList
  });

  useEffect(() => {
    if (!currentUser) return;

    void loadConversationsData();
    const interval = window.setInterval(loadConversationsData, 30000);
    return () => window.clearInterval(interval);
  }, [currentUser, loadConversationsData]);

  useEffect(() => {
    window.addEventListener('refresh-chat-list', refreshChatList);
    return () => window.removeEventListener('refresh-chat-list', refreshChatList);
  }, [refreshChatList]);

  // Handle click outside to close dropdown
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  function handleSelectChat(conversationId: string) {
    setIsOpen(false);
    // Dispatch event to open floating bubble
    const event = new CustomEvent('open-chat-bubble', {
      detail: { conversationId }
    });
    window.dispatchEvent(event);
  }

  function handleViewAll() {
    setIsOpen(false);
    navigate(ROUTE_PATHS.MESSAGES);
  }

  if (!currentUser) return null;

  return (
    <div className="message-shortcut-wrapper" ref={dropdownRef}>
      <button
        type="button"
        className={`message-shortcut-btn ${isOpen ? 'active' : ''}`}
        title="Tin nhắn"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(!isOpen)}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 15a2 2 0 0 1-2 2H8l-5 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
        </svg>
        {unreadCount > 0 && (
          <span className="message-shortcut-badge">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="message-shortcut-dropdown">
          <header className="dropdown-header">
            <h4>Tin nhắn gần đây</h4>
            {unreadCount > 0 && <span className="dropdown-item-badge">{unreadCount} mới</span>}
          </header>

          <div className="dropdown-list">
            {conversations.length === 0 ? (
              <div className="dropdown-empty">Không có cuộc trò chuyện nào</div>
            ) : (
              conversations.map(item => {
                const recipient = item.participants.find(p => p.userId !== currentUser.userId);
                return (
                  <button
                    key={item.id}
                    type="button"
                    className="dropdown-item-chat"
                    onClick={() => handleSelectChat(item.id)}
                  >
                    {recipient?.avatarUrl ? (
                      <img src={toAssetUrl(recipient.avatarUrl)} alt={item.title} className="avatar" />
                    ) : (
                      <div className="avatar-placeholder">
                        {item.title.trim().charAt(0).toUpperCase() || 'U'}
                      </div>
                    )}
                    <div className="dropdown-item-info">
                      <span className="dropdown-item-title">{item.title}</span>
                      <span className="dropdown-item-preview">
                        {item.lastMessagePreview || (item.type === 'Group' ? 'Nhóm chat' : 'Tin nhắn riêng')}
                      </span>
                    </div>
                    <div className="dropdown-item-meta">
                      {item.unreadCount > 0 && (
                        <span className="dropdown-item-badge">{item.unreadCount}</span>
                      )}
                    </div>
                  </button>
                );
              })
            )}
          </div>

          <footer className="dropdown-footer">
            <button type="button" className="view-all-link" onClick={handleViewAll}>
              Xem tất cả tin nhắn
            </button>
          </footer>
        </div>
      )}
    </div>
  );
}
