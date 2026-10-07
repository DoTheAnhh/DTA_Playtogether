# 10: ĐẶC TẢ MENU CÀI ĐẶT, BẢO MẬT & PHÒNG THỦ RE (SETTINGS & SECURITY)

> **Mục tiêu:** Xây dựng hệ thống cấu hình toàn diện, quản lý bản quyền HWID, cơ chế chống ban (Anti-Detection/Humanization) và thiết lập các lớp phòng thủ kiên cố chống Reverse Engineering (Anti-RE, Anti-Debug, Memory Shield, String Encryption) cho cả Client và Server.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/settings/
├── ISettingsService.hpp    # Interface dịch vụ cài đặt
├── SettingsService.cpp     # Lưu trữ cấu hình local (JSON mã hóa AES)
├── HotkeyManager.hpp       # Quản lý phím tắt toàn cục (Global Hotkeys)
├── ConfigModels.hpp        # Cấu trúc Cài đặt ứng dụng & Anti-ban
└── SettingsView.cpp        # ImGui Render Component

src/client/security/
├── XorStr.hpp              # Mã hóa chuỗi ký tự compile-time
├── AntiDebug.hpp           # Phát hiện máy ảo, debugger (x64dbg, IDA, CE)
├── MemoryShield.hpp        # Kiểm tra tính toàn vẹn bộ nhớ (Hash CRC32/SHA256)
├── HWIDProvider.hpp        # Trích xuất Hardware ID duy nhất của máy
└── PeStripper.hpp          # Xóa Header PE trong bộ nhớ khi chạy
```

---

## II. LỚP PHÒNG THỦ CHỐNG REVERSE ENGINEERING (SECURITY SHIELD)

### 1. Compile-Time String Encryption (`XorStr`)
Tuyệt đối không để lại bất kỳ chuỗi nhạy cảm nào trong file `.exe` (như URL server, tên hàm game, API key, offset string):

```cpp
// include/security/XorStr.hpp
#pragma once
#include <string>
#include <array>

template <size_t N, uint32_t Seed>
class XorString {
private:
    std::array<char, N> m_data;
public:
    constexpr XorString(const char(&str)[N]) {
        for (size_t i = 0; i < N; ++i) {
            m_data[i] = str[i] ^ static_cast<char>((Seed + i) % 255);
        }
    }
    std::string Decrypt() const {
        std::string res;
        res.resize(N - 1);
        for (size_t i = 0; i < N - 1; ++i) {
            res[i] = m_data[i] ^ static_cast<char>((Seed + i) % 255);
        }
        return res;
    }
};

#define _XOR(str) (XorString<sizeof(str), 0x5A7D3C>(str).Decrypt())
```

### 2. Hệ Thống Phát Hiện Trình Gỡ Lỗi (Anti-Debugging)
Luồng giám sát độc lập kiểm tra định kỳ mỗi 500ms:
- `IsDebuggerPresent()` & `CheckRemoteDebuggerPresent()`.
- Kiểm tra cờ `BeingDebugged` trong PEB (Process Environment Block).
- Kiểm tra các thanh ghi Hardware Breakpoint (`DR0, DR1, DR2, DR3, DR7`) thông qua `GetThreadContext`.
- Đo độ trễ thời gian CPU qua chỉ lệnh `__rdtsc()` để phát hiện bị trace từng bước trong IDA Pro / x64dbg.
- **Xử lý khi phát hiện:** Ngay lập tức crash có chủ đích (gây hỏng stack hoặc gọi `exit(0)`) mà không để lại thông báo.

### 3. Kiểm Tra Toàn Vẹn Bộ Nhớ (Memory Integrity Shield)
- Tính toán mã hash SHA256 / CRC32 của section `.text` trong bộ nhớ ngay sau khi khởi động.
- Quét liên tục kiểm tra xem có byte nào bị ghi đè bởi Cheat Engine (Opcode `0x90 NOP` hoặc `0xCC INT3`) không.

---

## III. CƠ CHẾ CHỐNG BAN (ANTI-DETECTION & HUMANIZATION)

1. **Micro-Jitter Randomizer:**
   - Khi thực hiện các chu kỳ giật cần, vung cuốc, vung vợt, tool tự động cộng thêm độ trễ ngẫu nhiên từ `8ms` đến `28ms` theo phân phối chuẩn Gaussian, triệt tiêu hoàn toàn dấu vết hành vi cơ học tuần hoàn.
2. **Player Proximity Safety (Cảnh Báo & Dừng An Toàn):**
   - Khi phát hiện có người chơi khác đến gần trong bán kính 10m và đứng yên quan sát quá 20 giây:
     + Chế độ 1: Tạm dừng bot, giả lập hành vi đứng yên hoặc emote chào hỏi.
     + Chế độ 2: Tự động đổi map hoặc dịch chuyển sang tọa độ khác.
3. **Panic Hotkey (Phím Tắt Khẩn Cấp):**
   - Phím `F12`: Lập tức dừng toàn bộ bot, đóng cửa sổ overlay, ẩn process trong 1ms.
