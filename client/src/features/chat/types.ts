export type ConversationType = 'Direct' | 'Group';
export type ChatMessageType = 'Text' | 'Icon' | 'Image' | 'System';

export interface ChatUser {
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string | null;
  roles: string[];
  contextLabel?: string | null;
}

export interface ChatParticipant {
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string | null;
  role: 'Owner' | 'Member';
  source: string;
  joinedAt: string;
  leftAt?: string | null;
}

export interface Conversation {
  id: string;
  type: ConversationType;
  title: string;
  createdByUserId: string;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  unreadCount: number;
  isClosed: boolean;
  isCurrentUserOwner: boolean;
  hasCurrentUserLeft: boolean;
  participants: ChatParticipant[];
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  messageType: ChatMessageType;
  content?: string | null;
  imageUrl?: string | null;
  clientMessageId?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  status?: 'sending' | 'sent' | 'error';
}

export interface SendChatMessageRequest {
  messageType: ChatMessageType;
  content?: string | null;
  imageUrl?: string | null;
  clientMessageId?: string;
}
