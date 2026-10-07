# 05: ĐẶC TẢ MENU & MODULE ĐÀO KHO BÁU (EXCAVATION MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống đào kho báu tự động bằng C++20. Tối ưu thuật toán tam giác hóa tín hiệu máy dò (Radar Signal Triangulation) để xác định vị trí rương ngầm trong tích tắc, dịch chuyển chuẩn xác vào tâm, kích hoạt cắm xẻng đào bằng hàm game và tự động mở rương kho báu.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/excavation/
├── IExcavationService.hpp   # Interface dịch vụ đào kho báu
├── ExcavationService.cpp    # Tương tác IL2CPP & quản lý xẻng
├── ExcavationBot.hpp        # State Machine điều khiển chu trình đào
├── ExcavationBot.cpp        # Luồng state machine
├── RadarSolver.hpp          # Thuật toán giải mã tọa độ rương từ tín hiệu dò
├── ExcavationModels.hpp     # Cấu trúc dữ liệu Tín hiệu, Rương & Thống kê
└── ExcavationView.cpp       # ImGui Render Component
```

### 1. Interface `IExcavationService`

```cpp
#pragma once
#include <cstdint>
#include <optional>
#include "ExcavationModels.hpp"

class IExcavationService {
public:
    virtual ~IExcavationService() = default;

    // Các hành động gọi hàm game trực tiếp
    virtual bool DigShovel() = 0;                   // Cắm xẻng đào
    virtual bool TeleportToPoint(const Vector3& pos) = 0;// Dịch chuyển lấy mẫu / tới rương
    virtual bool OpenTreasureChest() = 0;           // Mở rương kho báu
    virtual bool RepairShovel() = 0;                // Sửa xẻng đào
    virtual bool CloseRewardDialog() = 0;           // Đóng popup nhận thưởng

    // Dữ liệu máy dò
    virtual RadarSignalData GetRadarSignal() = 0;   // Đọc tần số bíp, cường độ sóng
    virtual std::optional<Vector3> CalculateChestPosition() = 0; // Tọa độ rương tính toán
    virtual bool IsShovelBroken() = 0;
};
```

---

## II. THUẬT TOÁN ĐỊNH VỊ TÂM KHO BÁU (TRIANGULATION SOLVER)

Thay vì đi bộ dò dẫm thủ công mất hàng phút:
1. **Lấy mẫu 3 điểm (3-Point Sampling):**
   - Đọc cường độ tín hiệu $S_1$ tại điểm hiện tại $P_1$.
   - Dịch chuyển sang $P_2 = P_1 + (10, 0, 0)$ đọc $S_2$.
   - Dịch chuyển sang $P_3 = P_1 + (0, 0, 10)$ đọc $S_3$.
2. **Giải hệ phương trình:**
   - Dựa trên mối quan hệ giữa cường độ sóng và khoảng cách $d \propto \frac{1}{\sqrt{S}}$, thuật toán thiết lập 3 đường tròn giao nhau và giải ra tọa độ tâm rương chính xác đến $0.05m$.
3. **Dịch chuyển & Đào:**
   - Dịch chuyển thẳng tới tâm rương.
   - Vòng lặp gọi `ShovelController.OnClick_Button(0)` cho đến khi rương lộ diện.
   - Gọi hàm mở rương và nhận thưởng.

---

## III. BẢNG MÃ HÀM IL2CPP ĐÀO KHO BÁU (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Cắm xẻng đào** | `ShovelController.OnClick_Button` | `0x5978AF4` | `(thisPtr, 0)` với `thisPtr` là instance của `ShovelController` |
| **Kích hoạt công cụ** | `AttachActionButton.OnClickActionButton` | `0x4CEB6A8` | `(thisPtr, 0)` |
| **Dịch chuyển tức thời**| `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(motorPtr, Vector3)` tiếp cận tâm rương |
| **Mở rương kho báu** | `DialogBoxMessage.OnClick_OK` / `DialogRewardPopup.OnClickYes` | `0x5A31C00` | Mở rương và thu hoạch phần thưởng |
| **Đóng bảng kết quả** | `DialogResultGetItemView.OnClick_ButtonClose`| `0x4D372CC` | `(dialogPtr)` đóng màn thu hoạch |
| **Sửa xẻng hỏng** | `DialogItemRepair.OnClick_Repair` | `0x5E6AC7C` | `(dialogPtr)` phục hồi độ bền |
| **Đóng dialog sửa** | `DialogItemRepair.OnClick_Close` | `0x5E6AD68` | `(dialogPtr)` đóng bảng sửa |
