# MASTER PROMPT: TÁI CẤU TRÚC TOÀN DIỆN DỰ ÁN DTA PLAYTOGETHER SANG C# & UNITY (ZERO-TAP, ULTRA-PERFORMANCE, MULTI-EMULATOR & APK)

> **Dành cho Claude / AI Agent Senior C# Systems, Unity Engine & Game Security Engineer**
> **Mục tiêu:** Xây dựng lại 100% toàn bộ hệ thống bot/tool tự động hóa game Play Together từ đầu bằng **C# & Unity Engine** (.NET 8 / C# 12, Unity uGUI / UI Toolkit / IMGUI). Tối ưu hóa hiệu năng cực hạn (Zero Latency, Zero GC Allocation, Zero Frame Drop), loại bỏ triệt để các lỗi tiềm ẩn của các phiên bản trước (nghẽn shell ADB, trễ giật cá, lỗi nút OK popup dialog, văng dịch chuyển do navmesh, crash cross-thread), 100% gọi hàm nội bộ IL2CPP trên Unity Main Thread, hỗ trợ đa giả lập (LDPlayer 9+, MEmu, MuMu) và Android APK gốc.

---

## 1. MỤC TIÊU CỐT LÕI & CÁC NGUYÊN TẮC BẤT KHẢ XÂM PHẠM

### 1.1. 100% C# & Unity Architecture — Xóa Bỏ Hoàn Toàn Python & C++ Cũ
- Toàn bộ codebase được phát triển bằng **C#** (.NET 8 / C# 12) trên nền tảng **Unity Engine**.
- Áp dụng các kỹ thuật C# hiệu năng cao: Zero-Allocation on Hot-Paths (`readonly ref struct`, `Span<T>`, `Memory<T>`, `ArrayPool<T>`), Lock-Free Concurrency (`System.Threading.Channels`, `ConcurrentQueue<T>`), SIMD Vectorization, Unsafe Direct Memory Access (`fixed`, pointers, `Unsafe.AsRef`).
- Kiến trúc Clean Architecture theo C# chuẩn doanh nghiệp: Interface Segregation, Dependency Injection (`Microsoft.Extensions.DependencyInjection` hoặc custom Service Locator siêu nhẹ), State Machine dựa trên Pattern hiện đại.

### 1.2. Khắc Phục Triệt Để Toàn Bộ Lỗi Tiềm Ẩn Đã Từng Gặp
1. **Lỗi ngắt kết nối ADB & Latency nghẽn Shell:**
   - *Nguyên nhân cũ:* Dùng lệnh `adb shell` qua subprocess đọc `/proc/<pid>/mem` với `dd`/`xxd` gây độ trễ 50-200ms và thường xuyên văng `GameError("Mất kết nối ADB với giả lập")`.
   - *Giải pháp C# Unity:* Sử dụng **Direct Memory Access / MemoryMappedFiles / Shared Memory IPC** qua Win32 API (`ReadProcessMemory`/`VirtualAllocEx` trực tiếp trên tiến trình giả lập LDPlayer/MEmu) hoặc **In-Process Unity Native Plugin (.so / .dll)** giao tiếp không qua ADB. Độ trễ đọc/ghi bộ nhớ đạt **< 0.1ms** (nhanh gấp 500-1000 lần so với ADB).
2. **Lỗi giật cần câu bị chậm so với lúc cá cắn (Reel Delay):**
   - *Nguyên nhân cũ:* Polling lặp lại việc query HUD, vị trí actor, tool info trên mỗi nhịp khiến thời gian từ lúc cá cắn (`!` xuất hiện) đến lúc phát lệnh giật mất tới 300-500ms làm cá chạy mất.
   - *Giải pháp C# Unity:* Tách riêng luồng **High-Speed State Monitor** (tần số 120-240 FPS hoặc trong Unity `Update`/`FixedUpdate`), theo dõi trực tiếp bit cờ cắn (`m_bBite` / `FishingPoleController.m_eState`) qua con trỏ bộ nhớ trực tiếp. Khi cờ bật, phát lệnh giật `Reel` ngay lập tức trong vòng **< 1ms**, đạt tỷ lệ giật dính 100%.
3. **Lỗi không gọi được nút OK / Popup Dialog ("vẫn k gọi đc hàm action OK"):**
   - *Nguyên nhân cũ:* Gọi hàm method logic thuần hoặc gọi ngoài main thread làm NGUI event loop không nhận diện click, popup bị kẹt trên màn hình.
   - *Giải pháp C# Unity:* Trỏ trực tiếp vào instance `UIButton` của nút OK/Confirm trên giao diện NGUI/uGUI, kích hoạt sự kiện `UIButton.OnClick()` hoặc dispatch qua NGUI `UICamera.Notify` trên Unity Main Thread. Bổ sung cơ chế fallback tự động rà soát con trỏ component button trong cây GameObject.
4. **Lỗi văng game khi dịch chuyển (Teleport NavMesh Crash):**
   - *Nguyên nhân cũ:* Hàm đọc navmesh phân mảnh đọc hàng nghìn polygon qua pipe ADB làm tràn bộ đệm shell; hoặc nhân vật rơi tự do xuống hư vô do bề mặt chưa sẵn sàng.
   - *Giải pháp C# Unity:* Quản lý NavMesh trong bộ nhớ RAM local (cache theo MapID), tính toán cao độ Y bằng thuật toán Point-in-Polygon đa giác tức thì, tự động fallback an toàn về `target_y` nếu khu vực chưa nạp đa giác. Dịch chuyển thông qua `KinematicCharacterMotor.set_TransientPosition` kết hợp triệt tiêu vận tốc cũ (`Vector3.zero`).
5. **Lỗi Garbage Collection Stutter (Đứng hình do GC):**
   - *Giải pháp C# Unity:* Cấm triệt để việc cấp phát bộ nhớ động (`new class`, string concatenation, LINQ) trong vòng lặp chính của Bot. Sử dụng `struct`, `Span<byte>`, `ArrayPool<byte>`, tái sử dụng buffer cố định để GC allocations = 0 bytes/frame.
6. **Lỗi ngoại lệ đa luồng (Cross-Thread Unity Exception):**
   - *Giải pháp C# Unity:* Mọi lời gọi can thiệp game đều được đẩy vào `UnityMainThreadDispatcher` thực thi đồng bộ trong nhịp `Update` của Unity, bảo vệ an toàn 100% cho IL2CPP GC và Unity Engine internals.

### 1.3. Nguyên Tắc Zero-Tap Tối Thượng (100% Game Function Call)
- **TUYỆT ĐỐI CẤM:** Không dùng ADB tap, không dùng `adb shell input tap`, không dùng Win32 Mouse click giả lập toạ độ màn hình, không dùng joystick ảo.
- **BẮT BUỘC:** 100% hành vi (quăng cần, giật cá, đập đá, vung vợt, đào xẻng, tưới cây, nhặt đồ, di chuyển, đổi map, đóng popup, nhận thưởng) đều được thực thi bằng cách gọi **trực tiếp hàm Native của game** (IL2CPP) trên **Unity Main Thread**:
  + Ví dụ: `FishingPoleController.OnClick_Button`, `PickaxController.OnClick_Button`, `KinematicCharacterMotor.set_TransientPosition`, `LayerSystem.ConnectToZoneMove`, `OnPickFieldObject`, `UIButton.OnClick`, v.v.
- Mọi con trỏ (`IntPtr`) trước khi gọi đều phải được kiểm tra tính hợp lệ (`IsValidPointer`, aligned, mapped, Unity Object alive `m_CachedPtr != 0`).

### 1.4. Cấm Tuyệt Đối Dùng NPC / Portal / Phone Để Dịch Chuyển
- Cấm đi bộ qua portal, cấm tương tác với NPC chuyển map, cấm mở điện thoại ảo.
- Chuyển map duy nhất hợp lệ là Direct Zone Move Hook: `LayerSystem.ConnectToZoneMove`. Dịch chuyển tức thời trên cùng bản đồ dùng `KinematicCharacterMotor.set_TransientPosition`.

---

## 2. KIẾN TRÚC CLIENT - SERVER (SERVER-DRIVEN UI & THIN-SERVER)

- **Client (C# Unity App / In-Game Overlay):**
  + Đảm nhiệm toàn bộ vòng lặp phân tích game state (Memory Reader, ESP calculation, Entity tracking, State Machine bot câu cá/đập đá/bắt bọ).
  + Render UI: Sử dụng **Unity uGUI / UI Toolkit / IMGUI** với phong cách hiện đại *Cyber Graphite & Arctic Cyan*.
  + Xử lý cục bộ 100% với tốc độ 60 - 144 FPS, không gửi raw memory ticks lên server.
- **Server (C# ASP.NET Core / Kestrel VPS):**
  + **Server-Driven UI Schema:** Layout UI, cấu hình menu, danh sách tính năng được Server định dạng qua JSON/Protobuf gửi xuống Client khi xác thực thành công. Muốn sửa đổi UI, thêm tính năng, bảo trì chức năng chỉ cần update trên Server, Client tự động render layout mới mà không cần re-compile!
  + **Authentication & HWID Licensing:** Mã hóa ECDSA + AES-256-GCM, Heartbeat định kỳ 60s, phát hiện tài khoản chia sẻ/crack.
  + **Dynamic Offset Cloud Sync:** Máy chủ lưu trữ bảng Offset/Signatures mới nhất cho từng phiên bản game.

---

## 3. CẤU TRÚC THƯ MỤC CHUẨN CỦA DỰ ÁN C# UNITY

```
F:\DTA_Playtogether\
├── dump.cs                      # File dump IL2CPP chuẩn từ Il2CppDumper (nguồn sự thật)
├── skill/                       # Toàn bộ tài liệu đặc tả kỹ thuật và rules C#
│   ├── MASTER_PROMPT.md         # File này
│   ├── 00_RULE_ARCH.md          # 17 nguyên tắc cốt lõi & Tiêu chuẩn Clean Architecture C# Unity
│   ├── 01_CORE_ENGINE.md        # Core: Direct Memory, IL2CPP Dispatcher, Main Thread Hook, FSM
│   ├── 02_MENU_FISHING.md       # Đặc tả Menu Câu Cá (Fishing)
│   ├── 03_MENU_MINING.md        # Đặc tả Menu Đập Đá (Mining)
│   ├── 04_MENU_INSECT.md        # Đặc tả Menu Bắt Côn Trùng (Insect)
│   ├── 05_MENU_EXCAVATION.md    # Đặc tả Menu Đào Kho Báu (Excavation)
│   ├── 06_MENU_FARM.md          # Đặc tả Menu Nông Trại (Farm)
│   ├── 07_MENU_COLLECT.md       # Đặc tả Menu Thu Thập Rác/Vật Phẩm (Collect)
│   ├── 08_MENU_TELEPORT.md      # Đặc tả Menu Dịch Chuyển & Đổi Map (Teleport & Zones)
│   ├── 09_MENU_ESP.md           # Đặc tả Menu ESP & Radar Overlay (Unity Canvas)
│   ├── 10_MENU_SETTINGS_SECURITY.md # Cài đặt, License, Anti-RE & Memory Shield
│   ├── 11_SERVER_CLIENT_PROTOCOL.md # Giao thức Client-Server & Tối ưu hóa tải VPS
│   ├── 12_UI_UX_DESIGN_SYSTEM.md    # Hệ thống Design UI/UX Dark-Cyan Gaming trong Unity
│   └── BUG_REGRESSION_LEDGER.md # Bảng ghi nhận toàn bộ lỗi đã khắc phục triệt để
├── src/
│   ├── Client/                  # C# Unity Client Solution
│   │   ├── Assets/Scripts/
│   │   │   ├── Core/            # Memory, IL2CPP Bridge, Main Thread Dispatcher
│   │   │   │   ├── Device/      # IDeviceDriver (LDPlayer, MEmu, Android Native)
│   │   │   │   ├── Memory/      # IMemoryService, FastMemoryReader, StructMarshaller
│   │   │   │   ├── Native/      # GameActionDispatcher, NativeBridge, PopupInterceptor
│   │   │   │   ├── Fsm/         # IStateMachine, BaseState, StateContext
│   │   │   │   └── Events/      # EventBus, GameEvents
│   │   │   ├── Features/        # Từng tính năng độc lập (Interface + Service + Bot + Models)
│   │   │   │   ├── Fishing/     # IFishingService, FishingService, FishingBot, FishingCatalog
│   │   │   │   ├── Mining/      # IMiningService, MiningService, MiningBot, OreScanner
│   │   │   │   ├── Insect/      # IInsectService, InsectService, InsectBot, InsectPredictor
│   │   │   │   ├── Excavation/  # IExcavationService, ExcavationService, ExcavationBot, RadarSolver
│   │   │   │   ├── Farm/        # IFarmService, FarmService, FarmBot, FarmPlotScanner
│   │   │   │   ├── Collect/     # ICollectService, CollectService, CollectBot, PathOptimizer
│   │   │   │   ├── Teleport/    # ITeleportService, TeleportService, WaypointManager
│   │   │   │   └── Esp/         # IEspService, EspService, EspOverlay
│   │   │   ├── UI/              # Unity UI System (Server-Driven Renderer & Menu Views)
│   │   │   │   ├── Framework/   # IMenuView, UIRenderer, DesignSystem
│   │   │   │   ├── Components/  # CyberCard, StatusBadge, ToggleSwitch, StatCounter
│   │   │   │   └── Views/       # DashboardView, FishingView, MiningView,...
│   │   │   ├── Security/        # AntiDebug, MemoryShield, HWIDProvider, XorString
│   │   │   └── Network/         # NetworkClient, WebSocketClient, CryptoHelper
│   ├── Server/                  # C# ASP.NET Core High-Performance Server
│   │   ├── Controllers/         # AuthController, OffsetController, UISchemaController
│   │   ├── Services/            # LicenseService, TokenBucketLimiter, DatabaseService
│   │   └── Models/              # User, License, HWID, UISchema
│   └── Shared/                  # Class Library dùng chung giữa Client & Server
│       ├── Enums/               # GameEnums, StateEnums, PacketType
│       ├── Models/              # FeatureModels, PacketModels, ThemeModels
│       └── Protocol/            # PacketHeader, CryptoProtocol
```

---

## 4. HƯỚNG DẪN BẮT ĐẦU CHO CLAUDE / AI AGENT

Khi triển khai mã nguồn C# và Unity cho dự án này, Claude PHẢI:
1. Đọc kỹ từng file trong thư mục `skill/` từ `00_RULE_ARCH.md` đến `12_UI_UX_DESIGN_SYSTEM.md`.
2. Luôn đối chiếu `dump.cs` cho bất kỳ Class, Method, Field Offset nào được sử dụng.
3. Thiết kế Interface C# (`IFeatureService.cs`, `IBotEngine.cs`, `IDeviceDriver.cs`) trước khi viết code triển khai.
4. Mọi hàm xử lý hành động game đều phải được đẩy vào `GameActionDispatcher` để thực thi trên Unity Main Thread.
5. Kiểm tra tính toàn vẹn cú pháp bằng `dotnet build` hoặc Unity Compilation, kiểm soát triệt để GC allocation.
