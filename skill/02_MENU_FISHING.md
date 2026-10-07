# 02: ĐẶC TẢ MENU & MODULE CÂU CÁ (FISHING MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống câu cá tự động bằng C++20. Tối ưu hóa zero-latency (phản xạ giật cá < 5ms ngay khi xuất hiện dấu chấm than/bite flag), loại bỏ hoàn toàn việc click màn hình, tích hợp bộ lọc cá thông minh đa tầng và xử lý kết quả tự động siêu tốc.

---

## I. KIẾN TRÚC MODULE & INTERFACES

Mỗi chức năng nằm trọn trong namespace và thư mục riêng `features/fishing/`:

```
src/client/features/fishing/
├── IFishingService.hpp      # Interface dịch vụ câu cá
├── FishingService.cpp       # Logic nghiệp vụ & tương tác IL2CPP
├── FishingBot.hpp           # State Machine điều khiển chu trình câu
├── FishingBot.cpp           # Luồng state machine
├── FishingCatalog.hpp       # Tra cứu thông tin cá (Bóng 1-7, Nền 1-5, Biến thể)
├── FishingModels.hpp        # Cấu trúc dữ liệu Tùy chọn (Options) & Thống kê (Stats)
└── FishingView.cpp          # Giao diện điều khiển (ImGui Render Component)
```

### 1. Interface `IFishingService`

```cpp
#pragma once
#include <cstdint>
#include <string>
#include "FishingModels.hpp"

class IFishingService {
public:
    virtual ~IFishingService() = default;

    // Các hành động gọi hàm game trực tiếp (Unity Main Thread)
    virtual bool CastRod() = 0;                     // Thả cần
    virtual bool ReelIn() = 0;                      // Giật cần
    virtual bool KeepFish() = 0;                    // Bảo quản cá
    virtual bool SellFish() = 0;                    // Bán nhanh cá
    virtual bool OpenBox() = 0;                     // Mở lon / hộp quà
    virtual bool RepairRod() = 0;                   // Sửa cần câu bị hỏng

    // Đọc trạng thái từ bộ nhớ game
    virtual FishingPoleState GetPoleState() = 0;     // IDLE, CASTING, WAITING_BITE, BITING, REELING
    virtual FishCurrentInfo GetCurrentFishInfo() = 0;// Đọc ID, kích thước bóng, loại cá đang cắn
    virtual bool IsRodBroken() = 0;                  // Kiểm tra độ bền cần
    virtual bool IsResultDialogOpen() = 0;           // Bảng kết quả đã mở chưa
};
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
       │ WAITING_BITE │ (Quét bộ nhớ 120 FPS)      │
       └──────┬───────┘                            │
              ▼                                    │
  ┌───────────────────────┐                        │
  │     FISH_APPROACH     │ (Phát hiện cá lại gần) │
  │ Lọc ID / Bóng cá 1-7  │                        │
  └───────────┬───────────┘                        │
              ├──────────[Không đạt bộ lọc] ───────┤ (Rút cần / Thả lại)
              ▼ [Đạt bộ lọc]                       │
       ┌──────────────┐                            │
       │  BITE_HOOK   │ (Dấu ! xuất hiện -> Giật)  │
       └──────┬───────┘ (ReelIn < 5ms)             │
              ▼                                    │
       ┌──────────────┐                            │
       │ HANDLE_RESULT│ (Đọc bảng kết quả)         │
       │  Bán / Giữ   │ (SellFish / KeepFish)      │
       └──────┬───────┘                            │
              ▼                                    │
       ┌──────────────┐                            │
       │ CHECK_REPAIR │ (Tự sửa nếu hỏng)          │
       └──────┬───────┘                            │
              └────────────────────────────────────┘
```

---

## III. BẢNG MÃ HÀM IL2CPP CÂU CÁ (TRA CỨU CHÍNH XÁC TỪ DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Cách Gọi / Con Trỏ Cần Truyền |
| :--- | :--- | :--- | :--- |
| **Thả cần / Giật cần** | `FishingPoleController.OnClick_Button` | `0x4EBD72C` | `(thisPtr, 0)` với `thisPtr` là instance của `FishingPoleController` đang cầm |
| **Bảo quản cá** | `DialogFishingGetItem.OnClick_ButtonClose` | `0x5DE6BB4` | `(dialogPtr)` đóng dialog và lưu cá vào balo |
| **Bán nhanh cá** | `DialogFishingGetItem.OnClick_Selling` | `0x5DE9C7C` | `(dialogPtr)` bán ngay lập tức lấy tiền sao |
| **Mở lon / hộp quà** | `DialogFishingGetItem.OnClick_OpenPackagePopup` | `0x5DE9FF8` | `(dialogPtr)` mở hộp báu câu trúng |
| **Bỏ qua hiệu ứng** | `DialogResultGetItemView.OnClick_ButtonSkip` | `0x4D370D8` | `(dialogPtr)` skip animation nhận đồ |
| **Đóng màn nhận đồ** | `DialogResultGetItemView.OnClick_ButtonClose`| `0x4D372CC` | `(dialogPtr)` đóng bảng thu hoạch |
| **Sửa cần câu** | `DialogItemRepair.OnClick_Repair` | `0x5E6AC7C` | `(dialogPtr)` phục hồi 100% độ bền |
| **Đóng bảng sửa** | `DialogItemRepair.OnClick_Close` | `0x5E6AD68` | `(dialogPtr)` đóng popup sau khi sửa |

---

## IV. BỘ LỌC CÁ THÔNG MINH ĐA TẦNG (ADVANCED FISH FILTER)

Người dùng có thể tùy biến cấu hình chi tiết:
1. **Lọc kích cỡ bóng (Shadow 1 to 7):**
   - Chỉ giật bóng to (Bóng 5, 6, 7 cho cá hiếm/huyền thoại).
   - Tự động bỏ qua cá bóng nhỏ (1, 2, 3) để tiết kiệm độ bền cần.
2. **Lọc phẩm chất nền cá (Grade 1 to 5):**
   - Nền Trắng (1), Xanh lá (2), Xanh dương (3), Tím (4), Vàng Vương Miện (5).
3. **Lọc biến thể & đột biến:**
   - Giữ lại cá biến thể (Variant), cá đột biến (Mutant) dù đang ở chế độ Bán Nhanh.
4. **Lọc theo danh sách ID cá cụ thể:**
   - Cung cấp danh sách ID cá muốn bắt, bỏ qua tất cả cá khác.

---

## V. TỐI ƯU HÓA HIỆU NĂNG C++ SO VỚI PYTHON CŨ

- **Loại bỏ vòng lặp sleep ngắt quãng:** Bản cũ dùng `time.sleep(0.1)` gây trễ nhịp giật cá. Bản C++ dùng polling theo frame đồng bộ với `Unity Update` (chu kỳ 8.3ms ở 120 FPS hoặc 16.6ms ở 60 FPS).
- **Phát hiện cắn câu tức thì:** Đọc trực tiếp con trỏ `m_IsBite` hoặc `m_CurState` của cần câu từ bộ nhớ. Khi chuyển sang trạng thái cắn, hàm `OnClick_Button(0)` được gọi ngay trong frame đó, tỷ lệ giật thành công đạt 100%.
- **Tự động đóng popup an toàn:** Không bao giờ bị kẹt bảng thông báo cá cắn đứt dây hay hết độ bền cần.
