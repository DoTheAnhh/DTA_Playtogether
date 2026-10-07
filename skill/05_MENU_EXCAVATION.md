# 05: ĐẶC TẢ MENU & MODULE ĐÀO KHO BÁU (EXCAVATION MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống đào kho báu tự động bằng C# & Unity. Tối ưu thuật toán tam giác hóa tín hiệu máy dò (Radar Signal Triangulation) để xác định vị trí rương ngầm trong tích tắc, dịch chuyển chuẩn xác vào tâm, kích hoạt cắm xẻng đào bằng hàm game và tự động mở rương kho báu.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Excavation/
├── IExcavationService.cs   # Interface dịch vụ đào kho báu
├── ExcavationService.cs    # Tương tác IL2CPP & quản lý xẻng
├── ExcavationBot.cs        # State Machine điều khiển chu trình đào
├── RadarSolver.cs          # Thuật toán giải mã tọa độ rương từ tín hiệu dò
├── ExcavationModels.cs     # Cấu trúc dữ liệu Tín hiệu, Rương & Thống kê
└── ExcavationView.cs       # Unity UI View Component
```

### 1. Interface `IExcavationService`

```csharp
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace DTA.Features.Excavation
{
    public interface IExcavationService
    {
        // Các hành động gọi hàm game trực tiếp
        Task<bool> DigShovelAsync();                     // Cắm xẻng đào
        Task<bool> TeleportToPointAsync(Vector3 pos);    // Dịch chuyển lấy mẫu / tới rương
        Task<bool> OpenTreasureChestAsync();             // Mở rương kho báu
        Task<bool> RepairShovelAsync();                  // Sửa xẻng đào
        Task<bool> CloseRewardDialogAsync();             // Đóng popup nhận thưởng

        // Dữ liệu máy dò
        RadarSignalData GetRadarSignal();                // Đọc tần số bíp, cường độ sóng
        Vector3? CalculateChestPosition();               // Tọa độ rương tính toán
        bool IsShovelBroken();
    }
}
```

---

## II. THUẬT TOÁN ĐỊNH VỊ TÂM KHO BÁU (TRIANGULATION SOLVER)

Thay vì đi bộ dò dẫm thủ công:
1. **Lấy mẫu 3 điểm (3-Point Sampling):**
   - Đọc cường độ tín hiệu $S_1$ tại điểm hiện tại $P_1$.
   - Dịch chuyển sang $P_2 = P_1 + (10, 0, 0)$ đọc $S_2$.
   - Dịch chuyển sang $P_3 = P_1 + (0, 0, 10)$ đọc $S_3$.
2. **Giải hệ phương trình:**
   - Dựa trên mối quan hệ giữa cường độ sóng và khoảng cách $d \propto \frac{1}{\sqrt{S}}$, thuật toán thiết lập 3 đường tròn giao nhau và giải ra tọa độ tâm rương chính xác đến $0.05\text{m}$.
3. **Dịch chuyển & Đào:**
   - Dịch chuyển thẳng tới tâm rương qua `set_TransientPosition`.
   - Vòng lặp gọi `ShovelController.OnClick_Button(0)` cho đến khi rương lộ diện.
   - Gọi hàm mở rương và nhận thưởng trên Main Thread.

---

## III. BẢNG MÃ HÀM IL2CPP ĐÀO KHO BÁU (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Cắm Xẻng** | `ShovelController.OnClick_Button` | `0x57D5600` | `(thisPtr, int actionType=0)` trên Main Thread |
| **Tín Hiệu Dò** | Field `m_RadarSignalStrength` | Offset `0xAC` | Float: Cường độ tín hiệu sóng máy dò |
| **Mở Rương** | `TreasureChest.OnClick_Open` | `0x58F2100` | Mở rương sau khi đào xong |
| **Độ Bền Xẻng** | Field `m_CurrentDurability` | Offset `0xA0` | Độ bền xẻng |
