import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { env } from '../../config/env';
import { useAuth } from '../../app/providers/AuthProvider';
import { getValidAccessToken } from '../../shared/api/getValidAccessToken';
import type { ChatMessage, Conversation } from './types';

interface UseChatHubOptions {
  currentConversationId?: string | null;
  onMessage: (message: ChatMessage) => void;
  onConversationUpdated: (conversation: Conversation) => void;
  onParticipantRemoved: (conversationId: string, userId?: string) => void;
  onConversationClosed: (conversation: Conversation) => void;
  onMessageDeleted?: (message: ChatMessage) => void;
  onUnreadCountUpdated?: (payload: {
    conversationId: string;
    unreadCount?: number;
    lastMessageAt?: string | null;
    conversation?: Conversation;
  }) => void;
  onMessageRead?: (payload: { conversationId: string; userId: string }) => void;
  onMessageCreated?: (payload: { message: ChatMessage; conversation: Conversation }) => void;
  onReconnected?: () => void;
}

export function useChatHub(options: UseChatHubOptions) {
  const { currentUser } = useAuth();
  const userId = currentUser?.userId;
  const connectionRef = useRef<HubConnection | null>(null);
  const currentConversationRef = useRef<string | null>(null);
  const handlersRef = useRef(options);
  const [isConnected, setIsConnected] = useState(false);

  currentConversationRef.current = options.currentConversationId ?? null;
  handlersRef.current = options;

  useEffect(() => {
    if (!userId) {
      setIsConnected(false);
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${env.apiBaseUrl}/hubs/chat`, {
        // accessTokenFactory is called on every (re)connect attempt.
        accessTokenFactory: () => getValidAccessToken().then(t => t ?? '')
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;
    let isDisposed = false;

    // ReceiveMessage is sent to the conversation group (JoinConversation required).
    // MessageCreated is sent to per-user groups and carries the same data.
    // When onMessageCreated is provided we rely on that exclusively to avoid duplicates.
    connection.on('ReceiveMessage', message => {
      if (!handlersRef.current.onMessageCreated) {
        handlersRef.current.onMessage(message);
      }
    });

    connection.on('ConversationUpdated', conversation => handlersRef.current.onConversationUpdated(conversation));
    connection.on('ParticipantRemoved', (payload: { conversationId: string; userId?: string }) => {
      handlersRef.current.onParticipantRemoved(payload.conversationId, payload.userId);
    });
    connection.on('ConversationClosed', conversation => handlersRef.current.onConversationClosed(conversation));
    connection.on('UnreadCountUpdated', payload => handlersRef.current.onUnreadCountUpdated?.(payload));
    connection.on('MessageRead', payload => handlersRef.current.onMessageRead?.(payload));
    connection.on('MessageDeleted', message => handlersRef.current.onMessageDeleted?.(message));

    connection.on('MessageCreated', payload => {
      if (handlersRef.current.onMessageCreated) {
        handlersRef.current.onMessageCreated(payload);
      } else {
        handlersRef.current.onMessage(payload.message);
      }
    });

    connection.onreconnected(() => {
      if (isDisposed || connectionRef.current !== connection) return;
      console.warn('SignalR chat connection restored successfully.');
      setIsConnected(true);
      const conversationId = currentConversationRef.current;
      if (conversationId) {
        void connection.invoke('JoinConversation', conversationId).catch(() => undefined);
      }
      handlersRef.current.onReconnected?.();
    });

    connection.onreconnecting((err) => {
      if (isDisposed || connectionRef.current !== connection) return;
      console.warn('SignalR chat connection lost. Reconnecting...', err);
      setIsConnected(false);
    });

    connection.onclose((err) => {
      if (isDisposed || connectionRef.current !== connection) return;
      console.warn('SignalR chat connection closed.', err);
      setIsConnected(false);
    });

    void connection.start()
      .then(async () => {
        if (isDisposed || connectionRef.current !== connection) {
          void connection.stop().catch(() => undefined);
          return;
        }
        setIsConnected(true);
        const conversationId = currentConversationRef.current;
        if (conversationId) {
          await connection.invoke('JoinConversation', conversationId).catch(() => undefined);
        }
      })
      .catch((err) => {
        if (isDisposed || connectionRef.current !== connection) return;
        console.warn('SignalR chat connection failed to start.', err);
        setIsConnected(false);
      });

    return () => {
      isDisposed = true;
      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
      setIsConnected(false);
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop().catch(() => undefined);
      }
    };
  }, [userId]);

  const joinConversation = useCallback(async (conversationId: string) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;
    await connection.invoke('JoinConversation', conversationId);
  }, []);

  const leaveConversation = useCallback(async (conversationId: string) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;
    await connection.invoke('LeaveConversation', conversationId).catch(() => undefined);
  }, []);

  const sendHubMessage = useCallback(async (conversationId: string, request: unknown) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) {
      throw new Error('SignalR is not connected.');
    }
    return connection.invoke('SendMessage', conversationId, request) as Promise<ChatMessage>;
  }, []);

  return {
    isConnected,
    joinConversation,
    leaveConversation,
    sendHubMessage
  };
}
