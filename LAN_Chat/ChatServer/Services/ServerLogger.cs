namespace Chat.Server.Services;

/// <summary>
/// Structured logger for the chat server. Outputs colored messages to the console
/// and simultaneously writes to a log file (server.log).
/// </summary>
public sealed class ServerLogger : IDisposable
{
    private readonly StreamWriter _fileWriter;
    private readonly object _consoleLock = new();
    private readonly string _logFilePath;

    public enum LogLevel { Info, Warning, Error }
    public enum LogCategory { Connection, Chat, FileTransfer, System, Error }

    public ServerLogger(string logFilePath = "server.log")
    {
        _logFilePath = logFilePath;
        _fileWriter = new StreamWriter(
            new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read),
            System.Text.Encoding.UTF8)
        {
            AutoFlush = true
        };

        Log(LogLevel.Info, LogCategory.System, $"Server logger initialized. Log file: {Path.GetFullPath(logFilePath)}");
    }

    public void Log(LogLevel level, LogCategory category, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logLine = $"[{timestamp}] [{level,-7}] [{category,-12}] {message}";

        // Write to file
        try
        {
            _fileWriter.WriteLine(logLine);
        }
        catch (IOException)
        {
            // Silently ignore file write failures to avoid crashing the server
        }

        // Write to console with color
        lock (_consoleLock)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            // Timestamp in gray
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");

            // Category badge
            Console.ForegroundColor = GetCategoryColor(category);
            Console.Write($"[{category,-12}] ");

            // Level indicator
            Console.ForegroundColor = GetLevelColor(level);
            Console.Write($"{GetLevelIcon(level)} ");

            // Message
            Console.ForegroundColor = GetMessageColor(category);
            Console.WriteLine(message);

            Console.ForegroundColor = originalColor;
        }
    }

    public void LogConnection(string message) => Log(LogLevel.Info, LogCategory.Connection, message);
    public void LogChat(string sender, string content)
    {
        string sanitized = SanitizeForConsole(content);
        Log(LogLevel.Info, LogCategory.Chat, $"{sender}: {sanitized}");
    }
    public void LogFileTransfer(string message) => Log(LogLevel.Info, LogCategory.FileTransfer, message);
    public void LogSystem(string message) => Log(LogLevel.Info, LogCategory.System, message);
    public void LogError(string message) => Log(LogLevel.Error, LogCategory.Error, message);
    public void LogWarning(string message) => Log(LogLevel.Warning, LogCategory.System, message);

    private static ConsoleColor GetCategoryColor(LogCategory category) => category switch
    {
        LogCategory.Connection => ConsoleColor.Green,
        LogCategory.Chat => ConsoleColor.Cyan,
        LogCategory.FileTransfer => ConsoleColor.Yellow,
        LogCategory.System => ConsoleColor.Magenta,
        LogCategory.Error => ConsoleColor.Red,
        _ => ConsoleColor.White
    };

    private static ConsoleColor GetLevelColor(LogLevel level) => level switch
    {
        LogLevel.Info => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        _ => ConsoleColor.White
    };

    private static ConsoleColor GetMessageColor(LogCategory category) => category switch
    {
        LogCategory.Connection => ConsoleColor.DarkGreen,
        LogCategory.Chat => ConsoleColor.White,
        LogCategory.FileTransfer => ConsoleColor.DarkYellow,
        LogCategory.System => ConsoleColor.DarkMagenta,
        LogCategory.Error => ConsoleColor.DarkRed,
        _ => ConsoleColor.Gray
    };

    private static string GetLevelIcon(LogLevel level) => level switch
    {
        LogLevel.Info => "[INFO]",
        LogLevel.Warning => "[WARN]",
        LogLevel.Error => "[ERR!]",
        _ => "     "
    };

    /// <summary>
    /// Replace emoji/unicode characters with readable text for console display.
    /// </summary>
    private static string SanitizeForConsole(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new System.Text.StringBuilder(input.Length);
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(input);

        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            // If the text element is a single ASCII-range char, keep it.
            // Otherwise it's an emoji/symbol — replace with a tag.
            if (element.Length == 1 && element[0] < 0x2600)
            {
                sb.Append(element);
            }
            else
            {
                sb.Append("[emoji]");
            }
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        _fileWriter.Dispose();
    }
}
