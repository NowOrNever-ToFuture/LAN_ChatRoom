namespace Chat.Shared.Models;

/// <summary>
/// Represents a single emoji reaction on a message, with a count.
/// </summary>
public class MessageReaction
{
    public string MessageId { get; set; } = string.Empty;
    public string ReactionType { get; set; } = string.Empty; // e.g. "👍", "❤️", "😂"
    public int Count { get; set; }
}
