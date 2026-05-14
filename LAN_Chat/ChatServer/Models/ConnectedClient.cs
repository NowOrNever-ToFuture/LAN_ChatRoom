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
}
