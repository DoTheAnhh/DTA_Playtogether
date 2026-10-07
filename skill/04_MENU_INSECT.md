# 04: ĐẶC TẢ MENU & MODULE BẮT CÔN TRÙNG (INSECT MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống bắt côn trùng tự động bằng C++20. Tối ưu thuật toán quét bọ, dự đoán vector vận tốc và hướng bay (Velocity Trajectory Prediction), tiếp cận góc đón đầu chính xác tuyệt đối, vung vợt bắt bọ thông qua gọi hàm game và tự đóng bảng kết quả ngay lập tức.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/insect/
├── IInsectService.hpp      # Interface dịch vụ bắt bọ
├── InsectService.cpp       # Tương tác IL2CPP & quản lý net controller
├── InsectBot.hpp           # State Machine điều khiển chu trình bắt bọ
├── InsectBot.cpp           # Luồng state machine
├── InsectPredictor.hpp     # Dự đoán tọa độ đón đầu côn trùng đang bay
├── InsectModels.hpp        # Cấu trúc Côn trùng, Bộ lọc phẩm cấp & Thống kê
└── InsectView.cpp          # ImGui Render Component
```

### 1. Interface `IInsectService`

```cpp
#pragma once
#include <cstdint>
#include <vector>
#include "InsectModels.hpp"

class IInsectService {
public:
    virtual ~IInsectService() = default;

    // Các hành động gọi hàm game trực tiếp
    virtual bool SwingNet() = 0;                    // Vung vợt bắt bọ
    virtual bool ApproachInsect(const Vector3& targetPos, float yawAngle) = 0; // Đón đầu
    virtual bool FreezeInsect(uint32_t insectUid) = 0;// Đóng băng tốc độ bay (nếu hỗ trợ)
    virtual bool RepairNet() = 0;                   // Sửa vợt khi hỏng
    virtual bool CloseResultDialog() = 0;           // Đóng bảng nhận bọ

    // Dò tìm và lọc côn trùng từ bộ nhớ
    virtual std::vector<InsectEntity> ScanInsectsAround(float radius) = 0;
    virtual InsectEntity* GetBestTargetInsect() = 0;
    virtual bool IsNetBroken() = 0;
};
```

---

## II. THUẬT TOÁN ĐÓN ĐẦU & VUNG VỢT CHÍNH XÁC (INTERCEPT ALGORITHM)

Khác với quặng đá đứng yên, côn trùng (bướm, chuồn chuồn, bọ cánh cứng bay) liên tục đổi hướng:
1. **Velocity Tracking:** Đọc liên tục vị trí $P(t)$ và $P(t - \Delta t)$ để tính vector vận tốc $\vec{v}$.
2. **Intercept Point Calculation:** Tính điểm đón đầu $P_{target} = P_{current} + \vec{v} \cdot t_{swing}$, trong đó $t_{swing}$ là thời gian từ lúc kích hoạt vung vợt tới lúc lưới vợt quét trúng mục tiêu (khoảng 120ms).
3. **Approach & Orientation:** Dịch chuyển người chơi tới vị trí cách $P_{target}$ một khoảng $1.8m$ theo hướng ngược với vector bay của bọ, đồng thời set góc quay nhân vật hướng thẳng vào bọ.
4. **Trigger Action:** Gọi ngay `InsectNetController.OnClick_Button(0)` trên Main Thread. Tỷ lệ trúng đạt tuyệt đối.

---

## III. BẢNG MÃ HÀM IL2CPP BẮT CÔN TRÙNG (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Vung vợt bắt bọ** | `InsectNetController.OnClick_Button` | `0x4F77F60` | `(thisPtr, 0)` với `thisPtr` là instance của `InsectNetController` |
| **Kích hoạt công cụ** | `AttachActionButton.OnClickActionButton` | `0x4CEB6A8` | `(thisPtr, 0)` |
| **Dịch chuyển đón đầu** | `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(motorPtr, Vector3)` tiếp cận cự ly bắt |
| **Bỏ qua hiệu ứng** | `DialogResultGetItemView.OnClick_ButtonSkip` | `0x4D370D8` | `(dialogPtr)` lật nhanh thẻ côn trùng |
| **Đóng bảng nhận bọ** | `DialogResultGetItemView.OnClick_ButtonClose`| `0x4D372CC` | `(dialogPtr)` đóng dialog thu hoạch |
| **Sửa vợt hỏng** | `DialogItemRepair.OnClick_Repair` | `0x5E6AC7C` | `(dialogPtr)` phục hồi độ bền vợt |
| **Đóng dialog sửa** | `DialogItemRepair.OnClick_Close` | `0x5E6AD68` | `(dialogPtr)` đóng bảng sửa đồ |

---

## IV. BỘ LỌC CÔN TRÙNG THEO PHẨM CẤP

- **Lọc theo loại:** Bướm hiếm, Bọ vương miện, Bọ cánh cứng khổng lồ, Đom đóm, Ve sầu.
- **Lọc theo kích cỡ & vương miện:** Tự động bỏ qua các con bọ kích thước nhỏ, chỉ nhắm vào côn trùng vương miện (Crown/Giant).
- **Tránh phát hiện (Anti-flee):** Một số loài bọ sẽ bỏ chạy nếu người chơi chạy nhanh lại gần. Tool thực hiện dịch chuyển trực tiếp ở trạng thái ẩn hoặc vào đúng frame vung vợt, triệt tiêu cơ hội bay mất của bọ.
