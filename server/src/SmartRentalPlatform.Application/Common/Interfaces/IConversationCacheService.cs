namespace SmartRentalPlatform.Application.Common.Interfaces;

/// <summary>
/// Stores short-term conversation history for the AI chatbot.
/// Each conversation is keyed by a client-provided ConversationId.
/// History expires after 30 minutes of inactivity.
/// </summary>
public interface IConversationCacheService
{
    /// <summary>
    /// Return the existing history for a conversation, or an empty list.
    /// </summary>
    IReadOnlyList<ConversationMessage> GetHistory(string conversationId);

    /// <summary>
    /// Append a user message and its assistant reply to the conversation history.
    /// </summary>
    void Append(string conversationId, ConversationMessage userMessage, ConversationMessage assistantMessage);

    /// <summary>
    /// Remove a conversation from the cache entirely.
    /// </summary>
    void Remove(string conversationId);
}

public sealed record ConversationMessage(string Role, string Text);
