using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chat.Shared.Constants;
using Chat.Shared.Models;
using ChatClient.Models;
using ChatClient.Services;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ChatClient;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ── Services ──
    private readonly ChatHubService _hubService = new();

    // ── State ──
    private string _username = string.Empty;
    private string _currentRoom = AppConstants.DefaultRoom;
    // All messages keyed by Id for reaction lookup
    private readonly Dictionary<string, DisplayMessage> _messageById = new();

    // Typing indicator debounce
    private System.Threading.Timer? _typingTimer;
    private bool _isTyping;

    // ── Observable properties ──
    public ObservableCollection<DisplayMessage> Messages { get; } = [];
    public ObservableCollection<OnlineUser> OnlineUsers { get; } = [];

    private ObservableCollection<string> _typingUsers = [];
    public bool HasTypingUsers => _typingUsers.Count > 0;
    public string TypingIndicatorText =>
        _typingUsers.Count == 0 ? string.Empty
        : $"{string.Join(", ", _typingUsers)} đang nhập tin nhắn...";

    private readonly string[] _emojiList =
    [
        "😀","😃","😄","😁","😆","😅","🤣","😂","🙂","😊",
        "😇","🥰","😍","🤩","😘","😗","😚","😋","😛","😜",
        "🤪","😝","🤑","🤗","🤭","🤫","🤔","🤐","🤨","😐",
        "😑","😶","😏","😒","🙄","😬","🤥","😌","😔","😪",
        "🤤","😴","😷","🤒","🤕","🤢","🤮","🥵","🥶","🥴",
        "😵","🤯","🤠","🥳","😎","🤓","🧐","😕","😟","🙁",
        "😮","😯","😲","😳","🥺","😦","😨","😰","😥","😢",
        "😭","😱","😖","😣","😞","😓","😩","😫","🥱","😤",
        "😡","😠","🤬","👍","👎","👏","🙏","💪","🔥","✨",
        "🎉","❤️","🧡","💛","💚","💙","💜","🖤","💯","🚀"
    ];

    // ────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        EmojiItemsControl.ItemsSource = _emojiList;
        OnlineUsersPanel.ItemsSource = OnlineUsers;

        // Register SignalR event handlers
        _hubService.MessageReceived     += OnMessageReceived;
        _hubService.SystemMessageReceived += OnSystemMessageReceived;
        _hubService.UserListUpdated     += OnUserListUpdated;
        _hubService.CachedMessagesReceived += OnCachedMessagesReceived;
        _hubService.TypingStatusReceived += OnTypingStatusReceived;
        _hubService.ReactionUpdated     += OnReactionUpdated;
        _hubService.Reconnecting        += OnReconnecting;
        _hubService.Reconnected         += OnReconnected;
        _hubService.Closed              += OnConnectionClosed;
    }

    // ══════════════════════════════════════════════════════
    // INotifyPropertyChanged
    // ══════════════════════════════════════════════════════

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ══════════════════════════════════════════════════════
    // Connection
    // ══════════════════════════════════════════════════════

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        string serverIp = ServerIpTextBox.Text.Trim();
        string username = UsernameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(serverIp) || string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Vui lòng nhập Server IP và Username.", "Thiếu thông tin",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConnectButton.IsEnabled = false;
        try
        {
            _username = username;
            await _hubService.ConnectAsync(serverIp, username, _currentRoom);

            StatusTextBlock.Text = $"Online • {_currentRoom}";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            ConnectButton.Content = "Đã kết nối";
        }
        catch (Exception ex)
        {
            ConnectButton.IsEnabled = true;
            MessageBox.Show($"Không thể kết nối: {ex.Message}", "Lỗi kết nối",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _typingTimer?.Dispose();
        await _hubService.DisposeAsync();
    }

    // ══════════════════════════════════════════════════════
    // Room Tabs
    // ══════════════════════════════════════════════════════

    private async void RoomTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        string newRoom = rb.Tag?.ToString() ?? AppConstants.DefaultRoom;
        if (newRoom == _currentRoom) return;

        string oldRoom = _currentRoom;
        _currentRoom = newRoom;
        CurrentRoomText.Text = newRoom;

        // Clear current messages — we'll get cached messages from server
        Dispatcher.Invoke(Messages.Clear);
        _messageById.Clear();

        if (_hubService.IsConnected)
        {
            await _hubService.SwitchRoomAsync(oldRoom, newRoom);
            StatusTextBlock.Text = $"Online • {newRoom}";
        }
    }

    // ══════════════════════════════════════════════════════
    // Sending: Text
    // ══════════════════════════════════════════════════════

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendTextMessageAsync();
    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
        {
            e.Handled = true;
            await SendTextMessageAsync();
        }
    }

    private async Task SendTextMessageAsync()
    {
        if (!_hubService.IsConnected)
        {
            MessageBox.Show("Chưa kết nối server.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string text = MessageInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        MessageInputTextBox.Clear();

        // Stop typing indicator
        if (_isTyping)
        {
            _isTyping = false;
            _typingTimer?.Dispose();
            _ = _hubService.SendTypingStatusAsync(_currentRoom, false);
        }

        var message = new ChatMessage
        {
            SenderName = _username,
            Content = text,
            GroupName = _currentRoom
        };

        // Optimistic local display
        var dm = AddDisplayMessage(new DisplayMessage(_username, text, _username));
        _messageById[message.Id] = dm;

        try { await _hubService.SendMessageAsync(message); }
        catch (Exception ex)
        {
            MessageBox.Show($"Gửi tin nhắn thất bại: {ex.Message}", "Lỗi");
        }
    }

    // ══════════════════════════════════════════════════════
    // Sending: Image
    // ══════════════════════════════════════════════════════

    private async void ImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hubService.IsConnected) { MessageBox.Show("Chưa kết nối server."); return; }
        var dlg = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*",
            Title = "Chọn ảnh để gửi"
        };
        if (dlg.ShowDialog() != true) return;
        await SendImageAsync(dlg.FileName);
    }

    private async Task SendImageAsync(string filePath)
    {
        try
        {
            byte[] data = await ImageService.CompressImageAsync(filePath);
            string fileName = Path.GetFileName(filePath);
            string base64 = Convert.ToBase64String(data);

            var message = new ChatMessage
            {
                SenderName = _username,
                Content = fileName,
                GroupName = _currentRoom,
                Type = MessageType.Image,
                FileData = base64,
                FileSize = data.Length
            };

            var dm = CreateDisplayMessage(message);
            AddDisplayMessage(dm);
            _messageById[message.Id] = dm;

            await _hubService.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gửi ảnh thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════
    // Sending: File
    // ══════════════════════════════════════════════════════

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hubService.IsConnected) { MessageBox.Show("Chưa kết nối server."); return; }
        var dlg = new OpenFileDialog { Title = "Chọn file để gửi" };
        if (dlg.ShowDialog() != true) return;
        await SendFileAsync(dlg.FileName);
    }

    private async Task SendFileAsync(string filePath)
    {
        try
        {
            string fileName = Path.GetFileName(filePath);
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            long fileSize = fileBytes.Length;

            var message = new ChatMessage
            {
                SenderName = _username,
                Content = fileName,
                GroupName = _currentRoom,
                Type = MessageType.File,
                FileData = Convert.ToBase64String(fileBytes),
                FileSize = fileSize
            };

            var dm = CreateDisplayMessage(message);
            AddDisplayMessage(dm);
            _messageById[message.Id] = dm;

            await _hubService.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gửi file thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════
    // Message Rendering Helper
    // ══════════════════════════════════════════════════════

    private DisplayMessage CreateDisplayMessage(ChatMessage msg)
    {
        var dm = new DisplayMessage(msg.SenderName, msg.Content, _username)
        {
            Id = msg.Id,
            GroupName = msg.GroupName,
            Timestamp = msg.Timestamp
        };

        if (msg.Type == MessageType.Image && !string.IsNullOrEmpty(msg.FileData))
        {
            try
            {
                byte[] data = Convert.FromBase64String(msg.FileData);
                var thumbnail = ImageService.CreateThumbnail(data);

                string tempDir = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received");
                Directory.CreateDirectory(tempDir);
                string savePath = Path.Combine(tempDir, $"{msg.Id}_{msg.Content}");
                if (!File.Exists(savePath)) File.WriteAllBytes(savePath, data);

                dm.IsImageMessage = true;
                dm.FileInfo = new FileTransferInfo { FileId = Guid.Parse(msg.Id), FileName = msg.Content, FileSize = msg.FileSize, SenderName = msg.SenderName, IsImage = true };
                dm.ImageThumbnail = thumbnail;
                dm.TransferStatus = FileTransferStatus.Completed;
                dm.LocalFilePath = savePath;
                dm.Content = $"Gửi ảnh: {msg.Content}";
            }
            catch { /* ignore bad base64 */ }
        }
        else if (msg.Type == MessageType.Image && string.IsNullOrEmpty(msg.FileData))
        {
            // Cached image — FileData stripped on server.
            // Try to load thumbnail from local temp if this client already received it.
            dm.IsImageMessage = true;
            dm.FileInfo = new FileTransferInfo { FileId = Guid.Parse(msg.Id), FileName = msg.Content, FileSize = msg.FileSize, SenderName = msg.SenderName, IsImage = true };
            dm.TransferStatus = FileTransferStatus.Completed;
            dm.Content = $"Gửi ảnh: {msg.Content}";

            string localPath = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received", $"{msg.Id}_{msg.Content}");
            if (File.Exists(localPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(localPath);
                    dm.ImageThumbnail = ImageService.CreateThumbnail(data);
                    dm.LocalFilePath = localPath;
                }
                catch { dm.ImageThumbnail = null; }
            }
            // else: ImageThumbnail stays null → placeholder shown in XAML
        }
        else if (msg.Type == MessageType.File && !string.IsNullOrEmpty(msg.FileData))
        {
            try
            {
                byte[] data = Convert.FromBase64String(msg.FileData);
                string tempDir = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received");
                Directory.CreateDirectory(tempDir);
                string savePath = Path.Combine(tempDir, $"{msg.Id}_{msg.Content}");
                if (!File.Exists(savePath)) File.WriteAllBytes(savePath, data);

                dm.IsFileMessage = true;
                dm.FileInfo = new FileTransferInfo { FileId = Guid.Parse(msg.Id), FileName = msg.Content, FileSize = msg.FileSize, SenderName = msg.SenderName };
                dm.TransferStatus = FileTransferStatus.Completed;
                dm.LocalFilePath = savePath;
                dm.TransferProgress = 100;
                dm.Content = $"Gửi file: {msg.Content}";
            }
            catch { /* ignore bad base64 */ }
        }
        else if (msg.Type == MessageType.File && string.IsNullOrEmpty(msg.FileData))
        {
            // Cached file — FileData stripped.
            dm.IsFileMessage = true;
            dm.FileInfo = new FileTransferInfo { FileId = Guid.Parse(msg.Id), FileName = msg.Content, FileSize = msg.FileSize, SenderName = msg.SenderName };
            dm.TransferStatus = FileTransferStatus.Completed;
            dm.Content = $"Gửi file: {msg.Content}";

            // Restore LocalFilePath if file still exists in temp
            string localPath = Path.Combine(Path.GetTempPath(), "LAN_Chat_Received", $"{msg.Id}_{msg.Content}");
            if (File.Exists(localPath))
                dm.LocalFilePath = localPath; // re-enables Lưu button
        }

        // Restore reactions from cache snapshot (populated by server in GetRoomCache)
        if (msg.Reactions is { Count: > 0 })
        {
            foreach (var (reactionType, count) in msg.Reactions)
                dm.UpdateReaction(reactionType, count);
        }

        return dm;
    }

    // ══════════════════════════════════════════════════════
    // Receive Handlers (all dispatch to UI thread)
    // ══════════════════════════════════════════════════════

    private void OnMessageReceived(ChatMessage msg)
    {
        // Skip if it's our own optimistic message (already shown)
        if (string.Equals(msg.SenderName, _username, StringComparison.OrdinalIgnoreCase))
        {
            // Update the Id of the optimistic message if needed
            return;
        }
        Dispatcher.Invoke(() =>
        {
            var dm = CreateDisplayMessage(msg);
            AddDisplayMessage(dm);
            _messageById[msg.Id] = dm;
        });
    }

    private void OnSystemMessageReceived(string content, string room)
    {
        if (room != _currentRoom) return;
        Dispatcher.Invoke(() =>
        {
            AddDisplayMessage(new DisplayMessage(AppConstants.SystemSenderName, content, _username)
            { IsSystemMessage = true });
        });
    }

    private void OnUserListUpdated(string room, string[] users)
    {
        if (room != _currentRoom) return;
        Dispatcher.Invoke(() =>
        {
            OnlineUsers.Clear();
            foreach (var u in users)
                OnlineUsers.Add(new OnlineUser(u));
        });
    }

    private void OnCachedMessagesReceived(List<ChatMessage> messages)
    {
        Dispatcher.Invoke(() =>
        {
            Messages.Clear();
            _messageById.Clear();
            foreach (var msg in messages)
            {
                var dm = CreateDisplayMessage(msg);
                Messages.Add(dm);
                _messageById[msg.Id] = dm;
            }
            ScrollToBottom();
        });
    }

    private void OnTypingStatusReceived(string username, bool isTyping)
    {
        Dispatcher.Invoke(() =>
        {
            if (isTyping && !_typingUsers.Contains(username))
                _typingUsers.Add(username);
            else if (!isTyping)
                _typingUsers.Remove(username);

            OnPropertyChanged(nameof(HasTypingUsers));
            OnPropertyChanged(nameof(TypingIndicatorText));
        });
    }

    private void OnReactionUpdated(string messageId, string reactionType, int count)
    {
        Dispatcher.Invoke(() =>
        {
            if (_messageById.TryGetValue(messageId, out var dm))
                dm.UpdateReaction(reactionType, count);
        });
    }

    // ══════════════════════════════════════════════════════
    // Reconnect handlers
    // ══════════════════════════════════════════════════════

    private void OnReconnecting(Exception? ex)
    {
        Dispatcher.Invoke(() =>
        {
            ReconnectBanner.Visibility = Visibility.Visible;
            ReconnectText.Text = "Đang thử kết nối lại...";
            InputGrid.IsEnabled = false;
            InputGrid.Opacity = 0.5;
            StatusTextBlock.Text = "Đang kết nối lại...";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
        });
    }

    private async void OnReconnected(string? connectionId)
    {
        Dispatcher.Invoke(() =>
        {
            ReconnectBanner.Visibility = Visibility.Collapsed;
            InputGrid.IsEnabled = true;
            InputGrid.Opacity = 1.0;
            StatusTextBlock.Text = $"Online • {_currentRoom}";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
        });
        // Re-join current room to refresh user list + get cache
        try { await _hubService.SwitchRoomAsync(string.Empty, _currentRoom); }
        catch { /* ignore */ }
    }

    private void OnConnectionClosed(Exception? ex)
    {
        Dispatcher.Invoke(() =>
        {
            ReconnectBanner.Visibility = Visibility.Visible;
            ReconnectText.Text = ex is null ? "Đã ngắt kết nối." : $"Mất kết nối: {ex.Message}";
            InputGrid.IsEnabled = false;
            StatusTextBlock.Text = "Offline";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.IndianRed;
            ConnectButton.IsEnabled = true;
            ConnectButton.Content = "Kết nối";
        });
    }

    // ══════════════════════════════════════════════════════
    // Emoji
    // ══════════════════════════════════════════════════════

    private void EmojiToggle_Click(object sender, RoutedEventArgs e)
        => EmojiPopup.IsOpen = !EmojiPopup.IsOpen;

    private void EmojiButton_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emoji)
        {
            MessageInputTextBox.Text += emoji;
            MessageInputTextBox.CaretIndex = MessageInputTextBox.Text.Length;
            EmojiPopup.IsOpen = false;
            MessageInputTextBox.Focus();
        }
    }

    // ══════════════════════════════════════════════════════
    // Typing indicator (debounce)
    // ══════════════════════════════════════════════════════

    private void MessageInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_hubService.IsConnected) return;

        if (!_isTyping)
        {
            _isTyping = true;
            _ = _hubService.SendTypingStatusAsync(_currentRoom, true);
        }

        _typingTimer?.Dispose();
        _typingTimer = new System.Threading.Timer(_ =>
        {
            _isTyping = false;
            _ = _hubService.SendTypingStatusAsync(_currentRoom, false);
            _typingTimer = null;
        }, null, 2000, System.Threading.Timeout.Infinite);
    }

    // ══════════════════════════════════════════════════════
    // Reactions
    // ══════════════════════════════════════════════════════

    private async void Reaction_Click(object sender, RoutedEventArgs e)
    {
        if (!_hubService.IsConnected) return;
        if (sender is not Button btn) return;

        string reactionType = btn.Tag?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(reactionType)) return;

        // Walk up the LOGICAL tree from the Button to find the parent ContextMenu.
        // btn.Parent is the StackPanel (IsItemsHost=true), not the ContextMenu itself.
        DependencyObject? node = btn;
        ContextMenu? cm = null;
        while (node is not null)
        {
            if (node is ContextMenu found) { cm = found; break; }
            node = LogicalTreeHelper.GetParent(node);
        }

        if (cm?.PlacementTarget is not FrameworkElement target) return;

        // PlacementTarget is the Border; its DataContext (inherited) is the DisplayMessage.
        DependencyObject? el = target;
        while (el is not null)
        {
            if (el is FrameworkElement fe && fe.DataContext is DisplayMessage dm)
            {
                try { await _hubService.SendReactionAsync(dm.Id, reactionType); }
                catch { }
                cm.IsOpen = false;
                return;
            }
            el = VisualTreeHelper.GetParent(el);
        }
    }

    // ══════════════════════════════════════════════════════
    // Image Viewer Overlay
    // ══════════════════════════════════════════════════════

    private void ImageThumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image img) return;

        // Walk up visual tree to find the DisplayMessage DataContext
        var element = img as FrameworkElement;
        while (element is not null)
        {
            if (element.DataContext is DisplayMessage dm && dm.LocalFilePath is not null && File.Exists(dm.LocalFilePath))
            {
                // Load the full-quality original image from disk
                try
                {
                    var fullImage = new BitmapImage();
                    fullImage.BeginInit();
                    fullImage.UriSource = new Uri(dm.LocalFilePath, UriKind.Absolute);
                    fullImage.CacheOption = BitmapCacheOption.OnLoad;
                    fullImage.EndInit();
                    fullImage.Freeze();

                    ViewerImage.Source = fullImage;
                    ImageViewerOverlay.Visibility = Visibility.Visible;
                }
                catch
                {
                    // Fallback to thumbnail if file not accessible
                    ViewerImage.Source = img.Source;
                    ImageViewerOverlay.Visibility = Visibility.Visible;
                }
                return;
            }
            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DisplayMessage dm) return;
        if (dm.LocalFilePath is null || !File.Exists(dm.LocalFilePath)) return;

        var dlg = new SaveFileDialog
        {
            FileName = dm.FileInfo?.FileName ?? "file",
            Title = "Lưu file"
        };
        if (dlg.ShowDialog() == true)
            File.Copy(dm.LocalFilePath, dlg.FileName, overwrite: true);
    }

    private void CloseImageViewer_Click(object sender, RoutedEventArgs e)
    {
        ImageViewerOverlay.Visibility = Visibility.Collapsed;
        ViewerImage.Source = null;
    }

    private DisplayMessage AddDisplayMessage(DisplayMessage dm)
    {
        Dispatcher.Invoke(() =>
        {
            Messages.Add(dm);
            ScrollToBottom();
        });
        return dm;
    }

    private void ScrollToBottom()
    {
        if (MessagesListBox.Items.Count > 0)
            MessagesListBox.ScrollIntoView(MessagesListBox.Items[^1]);
    }
}

// ── Supporting models ──

public record OnlineUser(string Name)
{
    public string Initial => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
}
