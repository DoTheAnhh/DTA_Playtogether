# 09 — ESP / RADAR / OVERLAY MODULE

## 1. MỤC TIÊU

Render thông tin runtime bằng pipeline hiệu năng cao, tách hoàn toàn data collection khỏi rendering.

## 2. PIPELINE

```text
Game Snapshot -> Entity Tracker -> Filter -> Projection -> Render Model -> Unity Renderer
```

`EntityTracker` cache entity identity và chỉ cập nhật field thay đổi.

## 3. DATA-DRIVEN ESP PROFILE

```text
EspProfile
 ├── EntityType
 ├── Enabled
 ├── LabelTemplate
 ├── ShowDistance
 ├── ShowHealth
 ├── ShowTargetLine
 ├── VisibilityRule
 └── RenderPriority
```

Profile có thể server/local-config, không sửa code để thêm entity type.

## 4. WORLD-TO-SCREEN

Tách interface:
```csharp
IWorldProjector.Project(WorldPosition, CameraSnapshot)
```

Không gọi `Camera.main` trong mỗi entity loop. Cache camera reference/snapshot và invalidate khi scene/camera thay đổi.

## 5. RENDERING

Ưu tiên:
- pooled UI elements;
- batch update;
- dirty flag;
- no Instantiate/Destroy mỗi frame;
- Canvas/UIToolkit layer riêng;
- culling theo viewport và distance.

## 6. UI/UX

Có master toggle, per-category toggle, opacity, scale, distance limit, labels và hotkey. Thay đổi settings chỉ dirty những renderer liên quan.

## 7. PERFORMANCE METRICS

Theo dõi:
- tracked entities;
- projected entities;
- rendered entities;
- projection ms;
- render ms;
- allocations/frame.

Không scan toàn bộ entity list nếu snapshot revision không đổi.
