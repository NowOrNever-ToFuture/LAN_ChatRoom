# LAN Chat Room

LAN Chat Room là ứng dụng chat nội bộ trong mạng LAN theo mô hình Client-Server, được xây dựng bằng C# .NET 8.

Ứng dụng gồm 2 chương trình chính:

- `Chat.Server.exe`: máy chủ trung chuyển tin nhắn.
- `Chat.Client.exe`: ứng dụng WPF cho người dùng kết nối và chat.


## Tính năng chính

- Chat trong mạng LAN qua TCP/IP.
- Một server có thể phục vụ nhiều client cùng lúc.
- Hỗ trợ gửi và nhận Emoji nhờ UTF-8.
- Như một forum nhỏ.
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
