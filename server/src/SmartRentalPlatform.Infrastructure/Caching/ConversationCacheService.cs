using Microsoft.Extensions.Caching.Memory;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Caching;

public sealed class ConversationCacheService : IConversationCacheService
{
    private const int MaxMessagesPerConversation = 20;
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Cache key prefix to avoid collisions with other cache users.
    /// </summary>
    private const string CacheKeyPrefix = "ai_chat_conversation_";

    private readonly IMemoryCache cache;

    public ConversationCacheService(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public IReadOnlyList<ConversationMessage> GetHistory(string conversationId)
    {
        var key = BuildKey(conversationId);
        return cache.Get<List<ConversationMessage>>(key) ?? [];
    }

    public void Append(string conversationId, ConversationMessage userMessage, ConversationMessage assistantMessage)
    {
        var key = BuildKey(conversationId);
        var history = cache.Get<List<ConversationMessage>>(key) ?? [];

        history.Add(userMessage);
        history.Add(assistantMessage);

        // Trim oldest messages if exceeding the limit
        if (history.Count > MaxMessagesPerConversation)
        {
            var excess = history.Count - MaxMessagesPerConversation;
            history.RemoveRange(0, excess);
        }

        cache.Set(key, history, SlidingExpiration);
    }

    public void Remove(string conversationId)
    {
        cache.Remove(BuildKey(conversationId));
    }

    private static string BuildKey(string conversationId)
        => $"{CacheKeyPrefix}{conversationId}";
}
