using System.Text;
using Chat.Server.Services;
using Chat.Shared.Constants;
using Chat.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Server.Hubs;

/// <summary>
/// Main SignalR hub — handles all real-time communication:
/// text messages, images, file transfers, room management,
/// typing indicators, and message reactions.
/// </summary>
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly ServerLogger _logger;

    public ChatHub(ChatService chatService, ServerLogger logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    // ════════════════════════════════════════════
    // Connection lifecycle
    // ════════════════════════════════════════════

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_chatService.RemoveUser(Context.ConnectionId, out string? room, out string? username))
        {
            if (room is not null && username is not null)
            {
                _logger.LogConnection($"{username} disconnected from {room}.");
                await Clients.Group(room).SendAsync("ReceiveSystemMessage",
                    $"{username} đã rời phòng {room}.", room);
                await BroadcastUserListAsync(room);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ════════════════════════════════════════════
    // Room Management
    // ════════════════════════════════════════════

    /// <summary>Join a room: adds to SignalR group, pushes cache, broadcasts user list.</summary>
    public async Task JoinRoomAsync(string username, string roomName)
    {
        username = username.Trim();
        roomName = roomName.Trim();

        _chatService.AddUser(Context.ConnectionId, username, roomName);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        _logger.LogConnection($"{username} joined {roomName}.");

        // Push last 50 cached messages to this client
        var cached = _chatService.GetRoomCache(roomName);
        if (cached.Count > 0)
            await Clients.Caller.SendAsync("ReceiveCachedMessages", cached);

        // Notify room
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveSystemMessage",
            $"{username} đã tham gia phòng {roomName}.", roomName);

        await BroadcastUserListAsync(roomName);
    }

    /// <summary>Switch from one room to another atomically.</summary>
    public async Task SwitchRoomAsync(string oldRoom, string newRoom)
    {
        string? username = _chatService.GetUsername(Context.ConnectionId);
        if (username is null) return;

        // Leave old room
        if (!string.IsNullOrEmpty(oldRoom))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldRoom);
            _chatService.SwitchUserRoom(Context.ConnectionId, oldRoom, newRoom);
            await Clients.Group(oldRoom).SendAsync("ReceiveSystemMessage",
                $"{username} đã chuyển sang phòng {newRoom}.", oldRoom);
            await BroadcastUserListAsync(oldRoom);
        }
        else
        {
            _chatService.SwitchUserRoom(Context.ConnectionId, oldRoom, newRoom);
        }

        // Join new room
        await Groups.AddToGroupAsync(Context.ConnectionId, newRoom);
        _logger.LogRoom(newRoom, $"{username} switched to {newRoom}.");

        // Push cache of new room
        var cached = _chatService.GetRoomCache(newRoom);
        if (cached.Count > 0)
            await Clients.Caller.SendAsync("ReceiveCachedMessages", cached);

        await Clients.OthersInGroup(newRoom).SendAsync("ReceiveSystemMessage",
            $"{username} đã tham gia phòng {newRoom}.", newRoom);
        await BroadcastUserListAsync(newRoom);
    }

    // ════════════════════════════════════════════
    // Text Messaging
    // ════════════════════════════════════════════

    public async Task SendMessageAsync(ChatMessage message)
    {
        _chatService.CacheMessage(message);
        _logger.LogChat(message.SenderName, $"[{message.GroupName}] {message.Content}");
        await Clients.Group(message.GroupName).SendAsync("ReceiveMessage", message);
    }

    // Image and File transfers are now unified into SendMessageAsync
    // by embedding Base64 FileData into the ChatMessage model.

    // ════════════════════════════════════════════
    // Typing Indicator
    // ════════════════════════════════════════════

    public async Task SendTypingStatusAsync(string roomName, bool isTyping)
    {
        string? username = _chatService.GetUsername(Context.ConnectionId);
        if (username is null) return;
        await Clients.OthersInGroup(roomName).SendAsync("UserTypingStatus", username, isTyping);
    }

    // ════════════════════════════════════════════
    // Message Reactions
    // ════════════════════════════════════════════

    public async Task SendReactionAsync(string messageId, string reactionType)
    {
        string? username = _chatService.GetUsername(Context.ConnectionId);
        if (username is null) return;

        // Pass raw reactionType for broadcast so clients can render it faithfully,
        // but ChatService uses a canonical stripped key for counting.
        int count = _chatService.AddReaction(messageId, reactionType);
        string room = _chatService.GetUserRoom(Context.ConnectionId) ?? string.Empty;

        _logger.LogChat(username, $"Reacted {reactionType} on message {messageId} (count={count})");
        await Clients.Group(room).SendAsync("UpdateReaction", messageId, reactionType, count);
    }

    // ════════════════════════════════════════════
    // Private helpers
    // ════════════════════════════════════════════

    private async Task BroadcastUserListAsync(string room)
    {
        var users = _chatService.GetRoomUsers(room).ToArray();
        await Clients.Group(room).SendAsync("UpdateUserList", room, users);
    }
}
