# 06: ĐẶC TẢ MENU & MODULE NÔNG TRẠI (FARM MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống quản lý nông trại tự động bằng C++20. Tối ưu quét trạng thái các chậu cây và luống đất trong sân nhà (Home Garden), tự động gieo hạt, tưới nước, bón phân và thu hoạch nông sản thông qua gọi hàm game mà không cần click chạm màn hình.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/farm/
├── IFarmService.hpp        # Interface dịch vụ nông trại
├── FarmService.cpp         # Tương tác IL2CPP & quản lý luống cây
├── FarmBot.hpp             # State Machine điều khiển chu trình nông trại
├── FarmBot.cpp             # Luồng state machine
├── FarmPlotScanner.hpp     # Quét danh sách chậu cây và tình trạng phát triển
├── FarmModels.hpp          # Dữ liệu Chậu cây, Cây trồng & Hạt giống
└── FarmView.cpp            # ImGui Render Component
```

### 1. Interface `IFarmService`

```cpp
#pragma once
#include <vector>
#include <cstdint>
#include "FarmModels.hpp"

class IFarmService {
public:
    virtual ~IFarmService() = default;

    // Các hành động gọi hàm game trực tiếp
    virtual bool WaterPlot(uint32_t plotUid) = 0;       // Tưới nước cho cây
    virtual bool HarvestPlot(uint32_t plotUid) = 0;     // Thu hoạch nông sản
    virtual bool PlantSeed(uint32_t plotUid, uint32_t seedId) = 0; // Gieo hạt
    virtual bool RemoveDeadPlant(uint32_t plotUid) = 0; // Dọn cây hỏng / cỏ
    virtual bool CloseFarmDialog() = 0;                 // Đóng popup nông trại

    // Quét dữ liệu vườn nhà
    virtual std::vector<FarmPlotEntity> ScanAllPlots() = 0;
    virtual FarmStats GetGardenStatus() = 0;
};
```

---

## II. CHU TRÌNH TỰ ĐỘNG HÓA NÔNG TRẠI (FARM PIPELINE)

```
       ┌──────────────┐
       │  SCAN_PLOTS  │ (Quét toàn bộ luống cây trong sân vườn)
       └──────┬───────┘
              ▼
  ┌───────────────────────┐
  │   PHÂN LOẠI TRẠNG THÁI│
  ├───────────────────────┤
  │ 1. Chín -> Harvest    │
  │ 2. Khát nước -> Water │
  │ 3. Đất trống -> Plant │
  └───────────┬───────────┘
              ▼
  ┌───────────────────────┐
  │  THỰC THI HÀNG LOẠT   │
  │ Gọi Native Action     │ (Xử lý tuần tự trên Main Thread)
  │ Mỗi chậu cách nhau 5ms│
  └───────────┬───────────┘
              ▼
       ┌──────────────┐
       │ HOÀN TẤT VÒNG│
       └──────────────┘
```

---

## III. BẢNG MÃ HÀM IL2CPP NÔNG TRẠI (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Tương tác chậu cây** | `FarmPlotObject.OnInteract` | `0x51E2A40` | `(plotPtr, actionType)` thực hiện tưới/gặt |
| **Gieo hạt giống** | `FarmPlotObject.PlantSeed` | `0x51E2DC8` | `(plotPtr, seedItemId)` chọn hạt từ túi |
| **Thu hoạch nông sản**| `FarmPlotObject.Harvest` | `0x51E3100` | `(plotPtr)` nhận hoa quả vào túi |
| **Đóng dialog thu hoạch**| `DialogResultGetItemView.OnClick_ButtonClose` | `0x4D372CC` | `(dialogPtr)` đóng popup nhận nông sản |
| **Xác nhận OK** | `DialogBoxMessage.OnClick_OK` | `0x5A29F80` | `(dialogPtr)` xác nhận thông báo |
