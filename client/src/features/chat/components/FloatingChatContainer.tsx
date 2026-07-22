import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getConversation } from '../api';
import { toAssetUrl } from '../../../shared/api/assets';
import { ChatWindow } from './ChatWindow';
import './FloatingChatContainer.css';

interface ChatBubble {
  id: string;
  title: string;
  avatarUrl?: string | null;
  type?: 'Direct' | 'Group';
  unreadCount?: number;
  minimized: boolean;
}

export function FloatingChatContainer() {
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const [bubbles, setBubbles] = useState<ChatBubble[]>([]);

  useEffect(() => {
    async function handleOpen(event: Event) {
      const customEvent = event as CustomEvent<{ conversationId: string }>;
      const { conversationId } = customEvent.detail;
      if (!conversationId) return;

      setBubbles(prev => {
        const existing = prev.find(b => b.id === conversationId);
        let next: ChatBubble[];
        if (existing) {
          next = [...prev.filter(b => b.id !== conversationId), { ...existing, minimized: false }];
        } else {
          next = [...prev, { id: conversationId, title: 'Trò chuyện', minimized: false, unreadCount: 0 }];
        }
        return next.slice(Math.max(0, next.length - 3));
      });

      try {
        const conversation = await getConversation(conversationId);
        const otherParticipant = conversation.participants.find(p => p.userId !== currentUser?.userId);
        const resolvedAvatar = conversation.avatarUrl || otherParticipant?.avatarUrl;

        setBubbles(prev => prev.map(b => b.id === conversationId ? {
          ...b,
          title: conversation.title,
          avatarUrl: resolvedAvatar,
          type: conversation.type,
          unreadCount: conversation.unreadCount
        } : b));
      } catch {
        // Fallback title remains if API error
      }
    }

    window.addEventListener('open-chat-bubble', handleOpen);
    return () => {
      window.removeEventListener('open-chat-bubble', handleOpen);
    };
  }, [currentUser]);

  const handleClose = useCallback((id: string) => {
    setBubbles(prev => prev.filter(b => b.id !== id));
  }, []);

  const handleToggleMinimize = useCallback((id: string, isMin?: boolean) => {
    setBubbles(prev => prev.map(b => {
      if (b.id === id) {
        const nextMin = isMin !== undefined ? isMin : !b.minimized;
        return { ...b, minimized: nextMin, unreadCount: nextMin ? b.unreadCount : 0 };
      }
      return b;
    }));
  }, []);

  const handleRestore = useCallback((id: string) => {
    setBubbles(prev => {
      const target = prev.find(b => b.id === id);
      if (!target) return prev;
      return [
        ...prev.filter(b => b.id !== id),
        { ...target, minimized: false, unreadCount: 0 }
      ];
    });
  }, []);

  const handleConversationUpdated = useCallback((conv: {
    id: string;
    title: string;
    avatarUrl?: string | null;
    unreadCount: number;
    participants: Array<{ userId: string; avatarUrl?: string | null }>;
  }) => {
    setBubbles(prev => prev.map(b => {
      if (b.id !== conv.id) return b;

      const otherParticipant = conv.participants.find(p => p.userId !== currentUser?.userId);
      return {
        ...b,
        title: conv.title,
        avatarUrl: conv.avatarUrl || otherParticipant?.avatarUrl,
        unreadCount: b.minimized ? conv.unreadCount : 0
      };
    }));
  }, [currentUser?.userId]);

  const minimizedBubbles = bubbles.filter(b => b.minimized);

  if (bubbles.length === 0) return null;

  return (
    <div className="floating-chat-container">
      {/* Minimized Stack */}
      {minimizedBubbles.length > 0 && (
        <div className="floating-chat-avatar-stack">
          {minimizedBubbles.map((bubble, index) => {
            const hasUnread = (bubble.unreadCount ?? 0) > 0;
            return (
              <button
                key={bubble.id}
                type="button"
                className="floating-chat-avatar-btn"
                style={{
                  transform: `translateX(-${index * 8}px)`,
                  zIndex: 100 + index
                }}
                onClick={() => handleRestore(bubble.id)}
                title={bubble.title}
              >
                {bubble.avatarUrl ? (
                  <img
                    src={toAssetUrl(bubble.avatarUrl)}
                    alt={bubble.title}
                    className="floating-chat-avatar-img"
                  />
                ) : (
                  <div className="floating-chat-avatar-placeholder">
                    {bubble.title.trim().charAt(0).toUpperCase() || 'U'}
                  </div>
                )}
                {hasUnread && (
                  <span className="floating-chat-avatar-badge">
                    {bubble.unreadCount}
                  </span>
                )}
              </button>
            );
          })}
        </div>
      )}

      {/* Active Windows */}
      <div className="floating-chat-active-windows">
        {bubbles.map(bubble => (
          <div
            key={bubble.id}
            className="floating-chat-active-window"
            style={{ display: bubble.minimized ? 'none' : 'block' }}
          >
            <ChatWindow
              conversationId={bubble.id}
              mode="bubble"
              isActive={!bubble.minimized}
              onCloseBubble={() => handleClose(bubble.id)}
              onMinimizeBubble={() => handleToggleMinimize(bubble.id, true)}
              onConversationUpdated={handleConversationUpdated}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
