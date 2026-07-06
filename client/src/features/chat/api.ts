import { apiClient } from '../../shared/api/apiClient';
import type { ApiResponse } from '../../shared/api/apiResponse.types';
import { ENDPOINTS } from '../../shared/api/endpoints';
import type { ChatMessage, ChatUser, Conversation, SendChatMessageRequest } from './types';

export async function getConversations(): Promise<Conversation[]> {
  const response = await apiClient<ApiResponse<Conversation[]>>(ENDPOINTS.CHAT.CONVERSATIONS, { auth: true });
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

export async function createGroupConversation(title: string, participantUserIds: string[]): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.GROUPS, {
    method: 'POST',
    auth: true,
    body: { title, participantUserIds }
  });
  return response.data;
}

export async function updateConversation(id: string, title: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.CONVERSATION(id), {
    method: 'PATCH',
    auth: true,
    body: { title }
  });
  return response.data;
}

export async function getMessages(conversationId: string, before?: string | null): Promise<ChatMessage[]> {
  const query = before ? `?before=${encodeURIComponent(before)}&limit=30` : '?limit=30';
  const response = await apiClient<ApiResponse<ChatMessage[]>>(
    `${ENDPOINTS.CHAT.MESSAGES(conversationId)}${query}`,
    { auth: true }
  );
  return response.data;
}

export async function sendMessage(conversationId: string, request: SendChatMessageRequest): Promise<ChatMessage> {
  const response = await apiClient<ApiResponse<ChatMessage>>(ENDPOINTS.CHAT.MESSAGES(conversationId), {
    method: 'POST',
    auth: true,
    body: request
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

export async function addParticipants(conversationId: string, userIds: string[]): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.PARTICIPANTS(conversationId), {
    method: 'POST',
    auth: true,
    body: { userIds }
  });
  return response.data;
}

export async function removeParticipant(conversationId: string, userId: string): Promise<Conversation> {
  const response = await apiClient<ApiResponse<Conversation>>(ENDPOINTS.CHAT.PARTICIPANT(conversationId, userId), {
    method: 'DELETE',
    auth: true
  });
  return response.data;
}

export async function getQuickContacts(): Promise<ChatUser[]> {
  const response = await apiClient<ApiResponse<ChatUser[]>>(ENDPOINTS.CHAT.QUICK_CONTACTS, { auth: true });
  return response.data;
}

export async function searchChatUsers(email: string): Promise<ChatUser[]> {
  const response = await apiClient<ApiResponse<ChatUser[]>>(
    `${ENDPOINTS.CHAT.SEARCH_USERS}?email=${encodeURIComponent(email)}`,
    { auth: true }
  );
  return response.data;
}

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
