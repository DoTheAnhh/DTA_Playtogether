# 02: ĐẶC TẢ MENU & MODULE CÂU CÁ (FISHING MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống câu cá tự động bằng C# & Unity. Tối ưu hóa zero-latency (phản xạ giật cá < 1ms ngay khi xuất hiện cờ cắn `m_bBite` / dấu chấm than), loại bỏ hoàn toàn việc click màn hình hay đọc HUD dư thừa, tích hợp bộ lọc cá thông minh đa tầng và tự động nhận thưởng/bán cá siêu tốc.

---

## I. KIẾN TRÚC MODULE & INTERFACES

Mỗi chức năng nằm trọn trong namespace và thư mục riêng `Features/Fishing/`:

```
src/Client/Features/Fishing/
├── IFishingService.cs      # Interface dịch vụ câu cá
├── FishingService.cs       # Logic nghiệp vụ & tương tác IL2CPP
├── FishingBot.cs           # State Machine điều khiển chu trình câu
├── FishingCatalog.cs       # Tra cứu thông tin cá (Bóng 1-7, Nền 1-5, Biến thể)
├── FishingModels.cs        # Cấu trúc dữ liệu Tùy chọn (Options) & Thống kê (Stats)
└── FishingView.cs          # Giao diện điều khiển (Unity UI View Component)
```

### 1. Interface `IFishingService`

```csharp
using System;
using System.Threading.Tasks;

namespace DTA.Features.Fishing
{
    public interface IFishingService
    {
        // Các hành động gọi hàm game trực tiếp (Unity Main Thread)
        Task<bool> CastRodAsync();                     // Thả cần (OnClick_Button(0))
        Task<bool> ReelInAsync();                      // Giật cần (OnClick_Button(0))
        Task<bool> KeepFishAsync();                    // Bảo quản cá
        Task<bool> SellFishAsync();                    // Bán nhanh cá
        Task<bool> OpenBoxAsync();                     // Mở lon / hộp quà
        Task<bool> RepairRodAsync();                   // Sửa cần câu bị hỏng

        // Đọc trạng thái từ bộ nhớ game siêu tốc
        FishingPoleState GetPoleState();               // Idle, Casting, WaitingBite, Biting, Reeling
        FishCurrentInfo GetCurrentFishInfo();          // Đọc ID, kích thước bóng, loại cá đang cắn
        bool IsRodBroken();                            // Kiểm tra độ bền cần
        bool IsResultDialogOpen();                     // Bảng kết quả đã mở chưa
    }
}
```

---

## II. QUY TRÌNH STATE MACHINE TỐI ƯU CỰC HẠN (ZERO-LATENCY FSM)

```
       ┌──────────────┐
       │   INIT_ROD   │◄───────────────────────────┐
       └──────┬───────┘                            │
              ▼                                    │
       ┌──────────────┐                            │
       │   CASTING    │ (Gọi OnClick_Button(0))     │
       └──────┬───────┘                            │
              ▼                                    │
       ┌──────────────┐                            │
       │ WAITING_BITE │ (Quét bộ nhớ 240 FPS)      │
       └──────┬───────┘                            │
              ▼                                    │
  ┌───────────────────────┐                        │
  │     FISH_APPROACH     │ (Phát hiện cá lại gần) │
  │ Lọc ID / Bóng cá 1-7  │                        │
  └───────────┬───────────┘                        │
              ├──────────[Không đạt bộ lọc] ───────┤ (Rút cần / Thả lại)
              ▼ [Đạt bộ lọc]                       │
       ┌──────────────┐                            │
       │  BITE_HOOK   │ (Cờ Bite = 1 -> Giật)      │
       └──────┬───────┘ (ReelIn < 1ms)             │
              ▼                                    │
       ┌──────────────┐                            │
       │ HANDLE_RESULT│ (Đọc bảng kết quả)         │
       │  Bán / Giữ   │ (SellFish / KeepFish)      │
       └──────┬───────┘                            │
              ▼                                    │
       ┌──────────────┐                            │
       │ REPAIR_CHECK │ (Kiểm tra độ bền)          │
       └──────────────┴────────────────────────────┘
```

---

## III. BẢNG MÃ HÀM IL2CPP CÂU CÁ (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Quăng Cần / Giật Cần** | `FishingPoleController.OnClick_Button` | `0x57D233C` | `(thisPtr, int actionType=0)` trên Main Thread |
| **Trạng Thái Cần** | Field `m_eState` trong `FishingPoleController` | Offset `0x98` | Enum: `0=None, 1=Cast, 2=Wait, 3=Bite, 4=Pull` |
| **Cờ Cá Cắn (!)** | Field `m_bBite` / `isBite` | Offset `0xA4` | `bool (1 byte)`: Khi chuyển `true`, giật ngay |
| **Thông Tin Cá** | Pointer `m_CurrentFishData` | Offset `0xB0` | Trỏ tới struct chứa Fish ID, Shadow Size, Rare Level |
| **Bảo Quản Cá** | `FishingResultDialog.OnClick_Keep` | `0x57E1200` | Click nút giữ cá |
| **Bán Cá Nhanh** | `FishingResultDialog.OnClick_Sell` | `0x57E1340` | Click nút bán cá ngay |
| **Sửa Cần** | `ItemRepairDialog.OnClick_Repair` | `0x56F0890` | Sửa chữa khi độ bền về 0 |
| **Nút OK Nhận Thưởng**| `UIButton.OnClick` | `0x524D90C` | Click trực tiếp UIButton của dialog kết quả |
