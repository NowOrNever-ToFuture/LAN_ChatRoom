using System.IO;
using System.Net.Sockets;
using System.Text;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace ChatClient.Services;

public sealed class NetworkService : IAsyncDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _receiveCts;

    public event EventHandler<string>? MessageReceived;

    public bool IsConnected => _tcpClient is { Connected: true };

    public async Task ConnectAsync(string serverIp, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverIp))
        {
            throw new ArgumentException("Server IP is required.", nameof(serverIp));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (IsConnected)
        {
            return;
        }

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(serverIp.Trim(), AppConstants.Port, cancellationToken);
        _networkStream = _tcpClient.GetStream();

        // Gửi handshake ngay sau khi kết nối để server ghi nhận username.
        string connectPacket = $"{AppConstants.ConnectCommand}{AppConstants.MessageSeparator}{username.Trim()}";
        await WritePacketAsync(_networkStream, connectPacket, cancellationToken);

        _receiveCts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    public async Task SendMessageAsync(string senderName, string content, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _networkStream is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(senderName) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ChatMessage chatMessage = new()
        {
            SenderName = senderName.Trim(),
            Content = content,
            Timestamp = DateTime.Now
        };

        string packet = $"{chatMessage.SenderName}{AppConstants.MessageSeparator}{chatMessage.Content}";
        await WritePacketAsync(_networkStream, packet, cancellationToken);
    }

    /// <summary>
    /// Send a pre-formatted packet string (used for IMAGE/FILE/PROGRESS/COMPLETE commands).
    /// </summary>
    public async Task SendRawPacketAsync(string packet, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _networkStream is null) return;
        await WritePacketAsync(_networkStream, packet, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_networkStream is null)
        {
            return;
        }

        try
        {
            // Luồng nền chỉ đọc data và bắn event; tuyệt đối không cập nhật UI tại đây.
            while (!cancellationToken.IsCancellationRequested)
            {
                string? packet = await ReadPacketAsync(_networkStream, cancellationToken);
                if (packet is null)
                {
                    break;
                }

                MessageReceived?.Invoke(this, packet);
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
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

    public async ValueTask DisposeAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        _networkStream?.Dispose();
        _tcpClient?.Dispose();
        _networkStream = null;
        _tcpClient = null;
    }
}
