# 07: ĐẶC TẢ MENU & MODULE THU THẬP VẬT PHẨM (COLLECT MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống thu thập vật phẩm trên bản đồ (Cành cây, Vỏ sò, Rác biển, Hoa dại, Vật phẩm sự kiện) bằng C++20. Tối ưu thuật toán tìm đường đi ngắn nhất (Shortest Path TSP), tự động nhặt thông qua hàm native `OnPickFieldObject` mà không cần click chạm màn hình.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/collect/
├── ICollectService.hpp     # Interface dịch vụ thu thập
├── CollectService.cpp      # Tương tác IL2CPP & quản lý nhặt đồ
├── CollectBot.hpp          # State Machine điều khiển chu trình nhặt
├── CollectBot.cpp          # Luồng state machine
├── PathOptimizer.hpp       # Thuật toán TSP tối ưu thứ tự nhặt đồ
├── CollectModels.hpp       # Dữ liệu Vật thể, Bộ lọc loại & Thống kê
└── CollectView.cpp         # ImGui Render Component
```

### 1. Interface `ICollectService`

```cpp
#pragma once
#include <vector>
#include <cstdint>
#include "CollectModels.hpp"

class ICollectService {
public:
    virtual ~ICollectService() = default;

    // Các hành động gọi hàm game trực tiếp
    virtual bool PickObject(uint32_t objectUid) = 0;    // Gọi OnPickFieldObject
    virtual bool TeleportToObject(const Vector3& pos) = 0;// Dịch chuyển tới vật thể
    virtual bool CloseResultDialog() = 0;               // Đóng dialog nhận đồ

    // Quét thực thể từ bộ nhớ game
    virtual std::vector<FieldObjectEntity> ScanFieldObjects(float radius) = 0;
    virtual void SetCollectFilter(const CollectFilter& filter) = 0;
};
```

---

## II. THUẬT TOÁN TỐI ƯU HÓA ĐƯỜNG ĐI (SHORTEST PATH ROUTE)

Bản đồ có hàng chục vật phẩm rải rác:
1. **Quét và lọc:** Quét toàn bộ `FieldObject` có trạng thái active trên map, lọc theo cấu hình người dùng (ví dụ: chỉ nhặt Rác sự kiện và Vỏ sò hiếm).
2. **Quy hoạch tuyến đường (Greedy Nearest Neighbor / 2-Opt):**
   - Sắp xếp thứ tự các điểm nhặt $P_1 \to P_2 \to \dots \to P_n$ sao cho tổng khoảng cách di chuyển nhỏ nhất.
3. **Thực thi thần tốc:**
   - Dịch chuyển tới $P_i$ qua `set_TransientPosition`.
   - Gọi `OnPickFieldObject(localPlayerActor, uid)`.
   - Xác nhận vật thể biến mất khỏi bộ nhớ -> Nhảy ngay lập tức sang $P_{i+1}$.
   - Tốc độ hoàn thành: Nhặt sạch bản đồ chỉ trong vòng dưới 30 giây.

---

## III. BẢNG MÃ HÀM IL2CPP THU THẬP (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Nhặt vật thể trên sân**| `OnPickFieldObject` | `0x57CE884` | `(actorPtr, uint32 uid)` nhặt vật thể tức thời |
| **Dịch chuyển vị trí** | `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(motorPtr, Vector3)` tiếp cận vật phẩm |
| **Đóng dialog thu hoạch**| `DialogResultGetItemView.OnClick_ButtonClose` | `0x4D372CC` | `(dialogPtr)` đóng màn nhận đồ |
| **Bỏ qua hiệu ứng** | `DialogResultGetItemView.OnClick_ButtonSkip` | `0x4D370D8` | `(dialogPtr)` skip animation |
