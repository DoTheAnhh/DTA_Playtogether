# 06: ĐẶC TẢ MENU & MODULE NÔNG TRẠI (FARM MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống quản lý nông trại tự động bằng C# & Unity. Tối ưu quét trạng thái các chậu cây và luống đất trong sân nhà (Home Garden), tự động gieo hạt, tưới nước, bón phân và thu hoạch nông sản thông qua gọi hàm game mà không cần click chạm màn hình.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Farm/
├── IFarmService.cs         # Interface dịch vụ nông trại
├── FarmService.cs          # Tương tác IL2CPP & quản lý luống cây
├── FarmBot.cs              # State Machine điều khiển chu trình nông trại
├── FarmPlotScanner.cs      # Quét danh sách chậu cây và tình trạng phát triển
├── FarmModels.cs           # Dữ liệu Chậu cây, Cây trồng & Hạt giống
└── FarmView.cs             # Unity UI View Component
```

### 1. Interface `IFarmService`

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DTA.Features.Farm
{
    public interface IFarmService
    {
        // Các hành động gọi hàm game trực tiếp
        Task<bool> WaterPlotAsync(uint plotUid);       // Tưới nước cho cây
        Task<bool> HarvestPlotAsync(uint plotUid);     // Thu hoạch nông sản
        Task<bool> PlantSeedAsync(uint plotUid, uint seedId); // Gieo hạt
        Task<bool> RemoveDeadPlantAsync(uint plotUid); // Dọn cây hỏng / cỏ
        Task<bool> CloseFarmDialogAsync();             // Đóng popup nông trại

        // Quét dữ liệu vườn nhà
        IReadOnlyList<FarmPlotEntity> ScanAllPlots();
        FarmStats GetGardenStatus();
    }
}
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
| **Tưới Nước** | `GardenManager.WaterPlot` | `0x59B1200` | `(thisPtr, uint plotUid)` |
| **Thu Hoạch** | `GardenManager.HarvestPlot` | `0x59B1450` | `(thisPtr, uint plotUid)` |
| **Gieo Hạt** | `GardenManager.PlantSeed` | `0x59B1600` | `(thisPtr, uint plotUid, uint seedId)` |
| **Dọn Cây Hỏng**| `GardenManager.RemovePlot` | `0x59B1800` | `(thisPtr, uint plotUid)` |
