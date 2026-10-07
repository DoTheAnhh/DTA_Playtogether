# 03: ĐẶC TẢ MENU & MODULE ĐẬP ĐÁ / KHAI KHOÁNG (MINING MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống đập đá khai khoáng tự động bằng C++20. Tối ưu thuật toán quét quặng theo bán kính, tự động chọn mục tiêu giá trị cao (Kim cương, Vàng, Đá thiên thạch), di chuyển tức thời không delay, vung cuốc đập vỡ đá thông qua gọi hàm game và tự động nhặt toàn bộ quặng rơi trên mặt đất.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/mining/
├── IMiningService.hpp      # Interface dịch vụ đập đá
├── MiningService.cpp       # Tương tác IL2CPP & quản lý controller
├── MiningBot.hpp           # State Machine điều khiển chu trình đập đá
├── MiningBot.cpp           # Luồng state machine
├── OreScanner.hpp          # Quét và phân loại quặng từ bộ nhớ
├── MiningModels.hpp        # Cấu trúc dữ liệu Quặng, Tùy chọn & Thống kê
└── MiningView.cpp          # ImGui Render Component
```

### 1. Interface `IMiningService`

```cpp
#pragma once
#include <cstdint>
#include <vector>
#include "MiningModels.hpp"

class IMiningService {
public:
    virtual ~IMiningService() = default;

    // Các hành động gọi hàm game trực tiếp
    virtual bool SwingPickax() = 0;                 // Vung cuốc đập đá
    virtual bool PickOreItem(uint32_t objectUid) = 0;// Nhặt quặng rơi
    virtual bool RepairPickax() = 0;                // Tự sửa cuốc khi hỏng
    virtual bool TeleportToOre(const Vector3& pos) = 0; // Tiếp cận quặng

    // Quét và đọc dữ liệu quặng từ game
    virtual std::vector<OreEntity> ScanOresAround(float radius) = 0;
    virtual OreEntity* GetTargetOre() = 0;
    virtual bool IsPickaxBroken() = 0;
    virtual bool IsMiningAnimationRunning() = 0;
};
```

---

## II. QUY TRÌNH STATE MACHINE KHAI KHOÁNG (MINING FSM)

```
       ┌──────────────┐
       │   SCAN_ORES  │ (Quét toàn bộ quặng trong bán kính)
       └──────┬───────┘
              ▼
       ┌──────────────┐
       │ SELECT_TARGET│ (Ưu tiên: Kim cương > Vàng > Thiên thạch > Thường)
       └──────┬───────┘
              ▼
       ┌──────────────┐
       │  APPROACH    │ (Dịch chuyển tới tọa độ mỏ đá)
       │ set_Transient│
       └──────┬───────┘
              ▼
  ┌───────────────────────┐
  │      SWING_LOOP       │◄────────────────────────┐
  │ OnClick_Button(0)     │                         │ (Đập tiếp nếu chưa vỡ)
  │ Đọc máu quặng (HP)   │                         │
  └───────────┬───────────┘                         │
              ├──────────[Máu quặng > 0] ───────────┘
              ▼ [Máu quặng = 0 (Đã vỡ)]
       ┌──────────────┐
       │  COLLECT_ALL │ (Gọi OnPickFieldObject nhặt hết quặng rơi)
       └──────┬───────┘
              ▼
       ┌──────────────┐
       │ CHECK_REPAIR │ (Hết độ bền -> Tự động sửa cuốc)
       └──────┬───────┘
              └─────────► Quay lại SCAN_ORES
```

---

## III. BẢNG MÃ HÀM IL2CPP ĐẬP ĐÁ (TRA CỨU TỪ DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Vung cuốc đập đá** | `PickaxController.OnClick_Button` | `0x597829C` | `(thisPtr, 0)` với `thisPtr` là instance của `PickaxController` |
| **Kích hoạt công cụ** | `AttachActionButton.OnClickActionButton` | `0x4CEB6A8` | `(thisPtr, 0)` kích hoạt động tác dùng cuốc |
| **Nhặt quặng rơi** | `OnPickFieldObject` | `0x57CE884` | `(actorPtr, uint32 uid)` nhặt quặng rơi ngay lập tức |
| **Dịch chuyển vị trí** | `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(motorPtr, Vector3)` tiếp cận vị trí đứng đập đá |
| **Sửa cuốc hỏng** | `DialogItemRepair.OnClick_Repair` | `0x5E6AC7C` | `(dialogPtr)` phục hồi độ bền |
| **Đóng dialog sửa** | `DialogItemRepair.OnClick_Close` | `0x5E6AD68` | `(dialogPtr)` đóng bảng sửa cuốc |

---

## IV. CÁC NÂNG CẤP & TỐI ƯU CỰC HẠN

1. **Smart Target Selection (Ưu tiên quặng hiếm):**
   - Phân tích `OreType` và `ItemGrade` trực tiếp từ bộ nhớ game.
   - Sắp xếp thứ tự ưu tiên: Quặng Huyền Thoại / Thiên Thạch > Kim Cương > Quặng Vàng > Đá Quý > Đá Thường.
   - Bỏ qua các khối đá thường nếu người dùng chỉ cấu hình farm đá hiếm.

2. **Animation Timing Tuyệt Đối:**
   - Phiên bản Python cũ delay cứng `0.5s - 0.8s` giữa các lần vung cuốc gây lãng phí thời gian.
   - Bản C++ lắng nghe cờ `IsActionFinished` hoặc đọc animation state từ Unity Animator. Vung cuốc tiếp theo ngay tại frame kết thúc cú đập, gia tăng tốc độ đập đá lên 30% - 50%.

3. **Zero-Drop Ore Collection:**
   - Ngay khi khối đá vỡ (`HP == 0`), quét ngay lập tức danh sách thực thể `FieldItem` rơi ra xung quanh trong bán kính 5m.
   - Gọi `OnPickFieldObject` tuần tự trên Main Thread gom sạch vật phẩm, không bỏ sót bất kỳ viên kim cương hay thỏi vàng nào.
