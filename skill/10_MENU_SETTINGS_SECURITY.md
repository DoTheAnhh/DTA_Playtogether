# 10: ĐẶC TẢ MENU CÀI ĐẶT, BẢO MẬT & PHÒNG THỦ RE, CHỐNG DUMP & ANTI-TAMPER (SETTINGS & SECURITY)

> **Mục tiêu:** Xây dựng hệ thống cấu hình toàn diện, quản lý bản quyền HWID, cơ chế chống ban (Anti-Detection/Humanization) và thiết lập các lớp phòng thủ kiên cố chống Reverse Engineering (Anti-RE, Anti-Debug, Anti-Dump, Memory Shield, String Encryption, Anti-Frida/CE) cho cả Client và Server C# & Unity trên cả nền tảng Windows Giả Lập và Android APK.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Settings/
├── ISettingsService.cs     # Interface dịch vụ cài đặt
├── SettingsService.cs      # Lưu trữ cấu hình local (JSON mã hóa AES)
├── HotkeyManager.cs        # Quản lý phím tắt toàn cục (Global Hotkeys)
├── ConfigModels.cs         # Cấu trúc Cài đặt ứng dụng & Anti-ban
└── SettingsView.cs         # Unity UI View Component (Windows & APK Floating)

src/Client/Security/
├── XorString.cs            # Mã hóa chuỗi ký tự runtime/compile-time
├── AntiDebug.cs            # Phát hiện máy ảo, debugger (x64dbg, IDA, CE, Frida)
├── AntiDump.cs             # Chống Memory Dump, xóa PE Header, giấu Assembly
├── MemoryShield.cs         # Kiểm tra tính toàn vẹn bộ nhớ (Code hash integrity)
└── HWIDProvider.cs         # Trích xuất Hardware ID duy nhất của máy / thiết bị
```

---

## II. LỚP PHÒNG THỦ CHỐNG REVERSE ENGINEERING & DUMP (SECURITY SHIELD)

### 1. Compile-Time & Runtime String Encryption (`XorString`)
Mọi chuỗi ký tự nhạy cảm (URL server, offset keys, API endpoints, method names, class names trong `dump.cs`) tuyệt đối không để lộ dưới dạng plain text trong binary hay qua ILSpy/dnSpy/strings:

```csharp
using System;
using System.Text;
using System.Runtime.CompilerServices;

namespace DTA.Security
{
    public static class XorString
    {
        private const byte MasterKey = 0xAA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Decode(ReadOnlySpan<byte> encrypted, byte salt = 0x55)
        {
            Span<byte> decrypted = stackalloc byte[encrypted.Length];
            for (int i = 0; i < encrypted.Length; i++)
            {
                decrypted[i] = (byte)(encrypted[i] ^ (MasterKey ^ salt ^ (byte)(i % 11)));
            }
            return Encoding.UTF8.GetString(decrypted);
        }

        public static byte[] Encode(string plainText, byte salt = 0x55)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            byte[] enc = new byte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                enc[i] = (byte)(bytes[i] ^ (MasterKey ^ salt ^ (byte)(i % 11)));
            }
            return enc;
        }
    }
}
```

### 2. Anti-Debug Đa Lớp (Windows & Android APK)
- **Windows (Giả lập LDPlayer/MEmu):**
  + Kiểm tra `IsDebuggerPresent`, `CheckRemoteDebuggerPresent`.
  + Truy vấn `NtQueryInformationProcess` với các cờ `ProcessDebugPort (7)`, `ProcessDebugFlags (0x1F)`, `ProcessDebugObjectHandle (0x1E)`.
  + Quét thanh ghi gỡ lỗi phần cứng (`Hardware Breakpoints DR0 - DR3`).
  + Theo dõi độ trễ thực thi bằng `Stopwatch.GetTimestamp()`: Nếu delta giữa 2 bước lệnh vượt ngưỡng bất thường (> 200ms) $\to$ Phát hiện đang bị trace/step trong debugger $\to$ Tự thoát tiến trình.
- **Android APK:**
  + Kiểm tra `TracerPid` trong `/proc/self/status` (nếu khác 0 là đang bị ptrace/gdb).
  + Kiểm tra cổng Frida mặc định (`27042`, `27043`), quét thư mục `/data/local/tmp` tìm `frida-server`, `magisk`, `xposed`.
  + Kiểm tra biến môi trường `LD_PRELOAD`, thư viện `frida-gadget.so`.

### 3. Anti-Dump & Bảo Vệ Bộ Nhớ (`AntiDump`)
- **Xóa PE Header trong RAM (PE Header Stripping):**
  Ngay khi nạp xong vào RAM, hàm bảo mật dùng `VirtualProtect` đổi quyền vùng nhớ chứa DOS Header và NT Headers (`0x400000` hoặc base address) sang `PAGE_READWRITE`, ghi đè bằng `0x00`, sau đó khóa lại bằng `PAGE_NOACCESS` hoặc `PAGE_READONLY`. Khi dùng Scylla, Megadumper hay Cheat Engine dump binary ra đĩa sẽ thu được file rác bị hỏng cấu trúc không thể unpack/decompile.
- **Chống đọc bộ nhớ trái phép (Memory Protection):**
  Đặt bẫy `PAGE_GUARD` trên các trang nhớ nhạy cảm chứa bảng Key giải mã. Khi có tiến trình lạ cố đọc, exception kích hoạt lập tức tự xóa sạch key khỏi RAM.

### 4. Memory Integrity & Hook Detection (`MemoryShield`)
- Định kỳ tính toán giá trị băm (Hash CRC32/SHA-256) của phân vùng mã lệnh `.text` / Managed Assembly bytecodes.
- Phát hiện các lệnh tiêm breakpoint `0xCC` (INT 3) hoặc JMP hook (`0xE9`) vào các hàm Core Dispatcher.
- Nếu phát hiện sai lệch băm $\to$ Đóng kết nối Server, tự xóa dữ liệu tạm và tắt tiến trình ngay lập tức.

### 5. HWID Provider Khóa Thiết Bị Cứng
- Trên Windows: Hashing kết hợp Motherboard UUID (`wmic csproduct get uuid`), Processor ID, Volume Serial Number của ổ C:, và MAC Address card mạng.
- Trên Android: Hashing Android ID (`Settings.Secure.ANDROID_ID`), Hardware Serial, Build Fingerprint.
- Mã băm SHA-256 kết hợp Nonce của Server tạo thành Token chữ ký số duy nhất, chống giả mạo máy.
