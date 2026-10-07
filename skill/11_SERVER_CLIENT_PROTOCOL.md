# 11: ĐẶC TẢ KIẾN TRÚC CLIENT - SERVER & GIAO THỨC TỐI ƯU HÓA TẢI VPS (SERVER-DRIVEN UI)

> **Mục tiêu:** Thiết kế hệ thống mạng Client - Server bằng C# (.NET 8 ASP.NET Core & Unity WebSocket Client) chịu tải cao (High Concurrency), đảm bảo bảo mật tuyệt đối, chống crack bản quyền và giải quyết triệt để bài toán quá tải VPS khi có hàng chục nghìn người dùng sử dụng tool đồng thời.

---

## I. NGUYÊN TẮC CÂN BẰNG TẢI: SERVER-DRIVEN UI & LOCAL EXECUTION

### 1. Phân Tách Trách Nhiệm
- **Client (Fat-Engine, Thin-Renderer):**
  + Chạy toàn bộ logic quét bộ nhớ và gọi hàm game cục bộ trên máy người dùng (độ trễ 0ms, không tiêu tốn 1 byte băng thông server cho vòng lặp game).
  + Client **không chứa giao diện tĩnh cố định**. Client là một **Renderer Engine** (Unity uGUI/UI Toolkit) chờ nhận bản thiết kế giao diện (UI Schema) từ Server.
- **Server (ASP.NET Core trên VPS):**
  + **Server-Driven UI:** Server lưu trữ layout, các tab, nút bấm, slider, màu sắc dưới dạng JSON/Protobuf nhị phân. Khi user đăng nhập thành công, Server gửi UI Schema xuống.
  + **Authentication & HWID Lock:** Xác thực mã máy, thời hạn VIP.
  + **Heartbeat Thưa (Sparse Heartbeat):** Mỗi 60 giây gửi 1 gói tin kiểm tra sự sống (32 bytes).
  + **Dynamic Offset Cloud:** Khi game Play Together cập nhật phiên bản, Admin cập nhật offset mới lên Server, Client tự động tải về bộ offset mới mà không cần cài lại tool.

---

## II. GIAO THỨC TRUYỀN THÔNG (NETWORK PROTOCOL & PACKET DESIGN)

### 1. Cấu Trúc Khung Tin Nhị Phân (Packet Header)

```csharp
using System.Runtime.InteropServices;

namespace DTA.Shared.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public uint Magic;          // 0x44544150 ("DTAP")
        public ushort PacketId;     // Mã lệnh (Auth, Heartbeat, UISchema, OffsetSync)
        public uint PayloadLength;  // Độ dài dữ liệu
        public ulong Timestamp;     // Thời gian gửi (chống phát lại)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] Nonce;        // AES-GCM IV Nonce
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Tag;          // AES-GCM Auth Tag
    }
}
```

---

## III. SCHEMA GIAO DIỆN PHÁT TỪ MÁY CHỦ (SERVER-DRIVEN UI SCHEMA)

```json
{
  "version": "2.5.0",
  "theme": {
    "accent_color": "#00E5FF",
    "background_color": "#0D0F12",
    "card_color": "#1C2129"
  },
  "menus": [
    {
      "id": "menu_fishing",
      "title": "Câu Cá (Fishing)",
      "cards": [
        {
          "title": "Điều Khiển Tự Động",
          "controls": [
            { "type": "toggle", "id": "auto_cast", "label": "Tự Quăng Cần", "default": true },
            { "type": "toggle", "id": "instant_reel", "label": "Giật Tức Thì (< 1ms)", "default": true }
          ]
        }
      ]
    }
  ]
}
```
