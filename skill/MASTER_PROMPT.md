# MASTER PROMPT: TÁI CẤU TRÚC TOÀN DIỆN DỰ ÁN DTA PLAYTOGETHER SANG C++20 NATIVE (ZERO-TAP, ULTRA-PERFORMANCE, MULTI-EMULATOR & APK)

> **Dành cho Claude / AI Agent Senior Systems & Game Security Engineer**
> **Mục tiêu:** Đập đi xây lại 100% toàn bộ hệ thống bot/tool tự động hóa game Play Together từ Python sang **C++20 Native**. Tối ưu hóa hiệu năng cực hạn (Zero Latency, Zero Frame Drop), chuẩn hóa kiến trúc Clean Architecture (Interface/Service/DI), xóa bỏ toàn bộ thao tác giả lập bấm màn hình (100% gọi hàm game nội bộ IL2CPP trên Main Thread), chia tách module menu độc lập, hỗ trợ đa giả lập (LDPlayer 9+, MEmu v9+, Android APK gốc) và phân tách mô hình Client/Server chịu tải hàng chục nghìn người dùng song song mà không nghẽn VPS.

---

## 1. MỤC TIÊU CỐT LÕI & CÁC NGUYÊN TẮC BẤT KHẢ XÂM PHẠM

### 1.1. 100% C++20 Native — Xóa bỏ Python
- Toàn bộ codebase chuyển sang **C++20** (sử dụng MSVC v143 / Clang 17+ trên Windows, NDK r26+ cho Android ARM64).
- Zero-cost abstractions, RAII, Smart Pointers (`std::shared_ptr`, `std::unique_ptr`), Lock-free queues (`std::atomic`, ring buffers), compile-time string encryption (`constexpr xorstr`).
- Không sử dụng Python, không dùng các scripting runtime nặng nề ở tầng Client.

### 1.2. Nguyên tắc Zero-Tap (100% Game Function Call)
- **TUYỆT ĐỐI CẤM:** Không dùng ADB tap, không dùng `adb shell input tap`, không dùng Win32 PostMessage mouse click, không dùng Touch injection/joystick ảo để điều khiển nhân vật.
- **BẮT BUỘC:** Mọi hành vi (quăng cần, giật cá, đập đá, bắt bọ, đào kho báu, tưới cây, nhặt đồ, di chuyển, chuyển map, đóng popup, xác nhận thưởng) đều phải được thực thi bằng cách gọi **trực tiếp hàm Native của game** (IL2CPP) trên **Unity Main Thread**:
  + Ví dụ: `FishingPoleController::OnClick_Button`, `PickaxController::OnClick_Button`, `KinematicCharacterMotor::set_TransientPosition`, `LayerSystem::ConnectToZoneMove`, `OnPickFieldObject`, `DialogBoxMessage::OnClick_OK`, v.v.
- Mọi con trỏ (`thisPtr`), struct truyền vào phải được kiểm tra hợp lệ (`IsValidPointer`, aligned, mapped, Unity Object alive) trước khi gọi để triệt tiêu 100% nguy cơ crash/văng game.

### 1.3. Cấm tuyệt đối NPC / Portal / Phone để dịch chuyển
- Cấm đi bộ qua portal, cấm tương tác với NPC chuyển map, cấm mở điện thoại ảo.
- Mọi chuyển map phải thông qua Direct Zone Move Hook (`LayerSystem.ConnectToZoneMove`). Dịch chuyển tức thời trên cùng bản đồ dùng `KinematicCharacterMotor.set_TransientPosition`.

### 1.4. Đa nền tảng Giả Lập & Thiết kế Mở rộng APK Android
- Hệ thống hỗ trợ cắm chạy (Plug & Play) không hardcode đường dẫn:
  + Tự động phát hiện giả lập đang chạy: **LDPlayer 9+** (`dnplayer.exe`), **MEmu mới nhất** (`MEmuConsole.exe` / `MEmuHeadless.exe`), Nox, MuMu.
  + Cơ chế kết nối: Hỗ trợ cả 2 chế độ:
    1. **External Memory & Native Bridge:** Giao tiếp qua Named Pipe / Shared Memory IPC tới In-Game Native Payload.
    2. **Direct In-Process Injection (.so):** Thiết kế codebase theo cấu trúc Header-only / Static Library để khi cần có thể compile trực tiếp thành `libdta_core.so` nạp thẳng vào Android APK qua Zygisk / Magisk / Frida Gadget mà không cần sửa đổi logic nghiệp vụ.

---

## 2. KIẾN TRÚC CLIENT - SERVER TỐI ƯU HÓA TẢI VPS (SERVER-DRIVEN UI & THIN-SERVER)

Để phục vụ hàng nghìn đến hàng chục nghìn người dùng mà VPS không bị quá tải:
- **Client (C++ Native App):**
  + Đảm nhiệm toàn bộ vòng lặp phân tích game state (Memory Reader, ESP calculation, Entity tracking, State Machine bot câu cá/đập đá/bắt bọ).
  + Xử lý cục bộ 100% với tốc độ 60 - 144 FPS, không gửi raw memory ticks lên server.
  + Client Render UI: Dùng **ImGui (DirectX 11 / Vulkan)** hoặc Custom C++ Graphics Engine siêu nhẹ, không tiêu tốn CPU.
- **Server (C++ High-Performance Server - Boost.Asio / Crow / Drogon):**
  + **Server-Driven UI Schema:** Layout UI, cấu hình menu, danh sách tính năng được Server định dạng qua JSON/FlatBuffers gửi xuống Client khi xác thực thành công. Muốn sửa đổi UI, thêm tính năng, bảo trì chức năng chỉ cần update trên Server, Client tự động render layout mới mà không cần re-compile!
  + **Authentication & HWID Licensing:** Mã hóa ECDSA + AES-256-GCM, Heartbeat định kỳ 60s, phát hiện tài khoản chia sẻ/crack.
  + **Dynamic Offset Cloud Sync:** Máy chủ lưu trữ bảng Offset/Signatures mới nhất cho từng phiên bản game. Khi game cập nhật, server đẩy offset mới, client tự map mà không cần phát hành bản cập nhật mới.
  + **Cloud Waypoints & Anti-Ban Policies:** Phục vụ dữ liệu tọa độ bản đồ, phân tích hành vi bot an toàn.

---

## 3. THIẾT KẾ CẤU TRÚC GIAO DIỆN (UI/UX) HIỆN ĐẠI — CHỐNG "AI-LOOK"

Giao diện cũ hoặc các giao diện sinh bởi AI thường dùng màu tím neon lòe loẹt, layout thô kệch, bảng điều khiển nhồi nhét. Hệ thống mới được thiết kế theo tiêu chuẩn công nghiệp:
- **Design Language:** *Cyber Graphite & Arctic Cyan* (Tối giản, chuyên nghiệp, góc cạnh sắc sảo).
  + Background: Deep Charcoal `#0D0F12`, Dark Slate `#15181E`, Card Surface `#1C2129`.
  + Accent: Cold Cyan `#00E5FF`, Mint Green `#10B981` (Running), Amber Gold `#F59E0B` (Warning), Crimson `#EF4444` (Danger).
  + Typography: Sạch sẽ, monospace cho chỉ số/tọa độ/offset, phân cấp thị giác rõ ràng.
- **Cấu trúc Menu Module riêng biệt:**
  + Mỗi menu là 1 Class Service + 1 View Renderer độc lập kế thừa từ `IMenuView`:
    1. **Dashboard & Status:** Chỉ số FPS, trạng thái kết nối game, CPU/RAM, license thời hạn.
    2. **Fishing (Câu cá):** Tự quăng, tự giật, lọc bóng 1-7, lọc màu nền 1-5, lọc ID cá, lọc biến thể/đột biến, tự bảo quản/bán nhanh, tự sửa cần.
    3. **Mining (Đập đá):** Quét quặng theo bán kính, tự dịch chuyển đến quặng, tự vung cuốc đập vỡ đá, nhặt quặng tức thì.
    4. **Insect (Bắt bọ):** Dò côn trùng, đóng băng/hạ cánh bọ, tự dịch chuyển theo khoảng cách tối ưu, vung vợt bắt bọ, lọc côn trùng hiếm.
    5. **Excavation (Đào kho báu):** Giải mã tín hiệu máy dò, định vị điểm kho báu, tự đào, tự mở rương.
    6. **Farm (Nông trại):** Tự động gieo hạt, tưới nước, bón phân, thu hoạch tất cả chậu cây.
    7. **Collect (Nhặt rác/Vật phẩm):** Tự nhặt cành cây, rác biển, vỏ sò, tài nguyên trên đảo.
    8. **Teleport & Zones:** Quản lý toạ độ, dịch chuyển tức thời, chuyển map trực tiếp không NPC/Portal.
    9. **ESP & Radar Overlay:** DirectX 11 overlay siêu mượt, vẽ box 3D/2D, khoảng cách, tên vật thể.
    10. **Settings & Security:** Cài đặt phím tắt, cấu hình delay, hệ thống anti-detection, quản lý license.

---

## 4. AN TOÀN BẢO MẬT & CHỐNG REVERSE ENGINEERING

Cả Client và Server đều phải tích hợp các lớp phòng thủ:
- **String Encryption:** Mã hóa toàn bộ chuỗi ký tự lúc biên dịch bằng template `xorstr`. Không để lộ API URL, tên hàm game, offset string trong binary.
- **Anti-Debugging:** Kiểm tra định kỳ `IsDebuggerPresent`, `NtQueryInformationProcess`, hardware breakpoints (`DR0-DR3`), `RDTSC` time delta detection.
- **Memory Integrity & Anti-Hook:** Hash checksum các đoạn `.text` code segment để phát hiện việc can thiệp bằng Cheat Engine / x64dbg.
- **PE Header Stripping & Packing:** Tương thích với OLLVM / VMProtect 3.x / Themida.
- **Secure Transport:** Toàn bộ giao tiếp Client - Server dùng TLS 1.3 với Certificate Pinning (chống Fiddler/Charles/Burp Suite).

---

## 5. CẤU TRÚC FOLDER CHUẨN CỦA DỰ ÁN MỚI

```
DTA_Playtogether/
├── dump.cs                      # File dump IL2CPP chuẩn từ Il2CppDumper (nguồn sự thật)
├── skill/                       # Toàn bộ tài liệu đặc tả kỹ thuật và rules
│   ├── MASTER_PROMPT.md         # File này
│   ├── 00_RULE_ARCH.md          # 17 nguyên tắc cốt lõi & Tiêu chuẩn Clean Architecture C++
│   ├── 01_CORE_ENGINE.md        # Core: Memory, IL2CPP Dispatcher, Main Thread Hook, State Machine
│   ├── 02_MENU_FISHING.md       # Đặc tả Menu Câu Cá (Fishing)
│   ├── 03_MENU_MINING.md        # Đặc tả Menu Đập Đá (Mining)
│   ├── 04_MENU_INSECT.md        # Đặc tả Menu Bắt Côn Trùng (Insect)
│   ├── 05_MENU_EXCAVATION.md    # Đặc tả Menu Đào Kho Báu (Excavation)
│   ├── 06_MENU_FARM.md          # Đặc tả Menu Nông Trại (Farm)
│   ├── 07_MENU_COLLECT.md       # Đặc tả Menu Thu Thập Rác/Vật Phẩm (Collect)
│   ├── 08_MENU_TELEPORT.md      # Đặc tả Menu Dịch Chuyển & Đổi Map (Teleport & Zones)
│   ├── 09_MENU_ESP.md           # Đặc tả Menu ESP & Radar Overlay
│   ├── 10_MENU_SETTINGS_SECURITY.md # Cài đặt, License, Anti-RE & Memory Shield
│   ├── 11_SERVER_CLIENT_PROTOCOL.md # Giao thức Client-Server & Tối ưu hóa tải VPS
│   └── 12_UI_UX_DESIGN_SYSTEM.md    # Hệ thống Design UI/UX Dark-Cyan Gaming
├── src/
│   ├── client/                  # Client C++ Source Code
│   │   ├── core/                # Core engine (Memory, Hook, Injector, Dispatcher)
│   │   ├── emulator/            # Emulator Abstraction (LDPlayer, MEmu, Android Native)
│   │   ├── features/            # Từng feature chia folder riêng (Interface + Service + Bot)
│   │   │   ├── fishing/
│   │   │   ├── mining/
│   │   │   ├── insect/
│   │   │   ├── excavation/
│   │   │   ├── farm/
│   │   │   ├── collect/
│   │   │   ├── teleport/
│   │   │   └── esp/
│   │   ├── ui/                  # Dynamic Renderer (ImGui DX11) & Menu Views
│   │   ├── security/            # Anti-RE, XorStr, Integrity Check
│   │   └── network/             # WebSocket / TLS Client giao tiếp với VPS
│   ├── server/                  # Server C++ / Go Source Code (VPS)
│   │   ├── api/                 # Auth, Heartbeat, License Controller
│   │   ├── ui_schema/           # Quản lý Layout UI động gửi xuống Client
│   │   ├── database/            # SQLite/PostgreSQL quản lý user, HWID, key
│   │   └── security/            # Crypto, Token Bucket Rate Limiter
│   └── shared/                  # Header dùng chung (Packet Protocol, Enums, Models)
├── build/                       # CMake & Build Scripts (MSVC, Clang, Android NDK)
└── bin/                         # Thư mục Output sau khi build
    ├── Client/                  # Chứa DTA_Client.exe và dependencies cho người dùng
    └── Server/                  # Chứa DTA_Server binary chạy trên Linux/Windows VPS
```

---

## 6. HƯỚNG DẪN BẮT ĐẦU CHO CLAUDE / AI AGENT

Khi bắt đầu triển khai code cho dự án này, Claude PHẢI:
1. Đọc kỹ từng file trong thư mục `skill/` theo thứ tự từ `00_RULE_ARCH.md` đến `12_UI_UX_DESIGN_SYSTEM.md`.
2. Luôn đối chiếu `dump.cs` cho bất kỳ Class, Method, Field Offset nào được sử dụng.
3. Tạo các file `interface` (`IFeatureService.hpp`, `IBotEngine.hpp`, `IDeviceBridge.hpp`) trước khi viết code triển khai.
4. Mọi hàm xử lý hành động game đều phải được đóng gói qua `GameActionDispatcher` để đẩy vào queue thực thi trên Unity Main Thread.
5. Biên dịch thử nghiệm bằng Clang / MSVC C++20 với cờ cảnh báo cao nhất (`/W4` hoặc `-Wall -Wextra`).
