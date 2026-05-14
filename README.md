# LAN Chat Room

## English

LAN Chat Room is a local network chat application based on the Client-Server model, built with C# and .NET 8.

The application includes two main programs:

- `Chat.Server.exe`: the message relay server.
- `Chat.Client.exe`: the WPF client application used by users to connect and chat.

## Key Features

- Chat over a LAN network using TCP/IP.
- One server can handle multiple clients at the same time.
- Supports sending and receiving Emoji through UTF-8 encoding.
- Works like a small forum with recent message cache.
- New clients automatically receive cached chat history when they connect.
- WPF interface with connection area, chat area, message input area, and Emoji picker.
- The `win-x64 self-contained` release does not require users to install the .NET Runtime separately.

## System Requirements

- Windows 10/11 64-bit.
- Devices must be on the same LAN network, or clients must be able to reach the server IP address.
- The firewall on the server machine must allow `Chat.Server.exe` to receive connections on port `8080`.

## Download and Installation

1. Go to the `Releases` page of the GitHub repository.
2. Download this file:
   - `LAN_Chat_win-x64.zip`: includes both Server and Client.
3. Extract the `.zip` file to any folder.
4. Run the corresponding `.exe` file.

## How to Run the Server

The server should run on one machine in the LAN and act as the central chat room.

1. Open the extracted server folder.
2. Run:

```text
Chat.Server.exe
```

3. If Windows Firewall asks for network permission, choose `Allow access`.
4. The server will listen on port:

```text
8080
```

5. Note the LAN IP address of the server machine so clients can connect to it.

You can check the LAN IP address using PowerShell or Command Prompt:

```powershell
ipconfig
```

Find the `IPv4 Address` line, for example:

```text
192.168.1.25
```

## How to Run the Client

The client is the application users use to join the chat room.

1. Open the extracted client folder.
2. Run:

```text
Chat.Client.exe
```

3. Enter the connection information:
   - `Server IP`: the LAN IP of the machine running the server, for example `192.168.1.25`.
   - `Username`: your display name in the chat room.
4. Click `Connect`.
5. Type your message in the chat input box.
6. Click `Send` or press `Enter` to send.
7. You can click the Emoji button to select an icon and send it with your message.

## Quick Test on One Machine

If you want to test quickly without multiple computers:

1. Run `Chat.Server.exe` first.
2. Run `Chat.Client.exe`.
3. In the `Server IP` box, enter:

```text
127.0.0.1
```

4. Enter a username and click `Connect`.
5. You can open multiple client windows to simulate multiple users.

## How It Works

1. The server opens port `8080` and waits for client connections.
2. The client sends a handshake using this format:

```text
[CONNECT]|&|Username
```

3. The server stores the client in the online list.
4. The server sends the latest 50 cached messages to the new client.
5. When a client sends a message, the data format is:

```text
SenderName|&|Message content
```

6. The server broadcasts the message to all online clients.

## Usage Notes

- The server must be started before clients connect.
- If a client cannot connect, check:
  - Whether the server is running.
  - Whether the server IP entered in the client is correct.
  - Whether both machines are on the same LAN.
  - Whether the firewall blocks port `8080`.
- Message cache is stored only in RAM, so chat history is lost when the server is closed.
- The application currently supports one shared chat room for all connected users.

## Build from Source Code

Requirements:

- .NET 8 SDK
- Windows with WPF support

Build the whole solution:

```powershell
dotnet build LAN_Chat/LAN_Chat.slnx
```

Publish the Windows x64 Server:

```powershell
dotnet publish LAN_Chat/ChatServer/Chat.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o LAN_Chat/release/LAN_Chat_Server_win-x64
```

Publish the Windows x64 Client:

```powershell
dotnet publish LAN_Chat/ChatClient/Chat.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o LAN_Chat/release/LAN_Chat_Client_win-x64
```

## Technologies Used

- C# 12
- .NET 8
- WPF
- TCP/IP with `TcpListener` and `TcpClient`
- UTF-8 Encoding

---

## Tiếng Việt

LAN Chat Room là ứng dụng chat nội bộ trong mạng LAN theo mô hình Client-Server, được xây dựng bằng C# .NET 8.

Ứng dụng gồm 2 chương trình chính:

- `Chat.Server.exe`: máy chủ trung chuyển tin nhắn.
- `Chat.Client.exe`: ứng dụng WPF cho người dùng kết nối và chat.

## Tính năng chính

- Chat trong mạng LAN qua TCP/IP.
- Một server có thể phục vụ nhiều client cùng lúc.
- Hỗ trợ gửi và nhận Emoji nhờ UTF-8.
- Như một forum nhỏ với cơ chế cache tin nhắn gần đây.
- Client mới kết nối sẽ tự động nhận lại lịch sử tin nhắn cache.
- Giao diện WPF có vùng kết nối, vùng chat, vùng nhập tin nhắn và nút chọn Emoji.
- Bản release `win-x64 self-contained` không yêu cầu người dùng cài .NET Runtime riêng.

## Yêu cầu hệ thống

- Windows 10/11 64-bit.
- Các máy phải nằm trong cùng mạng LAN hoặc có thể kết nối tới IP của máy chạy server.
- Firewall trên máy server cần cho phép `Chat.Server.exe` nhận kết nối qua port `8080`.

## Cách tải và cài đặt

1. Vào trang `Releases` của GitHub repository.
2. Tải file sau:
   - `LAN_Chat_win-x64.zip`: gồm cả Server và Client.
3. Giải nén file `.zip` vào một thư mục bất kỳ.
4. Chạy file `.exe` tương ứng.

## Cách chạy Server

Server nên chạy trên một máy trong mạng LAN đóng vai trò phòng chat trung tâm.

1. Mở thư mục đã giải nén server.
2. Chạy file:

```text
Chat.Server.exe
```

3. Nếu Windows Firewall hỏi quyền truy cập mạng, chọn `Allow access`.
4. Server sẽ lắng nghe trên port:

```text
8080
```

5. Ghi lại địa chỉ IP LAN của máy server để các client kết nối.

Có thể xem IP LAN bằng PowerShell hoặc Command Prompt:

```powershell
ipconfig
```

Tìm dòng `IPv4 Address`, ví dụ:

```text
192.168.1.25
```

## Cách chạy Client

Client là ứng dụng người dùng dùng để tham gia phòng chat.

1. Mở thư mục đã giải nén client.
2. Chạy file:

```text
Chat.Client.exe
```

3. Nhập thông tin kết nối:
   - `Server IP`: IP LAN của máy đang chạy server, ví dụ `192.168.1.25`.
   - `Username`: tên hiển thị của bạn trong phòng chat.
4. Bấm `Connect`.
5. Nhập tin nhắn vào ô chat.
6. Bấm `Send` hoặc nhấn `Enter` để gửi.
7. Có thể bấm nút Emoji để chọn icon và gửi kèm tin nhắn.

## Test nhanh trên cùng một máy

Nếu muốn test nhanh không cần nhiều máy:

1. Chạy `Chat.Server.exe` trước.
2. Chạy `Chat.Client.exe`.
3. Ở ô `Server IP`, nhập:

```text
127.0.0.1
```

4. Nhập username và bấm `Connect`.
5. Có thể mở nhiều cửa sổ client để giả lập nhiều user.

## Luồng hoạt động

1. Server mở port `8080` và chờ client kết nối.
2. Client gửi handshake theo định dạng:

```text
[CONNECT]|&|Username
```

3. Server lưu client vào danh sách online.
4. Server gửi lại 50 tin nhắn cache gần nhất cho client mới.
5. Khi client gửi tin nhắn, dữ liệu có định dạng:

```text
SenderName|&|Nội dung tin nhắn
```

6. Server broadcast tin nhắn tới toàn bộ client đang online.

## Lưu ý khi sử dụng

- Server phải được chạy trước client.
- Nếu client không kết nối được, hãy kiểm tra:
  - Server đã chạy chưa.
  - IP nhập trên client có đúng không.
  - Hai máy có cùng mạng LAN không.
  - Firewall có chặn port `8080` không.
- Cache tin nhắn chỉ nằm trong RAM, nên khi tắt server thì lịch sử chat sẽ mất.
- Ứng dụng hiện dùng một phòng chat chung cho tất cả user kết nối tới server.

## Build từ source code

Yêu cầu:

- .NET 8 SDK
- Windows với WPF support

Build toàn bộ solution:

```powershell
dotnet build LAN_Chat/LAN_Chat.slnx
```

Publish Server bản Windows x64:

```powershell
dotnet publish LAN_Chat/ChatServer/Chat.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o LAN_Chat/release/LAN_Chat_Server_win-x64
```

Publish Client bản Windows x64:

```powershell
dotnet publish LAN_Chat/ChatClient/Chat.Client.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o LAN_Chat/release/LAN_Chat_Client_win-x64
```

## Công nghệ sử dụng

- C# 12
- .NET 8
- WPF
- TCP/IP với `TcpListener` và `TcpClient`
- UTF-8 Encoding
