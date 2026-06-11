namespace Chat.Shared.Models;

/// <summary>
/// Metadata DTO for a file being transferred via SignalR chunked streaming.
/// </summary>
public class FileTransferInfo
{
    public Guid FileId { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsImage { get; set; }
}
