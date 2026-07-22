export type ConversationType = 'Direct' | 'Group';
export type ChatMessageType = 'Text' | 'Icon' | 'Image' | 'System' | 'File';
export type InboxBox = 'main' | 'pending';

export interface ChatFilterRoomingHouse {
  id: string;
  name: string;
  address: string;
}

export interface ChatCountsResponse {
  mainUnreadCount: number;
  pendingCount: number;
}

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
  role: 'Owner' | 'Admin' | 'Member';
  source: string;
  joinedAt: string;
  leftAt?: string | null;
  inboxStatus?: 'Main' | 'Pending';
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
  // Stash additions
  avatarUrl?: string | null;
  requiresJoinApproval?: boolean;
  isCurrentUserAdmin?: boolean;
  canManageMembers?: boolean;
  inboxStatus?: 'Main' | 'Pending';
  roomingHouseId?: string | null;
  roomingHouseName?: string | null;
  roomingHouseAddress?: string | null;
}

export interface ConversationJoinRequest {
  id: string;
  conversationId: string;
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string | null;
  createdAt: string;

  // Backend response compatibility fields
  requesterUserId?: string;
  requesterDisplayName?: string;
  requesterEmail?: string;
  requesterAvatarUrl?: string | null;
  status?: string;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderAvatarUrl?: string | null;
  messageType: ChatMessageType;
  content?: string | null;
  imageUrl?: string | null;
  fileUrl?: string | null;
  fileName?: string | null;
  fileContentType?: string | null;
  fileType?: string | null; // Compatibility field
  fileSize?: number | null;
  clientMessageId?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  status?: 'sending' | 'sent' | 'error';
}

export interface SendChatMessageRequest {
  messageType: ChatMessageType;
  content?: string | null;
  imageUrl?: string | null;
  fileUrl?: string | null;
  fileName?: string | null;
  fileContentType?: string | null;
  fileType?: string | null; // Compatibility field
  fileSize?: number | null;
  clientMessageId?: string;
}
