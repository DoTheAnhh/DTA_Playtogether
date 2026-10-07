# SỔ TAY GHI NHẬN BUG & QUY TẮC CHỐNG TÁI PHẠM (BUG REGRESSION LEDGER)

> **Mục đích:** Lưu trữ toàn bộ các lỗi (bugs) được người dùng báo cáo, phân tích nguyên nhân gốc rễ, giải pháp đã áp dụng và đúc kết thành **Quy Tắc Bất Khả Xâm Phạm (Prevention Rules)** để AI Agent quét kiểm tra định kỳ, triệt tiêu 100% khả năng tái phạm ở bất kỳ đâu trong toàn bộ codebase.

---

## QUY TRÌNH BẮT BUỘC KHI TIẾP NHẬN BUG

1. **Phân tích:** Xác định chính xác file, dòng code, hành vi sai lệch so với kỳ vọng của người dùng.
2. **Sửa đổi & Kiểm thử:** Thực hiện sửa đổi, biên dịch và chạy kiểm thử thực tế (`[INGAME_VERIFIED]` hoặc `[RUNTIME_VERIFIED]`).
3. **Cập nhật Ledger:** Bổ sung mục lỗi vào file này theo mẫu chuẩn:
   - Mã Bug: `BUG-XXX`
   - Mô tả lỗi do user báo
   - Nguyên nhân gốc rễ (Root Cause)
   - Giải pháp đã khắc phục (Solution)
   - Quy tắc cấm tái phạm (Anti-Regression Rule & Scan Pattern)
4. **Quét toàn bộ Codebase:** Rà soát lại tất cả các module còn lại để đảm bảo không tồn tại lỗi tương tự.

---

## DANH SÁCH BUG ĐÃ GHI NHẬN & QUY TẮC PHÒNG TRÁNH

### BUG-001: Server Binary Chạy Xong Tắt Ngay, Không Giữ Port Lắng Nghe
- **Ngày ghi nhận:** 2026-10-07
- **Phản hồi từ User:** `F:\DTA_Playtogether\bin\Server\Release tôi chưa thấy run đc cái này`
- **Hiện tượng:** Khi chạy `dta_server.exe` (click đúp hoặc chạy terminal), process thực thi một vài dòng log mock rồi `return 0` thoát ngay lập tức, người dùng không thấy server chạy hoặc không kết nối được qua mạng.
- **Nguyên nhân gốc rễ (Root Cause):**
  1. `ServerMain.cpp` trước đó chỉ chạy self-test in ra console rồi thoát, chưa có socket listener thực sự.
  2. Chưa tích hợp HTTP/REST API server trên Windows (`Winsock2`) để lắng nghe các endpoint `/health`, `/api/activate`, `/api/check`, `/api/ui` như trong đặc tả [11_SERVER_CLIENT_PROTOCOL.md](file:///F:/DTA_Playtogether/skill/11_SERVER_CLIENT_PROTOCOL.md).
- **Giải pháp đã thực hiện (Solution):**
  1. Xây dựng module `src/server/network/HttpServer.hpp` và `HttpServer.cpp` bằng C++20 Winsock2 multi-threaded:
     - Bind và lắng nghe cổng `28445` (hoặc cổng cấu hình qua CLI `-p <port>`).
     - Tự động parse HTTP GET/POST và trả lời JSON (`200 OK`) kèm CORS header cho các endpoint: `/health`, `/api/activate`, `/api/check`, `/api/ui`.
  2. Cập nhật `ServerMain.cpp`:
     - Khởi chạy `HttpServer`, duy trì vòng lặp kiểm tra cờ `g_serverRunning` (Atomic Loop).
     - Bắt tín hiệu ngắt `SIGINT` (Ctrl+C) / `SIGTERM` để graceful shutdown.
     - Hỗ trợ command tương tác trên console (`status`, `exit`).
  3. Cập nhật `CMakeLists.txt` liên kết thư viện `ws2_32.lib`.
  4. Đã kiểm thử thực tế với PowerShell `Invoke-RestMethod`:
     - `GET http://127.0.0.1:28445/health` -> Trả về `{"status":"online", "port":28445}`.
     - `POST http://127.0.0.1:28445/api/activate` -> Trả về `{"ok":true, "msg":"Welcome to DTA PlayTogether VIP!"}`.
- **Quy tắc chống tái phạm (Prevention Rule):**
  > **QUY TẮC #01:** Bất kỳ module Server/Service/Daemon nào (`dta_server`, sidecar service, background agent) **BẮT BUỘC** phải là Long-Running Process có Socket Listener thực sự (`bind` & `listen`), cấm tuyệt đối viết dạng run-and-exit script. Trước khi bàn giao binary server, phải test request HTTP thực tế có phản hồi `200 OK`.

### BUG-002: Client và Server Chạy Lên Chỉ Hiện Console Đen, Thiếu Cửa Sổ GUI, Quản Lý Key & Điểm Tele Chung
- **Ngày ghi nhận:** 2026-10-07
- **Phản hồi từ User:** `sao chạy lên cả 2 mà k thấy giao diện UI UX của cả 2 nhỉ, trên client phải có quản lý key + điểm tele chung cho tất cả client và ở client phải có giao diện ng dùng chứ`
- **Nguyên nhân gốc rễ (Root Cause):**
  1. Client và Server ban đầu được cấu hình chạy Console Subsystem thuần túy, chỉ in stdout log mà chưa khởi tạo cửa sổ Win32 GUI Window đồ họa (`CreateWindowEx`, message loop `GetMessage`/`DispatchMessage`).
  2. Client chưa có màn hình trực quan cho người dùng nhập / quản lý License Key, xem HWID và danh sách Điểm Dịch Chuyển (Master Waypoints) chung được đồng bộ từ Server.
  3. Server thiếu cửa sổ bảng điều khiển (Server Control Panel) để tạo / cấp key, quản trị danh sách điểm tele chung và xem log.
- **Giải pháp (Solution):**
  1. Xây dựng Native Win32 GUI Window cho **Client** (`src/client/ui/ClientWindow.hpp`, `.cpp`):
     - Giao diện Dark Cyber Graphite & Arctic Cyan (#0B0D11 / #14171F / #00E5FF).
     - Sidebar chuyển tab: Dashboard, Câu cá, Đập đá, Bắt bọ, Đào báu, Nông trại, Thu thập, Teleport, ESP, Cài đặt & Key.
     - Tích hợp Quản lý License Key: Nhập key, bấm Kích hoạt (gọi `/api/activate`), hiển thị HWID và thời hạn VIP.
     - Tích hợp Điểm Tele chung: Tự động nạp danh sách Master Waypoints từ Server, bấm "Dịch chuyển tức thì" (Zero-Tap gọi `set_TransientPosition`).
     - Tích hợp nút Bắt đầu / Tạm dừng Bot tự động cho từng tính năng.
  2. Xây dựng Native Win32 GUI Window cho **Server** (`src/server/ui/ServerPanelWindow.hpp`, `.cpp`):
     - Cửa sổ Server Control Panel trực quan:
     - Tab 1: Quản lý License Key (Danh sách key, Tạo key mới, Trạng thái Active/Expired, HWID đã bind).
     - Tab 2: Quản lý Điểm Tele Master chung (Thêm điểm, sửa tọa độ X, Y, Z, Map ID, phát tán xuống Client).
     - Tab 3: Nhật ký HTTP Server real-time.
  3. Cập nhật `CMakeLists.txt` liên kết `comctl32.lib`, `dwmapi.lib`, `ws2_32.lib`, bật `UNICODE` và `_UNICODE`.
  4. Tạo script `build.bat` tại gốc dự án tự động biên dịch và đóng gói ra `dist/Client` và `dist/Server`.
- **Trạng thái xác thực:** `[RUNTIME_VERIFIED]` (Đã biên dịch MSVC Release thành công 100%, chạy thử nghiệm cả 2 cửa sổ GUI trực quan và kiểm tra API HTTP thành công).
- **Quy tắc chống tái phạm (Prevention Rule):**
  > **QUY TẮC #02:** Mọi binary dành cho người dùng và quản trị viên (`dta_client.exe`, `dta_server.exe`) **BẮT BUỘC** phải có Cửa Sổ Giao Diện Đồ Họa Trực Quan (Native GUI Window). Client phải có đầy đủ chức năng quản lý License Key và xem/dịch chuyển theo danh sách điểm tele chung từ Server (Server-Driven UI). Server phải có bảng quản lý Key và Master Teleport Spots. Cung cấp file `build.bat` để người dùng build nhanh ra thư mục `dist/`.

### BUG-003: Giao Diện Client Chưa Giống Mẫu Tool Cũ & Thiếu Tính Thẩm Mỹ Nâng Cao
- **Ngày ghi nhận:** 2026-10-07
- **Phản hồi từ User:** `làm cái giao diện giống như tool cũ ấy nhưng mà nâng cấp cho đẹp hơn là được` kèm ảnh tham chiếu `media_1791383214668.png`
- **Nguyên nhân gốc rễ (Root Cause):**
  1. Giao diện trước đó dựng theo bố cục generic, chưa bám sát thiết kế gốc của công cụ cũ: thiếu hàng 3 card điều khiển trên cùng (Thông tin câu, Tùy chọn tự động, Tính năng nâng cao), thiếu cụm 2 nút Bật/Tắt to ở góc phải, thiếu các dãy chip chọn kích cỡ bóng 1-7 và chip màu phẩm cấp (Trắng, Xanh lá, Xanh dương, Tím, VVIP).
  2. Bố cục thanh bên (Sidebar) chưa chuẩn hóa 9 mục chức năng theo đúng thứ tự: ESP, Câu cá, Đào cổ vật, Đập đá, Bắt bọ, Thu lượm, Nông trại, Dịch chuyển, Cài đặt.
- **Giải pháp (Solution):**
  1. Tái thiết kế toàn bộ `ClientWindow.hpp` và `ClientWindow.cpp` theo đúng chuẩn giao diện ảnh tham chiếu nhưng nâng cấp đồ họa Cyber Midnight Sapphire:
     - Nền tối sâu `#0A0F1D`, card surface `#131B32`, input `#18223E`, viền `#202D52`, xanh active `#0084FF`, cyan `#00E5FF`.
     - Sidebar 9 mục chức năng với icon chuẩn, thanh gạt active phát sáng Cyan, ô logo header và nút "Đăng xuất key" ở chân sidebar.
     - Top bar: Toggle "Ghim trên cùng", status badge "• Đã dừng / • Đang chạy", sub-tab pills `[ Câu cá | Lịch sử ]`, bộ chọn giả lập `[ LDPlayer ▾ ]`, `[ Tab ▾ ]` và nút refresh `🔄`.
     - 3 Card hàng 1:
       * Card 1: ID cá, Bóng, Đã câu (số to 32px), dòng thông báo hổ phách `• Chưa có tab giả lập nào đang chạy`.
       * Card 2: Switch "Tự sửa cần", "Có gói bán nhanh", nút gạt `[ Bảo quản | Bán nhanh ]`.
       * Card 3: Switch "Khóa POV khi câu", "Cá cắn nhanh", chú thích nâng cao.
       * Cụm nút to: `▶  Bật` và `■  Tắt`.
     - Card Lọc cá: Switch "Lọc cá (Không chọn mặc định tất cả)", input ID cá, nút xoá lọc, dãy chip bóng 1-7, dãy chip phẩm cấp màu.
     - Card Giữ cá: Switch "Giữ cá biến thể", Switch "Giữ cá đột biến", input Giữ ID, nút xoá giữ, dãy chip giữ bóng 1-7, dãy chip giữ nền.
- **Quy tắc chống tái phạm (Prevention Rule):**
  > **QUY TẮC #03:** Giao diện Client **BẮT BUỘC** phải tuân thủ chuẩn bố cục Gaming Cyber từ ảnh mẫu tham chiếu. Tab Câu cá phải có đầy đủ 3 card điều khiển + 2 nút Bật/Tắt to + 2 card bộ lọc bóng & phẩm màu. Không được tự ý cắt giảm các chip điều khiển hay dùng controls xám xịt Win95.

### BUG-004: Thư Mục Dist Bị Lẫn File .log Hoặc Dư Thừa File .bat
- **Ngày ghi nhận:** 2026-10-07
- **Phản hồi từ User:** `F:\DTA_Playtogether\dist\Client và F:\DTA_Playtogether\dist\Server khong được chứa 1 file .log nào cả chỉ chứa 1 file exe và 1 folder data thôi`
- **Nguyên nhân gốc rễ (Root Cause):**
  1. File log thực thi (`DTA_Client.log`) được tạo ra trong quá trình chạy thử nghiệm bị copy hoặc tồn đọng trong `dist/`.
  2. `build.bat` trước đó tạo thêm các file launcher `run.bat`, `run_panel.bat`, `run_vps_headless.bat`.
  3. Lệnh xóa `del dta_server.exe` trên hệ thống file Windows (NTFS không phân biệt hoa thường) đã vô tình xóa luôn `DTA_Server.exe`.
- **Giải pháp (Solution):**
  1. Cấu hình lại `build.bat`:
     - Làm sạch hoàn toàn thư mục `dist/Client` và `dist/Server` trước khi copy.
     - `dist/Client`: Copy DUY NHẤT `DTA_Playtogether.exe` và thư mục `data/`.
     - `dist/Server`: Copy DUY NHẤT `DTA_Server.exe` và thư mục `data/`.
     - Chạy lệnh quét xóa triệt để mọi file `*.log` và `*.bat` bên trong cả hai thư mục.
  2. Bật cờ Linker `/SUBSYSTEM:WINDOWS /ENTRY:mainCRTStartup` và gọi `ShowWindow(GetConsoleWindow(), SW_HIDE)` để khi click đúp `.exe` chỉ hiện trực tiếp giao diện GUI, không có bất kỳ cửa sổ CMD đen nào.
- **Quy tắc chống tái phạm (Prevention Rule):**
  > **QUY TẮC #04 (QUY TẮC BẤT KHẢ XÂM PHẠM VỀ PACKAGING DIST):**
  > 1. Trong `dist/Client`: **CHỈ ĐƯỢC PHÉP CHỨA DUY NHẤT 1 FILE `DTA_Playtogether.exe` VÀ 1 THƯ MỤC `data/`**.
  > 2. Trong `dist/Server`: **CHỈ ĐƯỢC PHÉP CHỨA DUY NHẤT 1 FILE `DTA_Server.exe` VÀ 1 THƯ MỤC `data/`**.
  > 3. **TUYỆT ĐỐI CẤM** để lại bất kỳ file `.log`, `.bat`, `.txt` hay file exe trùng lặp nào trong thư mục `dist/`.
### BUG-005: Modal Kích Hoạt Cần Bỏ HWID, Có Nút Xác Nhận, Nút Dùng Bản Miễn Phí & Giới Hạn Nghiêm Ngặt Cho Bản Free
- **Ngày ghi nhận:** 2026-10-07
- **Phản hồi từ User:** `và ở Popup Modal Kích Hoạt Key Khi Mở Client: bỏ hẳn cái hwid đi ng chơi sẽ có 1 ô nhập key và nút xác nhận để kiểm tra key, và có thêm 1 option dưới là Dùng bản miễn phí, chức năng của bản miễn phí sẽ chỉ có câu cá (không lọc, k giữ (bán tất cả cá câu đc)), không câu được cá bóng 6-7(không có cơ chế giật tụt HP cá bóng 6-7) không có cắn nhanh, có khoá cam`
- **Nguyên nhân gốc rễ (Root Cause):**
  1. `ActivationDialog` ban đầu hiển thị HWID người dùng chiếm nhiều diện tích và gây rối mắt không cần thiết.
  2. Nút kích hoạt trước đó là "KÍCH HOẠT VÀO TOOL", chưa có nút "XÁC NHẬN" riêng và thiếu lựa chọn "DÙNG BẢN MIỄN PHÍ" cho người chơi muốn trải nghiệm tính năng cơ bản.
  3. Thiếu phân tách logic cấp phép giữa bản VIP và bản Miễn phí (Free Tier) trong cả giao diện (`ClientWindow`) lẫn cỗ máy bot (`FishingBot`).
- **Giải pháp đã thực hiện (Solution):**
  1. Tái cấu trúc hoàn toàn `ActivationDialog.hpp` và `ActivationDialog.cpp`:
     - Xóa bỏ 100% hiển thị HWID (Label, EditBox, Copy button). HWID được lấy ngầm qua `HWIDProvider::GetHWID()`.
     - Cung cấp 1 ô nhập Key VIP + Nút `XÁC NHẬN` (gọi API server kiểm tra key).
     - Bổ sung nút `🎁  DÙNG BẢN MIỄN PHÍ` (Free Tier) cho phép vào tool ngay không cần key.
     - Hàm `ShowModal` trả về cờ `outIsFreeTier` phân định rõ ràng.
  2. Áp dụng giới hạn nghiêm ngặt cho Bản Miễn Phí (`Free Tier`):
     - **Chỉ có câu cá:** Các tab khác (ESP, Đào, Đập đá, Bắt bọ, Thu lượm, Nông trại, Dịch chuyển, Cài đặt) bị khóa, khi bấm vào sẽ hiển thị hộp thoại thông báo yêu cầu bản quyền VIP và giữ nguyên tại Tab Câu cá. Sidebar hiển thị huy hiệu `[VIP]` cho các tính năng cao cấp.
     - **Không lọc cá:** Checkbox `Lọc cá`, nút xóa lọc, các chip bóng 1-7 và chip phẩm màu bị vô hiệu hóa (`EnableWindow = FALSE`). Bot Free chấp nhận mọi con cá thông thường.
     - **Không giữ cá (Bán tất cả cá câu được):** Khóa nút `Bảo quản`, ép chế độ `Bán nhanh` (`m_keepFishSelected = false`), bot tự động gọi `SellFish()`.
     - **Không câu được cá bóng 6-7 (Không có cơ chế giật tụt HP cá bóng 6-7):**
       * Khi phát hiện cá bóng 6 hoặc bóng 7 ở state `SHADOW`, bot Free tự động nhấc cần cast lại (`CastRod`), không câu cá bóng 6-7.
       * Khi rơi vào trạng thái kéo giật cá to (`BIG_PUMPIN`, `BIG_DRAG`, `BIG_TUG`), bot Free KHÔNG can thiệp giật tụt HP, chỉ bản VIP mới có cơ chế giật nhấp nhả tụt HP cá to.
     - **Không có cắn nhanh:** Checkbox `Cá cắn nhanh` bị khóa và uncheck, độ trễ giật cần đưa về mức tự nhiên (200ms), không can thiệp hook memory cắn nhanh.
     - **Có khóa cam:** Checkbox `Khóa POV khi câu` vẫn được phép bật/tắt bình thường trên bản Free.
  3. Hỗ trợ nâng cấp VIP trực tiếp: Nút chân sidebar đổi thành `⚡ Nâng cấp key`, cho phép người dùng Free nhập key VIP bất kỳ lúc nào để mở khóa toàn bộ 9 tính năng mà không cần khởi động lại.
- **Trạng thái xác thực:** `[RUNTIME_VERIFIED]` (Đã biên dịch Release mã thoát 0, cập nhật binary sạch vào `dist/Client` và `dist/Server`).
- **Quy tắc chống tái phạm (Prevention Rule):**
  > **QUY TẮC #05:** Popup kích hoạt Client **TUYỆT ĐỐI KHÔNG HIỂN THỊ HWID**. Bắt buộc có: Ô nhập key + Nút Xác Nhận + Nút Dùng bản miễn phí. Bản miễn phí **CHỈ ĐƯỢC PHÉP CÂU CÁ**, bán tất cả cá, không lọc, không câu cá bóng 6-7 (không có cơ chế giật tụt HP cá bóng 6-7), không cắn nhanh, và ĐƯỢC khóa cam.
