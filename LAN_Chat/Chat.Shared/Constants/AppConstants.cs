namespace Chat.Shared.Constants;

public static class AppConstants
{
    // ── Chat channel ──
    public const int Port = 8080;
    public const string MessageSeparator = "|&|";
    public const string SystemSenderName = "[SYSTEM]";
    public const string ConnectCommand = "[CONNECT]";
    public const int MaxCachedMessages = 50;

    // ── File transfer channel ──
    public const int FileTransferPort = 8081;
    public const int ChunkSize = 65536; // 64 KB per chunk
    public const int ChunkHeaderSize = 52; // Guid(16) + long(8) + long(8) + long(8) + int(4) = 52 bytes (binary)

    // ── Commands ──
    public const string ImageCommand = "[IMAGE]";
    public const string FileCommand = "[FILE]";
    public const string ProgressCommand = "[PROGRESS]";
    public const string CompleteCommand = "[COMPLETE]";
    public const string FileConnectCommand = "[FILECONNECT]";
    public const string UserListCommand = "[USERLIST]";

    // ── Image constraints ──
    public const int MaxImageDimension = 1920;
    public const int ImageQuality = 80;
}
