# 12: HỆ THỐNG THIẾT KẾ GIAO DIỆN UI/UX CHUYÊN NGHIỆP TRONG UNITY (ANTI-AI VIBE DESIGN SYSTEM)

> **Mục tiêu:** Xây dựng hệ thống giao diện trực quan, sang trọng, mang phong cách gaming hiện đại, tối giản (Minimalist Cyberpunk / Industrial Dark Mode), loại bỏ triệt để cảm giác "AI-generated" (tím gradient rẻ tiền, layout nhồi nhét lộn xộn). Cấu trúc phân tách từng menu thành các file component C# độc lập trong Unity.

---

## I. TRIẾT LÝ THIẾT KẾ: "CYBER GRAPHITE & ARCTIC ICE"

- ✔ **Bảng màu công nghiệp tinh tế:**
  + **Background (Nền chính):** Charcoal Đậm `#0B0D11`
  + **Surface (Bề mặt Card):** Dark Slate `#14171F`
  + **Card Hover & Border:** Graphite Viền Mảnh 1px `#232834`
  + **Primary Accent (Màu điểm nhấn):** Cold Cyan `#00E5FF`
  + **Success / Active:** Emerald Green `#10B981` (Bot đang chạy, kết nối ổn định)
  + **Warning:** Amber Gold `#F59E0B` (Độ bền cần câu/cuốc thấp)
  + **Danger:** Crimson Red `#EF4444` (Mất kết nối)
- ✔ **Font chữ & Typography:**
  + Nhãn văn bản (Labels): `Inter` / `Segoe UI` (13px - 14px, Clean).
  + Tọa độ, Offset, Tốc độ, Timer: `JetBrains Mono` / `Consolas` (Monospace).
- ✔ **Khoảng cách & Căn chỉnh (Spacing):** Hệ số chuẩn 4px/8px (Padding 12px, Item Spacing 8px, Border Radius 4px - 6px góc cạnh sắc sảo).

---

## II. CẤU TRÚC PHÂN TÁCH MENU ĐỘC LẬP TRONG C# & UNITY

Mỗi Menu nằm trong một file C# riêng biệt:

```
src/Client/UI/
├── Framework/
│   ├── IMenuView.cs         # Base Interface cho mọi tab
│   ├── UIRenderer.cs        # Quản lý vòng lặp vẽ và cập nhật UI
│   └── DesignSystem.cs      # Bảng màu, Style & Token thiết kế
├── Components/              # Các widget UI dùng chung
│   ├── CyberCard.cs         # Khung card có viền phát sáng nhẹ
│   ├── StatusBadge.cs       # Huy hiệu trạng thái Running / Idle
│   ├── ToggleSwitch.cs      # Công tắc bật tắt gạt mượt
│   └── StatCounter.cs       # Bộ đếm số lượng quặng/cá đã bắt
└── Views/                   # Từng Menu là 1 file C# độc lập
    ├── DashboardView.cs     # Tab Tổng quan & Trạng thái hệ thống
    ├── FishingView.cs       # Tab Câu cá
    ├── MiningView.cs        # Tab Đập đá
    ├── InsectView.cs        # Tab Bắt bọ
    ├── ExcavationView.cs    # Tab Đào kho báu
    ├── FarmView.cs          # Tab Nông trại
    ├── CollectView.cs       # Tab Thu thập vật phẩm
    ├── TeleportView.cs      # Tab Dịch chuyển & Bản đồ
    ├── EspView.cs           # Tab ESP & Radar
    └── SettingsView.cs      # Tab Cài đặt & Bản quyền
```

---

## III. BASE INTERFACE `IMenuView`

```csharp
using System.Threading.Tasks;

namespace DTA.UI.Framework
{
    public interface IMenuView
    {
        string MenuId { get; }
        string MenuTitle { get; }
        int DisplayOrder { get; }

        void Initialize();
        void Render();
        void OnStateChanged();
        void Dispose();
    }
}
```
