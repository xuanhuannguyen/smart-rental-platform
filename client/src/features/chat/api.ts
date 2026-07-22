import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type {
  ChatMessage,
  ChatUser,
  Conversation,
  ConversationJoinRequest,
  SendChatMessageRequest,
  ChatFilterRoomingHouse,
  ChatCountsResponse
} from './types';

// ─── Conversations ───────────────────────────────────────────────

export async function getConversations(box?: 'main' | 'pending'): Promise<Conversation[]> {
  const query = box ? `?box=${box}` : '';
  const response = await apiClient<ApiResponse<Conversation[]>>(
    `${ENDPOINTS.CHAT.CONVERSATIONS}${query}`,
    { auth: true }
  );
  return response.data;
}

export async function getRecentConversations(box?: 'main' | 'pending', take = 5, skip = 0): Promise<Conversation[]> {
  const queryBox = box ? `box=${box}&` : '';
  const response = await apiClient<ApiResponse<Conversation[]>>(
    `${ENDPOINTS.CHAT.CONVERSATIONS_RECENT}?${queryBox}take=${take}&skip=${skip}`,
    { auth: true }
  );
  return response.data;
}

export async function getConversation(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    ENDPOINTS.CHAT.CONVERSATION(conversationId),
    { auth: true }
  );
  return response.data;
}

export async function getConversationCounts(): Promise<ChatCountsResponse> {
  const response = await apiClient<ApiResponse<ChatCountsResponse>>(
    ENDPOINTS.CHAT.CONVERSATION_COUNTS,
    { auth: true }
  );
  return response.data;
}

export async function createDirectConversation(otherUserId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.DIRECT, {
    method: 'POST',
    auth: true,
    body: { otherUserId }
  });
  return response.data;
}

export async function createDirectConversationByRoomingHouse(roomingHouseId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/direct/rooming-houses/${roomingHouseId}/open`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

export async function contactLandlord(roomingHouseId: string, initialMessage: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/direct/rooming-houses/${roomingHouseId}`,
    { method: 'POST', auth: true, body: { initialMessage } }
  );
  return response.data;
}

export async function createGroupConversation(
  title: string,
  participantUserIds: string[],
  roomingHouseId?: string | null,
  avatarUrl?: string | null
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.GROUPS, {
    method: 'POST',
    auth: true,
    body: { title, participantUserIds, roomingHouseId, avatarUrl }
  });
  return response.data;
}

export async function updateConversation(
  id: string,
  title?: string,
  avatarUrl?: string | null
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.CONVERSATION(id), {
    method: 'PATCH',
    auth: true,
    body: { title, avatarUrl }
  });
  return response.data;
}

export async function markConversationRead(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.READ(conversationId), {
    method: 'PATCH',
    auth: true
  });
  return response.data;
}

export async function leaveConversation(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.LEAVE(conversationId), {
    method: 'POST',
    auth: true
  });
  return response.data;
}

export async function closeConversation(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.CLOSE(conversationId), {
    method: 'POST',
    auth: true
  });
  return response.data;
}

export async function clearConversation(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/clear`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

// ─── Contact Request ─────────────────────────────────────────────

export async function acceptContactRequest(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/accept-contact-request`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

export async function rejectContactRequest(conversationId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/reject-contact-request`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

// ─── Participants ─────────────────────────────────────────────────

export async function addParticipants(conversationId: string, userIds: string[]): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.PARTICIPANTS(conversationId), {
    method: 'POST',
    auth: true,
    body: { userIds }
  });
  return response.data;
}

export async function removeParticipant(conversationId: string, userId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    ENDPOINTS.CHAT.PARTICIPANT(conversationId, userId),
    { method: 'DELETE', auth: true }
  );
  return response.data;
}

export async function updateParticipantRole(
  conversationId: string,
  userId: string,
  role: 'Owner' | 'Member'
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/participants/${userId}/role`,
    { method: 'PATCH', auth: true, body: { role } }
  );
  return response.data;
}

export async function getEligibleMembers(
  conversationId: string,
  roomingHouseId?: string | null
): Promise<ChatUser[]> {
  const query = roomingHouseId ? `?roomingHouseId=${encodeURIComponent(roomingHouseId)}` : '';
  const response = await apiClient<ApiResponse<ChatUser[]>>(
    `/api/chat/conversations/${conversationId}/eligible-members${query}`,
    { auth: true }
  );
  return response.data;
}

// ─── Join Requests ────────────────────────────────────────────────

export async function createJoinRequest(
  conversationId: string,
  targetUserId?: string | null
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/join-requests`,
    {
      method: 'POST',
      auth: true,
      body: targetUserId ? { targetUserId } : undefined
    }
  );
  return response.data;
}

export async function getJoinRequests(conversationId: string): Promise<ConversationJoinRequest[]> {
  const response = await apiClient<ApiResponse<ConversationJoinRequest[]>>(
    `/api/chat/conversations/${conversationId}/join-requests`,
    { auth: true }
  );
  return response.data;
}

export async function approveJoinRequest(
  conversationId: string,
  requestId: string
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/join-requests/${requestId}/approve`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

export async function rejectJoinRequest(conversationId: string, requestId: string): Promise<void> {
  await apiClient<ApiResponse<unknown>>(
    `/api/chat/conversations/${conversationId}/join-requests/${requestId}/reject`,
    { method: 'POST', auth: true }
  );
}

export async function updateApprovalSettings(
  conversationId: string,
  requiresJoinApproval: boolean
): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(
    `/api/chat/conversations/${conversationId}/approval-settings`,
    { method: 'PATCH', auth: true, body: { requiresJoinApproval } }
  );
  return response.data;
}

// ─── Messages ─────────────────────────────────────────────────────

export async function getMessages(conversationId: string, before?: string | null): Promise<ChatMessage[]> {
  const query = before ? `?before=${encodeURIComponent(before)}&limit=30` : '?limit=30';
  const response = await apiClient<ApiResponse<ChatMessage[]>>(
    `${ENDPOINTS.CHAT.MESSAGES(conversationId)}${query}`,
    { auth: true }
  );
  return response.data;
}

export async function sendMessage(
  conversationId: string,
  request: SendChatMessageRequest
): Promise<ChatMessage> {
  const response = await apiClient<ApiResponse<ChatMessage>>(
    ENDPOINTS.CHAT.MESSAGES(conversationId),
    { method: 'POST', auth: true, body: request }
  );
  return response.data;
}

/** Soft-delete (xóa) tin nhắn của mình qua DELETE endpoint */
export async function deleteChatMessage(
  conversationId: string,
  messageId: string
): Promise<ChatMessage> {
  const response = await apiClient<ApiResponse<ChatMessage>>(
    ENDPOINTS.CHAT.DELETE_MESSAGE(conversationId, messageId),
    { method: 'DELETE', auth: true }
  );
  return response.data;
}

/** Thu hồi (unsend) tin nhắn qua POST endpoint */
export async function unsendMessage(
  conversationId: string,
  messageId: string
): Promise<ChatMessage> {
  const response = await apiClient<ApiResponse<ChatMessage>>(
    `/api/chat/conversations/${conversationId}/messages/${messageId}/unsend`,
    { method: 'POST', auth: true }
  );
  return response.data;
}

export async function downloadChatFile(conversationId: string, messageId: string): Promise<Blob> {
  return apiClient<Blob>(
    `/api/chat/conversations/${conversationId}/messages/${messageId}/file`,
    { auth: true, responseType: 'blob' } as Parameters<typeof apiClient>[1]
  );
}

// ─── Users / Contacts ─────────────────────────────────────────────

export async function getQuickContacts(): Promise<ChatUser[]> {
  const response = await apiClient<ApiResponse<ChatUser[]>>(ENDPOINTS.CHAT.QUICK_CONTACTS, {
    auth: true
  });
  return response.data;
}

export async function searchChatUsers(
  email: string,
  roomingHouseId?: string | null
): Promise<ChatUser[]> {
  let query = `?email=${encodeURIComponent(email)}`;
  if (roomingHouseId) {
    query += `&roomingHouseId=${encodeURIComponent(roomingHouseId)}`;
  }
  const response = await apiClient<ApiResponse<ChatUser[]>>(
    `${ENDPOINTS.CHAT.SEARCH_USERS}${query}`,
    { auth: true }
  );
  return response.data;
}

export async function getActiveTenantsByRoomingHouse(roomingHouseId: string): Promise<ChatUser[]> {
  const response = await apiClient<ApiResponse<ChatUser[]>>(
    `/api/chat/landlord/rooming-houses/${roomingHouseId}/tenants`,
    { auth: true }
  );
  return response.data;
}

export async function getFilterRoomingHouses(): Promise<ChatFilterRoomingHouse[]> {
  const response = await apiClient<ApiResponse<ChatFilterRoomingHouse[]>>(
    ENDPOINTS.CHAT.FILTER_ROOMING_HOUSES,
    { auth: true }
  );
  return response.data;
}

export async function getUnreadMessageCount(): Promise<number> {
  const response = await apiClient<ApiResponse<number>>(ENDPOINTS.CHAT.UNREAD_COUNT, {
    auth: true
  });
  return response.data;
}

// ─── File / Image Upload ──────────────────────────────────────────

export async function uploadChatImage(file: File): Promise<string> {
  const formData = new FormData();
  formData.append('file', file);
  const response = await apiClient<ApiResponse<{ url: string }>>(ENDPOINTS.CHAT.IMAGES, {
    method: 'POST',
    auth: true,
    body: formData
  });
  return response.data.url;
}

/**
 * Upload file chat. Backend trả về { url, fileName, contentType, size }.
 * Normalize thành { url, fileName, contentType, size } để tương thích với ChatWindow.
 */
export async function uploadChatFile(
  file: File
): Promise<{ url: string; fileName: string; contentType: string; size: number }> {
  const formData = new FormData();
  formData.append('file', file);
  const response = await apiClient<ApiResponse<{
    url?: string;
    fileUrl?: string;
    fileName: string;
    contentType?: string;
    fileType?: string;
    fileContentType?: string;
    size?: number;
    fileSize?: number;
  }>>(ENDPOINTS.CHAT.FILES, {
    method: 'POST',
    auth: true,
    body: formData
  });
  const d = response.data;
  return {
    url: d.url ?? d.fileUrl ?? '',
    fileName: d.fileName,
    contentType: d.contentType ?? d.fileType ?? d.fileContentType ?? '',
    size: d.size ?? d.fileSize ?? 0
  };
}
