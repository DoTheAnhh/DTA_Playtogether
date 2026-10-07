# 11: ĐẶC TẢ KIẾN TRÚC CLIENT - SERVER & GIAO THỨC TỐI ƯU HÓA TẢI VPS (SERVER-DRIVEN UI)

> **Mục tiêu:** Thiết kế hệ thống mạng Client - Server chịu tải cao (High Concurrency), đảm bảo bảo mật tuyệt đối, chống crack bản quyền và giải quyết triệt để bài toán quá tải VPS khi có hàng chục nghìn người dùng sử dụng tool đồng thời.

---

## I. NGUYÊN TẮC CÂN BẰNG TẢI: TẠI SAO VPS KHÔNG BAO GIỜ BỊ QUÁ TẢI?

### 1. Phân Tích Sai Lầm Kiến Trúc Truyền Thống
- Nếu chuyển toàn bộ logic bot (vòng lặp quét bộ nhớ, tìm quặng, tính toán giật cá) lên VPS: Với 1.000 user $\times$ 60 FPS = 60.000 requests/giây $\to$ Băng thông cạn kiệt, CPU VPS quá tải 100%, bot giật lag và văng mạng.

### 2. Mô Hình Đột Phá: Server-Driven UI & Local Engine Execution
- **Client (Fat-Engine, Thin-Renderer):**
  + Chạy toàn bộ logic quét bộ nhớ và gọi hàm game cục bộ trên máy người dùng (độ trễ 0ms, không tiêu tốn 1 byte băng thông server cho vòng lặp game).
  + Client **không chứa giao diện tĩnh cố định**. Client chỉ là một **Renderer Engine** (ImGui) chờ nhận bản thiết kế giao diện (UI Schema) từ Server.
- **Server (Brain & License Controller trên VPS):**
  + **Server-Driven UI:** Server lưu trữ layout, các tab, nút bấm, slider, màu sắc dưới dạng JSON/Protobuf nhị phân. Khi user đăng nhập thành công, Server gửi UI Schema xuống. Muốn đổi giao diện, thêm menu mới, tạm tắt tính năng đang bảo trì $\to$ Chỉ cần sửa trên Server, toàn bộ Client tự động cập nhật ngay lập tức mà không cần re-compile!
  + **Authentication & HWID Lock:** Xác thực mã máy, thời hạn VIP, gói tính năng được cấp phép.
  + **Heartbeat Thưa (Sparse Heartbeat):** Mỗi 60 giây gửi 1 gói tin kiểm tra sự sống (32 bytes).
  + **Dynamic Offset Cloud:** Khi game Play Together cập nhật phiên bản, Admin cập nhật offset mới lên Server, Client tự động tải về bộ offset mới mà không cần cài lại tool.

$$\text{Tải Server cho 10.000 user} = \frac{10.000 \text{ req}}{60 \text{ giây}} \approx 166 \text{ req/giây} \implies \text{CPU VPS < 3\%, RAM < 200MB!}$$

---

## II. GIAO THỨC TRUYỀN THÔNG (NETWORK PROTOCOL & PACKET DESIGN)

- **Giao thức tầng truyền dẫn:** TLS 1.3 / WebSocket Secure (WSS) qua cổng 443 (vượt mọi tường lửa và ISP).
- **Mã hóa gói tin:** AES-256-GCM kết hợp chữ ký điện tử ECDSA (secp256k1). Chống tuyệt đối tấn công Replay Attack bằng Nonce ngẫu nhiên và Timestamp.

### 1. Cấu Trúc Khung Tin Nhị Phân (Packet Header)

```cpp
#pragma pack(push, 1)
struct PacketHeader {
    uint32_t magic;         // 0x44544150 ("DTAP")
    uint16_t packetId;      // Mã lệnh (Auth, Heartbeat, UISchema, OffsetSync)
    uint32_t payloadLength; // Độ dài dữ liệu
    uint64_t timestamp;     // Thời gian gửi (chống phát lại)
    uint8_t  nonce[12];     // AES-GCM IV Nonce
    uint8_t  tag[16];       // AES-GCM Auth Tag
};
#pragma pack(pop)
```

---

## III. SCHEMA GIAO DIỆN PHÁT TỪ MÁY CHỦ (SERVER-DRIVEN UI SCHEMA)

Ví dụ gói tin Server gửi cấu hình Menu Câu Cá xuống Client:

```json
{
  "version": "2.4.0",
  "theme": {
    "accent_color": "#00E5FF",
    "background_color": "#0D0F12",
    "card_color": "#1C2129"
  },
  "menus": [
    {
      "id": "tab_fishing",
      "title": "Câu Cá Siêu Tốc",
      "icon": "icon_fish",
      "enabled": true,
      "components": [
        {
          "type": "Toggle",
          "id": "auto_cast_reel",
          "label": "Tự động thả & giật cá (Zero-Tap)",
          "default": true
        },
        {
          "type": "MultiSelect",
          "id": "shadow_filter",
          "label": "Lọc cỡ bóng cá",
          "options": ["Bóng 1", "Bóng 2", "Bóng 3", "Bóng 4", "Bóng 5", "Bóng 6", "Bóng 7"],
          "default": ["Bóng 5", "Bóng 6", "Bóng 7"]
        },
        {
          "type": "Dropdown",
          "id": "after_catch_action",
          "label": "Xử lý sau khi câu",
          "options": ["Bảo quản vào túi", "Bán nhanh tức thì"],
          "default": "Bảo quản vào túi"
        },
        {
          "type": "Toggle",
          "id": "auto_repair",
          "label": "Tự động sửa cần khi hỏng",
          "default": true
        }
      ]
    }
  ]
}
```

---

## IV. BẢO MẬT PHÍA MÁY CHỦ (SERVER HARDENING)

1. **Token Bucket Rate Limiting:**
   - Mỗi IP chỉ được gửi tối đa 5 request xác thực/phút. Quá ngưỡng tự động chặn IP qua iptables/nftables.
2. **HWID Fingerprinting Chống Gian Lận:**
   - HWID được hash tổng hợp từ: CPU Processor ID + BIOS Serial Number + Motherboard UUID + Primary Disk Serial.
   - 1 License Key chỉ được gắn với 1 HWID duy nhất. Đổi máy phải có xác nhận reset key từ admin bot.
3. **Chống Dò Quét & Dịch Ngược API:**
   - Server không phản hồi các HTTP request thông thường (trả về mã 404 rỗng nếu không có đúng TLS Client Certificate hoặc Header mã hóa độc quyền).
