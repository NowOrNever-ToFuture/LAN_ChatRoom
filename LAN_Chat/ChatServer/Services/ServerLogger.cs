namespace Chat.Server.Services;

/// <summary>
/// Structured console + file logger for the LAN Chat server.
/// Outputs color-coded categories and writes a plain-text server.log.
/// </summary>
public class ServerLogger
{
    private readonly string _logFilePath = "server.log";
    private readonly object _lock = new();

    // ── Public log methods ──

    public void LogSystem(string message) =>
        Write(ConsoleColor.Cyan, "[SYS] ", message);

    public void LogConnection(string message) =>
        Write(ConsoleColor.Green, "[CON] ", message);

    public void LogChat(string sender, string message) =>
        Write(ConsoleColor.White, "[MSG] ", $"{sender}: {message}");

    public void LogFileTransfer(string message) =>
        Write(ConsoleColor.Yellow, "[FIL] ", message);

    public void LogRoom(string room, string message) =>
        Write(ConsoleColor.Magenta, $"[{room}] ", message);

    public void LogError(string message) =>
        Write(ConsoleColor.Red, "[ERR] ", message);

    // ── Internal ──

    private void Write(ConsoleColor color, string prefix, string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        // Sanitize: replace non-ASCII characters that consoles may not render
        string safe = SanitizeForConsole(message);
        string line = $"[{timestamp}] {prefix}{safe}";

        lock (_lock)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ResetColor();

            try { File.AppendAllText(_logFilePath, line + Environment.NewLine); }
            catch { /* ignore file write errors */ }
        }
    }

    private static string SanitizeForConsole(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c < 128 || char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherSymbol
                  || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherLetter
                  || char.IsSurrogate(c))
                sb.Append("[emoji]");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
