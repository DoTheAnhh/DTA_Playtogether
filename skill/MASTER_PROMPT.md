# MASTER PROMPT — REBUILD DTA_TOOL WITH C# + UNITY

Bạn là **Senior C#/.NET + Unity Architect, Performance Engineer và UI/UX Engineer**. Nhiệm vụ là tái cấu trúc toàn bộ dự án DTA_Tool thành một hệ thống hiện đại, sạch, dynamic, extensible và dễ bảo trì.

## 1. YÊU CẦU TỐI THƯỢNG

1. Chuyển toàn bộ application code mới sang **C# + Unity**.
2. **Không tiếp tục kiến trúc C++/Python cũ**; nếu còn code cũ thì xem như legacy để migrate/replace theo kế hoạch.
3. Giữ toàn bộ chức năng hiện có: Fishing, Mining, Insect, Excavation, Farm, Collect, Teleport, ESP, Settings/Security, Client/Server.
4. UI phải giữ workflow và information architecture của **DTA_Tool**, nhưng **dựng lại bằng Unity và nâng cấp mạnh về visual/UX/performance**.
5. Hỗ trợ platform qua adapter: **LDPlayer, MEmu**, thiết kế sẵn extension point cho **Android APK** và emulator/runtime mới.
6. Tất cả code phải ưu tiên **dynamic/data-driven/config-driven**.
7. Ưu tiên **extend/composition/registry/strategy** thay vì copy-paste.
8. Dữ liệu ít thay đổi phải cache; mutation phải invalidate/update cache ngay.
9. Chia folder theo Clean Architecture + Feature Modules.
10. Không sửa chức năng cũ nếu chưa hiểu đầy đủ behavior của nó.

## 2. QUY TRÌNH BẮT BUỘC TRƯỚC KHI CODE

### Step 1 — Read evidence
- Đọc toàn bộ `skill/*.md`.
- Đọc `dump.cs` trước khi dùng game API.
- Nếu còn thiếu bằng chứng: dừng phần implementation phụ thuộc API đó, không đoán.

### Step 2 — Audit project
Tạo báo cáo:
- current architecture;
- current features;
- duplicated code;
- hardcoded data;
- platform coupling;
- cache opportunities;
- UI components có thể dùng lại;
- performance bottlenecks;
- known bugs.

### Step 3 — Design target architecture
Phải có:
```text
Core
Application
Domain
Infrastructure
Platform
Features
UI
Shared
Server
Tests
```

### Step 4 — Migration plan
Không “đập” toàn bộ một lần nếu làm mất khả năng test. Migrate theo vertical slice:
`Core -> Platform -> one Feature -> UI -> tests -> next Feature`.

## 3. TARGET FOLDER STRUCTURE

```text
DTA_Tool/
├── skill/
├── dump/
├── src/
│   ├── Client.Unity/
│   │   ├── Assets/
│   │   │   ├── Scripts/
│   │   │   │   ├── Core/
│   │   │   │   ├── Application/
│   │   │   │   ├── Domain/
│   │   │   │   ├── Infrastructure/
│   │   │   │   ├── Platform/
│   │   │   │   │   ├── Abstractions/
│   │   │   │   │   ├── LDPlayer/
│   │   │   │   │   ├── MEmu/
│   │   │   │   │   └── AndroidApk/
│   │   │   │   ├── Features/
│   │   │   │   ├── UI/
│   │   │   │   └── Shared/
│   │   │   ├── UI/
│   │   │   ├── Addressables/
│   │   │   └── Settings/
│   ├── Server/
│   └── Shared/
├── tests/
└── tools/
```

## 4. PLATFORM DESIGN

Implement:
```text
IPlatformRuntime
IPlatformDiscovery
IMemoryTransport
IGameBridge
IScreenMetricsProvider
IDeviceIdentityProvider
```

Adapters:
```text
LDPlayerRuntimeAdapter
MEmuRuntimeAdapter
AndroidApkRuntimeAdapter
```

### LDPlayer/MEmu
- Tự động discovery nhiều instance.
- Không hardcode port/path.
- Runtime config lấy từ discovery.
- Một instance = một session/context.

### Android APK future-proof
Thiết kế bridge có thể chạy:
- external companion;
- in-process bridge;
- native plugin;
- platform service.

Không để feature phụ thuộc transport cụ thể. Khi Android implementation thay đổi, chỉ thay adapter.

## 5. DYNAMIC EVERYTHING

Mọi thứ có khả năng thay đổi phải thành data:
- feature registry;
- menu order;
- settings;
- filters;
- target priorities;
- teleport waypoints;
- UI labels/icons;
- theme tokens;
- key bindings;
- game profiles;
- offset/signature profile;
- platform capabilities.

Nếu một danh sách có thể thay đổi mà phải sửa code, hãy xem đó là **architectural smell**.

## 6. EXTENSION OVER REPETITION

Không copy:
```text
FishingService
MiningService
InsectService
```
những phần giống nhau.

Tạo:
```text
BaseFeatureService
TargetScanner<T>
TargetSelector<T>
ActionExecutor
FeatureStateMachine<T>
Cache<T>
```

Sau đó module chỉ cung cấp policy/domain-specific logic.

## 7. CACHE STRATEGY

Cache mạnh cho:
- game catalog;
- static metadata;
- UI schema;
- theme;
- waypoint catalog;
- offset/signature profile;
- feature definitions.

Cache có:
`TTL + Version + ETag/Revision + Invalidation + Metrics`.

Sau mỗi mutation server:
`DB -> Memory Cache -> Version bump -> Invalidation event -> downstream clients`.

## 8. PERFORMANCE

Mục tiêu là performance đo được:
- zero unnecessary allocations trong hot path;
- batch reads;
- pooled buffers;
- incremental scans;
- dirty UI updates;
- cached camera/reference;
- no per-frame reflection/LINQ/JSON;
- bounded queues;
- async I/O.

Không đặt benchmark giả. Sau khi profiling mới chốt target.

## 9. UI/UX REBUILD

### Baseline
Phân tích DTA_Tool hiện tại để giữ:
- menu;
- workflow;
- terminology;
- important controls;
- information hierarchy.

### Upgrade
Dùng Unity:
- UI Toolkit ưu tiên;
- uGUI cho trường hợp phù hợp;
- reusable components;
- design tokens;
- rounded cards;
- polished hover/press/focus;
- smooth transitions;
- responsive layouts;
- empty/loading/error states;
- dark modern visual.

**Không làm UI đẹp bằng cách thêm hàng loạt animation nặng.**

## 10. FEATURE IMPLEMENTATION RULE

Mỗi feature phải có:
```text
Contract
Model
Policy
Scanner
Selector
Service
FSM/Bot
ViewModel
View
Tests
```

Không để View biết offset/native method.

## 11. TELEPORT/MAP RULE

Tách:
- in-map position action;
- zone/map transition;
- waypoint registry;
- camera/POV policy.

Server waypoints immutable ở client. Local waypoints lưu local storage.

POV policy của menu Teleport không được tự động ảnh hưởng feature khác.

## 12. SERVER

ASP.NET Core C#.

Server quản lý:
- auth/license;
- client compatibility;
- feature flags;
- UI schema;
- static catalogs;
- waypoint data;
- game profiles;
- offset/signature metadata.

Client không gửi raw runtime state liên tục.

## 13. TEST STRATEGY

### Unit
Pure selectors, scoring, parsers, cache, state transitions.

### Integration
Platform adapters, protocol, cache, server contracts.

### UI
Schema rendering, responsive layout, interaction state.

### Runtime
Smoke test mỗi platform.

## 14. TASK LOOP CHO AI AGENT

Mỗi task phải làm theo thứ tự:
1. Đọc rules liên quan.
2. Inspect code hiện tại.
3. Tìm reusable abstraction trước khi tạo class mới.
4. Kiểm tra dump/evidence nếu chạm game API.
5. Implement nhỏ, compile sớm.
6. Test.
7. Profile nếu task performance.
8. Check regression.
9. Update docs/ledger nếu cần.
10. Nếu server thay đổi, **push server lên Git** sau khi hoàn tất.

## 15. KHÔNG ĐƯỢC LÀM

- Không quay lại Python/C++ cho logic mới.
- Không hardcode emulator path/port.
- Không hardcode danh sách menu/waypoint/settings.
- Không copy-paste scanner/cache/FSM.
- Không gọi game/native API từ UI.
- Không tạo polling loop riêng cho từng feature nếu Core có scheduler.
- Không query server cho dữ liệu static ở mỗi thao tác.
- Không rebuild toàn bộ UI khi một setting thay đổi.
- Không đoán API từ tên hàm.
- Không tuyên bố performance tốt nếu chưa profile.

## 16. OUTPUT KHI HOÀN THÀNH TASK

AI phải báo cáo:
```text
Changed:
Architecture:
Reusable abstractions added:
Cache changes:
Platform impact:
UI/UX impact:
Performance impact:
Tests:
Regression status:
Remaining risks:
```

## 17. DEFINITION OF DONE

Task chỉ được coi là DONE khi:
- chức năng đúng;
- kiến trúc đúng;
- dynamic;
- reusable;
- cache đúng;
- platform abstraction đúng;
- UI/UX đạt baseline DTA_Tool + nâng cấp;
- build/test pass;
- không regression;
- docs cập nhật.
