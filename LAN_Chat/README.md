# LAN Chat Room

A high-performance, real-time local network (LAN) chat application built with **.NET 8**, **WPF**, and **SignalR**. The application features a decoupled architecture with a central chat server and modern desktop clients, allowing users on the same network to connect, chat, and share files seamlessly.

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-blue?style=for-the-badge&logo=windows)
![SignalR](https://img.shields.io/badge/SignalR-0078D4?style=for-the-badge)

## 🌟 Features

- **Real-time Messaging:** Powered by ASP.NET Core SignalR for instantaneous, low-latency message delivery.
- **Multiple Rooms:** Seamlessly switch between built-in channels (`#GiaiTri`, `#HocTap`, `#ThongBao`).
- **File & Image Sharing:**
  - Send and receive files (ZIP, RAR, documents).
  - Share images with automatic thumbnail rendering.
  - Click on images to view them in a high-resolution popup.
  - Smart temporary file caching via `%TEMP%` to speed up switching rooms.
- **Message Reactions:**
  - Right-click any message to drop an emoji reaction.
  - Smart UI aggregation (groups same-emoji variations automatically).
  - Real-time counting and syncing across all clients.
- **Typing Indicators:** See exactly who is typing in the current room in real time.
- **Resilient Connection:** Automatic reconnection handling with UI banners if the server drops.
- **Modern UI:** Clean, responsive, and beautiful WPF UI with custom styling, drop shadows, and rounded corners.

## 🏗️ Architecture

The solution is divided into three main projects:
1. **Chat.Shared:** Class library containing common models, constants, and network DTOs shared between the client and server.
2. **ChatServer:** A self-contained console application hosting the SignalR Hub. It manages user registries, state, message caching, and broadcasts.
3. **ChatClient:** A WPF desktop application serving as the UI. It uses the `SignalR.Client` package to communicate with the server.

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Running the Application

1. **Start the Server:**
   Navigate to the `ChatServer` directory and run:
   ```bash
   cd ChatServer
   dotnet run -c Release
   ```
   *Note the IP Address the server is listening on.*

2. **Start the Client:**
   Navigate to the `ChatClient` directory and run:
   ```bash
   cd ChatClient
   dotnet run -c Release
   ```

3. **Connect:**
   - In the Client UI, enter the **Server IP** (from Step 1) and your desired **Username**.
   - Click "Kết nối" (Connect) and start chatting!

### Publishing Standalone Executables
To build standalone `.zip` files (which don't require users to install the .NET SDK):

```bash
# Publish Client
dotnet publish ChatClient\Chat.Client.csproj -c Release -r win-x64 --self-contained

# Publish Server
dotnet publish ChatServer\Chat.Server.csproj -c Release -r win-x64 --self-contained
```

## 🛠️ Tech Stack & Libraries
- `Microsoft.AspNetCore.SignalR` & `Microsoft.AspNetCore.SignalR.Client`
- `Emoji.Wpf` (for native colorful emoji rendering in WPF)
