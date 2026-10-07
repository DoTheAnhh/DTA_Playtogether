# 08: ĐẶC TẢ MENU & MODULE DỊCH CHUYỂN & ĐỔI MAP (TELEPORT & ZONES MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống dịch chuyển tọa độ và chuyển đổi bản đồ bằng C# & Unity. Phản hồi dưới 0.1ms, triệt tiêu hiện tượng giật lùi vị trí (Anti-Rubberbanding), chuyển đổi bản đồ trực tiếp bằng hàm game nội bộ mà TUYỆT ĐỐI KHÔNG DÙNG NPC, CỔNG PORTAL HAY ĐIỆN THOẠI ẢO.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/Client/Features/Teleport/
├── ITeleportService.cs     # Interface dịch vụ dịch chuyển
├── TeleportService.cs      # Tương tác IL2CPP set position & zone move
├── WaypointManager.cs      # Quản lý danh sách điểm lưu (JSON/SQLite)
├── MapCatalog.cs           # Danh mục bản đồ và TargetMapID
├── TeleportModels.cs       # Cấu trúc Tọa độ, Waypoint & Zone
└── TeleportView.cs         # Unity UI View Component
```

### 1. Interface `ITeleportService`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace DTA.Features.Teleport
{
    public interface ITeleportService
    {
        // Dịch chuyển tức thời cùng bản đồ (Direct Memory & IL2CPP call)
        Task<bool> TeleportToAsync(Vector3 targetPos);
        Vector3 GetCurrentPosition();

        // Chuyển bản đồ trực tiếp (Direct Zone Move Hook)
        Task<bool> SwitchZoneAsync(uint targetMapId, int fromType = 10);
        uint GetCurrentMapId();

        // Quản lý Waypoints
        void SaveWaypoint(string name, Vector3 pos);
        IReadOnlyList<Waypoint> GetWaypoints(uint mapId);
        bool DeleteWaypoint(string name);
    }
}
```

---

## II. HAI CƠ CHẾ DỊCH CHUYỂN BẤT KHẢ XÂM PHẠM

### 1. Dịch chuyển cùng bản đồ (In-Map Instant Teleport)
- Sử dụng hàm nội bộ của Unity Character Controller:
  `KinematicCharacterMotor.set_TransientPosition(Vector3 value)` tại RVA Offset `0x52F2B20`.
- Gọi trên Unity Main Thread, đồng thời cập nhật trường `TransientPosition` tại offset `0x100` của struct `KinematicCharacterMotor`.
- **Cơ chế chống giật lùi (Anti-Rubberband):**
  + Đặt vận tốc tức thời `Velocity` về `Vector3.Zero` để physics engine không tính gia tốc quán tính cũ.
  + Cập nhật đồng bộ `Transform.position` để camera Unity bám sát ngay trong frame kế tiếp.
  + **Fallback Cao Độ Y:** Tự động fallback về `target_y` nếu khu vực NavMesh chưa nạp xong, chống rơi xuống void.

### 2. Chuyển bản đồ không NPC / Portal (Direct Zone Move)
- Gọi hàm nội bộ:
  `LayerSystem.ConnectToZoneMove(void* this, uint32 targetMapId, int32 fromType, void* serverName, bool useIris, bool useAdapt)` tại RVA Offset `0x5A13410`.
- **TUYỆT ĐỐI CẤM:** Không dùng NPC, không dùng cổng Portal, không dùng điện thoại ảo.

---

## III. BẢNG MÃ HÀM IL2CPP DỊCH CHUYỂN (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số & Ghi Chú |
| :--- | :--- | :--- | :--- |
| **Set Position Tức Thì** | `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(thisPtr, Vector3 value)` trên Main Thread |
| **Chuyển Bản Đồ Trực Tiếp** | `LayerSystem.ConnectToZoneMove` | `0x5A13410` | `(thisPtr, uint mapId, int fromType=10, ...)` |
| **Vận Tốc Nhân Vật** | Field `BaseVelocity` trong Motor | Offset `0x118` | Set về `Vector3.Zero` khi dịch chuyển |
| **Map ID Hiện Tại** | `ZoneManager.get_CurrentMapId` | `0x59C2010` | Đọc ID bản đồ đang đứng |
