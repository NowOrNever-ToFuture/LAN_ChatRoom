using System.IO;
using System.Net.Sockets;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace ChatClient.Services;

/// <summary>
/// Manages file transfers on a separate TCP connection (port 8081).
/// Runs entirely on background threads so chat remains responsive.
/// </summary>
public sealed class FileTransferService : IAsyncDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private readonly Dictionary<Guid, FileReceiveContext> _activeReceives = new();
    private readonly object _receiveLock = new();

    /// <summary>Fired when a file chunk is received (for progress tracking).</summary>
    public event EventHandler<FileProgressEventArgs>? FileProgressReceived;

    /// <summary>Fired when a complete file has been received and saved.</summary>
    public event EventHandler<FileCompletedEventArgs>? FileCompleted;

    public bool IsConnected => _tcpClient is { Connected: true };

    /// <summary>
    /// Connect to the file transfer channel on the server.
    /// </summary>
    public async Task ConnectAsync(string serverIp, string username, CancellationToken cancellationToken = default)
    {
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(serverIp, AppConstants.FileTransferPort, cancellationToken);
        _stream = _tcpClient.GetStream();

        // Handshake
        string handshake = $"{AppConstants.FileConnectCommand}{AppConstants.MessageSeparator}{username}\n";
        byte[] handshakeBytes = System.Text.Encoding.UTF8.GetBytes(handshake);
        await _stream.WriteAsync(handshakeBytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    /// <summary>
    /// Send a file over the file transfer channel. Runs asynchronously with progress reporting.
    /// </summary>
    public async Task SendFileAsync(string filePath, Guid fileId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected to file transfer channel.");

        FileInfo fi = new(filePath);
        long totalSize = fi.Length;
        long chunkIndex = 0;
        long totalSent = 0;

        byte[] buffer = new byte[AppConstants.ChunkSize];

        await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

        while (totalSent < totalSize)
        {
            int bytesRead = await fs.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) break;

            bool isLast = (totalSent + bytesRead) >= totalSize;

            FileChunkHeader header = new()
            {
                FileId = fileId,
                ChunkIndex = chunkIndex,
                PayloadSize = bytesRead,
                TotalFileSize = totalSize,
                IsLastChunk = isLast ? 1 : 0,
                Reserved = 0
            };

            byte[] headerBytes = header.ToBytes();
            await _stream.WriteAsync(headerBytes, cancellationToken);
            await _stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            totalSent += bytesRead;
            chunkIndex++;

            progress?.Report((double)totalSent / totalSize * 100);
        }
    }

    /// <summary>
    /// Register a file to be received. Call this when you get an IMAGE or FILE metadata packet.
    /// </summary>
    public void RegisterFileReceive(Guid fileId, string savePath, long totalSize)
    {
        lock (_receiveLock)
        {
            _activeReceives[fileId] = new FileReceiveContext
            {
                FileId = fileId,
                SavePath = savePath,
                TotalSize = totalSize,
                ReceivedSize = 0
            };
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_stream is null) return;

        byte[] headerBuf = new byte[FileChunkHeader.HeaderSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Read header
                int totalRead = 0;
                while (totalRead < FileChunkHeader.HeaderSize)
                {
                    int read = await _stream.ReadAsync(
                        headerBuf.AsMemory(totalRead, FileChunkHeader.HeaderSize - totalRead),
                        cancellationToken);
                    if (read == 0) return;
                    totalRead += read;
                }

                FileChunkHeader header = FileChunkHeader.FromBytes(headerBuf);

                // Read payload
                byte[] payload = new byte[header.PayloadSize];
                totalRead = 0;
                while (totalRead < header.PayloadSize)
                {
                    int read = await _stream.ReadAsync(
                        payload.AsMemory(totalRead, header.PayloadSize - totalRead),
                        cancellationToken);
                    if (read == 0) return;
                    totalRead += read;
                }

                // Write to file
                FileReceiveContext? ctx;
                lock (_receiveLock)
                {
                    _activeReceives.TryGetValue(header.FileId, out ctx);
                }

                if (ctx != null)
                {
                    // Ensure directory exists
                    string? dir = Path.GetDirectoryName(ctx.SavePath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    await using FileStream fs = new(ctx.SavePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    fs.Seek(ctx.ReceivedSize, SeekOrigin.Begin);
                    await fs.WriteAsync(payload.AsMemory(0, header.PayloadSize), cancellationToken);

                    ctx.ReceivedSize += header.PayloadSize;
                    double progress = ctx.TotalSize > 0 ? (double)ctx.ReceivedSize / ctx.TotalSize * 100 : 0;

                    FileProgressReceived?.Invoke(this, new FileProgressEventArgs(header.FileId, progress));

                    if (header.IsLastChunk == 1)
                    {
                        lock (_receiveLock)
                        {
                            _activeReceives.Remove(header.FileId);
                        }
                        FileCompleted?.Invoke(this, new FileCompletedEventArgs(header.FileId, ctx.SavePath));
                    }
                }
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        _stream?.Dispose();
        _tcpClient?.Dispose();
        _stream = null;
        _tcpClient = null;
    }

    private class FileReceiveContext
    {
        public Guid FileId { get; init; }
        public string SavePath { get; init; } = string.Empty;
        public long TotalSize { get; init; }
        public long ReceivedSize { get; set; }
    }
}

public class FileProgressEventArgs(Guid fileId, double progress) : EventArgs
{
    public Guid FileId { get; } = fileId;
    public double Progress { get; } = progress;
}

public class FileCompletedEventArgs(Guid fileId, string filePath) : EventArgs
{
    public Guid FileId { get; } = fileId;
    public string FilePath { get; } = filePath;
}
