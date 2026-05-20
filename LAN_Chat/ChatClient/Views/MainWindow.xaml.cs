using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chat.Shared.Constants;
using Chat.Shared.Models;
using ChatClient.Models;
using ChatClient.Services;
using Microsoft.Win32;

namespace ChatClient;

public partial class MainWindow : Window
{
    private readonly NetworkService _networkService = new();
    private readonly FileTransferService _fileTransferService = new();
    private string _username = string.Empty;

    private readonly Dictionary<Guid, DisplayMessage> _fileMessages = new();
    // Pending file metadata for receivers — only shown after [COMPLETE]
    private readonly Dictionary<Guid, (string SenderName, string FileName, long FileSize)> _pendingFileMetadata = new();

    private readonly string[] _emojiList =
    [
        "😀","😃","😄","😁","😆","😅","🤣","😂","🙂","😊",
        "😇","🥰","😍","🤩","😘","😗","😚","😋","😛","😜",
        "🤪","😝","🤑","🤗","🤭","🤫","🤔","🤐","🤨","😐",
        "😑","😶","😏","😒","🙄","😬","😮‍💨","🤥","😌","😔",
        "😪","🤤","😴","😷","🤒","🤕","🤢","🤮","🥵","🥶",
        "🥴","😵","🤯","🤠","🥳","🥸","😎","🤓","🧐","😕",
        "😟","🙁","😮","😯","😲","😳","🥺","😦","😨","😰",
        "😥","😢","😭","😱","😖","😣","😞","😓","😩","😫",
        "🥱","😤","😡","😠","🤬","👍","👎","👏","🙏","💪",
        "🔥","✨","🎉","❤️","🧡","💛","💚","💙","💜","🖤",
        "💯","⭐","✅","❌","💬","📌","☕","🍕","🎮","🚀"
    ];

    public MainWindow()
    {
        InitializeComponent();

        Messages = new ObservableCollection<DisplayMessage>();
        OnlineUsers = new ObservableCollection<OnlineUser>();
        DataContext = this;

        EmojiItemsControl.ItemsSource = _emojiList;

        _networkService.MessageReceived += OnMessageReceived;
        _fileTransferService.FileProgressReceived += OnFileProgress;
        _fileTransferService.FileCompleted += OnFileCompleted;
    }

    public ObservableCollection<DisplayMessage> Messages { get; }
    public ObservableCollection<OnlineUser> OnlineUsers { get; }

    // ── Connection ──

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_networkService.IsConnected) return;

        string serverIp = ServerIpTextBox.Text.Trim();
        string username = UsernameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(serverIp) || string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Vui lòng nhập Server IP và Username.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _networkService.ConnectAsync(serverIp, username);
            _username = username;

            try { await _fileTransferService.ConnectAsync(serverIp, username); }
            catch { /* File transfer channel is optional */ }

            ServerIpTextBox.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            StatusTextBlock.Text = "Online";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkGreen;
            MessageInputTextBox.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể kết nối tới server: {ex.Message}", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Send Message ──

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendCurrentMessageAsync();

    private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await SendCurrentMessageAsync(); }
    }

    private async Task SendCurrentMessageAsync()
    {
        if (!_networkService.IsConnected)
        {
            MessageBox.Show("Bạn cần kết nối tới server trước khi gửi tin nhắn.", "Chưa kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string content = MessageInputTextBox.Text;
        if (string.IsNullOrWhiteSpace(content)) return;

        await _networkService.SendMessageAsync(_username, content);
        MessageInputTextBox.Clear();
        MessageInputTextBox.Focus();
    }

    // ── Emoji ──

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
    }

    private void EmojiItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string emoji) return;

        int caretIndex = MessageInputTextBox.CaretIndex;
        MessageInputTextBox.Text = MessageInputTextBox.Text.Insert(caretIndex, emoji);
        MessageInputTextBox.CaretIndex = caretIndex + emoji.Length;
        MessageInputTextBox.Focus();
        EmojiPopup.IsOpen = false;
    }

    // ── Image ──

    private async void ImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_networkService.IsConnected)
        {
            MessageBox.Show("Bạn cần kết nối tới server trước.", "Chưa kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*",
            Title = "Select an image to send"
        };

        if (dialog.ShowDialog() != true) return;
        await SendImageAsync(dialog.FileName);
    }

    private async Task SendImageAsync(string filePath)
    {
        try
        {
            byte[] imageData = await ImageService.CompressImageAsync(filePath);
            string fileName = Path.GetFileName(filePath);
            Guid fileId = Guid.NewGuid();

            // Encode image as Base64 and send inline via chat channel
            string base64Data = Convert.ToBase64String(imageData);

            string metadataPacket = $"{AppConstants.ImageCommand}{AppConstants.MessageSeparator}" +
                                    $"{_username}{AppConstants.MessageSeparator}" +
                                    $"{fileId}{AppConstants.MessageSeparator}" +
                                    $"{fileName}{AppConstants.MessageSeparator}" +
                                    $"{imageData.Length}{AppConstants.MessageSeparator}" +
                                    $"{base64Data}";
            await _networkService.SendRawPacketAsync(metadataPacket);

            // Show thumbnail locally for the sender
            Application.Current.Dispatcher.Invoke(() =>
            {
                var thumbnail = ImageService.CreateThumbnail(imageData);
                FileTransferInfo info = new()
                {
                    FileId = fileId, FileName = fileName, FileSize = imageData.Length,
                    SenderName = _username, IsImage = true
                };
                DisplayMessage msg = new(_username, $"Sent image: {fileName}", _username)
                {
                    IsImageMessage = true, FileInfo = info, ImageThumbnail = thumbnail,
                    TransferStatus = FileTransferStatus.Completed
                };
                Messages.Add(msg);
                _fileMessages[fileId] = msg;
                MessagesListBox.ScrollIntoView(msg);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── File ──

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_networkService.IsConnected || !_fileTransferService.IsConnected)
        {
            MessageBox.Show("Bạn cần kết nối tới server trước.", "Chưa kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenFileDialog dialog = new() { Filter = "All files|*.*", Title = "Select a file to send" };
        if (dialog.ShowDialog() != true) return;
        await SendFileAsync(dialog.FileName);
    }

    private async Task SendFileAsync(string filePath)
    {
        try
        {
            FileInfo fi = new(filePath);
            Guid fileId = Guid.NewGuid();

            FileTransferInfo info = new()
            {
                FileId = fileId, FileName = fi.Name, FileSize = fi.Length,
                SenderName = _username, IsImage = false
            };
            info.ComputeTotalChunks(AppConstants.ChunkSize);

            string metadataPacket = $"{AppConstants.FileCommand}{AppConstants.MessageSeparator}" +
                                    $"{_username}{AppConstants.MessageSeparator}" +
                                    $"{fileId}{AppConstants.MessageSeparator}" +
                                    $"{fi.Name}{AppConstants.MessageSeparator}" +
                                    $"{fi.Length}";
            await _networkService.SendRawPacketAsync(metadataPacket);

            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessage msg = new(_username, $"Sent file: {fi.Name}", _username)
                {
                    IsFileMessage = true, FileInfo = info,
                    TransferStatus = FileTransferStatus.Transferring, LocalFilePath = filePath
                };
                Messages.Add(msg);
                _fileMessages[fileId] = msg;
                MessagesListBox.ScrollIntoView(msg);
            });

            Progress<double> progress = new(p =>
                Application.Current.Dispatcher.Invoke(() => { if (_fileMessages.TryGetValue(fileId, out var m)) m.TransferProgress = p; }));

            await _fileTransferService.SendFileAsync(filePath, fileId, progress);

            Application.Current.Dispatcher.Invoke(() =>
                { if (_fileMessages.TryGetValue(fileId, out var m)) m.TransferStatus = FileTransferStatus.Completed; });

            await _networkService.SendRawPacketAsync($"{AppConstants.CompleteCommand}{AppConstants.MessageSeparator}{fileId}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to send file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Click handlers for message templates ──

    private void ImageThumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image img || img.DataContext is not DisplayMessage message) return;
        if (message.LocalFilePath != null && File.Exists(message.LocalFilePath))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = message.LocalFilePath, UseShellExecute = true }); }
            catch { }
        }
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DisplayMessage message }) return;
        if (message.LocalFilePath == null || !File.Exists(message.LocalFilePath)) return;

        SaveFileDialog dialog = new() { FileName = message.FileInfo?.FileName ?? "file", Title = "Save file as" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.Copy(message.LocalFilePath, dialog.FileName, overwrite: true);
            MessageBox.Show($"File saved to: {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Incoming message handling ──

    private void OnMessageReceived(object? sender, string packet)
    {
        if (!TryParsePacket(packet, out string senderName, out string content)) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (senderName == AppConstants.UserListCommand) HandleUserList(content);
            else if (senderName == AppConstants.ImageCommand) HandleImageMetadata(content);
            else if (senderName == AppConstants.FileCommand) HandleFileMetadata(content);
            else if (senderName == AppConstants.ProgressCommand) HandleProgress(content);
            else if (senderName == AppConstants.CompleteCommand) HandleComplete(content);
            else
            {
                DisplayMessage displayMessage = new(senderName, content, _username);
                Messages.Add(displayMessage);
                MessagesListBox.ScrollIntoView(displayMessage);
            }
        });
    }

    private void HandleUserList(string content)
    {
        OnlineUsers.Clear();
        if (string.IsNullOrWhiteSpace(content)) { OnlineCountText.Text = "0 users"; return; }

        string[] users = content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string user in users)
        {
            OnlineUsers.Add(new OnlineUser(user));
        }
        OnlineCountText.Text = $"{users.Length} user{(users.Length != 1 ? "s" : "")}";
    }

    private void HandleImageMetadata(string content)
    {
        string[] parts = content.Split(AppConstants.MessageSeparator);
        if (parts.Length < 5) return;

        string senderName = parts[0];
        if (!Guid.TryParse(parts[1], out Guid fileId)) return;
        string fileName = parts[2];
        if (!long.TryParse(parts[3], out long fileSize)) return;
        string base64Data = parts[4];

        // Skip if own message (already displayed locally)
        if (string.Equals(senderName, _username, StringComparison.OrdinalIgnoreCase) && _fileMessages.ContainsKey(fileId))
            return;

        // Decode Base64 image data and create thumbnail
        try
        {
            byte[] imageData = Convert.FromBase64String(base64Data);
            var thumbnail = ImageService.CreateThumbnail(imageData);

            // Save to temp for "Open" functionality
            string tempDir = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received");
            Directory.CreateDirectory(tempDir);
            string savePath = Path.Combine(tempDir, $"{fileId}_{fileName}");
            File.WriteAllBytes(savePath, imageData);

            FileTransferInfo info = new()
            {
                FileId = fileId, FileName = fileName, FileSize = fileSize,
                SenderName = senderName, IsImage = true
            };

            DisplayMessage msg = new(senderName, $"Sent image: {fileName}", _username)
            {
                IsImageMessage = true, FileInfo = info, ImageThumbnail = thumbnail,
                TransferStatus = FileTransferStatus.Completed, LocalFilePath = savePath
            };
            Messages.Add(msg);
            _fileMessages[fileId] = msg;
            MessagesListBox.ScrollIntoView(msg);
        }
        catch { /* Invalid Base64 data */ }
    }

    private void HandleFileMetadata(string content)
    {
        string[] parts = content.Split(AppConstants.MessageSeparator);
        if (parts.Length < 4) return;

        string senderName = parts[0];
        if (!Guid.TryParse(parts[1], out Guid fileId)) return;
        string fileName = parts[2];
        if (!long.TryParse(parts[3], out long fileSize)) return;

        // Skip if own message (sender already shows progress locally)
        if (string.Equals(senderName, _username, StringComparison.OrdinalIgnoreCase) && _fileMessages.ContainsKey(fileId))
            return;

        // For receivers: DON'T show the file message yet — save metadata and wait for [COMPLETE]
        _pendingFileMetadata[fileId] = (senderName, fileName, fileSize);

        // Register to receive the binary data in background
        string tempDir = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received");
        Directory.CreateDirectory(tempDir);
        _fileTransferService.RegisterFileReceive(fileId, Path.Combine(tempDir, $"{fileId}_{fileName}"), fileSize);
    }

    private void HandleProgress(string content)
    {
        string[] parts = content.Split(AppConstants.MessageSeparator);
        if (parts.Length < 2) return;
        if (Guid.TryParse(parts[0], out Guid fileId) && double.TryParse(parts[1], out double percent))
            if (_fileMessages.TryGetValue(fileId, out var msg)) msg.TransferProgress = percent;
    }

    private void HandleComplete(string content)
    {
        if (!Guid.TryParse(content.Trim(), out Guid fileId)) return;

        // If sender's own message — just update status
        if (_fileMessages.TryGetValue(fileId, out var msg))
        {
            msg.TransferStatus = FileTransferStatus.Completed;
            return;
        }

        // For receiver: now create the DisplayMessage from pending metadata
        if (_pendingFileMetadata.TryGetValue(fileId, out var meta))
        {
            _pendingFileMetadata.Remove(fileId);

            string tempDir = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received");
            string savePath = Path.Combine(tempDir, $"{fileId}_{meta.FileName}");

            FileTransferInfo info = new()
            {
                FileId = fileId, FileName = meta.FileName, FileSize = meta.FileSize,
                SenderName = meta.SenderName, IsImage = false
            };

            DisplayMessage fileMsg = new(meta.SenderName, $"Sent file: {meta.FileName}", _username)
            {
                IsFileMessage = true, FileInfo = info,
                TransferStatus = FileTransferStatus.Completed,
                LocalFilePath = File.Exists(savePath) ? savePath : null
            };
            Messages.Add(fileMsg);
            _fileMessages[fileId] = fileMsg;
            MessagesListBox.ScrollIntoView(fileMsg);
        }
    }

    // ── File transfer events ──

    private void OnFileProgress(object? sender, FileProgressEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_fileMessages.TryGetValue(e.FileId, out var msg))
            { msg.TransferProgress = e.Progress; msg.TransferStatus = FileTransferStatus.Transferring; }
        });
    }

    private void OnFileCompleted(object? sender, FileCompletedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_fileMessages.TryGetValue(e.FileId, out var msg))
            {
                msg.TransferStatus = FileTransferStatus.Completed;
                msg.TransferProgress = 100;
                msg.LocalFilePath = e.FilePath;

                if (msg.IsImageMessage && File.Exists(e.FilePath))
                {
                    try { msg.ImageThumbnail = ImageService.CreateThumbnail(File.ReadAllBytes(e.FilePath)); }
                    catch { }
                }
            }
        });
    }

    // ── Utilities ──

    private static bool TryParsePacket(string packet, out string senderName, out string content)
    {
        senderName = string.Empty;
        content = string.Empty;
        if (string.IsNullOrWhiteSpace(packet)) return false;

        string[] parts = packet.Split(AppConstants.MessageSeparator, 2, StringSplitOptions.None);
        if (parts.Length != 2) return false;

        senderName = parts[0];
        content = parts[1];
        return true;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _networkService.MessageReceived -= OnMessageReceived;
        _fileTransferService.FileProgressReceived -= OnFileProgress;
        _fileTransferService.FileCompleted -= OnFileCompleted;
        await _fileTransferService.DisposeAsync();
        await _networkService.DisposeAsync();
        base.OnClosed(e);
    }
}

/// <summary>Simple model for the online users sidebar.</summary>
public class OnlineUser
{
    public OnlineUser(string name)
    {
        Name = name;
        Initial = string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpper();
    }

    public string Name { get; }
    public string Initial { get; }
}
