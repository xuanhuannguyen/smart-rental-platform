using System.Collections.Concurrent;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Caching;

public sealed class InMemoryChatPresenceTracker : IChatPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>> conversations = new();
    private readonly ConcurrentDictionary<string, List<(Guid ConversationId, Guid UserId)>> byConnection = new();

    public void JoinConversation(Guid conversationId, Guid userId, string connectionId)
    {
        var users = conversations.GetOrAdd(conversationId, _ => new ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>());
        var connections = users.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        connections[connectionId] = 0;

        byConnection.AddOrUpdate(
            connectionId,
            _ => new List<(Guid, Guid)> { (conversationId, userId) },
            (_, current) =>
            {
                lock (current)
                {
                    if (!current.Contains((conversationId, userId)))
                        current.Add((conversationId, userId));
                    return current;
                }
            });
    }

    public void LeaveConversation(Guid conversationId, Guid userId, string connectionId)
    {
        if (conversations.TryGetValue(conversationId, out var users) &&
            users.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
                users.TryRemove(userId, out _);
            if (users.IsEmpty)
                conversations.TryRemove(conversationId, out _);
        }

        if (byConnection.TryGetValue(connectionId, out var tracked))
        {
            lock (tracked)
            {
                tracked.RemoveAll(x => x.ConversationId == conversationId && x.UserId == userId);
                if (tracked.Count == 0)
                    byConnection.TryRemove(connectionId, out _);
            }
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (!byConnection.TryRemove(connectionId, out var tracked))
            return;

        List<(Guid ConversationId, Guid UserId)> snapshot;
        lock (tracked)
        {
            snapshot = tracked.ToList();
        }

        foreach (var (conversationId, userId) in snapshot)
        {
            if (!conversations.TryGetValue(conversationId, out var users) ||
                !users.TryGetValue(userId, out var connections))
                continue;

            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
                users.TryRemove(userId, out _);
            if (users.IsEmpty)
                conversations.TryRemove(conversationId, out _);
        }
    }

    public bool IsUserViewingConversation(Guid conversationId, Guid userId)
    {
        return conversations.TryGetValue(conversationId, out var users) &&
            users.TryGetValue(userId, out var connections) &&
            !connections.IsEmpty;
    }

    public IReadOnlyCollection<string> GetConversationConnectionIds(Guid conversationId, Guid userId)
    {
        if (!conversations.TryGetValue(conversationId, out var users) ||
            !users.TryGetValue(userId, out var connections))
            return Array.Empty<string>();

        return connections.Keys.ToList();
    }
}
