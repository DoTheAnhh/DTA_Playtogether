# 08: ĐẶC TẢ MENU & MODULE DỊCH CHUYỂN & ĐỔI MAP (TELEPORT & ZONES MODULE)

> **Mục tiêu:** Tái thiết kế toàn bộ hệ thống dịch chuyển tọa độ và chuyển đổi bản đồ bằng C++20. Phản hồi dưới 2ms, triệt tiêu hiện tượng giật lùi vị trí (Anti-Rubberbanding), chuyển đổi bản đồ trực tiếp bằng hàm game nội bộ mà TUYỆT ĐỐI KHÔNG DÙNG NPC, CỔNG PORTAL HAY ĐIỆN THOẠI ẢO.

---

## I. KIẾN TRÚC MODULE & INTERFACES

```
src/client/features/teleport/
├── ITeleportService.hpp    # Interface dịch vụ dịch chuyển
├── TeleportService.cpp     # Tương tác IL2CPP set position & zone move
├── WaypointManager.hpp     # Quản lý danh sách điểm lưu (JSON/SQLite)
├── MapCatalog.hpp          # Danh mục bản đồ và TargetMapID
├── TeleportModels.hpp      # Cấu trúc Tọa độ, Waypoint & Zone
└── TeleportView.cpp        # ImGui Render Component
```

### 1. Interface `ITeleportService`

```cpp
#pragma once
#include <string>
#include <vector>
#include "TeleportModels.hpp"

class ITeleportService {
public:
    virtual ~ITeleportService() = default;

    // Dịch chuyển tức thời cùng bản đồ (Direct Memory & IL2CPP call)
    virtual bool TeleportTo(const Vector3& targetPos) = 0;
    virtual Vector3 GetCurrentPosition() = 0;

    // Chuyển bản đồ trực tiếp (Direct Zone Move Hook)
    virtual bool SwitchZone(uint32_t targetMapId, int32_t fromType = 10) = 0;
    virtual uint32_t GetCurrentMapId() = 0;

    // Quản lý Waypoints
    virtual void SaveWaypoint(const std::string& name, const Vector3& pos) = 0;
    virtual std::vector<Waypoint> GetWaypoints(uint32_t mapId) = 0;
    virtual bool DeleteWaypoint(const std::string& name) = 0;
};
```

---

## II. HAI CƠ CHẾ DỊCH CHUYỂN BẤT KHẢ XÂM PHẠM

### 1. Dịch chuyển cùng bản đồ (In-Map Instant Teleport)
- Sử dụng hàm nội bộ của Unity Character Controller:
  `KinematicCharacterMotor.set_TransientPosition(Vector3 value)` tại RVA Offset `0x52F2B20`.
- Gọi trên Unity Main Thread, đồng thời ghi đè trực tiếp trường `TransientPosition` tại offset `0x100` của struct `KinematicCharacterMotor`.
- **Cơ chế chống giật lùi (Anti-Rubberband):**
  + Đặt vận tốc tức thời `Velocity` về `Vector3(0, 0, 0)` để physics engine không tính gia tốc quán tính cũ.
  + Cập nhật đồng bộ `Transform.position` để camera Unity bám sát ngay trong frame kế tiếp.

### 2. Chuyển bản đồ không NPC / Portal (Direct Zone Move)
- Gọi hàm nội bộ:
  `LayerSystem.ConnectToZoneMove(void* this, uint32 targetMapId, int32 fromType, void* serverName, bool useIris, bool useAdapt)` tại RVA Offset `0x5A13410`.
- Tham số chuẩn: `fromType = 10` (QuickMove), `serverName = nullptr`, `useIris = true`, `useAdapt = true`.
- Game tự động mở màn hình tải map và kết nối tới cụm server của bản đồ đích mà không yêu cầu người chơi phải chạy tới NPC hay chui qua portal.

---

## III. DANH MỤC MAP ID PHỔ BIẾN (VERIFIED)

| Tên Bản Đồ | Target Map ID | Mô Tả |
| :--- | :--- | :--- |
| **Khu Trung Tâm (Plaza)** | `1` | Khu quảng trường, hồ câu cá lớn, sự kiện chính |
| **Thị Trấn (Downtown)** | `2` | Khai khoáng quặng đá, trung tâm thương mại |
| **Khu Cắm Trại (Camping)** | `3` | Bắt côn trùng hiếm, suối câu cá, đập đá |
| **Khu Nghỉ Dưỡng (Resort)** | `4` | Biển đảo lớn, cá khổng lồ bóng 6-7, lặn biển |
| **Nhà Riêng (Home)** | `10` | Khu sân vườn, nông trại cá nhân, bể nuôi cá |

---

## IV. BẢNG MÃ HÀM IL2CPP DỊCH CHUYỂN (DUMP.CS)

| Chức Năng | Tên Hàm Game Trong `dump.cs` | Offset RVA | Tham Số Truyền Vào |
| :--- | :--- | :--- | :--- |
| **Đặt vị trí tức thời** | `KinematicCharacterMotor.set_TransientPosition` | `0x52F2B20` | `(motorPtr, [float x, float y, float z])` |
| **Chuyển bản đồ trực tiếp** | `LayerSystem.ConnectToZoneMove` | `0x5A13410` | `(layerSystemPtr, uint32 mapId, 10, 0, 1, 1)` |
| **Lấy Map ID hiện tại** | Đọc từ `LayerSystem.m_CurrentMapId` | Dynamic | Đọc 4 bytes int32 từ struct LayerSystem |
