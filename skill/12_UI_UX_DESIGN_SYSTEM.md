# 12: HỆ THỐNG THIẾT KẾ GIAO DIỆN UI/UX CHUYÊN NGHIỆP (ANTI-AI VIBE DESIGN SYSTEM)

> **Mục tiêu:** Xây dựng hệ thống giao diện trực quan, sang trọng, mang phong cách gaming hiện đại, tối giản (Minimalist Cyberpunk / Industrial Dark Mode), loại bỏ triệt để cảm giác "AI-generated" (tím gradient rẻ tiền, layout nhồi nhét lộn xộn). Cấu trúc phân tách từng menu thành các file component độc lập, render siêu nhẹ bằng C++ ImGui DirectX 11.

---

## I. TRIẾT LÝ THIẾT KẾ: TẠI SAO GIAO DIỆN CŨ BỊ "NHÌN AI"?

### 1. Dấu Hiệu Nhận Biết Giao Diện "AI Vibe" Cần Loại Bỏ
- ❌ Dùng dải màu Gradient Tím Neon (`#8A2BE2` đến `#FF00FF`) lòe loẹt, gây mỏi mắt.
- ❌ Bo tròn quá đà (Corner Radius 25px+) tạo cảm giác bong bóng đồ chơi.
- ❌ Icon nhồi nhét không ăn nhập với ngữ cảnh, các nút bấm không có phân cấp chính/phụ (Primary/Secondary).
- ❌ Nhồi nhét hàng chục switch và slider vào chung một màn hình phẳng mà không có phân nhóm thẻ (Card grouping).

### 2. Tiêu Chuẩn Mới: "Cyber Graphite & Arctic Ice"
- ✔ **Bảng màu công nghiệp tinh tế:**
  + **Background (Nền chính):** Charcoal Đậm `#0B0D11`
  + **Surface (Bề mặt Card):** Dark Slate `#14171F`
  + **Card Hover & Border:** Graphite Viền Mảnh 1px `#232834`
  + **Primary Accent (Màu điểm nhấn):** Cold Cyan `#00E5FF` (Đại diện cho sự sắc bén, công nghệ)
  + **Success / Active:** Emerald Green `#10B981` (Bot đang chạy, kết nối ổn định)
  + **Warning:** Amber Gold `#F59E0B` (Độ bền cần câu/cuốc thấp)
  + **Danger:** Crimson Red `#EF4444` (Mất kết nối, phát hiện admin)
- ✔ **Font chữ & Typography:**
  + Nhãn văn bản (Labels): `Segoe UI` / `Inter` (13px - 14px, Clean, Dễ đọc).
  + Tọa độ, Offset, Tốc độ, Timer: `JetBrains Mono` / `Consolas` (Monospace, căn lề thẳng hàng tuyệt đối).
- ✔ **Khoảng cách & Căn chỉnh (Spacing):** Hệ số chuẩn 4px/8px (Padding 12px, Item Spacing 8px, Border Radius 4px - 6px góc cạnh sắc sảo).

---

## II. CẤU TRÚC PHÂN TÁCH MENU ĐỘC LẬP (MODULAR MENU VIEWS)

Mỗi Menu nằm trong một file riêng biệt, không viết chung trong 1 file khổng lồ:

```
src/client/ui/
├── IMenuView.hpp           # Base Interface cho mọi tab
├── UIRenderer.hpp          # Quản lý vòng lặp vẽ ImGui DirectX 11
├── UIRenderer.cpp
├── components/             # Các widget dùng chung
│   ├── CyberCard.hpp       # Khung card có viền phát sáng nhẹ
│   ├── StatusBadge.hpp     # Huy hiệu trạng thái Running / Idle
│   ├── ToggleSwitch.hpp    # Công tắc bật tắt gạt mượt
│   └── StatCounter.hpp     # Bộ đếm số lượng quặng/cá đã bắt
└── views/                  # Từng Menu là 1 file độc lập
    ├── DashboardView.cpp   # Tab Tổng quan & Trạng thái hệ thống
    ├── FishingView.cpp     # Tab Câu cá
    ├── MiningView.cpp      # Tab Đập đá
    ├── InsectView.cpp      # Tab Bắt bọ
    ├── ExcavationView.cpp  # Tab Đào kho báu
    ├── FarmView.cpp        # Tab Nông trại
    ├── CollectView.cpp     # Tab Thu thập vật phẩm
    ├── TeleportView.cpp    # Tab Dịch chuyển & Bản đồ
    ├── EspView.cpp         # Tab ESP & Radar
    └── SettingsView.cpp    # Tab Cài đặt & Bản quyền
```

---

## III. BASE INTERFACE `IMenuView`

```cpp
// include/ui/IMenuView.hpp
#pragma once
#include <string_view>

class IMenuView {
public:
    virtual ~IMenuView() = default;

    // Tên hiển thị trên thanh Sidebar / Header
    virtual std::string_view GetMenuId() const = 0;
    virtual std::string_view GetTitle() const = 0;
    virtual const char* GetIcon() const = 0;

    // Hàm render chính được gọi mỗi frame khi tab đang active
    virtual void Render() = 0;

    // Khởi tạo và dọn dẹp tài nguyên
    virtual void OnInit() {}
    virtual void OnDestroy() {}
};
```

---

## IV. BỐ CỤC KHUNG NHÌN CHUẨN (LAYOUT BLUEPRINT)

```
┌───────────────────────────────────────────────────────────────────────────────┐
│  [DTA] PLAYTOGETHER NATIVE v3.0         [Status: RUNNING] [FPS: 144] [PID: 4892]│
├─────────────┬─────────────────────────────────────────────────────────────────┤
│  SIDEBAR    │  MAIN CONTENT AREA                                              │
│             │                                                                 │
│ 󰊴 Dashboard │  ┌─── CẤU HÌNH CÂU CÁ SIÊU TỐC ───────────────────────────────┐ │
│ 󰈲 Câu Cá    │  │  [✔] Tự Động Thả & Giật Cần (Zero-Tap)                      │ │
│ 󰛔 Đập Đá    │  │  [✔] Tự Động Sửa Cần Khi Hỏng                               │ │
│ 󰒋 Bắt Bọ    │  │  Xử lý cá: (o) Bảo quản vào túi   ( ) Bán nhanh             │ │
│ 󰀝 Đào Báu   │  └────────────────────────────────────────────────────────────┘ │
│ 󰠘 Nông Trại │                                                                 │
│ 󱁤 Thu Thập  │  ┌─── BỘ LỌC CÁ THÔNG MINH ───────────────────────────────────┐ │
│ 󰑭 Dịch Chuyển│  │  Cỡ bóng: [ ] 1  [ ] 2  [ ] 3  [ ] 4  [x] 5  [x] 6  [x] 7   │ │
│ 󰍉 ESP Radar │  │  Phẩm cấp: [ ] Trắng [ ] Xanh [x] Tím [x] Vương Miện        │ │
│ 󰒓 Cài Đặt   │  └────────────────────────────────────────────────────────────┘ │
│             │                                                                 │
│             │  ┌─── THỐNG KÊ PHIÊN CHẠY ────────────────────────────────────┐ │
│             │  │  Đã câu: 248 con | Cá hiếm: 14 con | Tốc độ: 1.2s/chu kỳ   │ │
│             │  └────────────────────────────────────────────────────────────┘ │
├─────────────┴─────────────────────────────────────────────────────────────────┤
│  [Server: ONLINE (12ms)] [VPS License: VIP ACTIVE - 28 Days] [Memory: 48MB]   │
└───────────────────────────────────────────────────────────────────────────────┘
```
