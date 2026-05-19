using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace ChatClient.Models;

public class DisplayMessage : ChatMessage, INotifyPropertyChanged
{
    private double _transferProgress;
    private FileTransferStatus _transferStatus = FileTransferStatus.Pending;
    private BitmapImage? _imageThumbnail;

    public DisplayMessage()
    {
    }

    public DisplayMessage(string senderName, string content, string currentUsername)
    {
        SenderName = senderName;
        Content = content;
        Timestamp = DateTime.Now;
        IsOwnMessage = string.Equals(senderName, currentUsername, StringComparison.OrdinalIgnoreCase);
        IsSystemMessage = string.Equals(senderName, AppConstants.SystemSenderName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsOwnMessage { get; set; }

    public bool IsSystemMessage { get; set; }

    public bool IsImageMessage { get; set; }

    public bool IsFileMessage { get; set; }

    public FileTransferInfo? FileInfo { get; set; }

    public BitmapImage? ImageThumbnail
    {
        get => _imageThumbnail;
        set { _imageThumbnail = value; OnPropertyChanged(); }
    }

    public double TransferProgress
    {
        get => _transferProgress;
        set { _transferProgress = value; OnPropertyChanged(); }
    }

    public FileTransferStatus TransferStatus
    {
        get => _transferStatus;
        set { _transferStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTransferring)); OnPropertyChanged(nameof(IsCompleted)); }
    }

    public bool IsTransferring => TransferStatus == FileTransferStatus.Transferring;
    public bool IsCompleted => TransferStatus == FileTransferStatus.Completed;

    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public string SenderDisplayName => IsOwnMessage ? $"{SenderName} (You)" : SenderName;

    public string FileSizeText => FileInfo is null ? string.Empty : FormatFileSize(FileInfo.FileSize);

    /// <summary>Path to the downloaded file on disk (set after transfer completes).</summary>
    public string? LocalFilePath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
