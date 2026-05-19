using System.Windows;
using System.Windows.Controls;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Selects the appropriate DataTemplate for each message type in the chat list.
/// </summary>
public class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextMessageTemplate { get; set; }
    public DataTemplate? ImageMessageTemplate { get; set; }
    public DataTemplate? FileMessageTemplate { get; set; }
    public DataTemplate? SystemMessageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DisplayMessage message) return base.SelectTemplate(item, container);

        if (message.IsSystemMessage) return SystemMessageTemplate;
        if (message.IsImageMessage) return ImageMessageTemplate;
        if (message.IsFileMessage) return FileMessageTemplate;
        return TextMessageTemplate;
    }
}
