import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { env } from '../../config/env';
import { tokenStorage } from '../../shared/api/tokenStorage';
import type { ChatMessage, Conversation } from './types';

interface UseChatHubOptions {
  currentConversationId?: string | null;
  onMessage: (message: ChatMessage) => void;
  onConversationUpdated: (conversation: Conversation) => void;
  onParticipantRemoved: (conversationId: string, userId?: string) => void;
  onConversationClosed: (conversation: Conversation) => void;
}

export function useChatHub(options: UseChatHubOptions) {
  const connectionRef = useRef<HubConnection | null>(null);
  const currentConversationRef = useRef<string | null>(null);
  const handlersRef = useRef(options);
  const [isConnected, setIsConnected] = useState(false);

  currentConversationRef.current = options.currentConversationId ?? null;
  handlersRef.current = options;

  useEffect(() => {
    const token = tokenStorage.getAccessToken();
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${env.apiBaseUrl}/hubs/chat`, {
        accessTokenFactory: () => tokenStorage.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;
    connection.on('ReceiveMessage', message => handlersRef.current.onMessage(message));
    connection.on('ConversationUpdated', conversation => handlersRef.current.onConversationUpdated(conversation));
    connection.on('ParticipantRemoved', (payload: { conversationId: string; userId?: string }) => {
      handlersRef.current.onParticipantRemoved(payload.conversationId, payload.userId);
    });
    connection.on('ConversationClosed', conversation => handlersRef.current.onConversationClosed(conversation));

    connection.onreconnected(() => {
      setIsConnected(true);
      const conversationId = currentConversationRef.current;
      if (conversationId) {
        void connection.invoke('JoinConversation', conversationId).catch(() => undefined);
      }
    });
    connection.onreconnecting(() => setIsConnected(false));
    connection.onclose(() => setIsConnected(false));

    void connection.start()
      .then(async () => {
        setIsConnected(true);
        const conversationId = currentConversationRef.current;
        if (conversationId) {
          await connection.invoke('JoinConversation', conversationId).catch(() => undefined);
        }
      })
      .catch(() => setIsConnected(false));

    return () => {
      connectionRef.current = null;
      void connection.stop();
    };
  }, []);

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
