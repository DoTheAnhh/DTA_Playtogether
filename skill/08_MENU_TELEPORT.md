# 08 — TELEPORT & MAP/ZONE MODULE

## 1. MỤC TIÊU

Tập trung vào quản lý vị trí, chuyển vị trí trong map và chuyển zone/map bằng abstraction động. Không phụ thuộc UI, emulator hay hardcoded coordinates.

## 2. TWO-LAYER POSITION SYSTEM

### A. In-map position
- Dùng `IGamePositionProvider` + `IGameActionDispatcher`.
- Target position là data object, không hardcode trong code.
- Validate scene/map readiness trước khi apply.

### B. Zone/map transition
- Dùng `IZoneTransitionProvider`.
- Không phụ thuộc NPC/portal/phone trong business logic.
- Binding cụ thể của game chỉ nằm trong `GameBindings` và phải được xác minh từ dump/runtime.

## 3. SERVER-DEFINED TELEPORT LOCATIONS

Server cung cấp vị trí chuẩn:
```text
ServerWaypoint
 ├── Id
 ├── Name
 ├── MapId
 ├── ZoneId
 ├── Position
 ├── Rotation
 ├── Tags
 ├── SortOrder
 ├── Version
 └── Locked
```

Client chỉ đọc; waypoint do server cấp **không được sửa/xóa ở client**.

## 4. LOCAL USER WAYPOINTS

Người dùng có thể tạo waypoint local:
```text
LocalWaypoint
 ├── LocalId
 ├── Name
 ├── MapId
 ├── Position
 ├── Rotation
 ├── Tags
 └── CreatedAt
```

Local data lưu local cache/SQLite/file an toàn, không ghi ngược server trừ khi có API riêng.

## 5. POV / CAMERA RULE

Tách camera policy khỏi teleport service:
- `TeleportMenu` có thể áp dụng POV policy nếu được bật.
- Feature khác không tự động thừa hưởng POV policy.
- Mỗi action có `CameraPolicy` explicit.

## 6. DYNAMIC WAYPOINT REGISTRY

```csharp
IWaypointRegistry
 ├── GetServerWaypoints()
 ├── GetLocalWaypoints()
 ├── Resolve(id)
 ├── Search(query,tags)
 └── Invalidate(map/version)
```

Không tạo `if/else` cho từng waypoint.

## 7. MAP CHANGE PIPELINE

```text
Request
 -> Validate target
 -> Ensure runtime attached
 -> Dispatch zone transition
 -> Wait GameIdentity/MapReady
 -> Invalidate volatile caches
 -> Warm-up scanner caches
 -> Publish MapChangedEvent
 -> Resume dependent features
```

Không để feature bắt đầu scan dữ liệu cũ trong map mới.

## 8. PERFORMANCE

Map metadata, waypoint catalog và static transform data phải cache. Khi sang map mới, ưu tiên prefetch các dữ liệu ít thay đổi trước; runtime entity scan chạy incremental.

## 9. UI

Teleport UI gồm:
- search;
- favorites;
- map grouping;
- tags;
- server/local badge;
- recent locations;
- loading state;
- disabled state khi map chưa sẵn sàng.

Không hardcode 1 danh sách vị trí trong prefab.
