# 00: BỘ QUY TẮC BẮT BUỘC & TIÊU CHUẨN KIẾN TRÚC C++20 (RULE & ARCHITECTURE)

> **Mục tiêu:** Định nghĩa chuẩn mực kỹ thuật bất khả xâm phạm cho toàn bộ dự án C++20 DTA PlayTogether. Bất kỳ dòng code nào vi phạm các nguyên tắc dưới đây đều bị coi là lỗi nghiêm trọng (Fatal Architectural Violation).

---

## I. 17 NGUYÊN TẮC BẮT BUỘC (THE 17 SACRED RULES)

### 1. `dump.cs` Trước Tiên (Evidence First)
- Chưa tra `F:\DTA_Playtogether\dump.cs` thì **CHƯA ĐƯỢC** viết dòng code nào chạm vào dữ liệu hay hàm của game.
- Mọi struct layout, offset field, method RVA/token phải được kiểm chứng trực tiếp từ `dump.cs`.

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
- Mọi thao tác gọi hàm IL2CPP bắt buộc phải thông qua `GameActionDispatcher` hoặc `NativeService`.
- Tách biệt hoàn toàn: UI/Bot ra lệnh -> Service chuyển đổi -> Dispatcher tuần tự hóa -> Thực thi trên Unity Main Thread.

### 6. Không Văng Game (Zero Crash Policy)
- Validate 100% mọi con trỏ trước khi dereference hoặc invoke:
  + Con trỏ khác `nullptr` và nằm trong dải địa chỉ bộ nhớ hợp lệ của tiến trình.
  + Địa chỉ phải aligned (4 bytes hoặc 8 bytes tùy kiến trúc 32/64 bit).
  + Kiểm tra flag `m_CachedPtr != 0` đối với Unity `UnityEngine.Object`.
- Mọi hàm gọi native phải được bao bọc trong SEH (`__try / __except` trên Windows) hoặc Signal Handler (`sigaction` SIGSEGV/SIGBUS trên Android Linux).

### 7. Không Deadlock (Strict Lock Hierarchy)
- Thứ tự chiếm lock cố định:
  `UI_State_Lock` -> `Bot_State_Lock` -> `Cache_Lock` -> `Memory_Lock` -> `IPC_Lock`.
- Mọi thao tác chờ lock phải có timeout (tối đa 2000ms), không dùng `std::mutex` đơn thuần mà dùng `std::timed_mutex` hoặc Lock-Free Ring Buffers cho dữ liệu tốc độ cao.
- Không bao giờ giữ lock khi thực hiện I/O mạng hoặc sleep thread.

### 8. Thiết Kế Hướng Interface & Service (Clean Architecture)
- Lớp `Core` không được phụ thuộc vào lớp `Features`.
- Các Feature (`Fishing`, `Mining`, `Insect`,...) không được gọi chéo nhau, chỉ giao tiếp thông qua Core Event Bus hoặc Shared Interfaces.
- Kế thừa chuẩn: Mọi bot kế thừa từ `IBotEngine` hoặc `BaseBot`.

### 9. Nhanh Bằng Thiết Kế (Zero-Cost & Batching)
- Gom cụm các lượt đọc bộ nhớ (Batch Memory Read): Đọc 1 block 512 bytes thay vì đọc 50 lần mỗi lần 4 bytes.
- Cache các đối tượng tĩnh (Scene generation, Local Player instance, Controller pointers) và tự động invalidate khi chuyển scene/map.

### 10. Không Phá Code Đang Hoạt Động (Regression-Free)
- Khi tối ưu hoặc viết lại một module, hành vi nghiệp vụ cốt lõi phải được giữ nguyên hoặc nâng cấp tốt hơn, không làm mất tính năng đã có.

### 11. Hệ Thống Log Cấu Trúc Hiệu Năng Cao (Async High-Speed Logger)
- Sử dụng Ring Buffer không khóa (Lock-free Ring Buffer) cho logger C++.
- Log có tiền tố `[DTA.<Module>]`, kèm timestamp độ chính xác microsecond (`std::chrono::high_resolution_clock`), mã lỗi và ngữ cảnh.

### 12. TUYỆT ĐỐI KHÔNG DÙNG NPC / PORTAL / PHONE
- Cấm đi bộ qua portal, cấm bấm vào NPC chuyển cảnh, cấm mở phone ảo.
- Cách chuyển bản đồ duy nhất được chấp nhận: Gọi hàm nội bộ chuyển zone của game (`LayerSystem::ConnectToZoneMove`).

### 13. Phòng Thủ Đa Lớp & Chống Reverse Engineering
- Mã hóa chuỗi compile-time (`constexpr xorstr`).
- Anti-Debug, Memory Integrity Checksum, chống đè hook, làm sạch PE header khi nạp vào bộ nhớ.

### 14. Báo Cáo Trung Thực Về Kiểm Thử
- Biên dịch thành công không có nghĩa là bot đã hoạt động trong game. Phải ghi rõ trạng thái kiểm thử: `[COMPILED]`, `[DRY_RUN_MOCK]`, `[INGAME_VERIFIED]`.

### 15. Tuân Thủ Cây Thư Mục Chuẩn Tách Biệt
- Tổ chức thư mục theo tiêu chuẩn C++ Enterprise: Phân định rõ `include/`, `src/`, `interfaces/`, `features/`, `platform/`.

### 16. MỌI HÀNH ĐỘNG = GỌI HÀM NATIVE CỦA GAME (ZERO-TAP SUPREME RULE)
- Tuyệt đối cấm tap màn hình giả lập (`adb shell input tap`, mouse event, virtual joystick).
- Mọi động tác: Quăng cần, giật cá, đập quặng, vung vợt, đào xẻng, tưới cây, nhặt đồ, nhảy, đóng dialog... **PHẢI GỌI TRỰC TIẾP HÀM IL2CPP CỦA GAME**.

### 17. Tối Giản, Sạch Sẽ & Không Để Lại Rác
- Giải phóng 100% tài nguyên khi tắt tool (RAII).
- Không để lại file tạm, log rác, scratch code hay zombie threads.

---

## II. KIẾN TRÚC MÃ NGUỒN C++20 (CLEAN ARCHITECTURE & DEPENDENCY INJECTION)

### 1. Kiến Trúc 4 Tầng Phân Lớp

```
┌─────────────────────────────────────────────────────────────┐
│                       PRESENTATION LAYER                    │
│   ImGui Native DX11 / Vulkan Renderer (Server-Driven UI)    │
│   Từng Menu là 1 IMenuView độc lập (FishingView, MiningView)│
└──────────────────────────────┬──────────────────────────────┘
                               │ (Dependency Inversion)
┌──────────────────────────────▼──────────────────────────────┐
│                       APPLICATION LAYER                     │
│   Services: IFishingService, IMiningService, ITeleportService│
│   Bot Engines: FishingBot, MiningBot, InsectBot             │
│   Popups & Dialog Interceptor Service                       │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                          CORE DOMAIN                        │
│   Memory Engine (IMemoryDriver, Scanner, Struct Deserializer│
│   Action Dispatcher (Unity Main Thread Queue Execution)     │
│   Event Bus & Entity Tracker                                │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│                      INFRASTRUCTURE LAYER                   │
│   Emulator Drivers (LDPlayer9Driver, MEmuDriver, AndroidNDK)│
│   Security Shield (AntiDebug, XorStr, IntegrityChecker)     │
│   Network Client (TLS 1.3 / WebSocket to VPS Server)        │
└─────────────────────────────────────────────────────────────┘
```

### 2. Tiêu Chuẩn Viết Code C++20
- **Smart Pointers:** Không sử dụng raw pointer để quản lý vòng đời bộ nhớ (`new`/`delete` trần bị cấm). Sử dụng `std::unique_ptr` cho sở hữu đơn lẻ và `std::shared_ptr` khi chia sẻ giữa các service.
- **Span & String View:** Dùng `std::string_view` và `std::span` để truyền chuỗi và buffer mà không phát sinh sao chép bộ nhớ (Zero Allocation).
- **Concepts & Templates:** Áp dụng C++20 Concepts để ràng buộc kiểu dữ liệu cho memory reader và packet serializer.
- **Thread Safety:** Sử dụng `std::jthread` tự động join khi hủy, tránh tạo thread rác.

---

## III. QUY TRÌNH BẮT BUỘC KHI XỬ LÝ BUG DO USER BÁO (ANTI-REGRESSION)
- Bất kỳ khi nào người dùng báo một bug và được fix xong, **BẮT BUỘC PHẢI LƯU VÀO** [skill/BUG_REGRESSION_LEDGER.md](file:///F:/DTA_Playtogether/skill/BUG_REGRESSION_LEDGER.md).
- File này lưu: Mã lỗi, mô tả, nguyên nhân gốc rễ, cách sửa và **Quy tắc cấm tái phạm**.
- Trước mỗi lần bàn giao code mới hoặc giải quyết yêu cầu, AI Agent phải đối chiếu danh sách này để đảm bảo không vi phạm lại bất kỳ lỗi nào đã từng xảy ra.
