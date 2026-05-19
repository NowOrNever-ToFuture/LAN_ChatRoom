using Chat.Server.Services;

using ServerLogger logger = new();
ServerManager serverManager = new(logger);
FileTransferManager fileTransferManager = new(logger);

logger.LogSystem("Starting LAN Chat Server...");

// Run both the chat server and file transfer server in parallel
await Task.WhenAll(
    serverManager.StartAsync(),
    fileTransferManager.StartAsync()
);
