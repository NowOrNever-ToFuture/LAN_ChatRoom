namespace Chat.Shared.Models;

/// <summary>
/// Metadata DTO describing a file being transferred through the chat system.
/// Shared by both server and client to track file transfer state.
/// </summary>
public class FileTransferInfo
{
    public Guid FileId { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public bool IsImage { get; set; }

    public long TotalChunks { get; set; }

    /// <summary>
    /// Compute chunk count based on file size and the standard chunk size.
    /// </summary>
    public void ComputeTotalChunks(int chunkSize)
    {
        TotalChunks = (FileSize + chunkSize - 1) / chunkSize;
    }
}
