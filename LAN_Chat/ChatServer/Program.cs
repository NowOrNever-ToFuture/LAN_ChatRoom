using Chat.Server.Hubs;
using Chat.Server.Services;
using Chat.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// ── Logging: use ServerLogger for structured output ──
builder.Logging.ClearProviders();
builder.Services.AddSingleton<ServerLogger>();

// ── SignalR ──
builder.Services.AddSignalR(options =>
{
    // 100 MB — supports large file/image transfers via Base64
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024;
    options.EnableDetailedErrors = true;
})
.AddJsonProtocol(options =>
{
    // Preserve emoji characters in JSON (UTF-8 unsafe relaxed)
    options.PayloadSerializerOptions.Encoder =
        System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// ── Services ──
builder.Services.AddSingleton<ChatService>();

var app = builder.Build();

// ── Hub endpoint ──
app.MapHub<ChatHub>(AppConstants.HubPath);

// ── Startup log ──
var logger = app.Services.GetRequiredService<ServerLogger>();
logger.LogSystem($"LAN Chat Server (SignalR) starting on port {AppConstants.Port}...");
logger.LogSystem($"Hub: http://0.0.0.0:{AppConstants.Port}{AppConstants.HubPath}");
logger.LogSystem($"Default rooms: {string.Join(", ", AppConstants.DefaultRooms)}");

await app.RunAsync($"http://0.0.0.0:{AppConstants.Port}");
