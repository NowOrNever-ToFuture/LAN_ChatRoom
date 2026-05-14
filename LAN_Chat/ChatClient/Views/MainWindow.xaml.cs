using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Chat.Shared.Constants;
using ChatClient.Models;
using ChatClient.Services;

namespace ChatClient;

public partial class MainWindow : Window
{
    private readonly NetworkService _networkService = new();
    private readonly string[] _emojiIcons =
    [
        "😀", "😄", "😂", "🥰", "😍", "😎", "🤔", "😭", "😡", "👍",
        "👏", "🙏", "💪", "🔥", "✨", "🎉", "❤️", "💛", "💚", "💙",
        "⭐", "✅", "❌", "📌", "💬", "☕", "🍕", "🎮", "🚀", "🏆"
    ];

    private string _username = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        Messages = new ObservableCollection<DisplayMessage>();
        DataContext = this;
        EmojiItemsControl.ItemsSource = _emojiIcons;
        _networkService.MessageReceived += OnMessageReceived;
    }

    public ObservableCollection<DisplayMessage> Messages { get; }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_networkService.IsConnected)
        {
            return;
        }

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

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendCurrentMessageAsync();
    }

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
    }

    private void EmojiItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Content: string emoji })
        {
            return;
        }

        int caretIndex = MessageInputTextBox.CaretIndex;
        MessageInputTextBox.Text = MessageInputTextBox.Text.Insert(caretIndex, emoji);
        MessageInputTextBox.CaretIndex = caretIndex + emoji.Length;
        MessageInputTextBox.Focus();
        EmojiPopup.IsOpen = false;
    }

    private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendCurrentMessageAsync();
        }
    }

    private async Task SendCurrentMessageAsync()
    {
        if (!_networkService.IsConnected)
        {
            MessageBox.Show("Bạn cần kết nối tới server trước khi gửi tin nhắn.", "Chưa kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string content = MessageInputTextBox.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await _networkService.SendMessageAsync(_username, content);
        MessageInputTextBox.Clear();
        MessageInputTextBox.Focus();
    }

    private void OnMessageReceived(object? sender, string packet)
    {
        if (!TryParsePacket(packet, out string senderName, out string content))
        {
            return;
        }

        // Event từ NetworkService chạy trên background thread, nên bắt buộc quay về UI thread.
        Application.Current.Dispatcher.Invoke(() =>
        {
            DisplayMessage displayMessage = new(senderName, content, _username);
            Messages.Add(displayMessage);
            MessagesListBox.ScrollIntoView(displayMessage);
        });
    }

    private static bool TryParsePacket(string packet, out string senderName, out string content)
    {
        senderName = string.Empty;
        content = string.Empty;

        if (string.IsNullOrWhiteSpace(packet))
        {
            return false;
        }

        string[] parts = packet.Split(AppConstants.MessageSeparator, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return false;
        }

        senderName = parts[0];
        content = parts[1];
        return true;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _networkService.MessageReceived -= OnMessageReceived;
        await _networkService.DisposeAsync();
        base.OnClosed(e);
    }
}
