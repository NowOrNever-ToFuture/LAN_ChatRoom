using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Chat.Server.Models;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace Chat.Server.Services;

public class ServerManager
{
    private readonly TcpListener _listener;
    private readonly Dictionary<TcpClient, string> _connectedClients = new();
    private readonly Queue<string> _messageCache = new();
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public ServerManager()
    {
        _listener = new TcpListener(IPAddress.Any, AppConstants.Port);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        Console.WriteLine($"LAN Chat Server is running on port {AppConstants.Port}...");

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
            _ = HandleClientAsync(tcpClient, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        string? username = null;

        try
        {
            using NetworkStream stream = tcpClient.GetStream();

            // Handshake đầu tiên bắt buộc có dạng: [CONNECT]|&|Username
            string? handshakePacket = await ReadPacketAsync(stream, cancellationToken);
            if (!TryParsePacket(handshakePacket, out string command, out string handshakePayload) || command != AppConstants.ConnectCommand)
            {
                return;
            }

            username = handshakePayload.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            await AddClientAsync(tcpClient, username, cancellationToken);
            await SendCachedMessagesAsync(stream, cancellationToken);
            await BroadcastSystemMessageAsync($"{username} đã tham gia phòng chat.", cancellationToken);

            // Vòng lặp nhận tin nhắn từ client và chuyển tiếp cho toàn bộ phòng.
            while (!cancellationToken.IsCancellationRequested)
            {
                string? incomingPacket = await ReadPacketAsync(stream, cancellationToken);
                if (incomingPacket is null)
                {
                    break;
                }

                if (!TryParsePacket(incomingPacket, out string senderName, out string content))
                {
                    continue;
                }

                ChatMessage chatMessage = new()
                {
                    SenderName = senderName,
                    Content = content,
                    Timestamp = DateTime.Now
                };

                string broadcastPacket = CreatePacket(chatMessage.SenderName, chatMessage.Content);
                await AddMessageToCacheAsync(broadcastPacket, cancellationToken);
                await BroadcastMessageAsync(broadcastPacket, cancellationToken);
            }
        }
        catch (IOException)
        {
            // Client đóng app/rớt mạng: xử lý ở finally để xóa khỏi danh sách online.
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await RemoveClientAsync(tcpClient, username, cancellationToken);
            tcpClient.Dispose();
        }
    }

    public async Task BroadcastMessageAsync(string packet, CancellationToken cancellationToken = default)
    {
        List<ConnectedClient> clients = await SnapshotClientsAsync(cancellationToken);

        foreach (ConnectedClient client in clients)
        {
            try
            {
                await WritePacketAsync(client.TcpClient.GetStream(), packet, cancellationToken);
            }
            catch (IOException)
            {
                await RemoveClientAsync(client.TcpClient, client.Username, cancellationToken, notifyRoom: true);
                client.TcpClient.Dispose();
            }
            catch (ObjectDisposedException)
            {
                await RemoveClientAsync(client.TcpClient, client.Username, cancellationToken, notifyRoom: true);
            }
        }
    }

    private async Task AddClientAsync(TcpClient tcpClient, string username, CancellationToken cancellationToken)
    {
        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            _connectedClients[tcpClient] = username;
        }
        finally
        {
            _clientLock.Release();
        }

        Console.WriteLine($"{username} connected.");
    }

    private async Task RemoveClientAsync(TcpClient tcpClient, string? username, CancellationToken cancellationToken, bool notifyRoom = true)
    {
        string? removedUsername = username;
        bool wasConnected = false;

        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            if (_connectedClients.TryGetValue(tcpClient, out string? registeredUsername))
            {
                removedUsername = registeredUsername;
                _connectedClients.Remove(tcpClient);
                wasConnected = true;
            }
        }
        finally
        {
            _clientLock.Release();
        }

        if (wasConnected && notifyRoom && !string.IsNullOrWhiteSpace(removedUsername))
        {
            Console.WriteLine($"{removedUsername} disconnected.");
            await BroadcastSystemMessageAsync($"{removedUsername} đã rời phòng chat.", cancellationToken);
        }
    }

    private async Task<List<ConnectedClient>> SnapshotClientsAsync(CancellationToken cancellationToken)
    {
        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            return _connectedClients
                .Select(item => new ConnectedClient(item.Key, item.Value))
                .ToList();
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task SendCachedMessagesAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        List<string> cachedMessages;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            cachedMessages = _messageCache.ToList();
        }
        finally
        {
            _cacheLock.Release();
        }

        foreach (string cachedMessage in cachedMessages)
        {
            await WritePacketAsync(stream, cachedMessage, cancellationToken);
        }
    }

    private async Task AddMessageToCacheAsync(string packet, CancellationToken cancellationToken)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _messageCache.Enqueue(packet);

            while (_messageCache.Count > AppConstants.MaxCachedMessages)
            {
                _messageCache.Dequeue();
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task BroadcastSystemMessageAsync(string content, CancellationToken cancellationToken)
    {
        string packet = CreatePacket(AppConstants.SystemSenderName, content);
        await AddMessageToCacheAsync(packet, cancellationToken);
        await BroadcastMessageAsync(packet, cancellationToken);
    }

    private static bool TryParsePacket(string? packet, out string senderName, out string content)
    {
        senderName = string.Empty;
        content = string.Empty;

        if (string.IsNullOrWhiteSpace(packet))
        {
            return false;
        }

        string[] parts = packet.Split(AppConstants.MessageSeparator, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return false;
        }

        senderName = parts[0];
        content = parts[1];
        return true;
    }

    private static string CreatePacket(string senderName, string content)
    {
        return $"{senderName}{AppConstants.MessageSeparator}{content}";
    }

    private static async Task<string?> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        List<byte> bytes = new();
        byte[] buffer = new byte[1];

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
            if (bytesRead == 0)
            {
                return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
            }

            if (buffer[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\r');
            }

            bytes.Add(buffer[0]);
        }
    }

    private static async Task WritePacketAsync(NetworkStream stream, string packet, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(packet + Environment.NewLine);
        await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
