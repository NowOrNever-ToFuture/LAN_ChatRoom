using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace Chat.Server.Services;

/// <summary>
/// Manages file transfers on a separate TCP port (8081).
/// Acts as a relay: receives binary chunks from the sender and forwards them to all
/// other clients that are subscribed to the same file transfer.
/// </summary>
public class FileTransferManager
{
    private readonly TcpListener _listener;
    private readonly ServerLogger _logger;

    // Maps FileId → list of receiver streams (clients waiting for chunks)
    private readonly ConcurrentDictionary<Guid, List<NetworkStream>> _fileReceivers = new();

    // Maps TcpClient → username (for file channel connections)
    private readonly ConcurrentDictionary<TcpClient, string> _fileClients = new();

    // All connected file-channel streams (for broadcasting)
    private readonly ConcurrentDictionary<string, TcpClient> _userFileClients = new();

    public FileTransferManager(ServerLogger logger)
    {
        _logger = logger;
        _listener = new TcpListener(IPAddress.Any, AppConstants.FileTransferPort);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        _logger.LogSystem($"File Transfer channel listening on port {AppConstants.FileTransferPort}...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleFileClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Register a username on the file channel so we can relay chunks to them.
    /// </summary>
    private async Task HandleFileClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string? username = null;
        try
        {
            NetworkStream stream = client.GetStream();

            // First packet is handshake: [FILECONNECT]|&|username
            byte[] handshakeBuf = new byte[4096];
            int bytesRead = await stream.ReadAsync(handshakeBuf, cancellationToken);
            if (bytesRead == 0) return;

            string handshake = System.Text.Encoding.UTF8.GetString(handshakeBuf, 0, bytesRead).Trim();
            string[] parts = handshake.Split(AppConstants.MessageSeparator, 2, StringSplitOptions.None);
            if (parts.Length != 2 || parts[0] != AppConstants.FileConnectCommand)
            {
                client.Dispose();
                return;
            }

            username = parts[1].Trim();
            _fileClients[client] = username;
            _userFileClients[username] = client;
            _logger.LogFileTransfer($"{username} connected to file transfer channel.");

            // Now this client can send or receive file chunks.
            // The relay loop reads chunk headers + payloads and forwards to all other file clients.
            await RelayChunksAsync(client, username, stream, cancellationToken);
        }
        catch (IOException)
        {
            // Client disconnected
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (username != null)
            {
                _userFileClients.TryRemove(username, out _);
                _logger.LogFileTransfer($"{username} disconnected from file transfer channel.");
            }
            _fileClients.TryRemove(client, out _);
            client.Dispose();
        }
    }

    private async Task RelayChunksAsync(TcpClient senderClient, string senderName, NetworkStream senderStream, CancellationToken cancellationToken)
    {
        byte[] headerBuf = new byte[FileChunkHeader.HeaderSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Read header
            int totalRead = 0;
            while (totalRead < FileChunkHeader.HeaderSize)
            {
                int read = await senderStream.ReadAsync(
                    headerBuf.AsMemory(totalRead, FileChunkHeader.HeaderSize - totalRead),
                    cancellationToken);
                if (read == 0) return; // connection closed
                totalRead += read;
            }

            FileChunkHeader header = FileChunkHeader.FromBytes(headerBuf);

            // Read payload
            byte[] payload = new byte[header.PayloadSize];
            totalRead = 0;
            while (totalRead < header.PayloadSize)
            {
                int read = await senderStream.ReadAsync(
                    payload.AsMemory(totalRead, header.PayloadSize - totalRead),
                    cancellationToken);
                if (read == 0) return;
                totalRead += read;
            }

            // Relay to all other connected file clients
            byte[] headerBytes = header.ToBytes();
            List<string> deadClients = new();

            foreach (var kvp in _userFileClients)
            {
                if (kvp.Key == senderName) continue; // don't echo back to sender

                try
                {
                    NetworkStream receiverStream = kvp.Value.GetStream();
                    await receiverStream.WriteAsync(headerBytes, cancellationToken);
                    await receiverStream.WriteAsync(payload, cancellationToken);
                    await receiverStream.FlushAsync(cancellationToken);
                }
                catch (Exception)
                {
                    deadClients.Add(kvp.Key);
                }
            }

            // Clean up dead clients
            foreach (string dead in deadClients)
            {
                if (_userFileClients.TryRemove(dead, out TcpClient? deadClient))
                {
                    _fileClients.TryRemove(deadClient, out _);
                    deadClient.Dispose();
                }
            }

            if (header.IsLastChunk == 1)
            {
                _logger.LogFileTransfer($"File {header.FileId} transfer complete from {senderName} ({header.TotalFileSize:N0} bytes).");
            }
        }
    }

    public void Stop()
    {
        _listener.Stop();
        foreach (var client in _fileClients.Keys)
        {
            client.Dispose();
        }
        _fileClients.Clear();
        _userFileClients.Clear();
    }
}
