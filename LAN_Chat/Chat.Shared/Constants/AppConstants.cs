namespace Chat.Shared.Constants;

public static class AppConstants
{
    // ── Network ──
    public const int Port = 8080;
    public const string HubPath = "/chathub";
    public const string SystemSenderName = "[SYSTEM]";

    // ── Rooms ──
    public static readonly string[] DefaultRooms = ["#GiaiTri", "#HocTap", "#ThongBao"];
    public const string DefaultRoom = "#GiaiTri";

    // ── Message cache ──
    public const int MaxCachedMessages = 50;

    // ── Image constraints ──
    public const int MaxImageDimension = 1920;
    public const int ImageQuality = 95;

    // ── File transfer ──
    public const int FileChunkSize = 64 * 1024; // 64KB per chunk
}
