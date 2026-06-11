using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media.Imaging;
using Chat.Shared.Models;

namespace ChatClient.Models;

/// <summary>
/// View model wrapping a ChatMessage for display in the WPF ListBox.
/// Adds UI-specific state: own-message flag, transfer status, reactions, etc.
/// </summary>
public class DisplayMessage : ChatMessage, INotifyPropertyChanged
{
    private double _transferProgress;
    private FileTransferStatus _transferStatus = FileTransferStatus.Pending;
    private BitmapSource? _imageThumbnail;

    public DisplayMessage(string senderName, string content, string currentUsername)
    {
        SenderName = senderName;
        Content = content;
        IsOwnMessage = string.Equals(senderName, currentUsername, StringComparison.OrdinalIgnoreCase);
        SenderDisplayName = IsOwnMessage ? $"{senderName} (You)" : senderName;
    }

    // ── Display properties ──
    public bool IsOwnMessage { get; }
    public string SenderDisplayName { get; }
    public bool IsSystemMessage { get; set; }
    public bool IsImageMessage { get; set; }
    public bool IsFileMessage { get; set; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss");
    public string FileSizeText => FileInfo is null ? string.Empty
        : FileInfo.FileSize < 1024 * 1024
            ? $"{FileInfo.FileSize / 1024.0:F1} KB"
            : $"{FileInfo.FileSize / (1024.0 * 1024):F1} MB";

    // ── File transfer state ──
    public FileTransferInfo? FileInfo { get; set; }
    public string? LocalFilePath
    {
        get => _localFilePath;
        set { _localFilePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSaveFile)); }
    }
    private string? _localFilePath;

    public double TransferProgress
    {
        get => _transferProgress;
        set { _transferProgress = value; OnPropertyChanged(); }
    }

    public FileTransferStatus TransferStatus
    {
        get => _transferStatus;
        set
        {
            _transferStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTransferring));
            OnPropertyChanged(nameof(IsCompleted));
        }
    }

    public bool IsTransferring => _transferStatus == FileTransferStatus.Transferring;
    public bool IsCompleted    => _transferStatus == FileTransferStatus.Completed;

    /// <summary>True only when the file is done AND the local copy exists for saving.</summary>
    public bool CanSaveFile    => IsCompleted && LocalFilePath is not null;

    // ── Image ──
    public BitmapSource? ImageThumbnail
    {
        get => _imageThumbnail;
        set
        {
            _imageThumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImageThumbnail));
        }
    }

    public bool HasImageThumbnail => _imageThumbnail is not null;

    // ── Reactions ──
    public new ObservableCollection<DisplayReaction> Reactions { get; } = [];

    public bool HasReactions => Reactions.Count > 0;

    public void UpdateReaction(string reactionType, int count)
    {
        // Strip variation selector (U+FE0F) to reliably match emoji bases
        // e.g. ❤ and ❤️ will match as the same key.
        string canonical = reactionType.Replace("\uFE0F", "");

        var existing = Reactions.FirstOrDefault(r =>
            r.ReactionType.Replace("\uFE0F", "") == canonical);

        if (count <= 0)
        {
            if (existing is not null) Reactions.Remove(existing);
        }
        else if (existing is not null)
        {
            existing.Count = count;
        }
        else
        {
            // Keep original string (with selector if present) for UI rendering
            Reactions.Add(new DisplayReaction { ReactionType = reactionType, Count = count });
        }
        OnPropertyChanged(nameof(HasReactions));
    }

    // ── INotifyPropertyChanged ──
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A single reaction type + count, observable for UI binding.</summary>
public class DisplayReaction : INotifyPropertyChanged
{
    private int _count;
    public string ReactionType { get; init; } = string.Empty;

    public int Count
    {
        get => _count;
        set { _count = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
