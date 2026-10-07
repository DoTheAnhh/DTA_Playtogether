# 00: BỘ QUY TẮC BẮT BUỘC & TIÊU CHUẨN KIẾN TRÚC C# & UNITY (RULE & ARCHITECTURE)

> **Mục tiêu:** Định nghĩa chuẩn mực kỹ thuật bất khả xâm phạm cho toàn bộ dự án C# & Unity DTA PlayTogether. Bất kỳ dòng code nào vi phạm các nguyên tắc dưới đây đều bị coi là lỗi nghiêm trọng (Fatal Architectural Violation).

---

## I. 17 NGUYÊN TẮC BẮT BUỘC (THE 17 SACRED RULES)

### 1. `dump.cs` Trước Tiên (Evidence First)
- Chưa tra `F:\DTA_Playtogether\dump.cs` thì **CHƯA ĐƯỢC** viết dòng code nào chạm vào dữ liệu hay hàm của game.
- Mọi class, struct layout, offset field, method RVA/token phải được kiểm chứng trực tiếp từ `dump.cs`.

### 2. Không Đoán (Zero Guesswork)
- Mọi class, field, method name, enum state value phải có bằng chứng từ `dump.cs` hoặc đo đạc bộ nhớ thực tế.
- Phân định rõ 3 mức bằng chứng trong code comments:
  + `// [VERIFIED]`: Đã xác thực trên runtime game thật.
  + `// [DUMP_ONLY]`: Có trong dump.cs, kèm kiểm tra con trỏ an toàn trước khi gọi.
  + `// [HYPOTHESIS]`: Giả định — **TUYỆT ĐỐI CẤM** dùng giả định để điều khiển bot.

### 3. Chỉ Dùng 3 Nguồn Dữ Liệu Hợp Lệ
- Nguồn 1: **Game Memory** (đọc trực tiếp các cấu trúc thực thể từ RAM).
- Nguồn 2: **Game State** (các cờ trạng thái đọc từ Controller/Manager của game).
- Nguồn 3: **Native/Game Functions** (gọi trực tiếp hàm IL2CPP).
- **CẤM:** Không dùng OpenCV/Screenshot/OCR cho logic tự động hóa lúc runtime.

### 4. Không Hardcode Offset Động & Địa Chỉ Tuyệt Đối
- Không hardcode con trỏ động, RVA trần không qua module base, toạ độ màn hình hay độ phân giải giả lập.
- Mọi offset phải được nạp thông qua Config Service hoặc Offset Provider (đồng bộ động từ Server).

### 5. Native Call Phải Đi Qua Core Dispatcher
- Mọi thao tác gọi hàm IL2CPP bắt buộc phải thông qua `GameActionDispatcher` trên Unity Main Thread.
- Tách biệt hoàn toàn: UI/Bot ra lệnh -> Service chuyển đổi -> Dispatcher tuần tự hóa -> Thực thi trên Unity Main Thread.

### 6. Không Văng Game (Zero Crash Policy)
- Validate 100% mọi con trỏ trước khi dereference hoặc invoke:
  + Con trỏ khác `IntPtr.Zero` và nằm trong dải địa chỉ bộ nhớ hợp lệ của tiến trình.
  + Địa chỉ phải aligned (4 bytes hoặc 8 bytes tùy kiến trúc 32/64 bit).
  + Kiểm tra flag `m_CachedPtr != IntPtr.Zero` đối với Unity `UnityEngine.Object`.
- Mọi hàm gọi native phải được bao bọc trong khối `try / catch` và validation nghiêm ngặt.

### 7. Không Deadlock (Strict Lock Hierarchy)
- Thứ tự chiếm lock cố định:
  `UI_State_Lock` -> `Bot_State_Lock` -> `Cache_Lock` -> `Memory_Lock` -> `IPC_Lock`.
- Mọi thao tác chờ lock phải có timeout (tối đa 2000ms), ưu tiên dùng `ReaderWriterLockSlim` hoặc Lock-Free `Channel<T>` / `ConcurrentQueue<T>` cho dữ liệu tốc độ cao.
- Không bao giờ giữ lock khi thực hiện I/O mạng hoặc sleep thread.

### 8. Thiết Kế Hướng Interface & Service (Clean Architecture)
- Lớp `Core` không được phụ thuộc vào lớp `Features`.
- Các Feature (`Fishing`, `Mining`, `Insect`,...) không được gọi chéo nhau, chỉ giao tiếp thông qua Core Event Bus hoặc Shared Interfaces.
- Kế thừa chuẩn: Mọi bot kế thừa từ `IBotEngine` hoặc `BaseBot`.

### 9. Nhanh Bằng Thiết Kế & Zero GC Allocation
- Gom cụm các lượt đọc bộ nhớ (Batch Memory Read): Đọc 1 block 512 bytes thay vì đọc 50 lần mỗi lần 4 bytes.
- Triệt tiêu hoàn toàn GC Allocation trên Hot-Paths: Sử dụng `Span<T>`, `Memory<T>`, `ArrayPool<T>`, `struct` thay vì `class` cho các gói dữ liệu tạm thời.
- Cache các đối tượng tĩnh (Scene generation, Local Player instance, Controller pointers) và tự động invalidate khi chuyển scene/map.

### 10. Không Phá Code Đang Hoạt Động (Regression-Free)
- Khi tối ưu hoặc viết lại một module, hành vi nghiệp vụ cốt lõi phải được giữ nguyên hoặc nâng cấp tốt hơn, không làm mất tính năng đã có.

### 11. Hệ Thống Log Cấu Trúc Hiệu Năng Cao (Async High-Speed Logger)
- Sử dụng Ring Buffer không khóa (`Channel<LogMessage>`) cho logger C#.
- Log có tiền tố `[DTA.<Module>]`, kèm timestamp độ chính xác cao (`Stopwatch`), mã lỗi và ngữ cảnh.

### 12. TUYỆT ĐỐI KHÔNG DÙNG NPC / PORTAL / PHONE
- Cấm đi bộ qua portal, cấm bấm vào NPC chuyển cảnh, cấm mở phone ảo.
- Cách chuyển bản đồ duy nhất được chấp nhận: Gọi hàm nội bộ chuyển zone của game (`LayerSystem.ConnectToZoneMove`).

### 13. Phòng Thủ Đa Lớp & Chống Reverse Engineering
- Mã hóa chuỗi compile-time / runtime obfuscation.
- Anti-Debug, Memory Integrity Checksum, chống can thiệp cheat engine.

### 14. Báo Cáo Trung Thực Về Kiểm Thử
- Biên dịch thành công không có nghĩa là bot đã hoạt động trong game. Phải ghi rõ trạng thái kiểm thử: `[COMPILED]`, `[DRY_RUN_MOCK]`, `[INGAME_VERIFIED]`.

### 15. Tuân Thủ Cây Thư Mục Chuẩn Tách Biệt
- Tổ chức thư mục theo tiêu chuẩn C# Clean Architecture: Phân định rõ `Core/`, `Features/`, `UI/`, `Security/`, `Shared/`.

### 16. MỌI HÀNH ĐỘNG = GỌI HÀM NATIVE CỦA GAME (ZERO-TAP SUPREME RULE)
- Tuyệt đối cấm tap màn hình giả lập (`adb shell input tap`, mouse event, virtual joystick).
- Mọi động tác: Quăng cần, giật cá, đập quặng, vung vợt, đào xẻng, tưới cây, nhặt đồ, nhảy, đóng dialog... **PHẢI GỌI TRỰC TIẾP HÀM IL2CPP CỦA GAME**.

### 17. Tối Giản, Sạch Sẽ & Không Để Lại Rác
- Giải phóng 100% tài nguyên khi tắt tool (`IDisposable`).
- Không để lại file tạm, log rác, scratch code hay background threads chạy ngầm.

---

## II. KIẾN TRÚC MÃ NGUỒN C# & UNITY (CLEAN ARCHITECTURE)

### 1. Kiến Trúc 4 Tầng Phân Lớp

```
┌─────────────────────────────────────────────────────────────┐
│                       PRESENTATION LAYER                    │
│   Unity uGUI / UI Toolkit / IMGUI (Server-Driven UI)        │
│   Từng Menu là 1 IMenuView độc lập (FishingView, MiningView)│
└──────────────────────────────┬──────────────────────────────┘
                               │ (Dependency Inversion)
┌──────────────────────────────▼──────────────────────────────┐
│                       APPLICATION LAYER                     │
│   Services: IFishingService, IMiningService, ITeleportService│
│   Bots: FishingBot, MiningBot (Finite State Machine)        │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                         DOMAIN LAYER                        │
│   Models: FishEntity, OreEntity, Waypoint, PlayerState      │
│   Interfaces: IDeviceDriver, IMemoryService, INativeBridge  │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                     INFRASTRUCTURE LAYER                    │
│   Memory: FastMemoryReader (Win32 RPM / Direct Ptr), Cache  │
│   Native: GameActionDispatcher (Unity Main Thread Queue)    │
│   Device: LDPlayer9Driver, MEmuDriver, AndroidNativeBridge  │
└─────────────────────────────────────────────────────────────┘
```

### 2. Tiêu Chuẩn Viết Code C# Trong Dự Án
- **Tên Interface:** Bắt đầu bằng chữ `I` (`IFishingService`, `IMemoryService`).
- **Asynchronous Hot-Paths:** Dùng `ValueTask` hoặc `UniTask` cho các tác vụ bất đồng bộ không cấp phát GC.
- **Unmanaged Memory:** Sử dụng `Span<byte>` và `fixed` khi đọc ghi struct nhị phân từ game memory.
- **Null Safety:** Bật `Nullable Reference Types` (`<Nullable>enable</Nullable>`).
