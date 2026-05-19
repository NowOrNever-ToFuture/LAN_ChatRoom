using System.Net.Sockets;

namespace Chat.Server.Models;

public class ConnectedClient
{
    public ConnectedClient(TcpClient tcpClient, string username)
    {
        TcpClient = tcpClient;
        Username = username;
    }

    public TcpClient TcpClient { get; }

    public string Username { get; }

    /// <summary>File transfer channel TCP client (port 8081), if connected.</summary>
    public TcpClient? FileTransferClient { get; set; }

    /// <summary>Set of active file transfer IDs this client is involved in.</summary>
    public HashSet<Guid> ActiveFileTransfers { get; } = new();
}
