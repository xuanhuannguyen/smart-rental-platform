import { useCallback, useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { useAuth } from '../../../app/providers/AuthProvider';
import { getConversations } from '../../../features/chat/api';
import './MessageShortcut.css';

interface MessageShortcutProps {
  to: string;
}

export function MessageShortcut({ to }: MessageShortcutProps) {
  const { currentUser } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);

  const loadUnreadCount = useCallback(async () => {
    try {
      const conversations = await getConversations();
      setUnreadCount(conversations.reduce((total, item) => total + item.unreadCount, 0));
    } catch {
      setUnreadCount(0);
    }
  }, []);

  useEffect(() => {
    if (!currentUser) return;

    void loadUnreadCount();
    const interval = window.setInterval(loadUnreadCount, 30000);
    return () => window.clearInterval(interval);
  }, [currentUser, loadUnreadCount]);

  return (
    <NavLink
      to={to || ROUTE_PATHS.ACCOUNT.MESSAGES}
      className="message-shortcut-btn"
      title="Tin nhắn"
      aria-label={`Tin nhắn${unreadCount > 0 ? ` (${unreadCount} chưa đọc)` : ''}`}
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 15a2 2 0 0 1-2 2H8l-5 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
      </svg>
      {unreadCount > 0 && (
        <span className="message-shortcut-badge">
          {unreadCount > 99 ? '99+' : unreadCount}
        </span>
      )}
    </NavLink>
  );
}
