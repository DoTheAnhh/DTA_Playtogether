# 03: ĐẶC TẢ MENU & MODULE ĐẬP ĐÁ / KHAI KHOÁNG (MINING MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống đập đá khai khoáng tự động bằng C# & Unity. Tối ưu thuật toán quét quặng theo bán kính, tự động chọn mục tiêu giá trị cao (Kim cương, Vàng, Đá thiên thạch), di chuyển tức thời không delay, vung cuốc đập vỡ đá thông qua gọi hàm game và tự động nhặt toàn bộ quặng rơi trên mặt đất.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Mining/
├── IMiningService.cs       # Interface dịch vụ đập đá
├── MiningService.cs        # Tương tác IL2CPP & quản lý controller
├── MiningBot.cs            # State Machine điều khiển chu trình đập đá
├── OreScanner.cs           # Quét và phân loại quặng từ bộ nhớ
├── MiningModels.cs         # Cấu trúc dữ liệu Quặng, Tùy chọn & Thống kê
└── MiningView.cs           # Unity UI View Component
```

### 1. Interface `IMiningService`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace DTA.Features.Mining
{
    public interface IMiningService
    {
        // Các hành động gọi hàm game trực tiếp
        Task<bool> SwingPickaxAsync();                   // Vung cuốc đập đá
        Task<bool> PickOreItemAsync(uint objectUid);     // Nhặt quặng rơi
        Task<bool> RepairPickaxAsync();                  // Tự sửa cuốc khi hỏng
        Task<bool> TeleportToOreAsync(Vector3 pos);      // Tiếp cận quặng tức thì

        // Quét và đọc dữ liệu quặng từ game
        IReadOnlyList<OreEntity> ScanOresAround(float radius);
        OreEntity GetTargetOre();
        bool IsPickaxBroken();
        bool IsMiningAnimationRunning();
    }
}
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
       │  APPROACH    │ (Dịch chuyển tới tọa độ mỏ đá qua TransientPosition)
       └──────┬───────┘
              ▼
       ┌──────────────┐
       │ SWING_PICKAX │ (Gọi PickaxController.OnClick_Button(0))
       └──────┬───────┘
              ├──────────[Đá chưa vỡ]──────────────┐ (Lặp lại vung cuốc)
              ▼ [Đá vỡ]                            │
       ┌──────────────┐                            │
       │  COLLECT_ORE │ (Gọi OnPickFieldObject)    │
       └──────┬───────┘                            │
              ▼                                    │
       ┌──────────────┐                            │
       │ CHECK_REPAIR │ (Kiểm tra độ bền cuốc)     │
       └──────────────┴────────────────────────────┘
```

---

## III. BẢNG MÃ HÀM IL2CPP ĐẬP ĐÁ (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Vung Cuốc** | `PickaxController.OnClick_Button` | `0x57D4500` | `(thisPtr, int actionType=0)` trên Main Thread |
| **Nhặt Quặng** | `PlayerActor.OnPickFieldObject` | `0x5821010` | `(thisPtr, uint objectUid)` nhặt khoáng sản rơi |
| **Trạng Thái Cuốc** | Field `m_eState` trong `PickaxController` | Offset `0x98` | Kiểm tra hoàn tất animation vung cuốc |
| **Độ Bền Cuốc** | Field `m_CurrentDurability` | Offset `0xA0` | Khi bằng 0 kích hoạt sửa chữa |
| **Tọa Độ Mỏ Đá** | `FieldOreObject.get_transform().get_position()` | `0x52E3100` | Lấy tọa độ thế giới (Vector3) mỏ đá |
