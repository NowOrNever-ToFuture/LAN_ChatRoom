using Chat.Shared.Constants;
using Chat.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ChatClient.Services;

/// <summary>
/// Wraps the SignalR HubConnection — replaces both NetworkService and FileTransferService.
/// Provides typed async methods for sending and typed events for receiving.
/// Supports automatic reconnect with UI-notification events.
/// </summary>
public sealed class ChatHubService : IAsyncDisposable
{
    private HubConnection? _connection;

    // ── Inbound events (Server → Client) ──
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string, string>? SystemMessageReceived;           // content, roomName
    public event Action<string, string[]>? UserListUpdated;               // roomName, usernames[]
    public event Action<List<ChatMessage>>? CachedMessagesReceived;
    public event Action<string, bool>? TypingStatusReceived;              // username, isTyping
    public event Action<string, string, int>? ReactionUpdated;            // messageId, reactionType, count

    // ── Connection state events ──
    public event Action<Exception?>? Reconnecting;
    public event Action<string?>? Reconnected;
    public event Action<Exception?>? Closed;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => State == HubConnectionState.Connected;

    // ════════════════════════════════════════════
    // Connect
    // ════════════════════════════════════════════

    public async Task ConnectAsync(string serverIp, string username, string roomName)
    {
        if (_connection is not null)
            await DisposeAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl($"http://{serverIp.Trim()}:{AppConstants.Port}{AppConstants.HubPath}")
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2),
                                     TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
                                     TimeSpan.FromSeconds(30)])
            .AddJsonProtocol(opt =>
            {
                // Preserve emoji in JSON
                opt.PayloadSerializerOptions.Encoder =
                    System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            })
            .Build();

        // ── Register inbound handlers ──
        _connection.On<ChatMessage>("ReceiveMessage",
            msg => MessageReceived?.Invoke(msg));

        _connection.On<string, string>("ReceiveSystemMessage",
            (content, room) => SystemMessageReceived?.Invoke(content, room));

        _connection.On<string, string[]>("UpdateUserList",
            (room, users) => UserListUpdated?.Invoke(room, users));

        _connection.On<List<ChatMessage>>("ReceiveCachedMessages",
            msgs => CachedMessagesReceived?.Invoke(msgs));

        _connection.On<string, bool>("UserTypingStatus",
            (username, isTyping) => TypingStatusReceived?.Invoke(username, isTyping));

        _connection.On<string, string, int>("UpdateReaction",
            (msgId, reactionType, count) => ReactionUpdated?.Invoke(msgId, reactionType, count));

        // ── Reconnect UI events ──
        _connection.Reconnecting  += ex => { Reconnecting?.Invoke(ex); return Task.CompletedTask; };
        _connection.Reconnected   += id => { Reconnected?.Invoke(id);  return Task.CompletedTask; };
        _connection.Closed        += ex => { Closed?.Invoke(ex);       return Task.CompletedTask; };

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinRoomAsync", username, roomName);
    }

    // ════════════════════════════════════════════
    // Outbound: Text, Image, File
    // ════════════════════════════════════════════

    public async Task SendMessageAsync(ChatMessage message)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SendMessageAsync", message);
    }

    // ════════════════════════════════════════════
    // Outbound: Typing, Reactions, Room switch
    // ════════════════════════════════════════════

    public async Task SendTypingStatusAsync(string roomName, bool isTyping)
    {
        if (!IsConnected) return;
        await _connection!.InvokeAsync("SendTypingStatusAsync", roomName, isTyping);
    }

    public async Task SendReactionAsync(string messageId, string reactionType)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SendReactionAsync", messageId, reactionType);
    }

    public async Task SwitchRoomAsync(string oldRoom, string newRoom)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SwitchRoomAsync", oldRoom, newRoom);
    }

    // ════════════════════════════════════════════
    // Cleanup
    // ════════════════════════════════════════════

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to the server.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
