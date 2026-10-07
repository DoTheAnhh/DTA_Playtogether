# 07: ĐẶC TẢ MENU & MODULE THU THẬP VẬT PHẨM (COLLECT MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống thu thập vật phẩm trên bản đồ (Cành cây, Vỏ sò, Rác biển, Hoa dại, Vật phẩm sự kiện) bằng C# & Unity. Tối ưu thuật toán tìm đường đi ngắn nhất (Shortest Path TSP), tự động nhặt thông qua hàm native `OnPickFieldObject` mà không cần click chạm màn hình.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Collect/
├── ICollectService.cs      # Interface dịch vụ thu thập
├── CollectService.cs       # Tương tác IL2CPP & quản lý nhặt đồ
├── CollectBot.cs           # State Machine điều khiển chu trình nhặt
├── PathOptimizer.cs        # Thuật toán TSP tối ưu thứ tự nhặt đồ
├── CollectModels.cs        # Dữ liệu Vật thể, Bộ lọc loại & Thống kê
└── CollectView.cs          # Unity UI View Component
```

### 1. Interface `ICollectService`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace DTA.Features.Collect
{
    public interface ICollectService
    {
        // Các hành động gọi hàm game trực tiếp
        Task<bool> PickObjectAsync(uint objectUid);       // Gọi OnPickFieldObject
        Task<bool> TeleportToObjectAsync(Vector3 pos);    // Dịch chuyển tới vật thể
        Task<bool> CloseResultDialogAsync();              // Đóng dialog nhận đồ

        // Quét thực thể từ bộ nhớ game
        IReadOnlyList<FieldObjectEntity> ScanFieldObjects(float radius);
        void SetCollectFilter(in CollectFilter filter);
    }
}
```

---

## II. THUẬT TOÁN TỐI ƯU HÓA ĐƯỜNG ĐI (SHORTEST PATH ROUTE)

1. **Quét và lọc:** Quét toàn bộ `FieldObject` có trạng thái active trên map, lọc theo cấu hình người dùng (ví dụ: chỉ nhặt Rác sự kiện và Vỏ sò hiếm).
2. **Quy hoạch tuyến đường (Greedy Nearest Neighbor / 2-Opt):**
   - Sắp xếp thứ tự các điểm nhặt $P_1 \to P_2 \to \dots \to P_n$ sao cho tổng khoảng cách di chuyển nhỏ nhất.
3. **Thực thi thần tốc:**
   - Dịch chuyển tới $P_i$ qua `KinematicCharacterMotor.set_TransientPosition`.
   - Gọi `OnPickFieldObject(localPlayerActor, uid)` trên Main Thread.
   - Xác nhận vật thể biến mất khỏi bộ nhớ -> Nhảy ngay lập tức sang $P_{i+1}$.

---

## III. BẢNG MÃ HÀM IL2CPP THU THẬP (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Nhặt Vật Thể** | `PlayerActor.OnPickFieldObject` | `0x5821010` | `(thisPtr, uint objectUid)` |
| **Danh Sách Vật Thể**| `FieldObjectManager.m_ActiveObjects` | Offset `0x48` | Mảng các FieldObject đang tồn tại trên bản đồ |
