using System.Collections.Concurrent;
using System.Text;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace Chat.Server.Services;

/// <summary>
/// Singleton service managing all shared server state:
/// connected users, per-room message caches, and message reactions.
/// </summary>
public class ChatService
{
    // connectionId → UserInfo
    private readonly ConcurrentDictionary<string, UserInfo> _users = new();

    // roomName → Queue<ChatMessage> (capped at MaxCachedMessages)
    private readonly Dictionary<string, Queue<ChatMessage>> _roomCaches;

    // messageId → (reactionType → count) — add-only, never decrements
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _reactions = new();

    private readonly object _cacheLock = new();
    private readonly object _reactionLock = new();

    public ChatService()
    {
        _roomCaches = AppConstants.DefaultRooms.ToDictionary(
            r => r,
            _ => new Queue<ChatMessage>());
    }

    // ── User management ──

    public void AddUser(string connectionId, string username, string room)
    {
        _users[connectionId] = new UserInfo(username, room);
    }

    /// <summary>Returns true if user existed and was removed.</summary>
    public bool RemoveUser(string connectionId, out string? room, out string? username)
    {
        if (_users.TryRemove(connectionId, out var info))
        {
            room = info.Room;
            username = info.Username;
            return true;
        }
        room = null;
        username = null;
        return false;
    }

    public void SwitchUserRoom(string connectionId, string oldRoom, string newRoom)
    {
        if (_users.TryGetValue(connectionId, out var info))
            _users[connectionId] = info with { Room = newRoom };
    }

    public string? GetUsername(string connectionId) =>
        _users.TryGetValue(connectionId, out var info) ? info.Username : null;

    public string? GetUserRoom(string connectionId) =>
        _users.TryGetValue(connectionId, out var info) ? info.Room : null;

    public IEnumerable<string> GetRoomUsers(string room) =>
        _users.Values
              .Where(u => u.Room == room)
              .Select(u => u.Username)
              .Distinct();

    // ── Message cache ──

    public void CacheMessage(ChatMessage message)
    {
        lock (_cacheLock)
        {
            if (!_roomCaches.TryGetValue(message.GroupName, out var queue)) return;

            // For Image/File: store only metadata (strip Base64 payload).
            // This keeps the cache small and prevents the reconnect-push from
            // exceeding SignalR's MaximumReceiveMessageSize.
            ChatMessage toCache = message.Type is MessageType.Image or MessageType.File
                ? new ChatMessage
                {
                    Id         = message.Id,
                    SenderName = message.SenderName,
                    Content    = message.Content,   // filename
                    GroupName  = message.GroupName,
                    Timestamp  = message.Timestamp,
                    Type       = message.Type,
                    FileSize   = message.FileSize,
                    FileData   = null               // ← stripped
                }
                : message;

            queue.Enqueue(toCache);
            if (queue.Count > AppConstants.MaxCachedMessages)
                queue.Dequeue();
        }
    }

    public IReadOnlyList<ChatMessage> GetRoomCache(string room)
    {
        lock (_cacheLock)
        {
            if (!_roomCaches.TryGetValue(room, out var queue))
                return [];

            var list = queue.ToList();

            // Embed current reaction snapshot into each message so clients
            // can restore reactions when switching channels.
            lock (_reactionLock)
            {
                foreach (var msg in list)
                {
                    if (_reactions.TryGetValue(msg.Id, out var msgReactions) && msgReactions.Count > 0)
                    {
                        msg.Reactions = msgReactions
                            .Where(kv => kv.Value > 0)
                            .ToDictionary(kv => kv.Key, kv => kv.Value);
                    }
                }
            }

            return list;
        }
    }

    // ── Reactions ──

    /// <summary>
    /// Add a reaction (add-only, never decrements).
    /// Each call from any user increments the count for this emoji on this message.
    /// Returns the new total count.
    /// </summary>
    public int AddReaction(string messageId, string reactionType)
    {
        // Strip variation selector so that ❤ and ❤️ map to the exact same dictionary key.
        string canonical = reactionType.Replace("\uFE0F", "");

        lock (_reactionLock)
        {
            var messageReactions = _reactions.GetOrAdd(messageId,
                _ => new Dictionary<string, int>());

            if (!messageReactions.TryGetValue(canonical, out int current))
                current = 0;

            messageReactions[canonical] = current + 1;
            return messageReactions[canonical];
        }
    }
}

/// <summary>Immutable user state stored per connection.</summary>
internal record UserInfo(string Username, string Room);
