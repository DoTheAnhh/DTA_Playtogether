# 00 — MASTER RULES & ARCHITECTURE — C# + UNITY

> **Mục tiêu:** Đây là luật tối cao cho toàn bộ dự án DTA_Tool. Mọi thay đổi code, UI, protocol, bot, platform adapter và server phải tuân thủ file này. Nếu một yêu cầu mới mâu thuẫn với file này, phải cập nhật rule trước rồi mới code.

## 1. MỤC TIÊU KIẾN TRÚC

- Viết **100% C#** cho application/client/server; **không dùng Python hoặc C++** cho logic dự án mới.
- **Unity** là framework UI/presentation chính để dựng giao diện đẹp, mượt và nhất quán.
- UI/UX phải lấy **DTA_Tool làm baseline hành vi và bố cục**, sau đó nâng cấp visual, animation, responsive layout, accessibility và tốc độ thao tác; không copy cứng từng màn hình.
- Core phải **platform-agnostic**: LDPlayer, MEmu và Android APK chỉ là adapter/driver.
- Code ưu tiên **dynamic/config-driven/data-driven**, tuyệt đối tránh hardcode danh sách, tọa độ, offset, menu, màu, kích thước, timeout hay đường dẫn khi có thể cấu hình.
- Ưu tiên **extend/composition** thay vì copy-paste. Chức năng dùng chung phải nằm ở Core/Shared.
- Dữ liệu ít thay đổi phải **cache**; dữ liệu runtime thay đổi phải có TTL/version/invalidation rõ ràng.
- Không tối ưu bằng cách phá tính đúng đắn. Mọi tối ưu phải đo được bằng profiling/benchmark.

## 2. EVIDENCE FIRST — KHÔNG ĐOÁN API GAME

Trước khi dùng class/field/method/enum/offset/RVA của game:
1. Đọc `dump.cs` và các tài liệu runtime có liên quan.
2. Ghi rõ nguồn bằng một trong ba tag:
   - `[VERIFIED]`: đã xác minh runtime.
   - `[DUMP_ONLY]`: có trong dump nhưng chưa xác minh runtime.
   - `[HYPOTHESIS]`: giả thuyết; **không được dùng để điều khiển hành vi production**.
3. Không tự bịa offset, signature, enum value hoặc object layout.
4. Khi game update, invalidate dữ liệu cũ theo `GameVersion + BuildId + Platform`.

## 3. PLATFORM ABSTRACTION — KHÔNG ĐỂ LD/MEMU/APK CHUI VÀO CORE

### Supported targets
- `LDPlayer` / LDPlayer 9+.
- `MEmu`.
- Có thể mở rộng `MuMu`, `BlueStacks` hoặc emulator khác.
- `AndroidApk` native runtime trong tương lai.
- Mọi target mới phải implement interface thay vì sửa business logic.

```text
Core
 └── IPlatformRuntime
      ├── LDPlayerRuntimeAdapter
      ├── MEmuRuntimeAdapter
      ├── AndroidApkRuntimeAdapter
      └── FutureRuntimeAdapter
```

Core chỉ biết capability (`Attach`, `ReadState`, `Invoke`, `GetScreenMetrics`, `GetGameIdentity`...), không biết process name, ADB port hay đường dẫn cài đặt cụ thể.

## 4. ZERO HARD-CODE

Cấm hardcode:
- Dynamic address/pointer/RVA.
- Emulator executable path/ADB port.
- Resolution và DPI.
- Menu item list.
- Feature settings.
- Server endpoint trong business code.
- Theme values trong từng view.
- Magic number không có tên/config.

Cho phép constant chỉ khi là **immutable domain invariant** và phải đặt tên rõ ràng.

## 5. CACHE-FIRST

Phân loại dữ liệu:

| Loại | Cách xử lý |
|---|---|
| Static catalog / enum metadata | Memory cache + versioned snapshot |
| Game signature / offset | Versioned cache + validation |
| UI schema/theme | Local persistent cache + ETag/version |
| Map metadata | Map cache + invalidation khi build/map đổi |
| Runtime entity | Short TTL/frame cache |
| Player position/state | Runtime snapshot, không persistent |
| License/session | Secure cache + expiry |

Cache bắt buộc có:
- key chuẩn;
- TTL hoặc version;
- invalidation event;
- fallback khi cache lỗi;
- giới hạn kích thước;
- metric hit/miss.

## 6. EXTENSION-FIRST

Không viết:
```csharp
if (feature == "Fishing") { ... }
else if (feature == "Mining") { ... }
else if (feature == "Insect") { ... }
```
cho logic có khả năng mở rộng.

Thay bằng:
```csharp
IFeatureModule module = registry.Resolve(featureId);
await module.ExecuteAsync(context);
```

Dùng:
- interfaces;
- strategy pattern;
- registry;
- factory;
- composition;
- event bus;
- generic base service;
- capability interfaces.

## 7. GAME ACTION DISPATCH

Mọi call vào Unity/Game/IL2CPP phải đi qua một abstraction trung tâm như `IGameActionDispatcher`. Không gọi native/game API trực tiếp từ View.

```text
UI -> Application Command -> Feature Service -> GameActionDispatcher -> Platform/Game Bridge
```

Nếu action yêu cầu Unity main thread, dispatcher phải bảo đảm main-thread affinity.

## 8. MEMORY & PERFORMANCE

- Batch read thay vì nhiều read nhỏ.
- Reuse buffer bằng `ArrayPool<T>` khi phù hợp.
- Tránh LINQ/string allocation trong hot path.
- Dùng `readonly struct` cho snapshot nhỏ.
- Không tạo Task/closure mỗi frame nếu không cần.
- Có backpressure cho producer/consumer.
- Không giữ lock khi I/O.
- Ưu tiên immutable snapshots + atomic swap cho read-heavy state.
- Mọi claim performance phải có benchmark.

## 9. CONCURRENCY

Thứ tự lock thống nhất nếu thật sự cần lock:
`UI -> Bot -> Cache -> Memory -> IPC`.

Không giữ lock trong:
- network I/O;
- disk I/O;
- Unity frame yield;
- external process call.

Ưu tiên lock-free/immutable snapshot/channel.

## 10. UI/UX — DTA_TOOL BASELINE, UNITY UPGRADE

UI phải:
- giữ workflow quen thuộc của DTA_Tool;
- có navigation rõ ràng;
- bo góc hợp lý;
- hierarchy thị giác tốt;
- animation ngắn, mượt, không gây chậm thao tác;
- responsive theo resolution/DPI/aspect ratio;
- dark/light theme nếu schema hỗ trợ;
- keyboard/gamepad-friendly khi cần;
- trạng thái loading/empty/error/disabled đầy đủ.

Không dùng animation chỉ để “cho đẹp”; animation phải phục vụ feedback.

## 11. UI DATA-DRIVEN

Menu, field, toggle, slider, dropdown, card, badge, hotkey, tooltip và permission phải có model/schema.

```text
FeatureDefinition
 ├── Id
 ├── DisplayName
 ├── Icon
 ├── Order
 ├── Availability
 ├── SettingsSchema
 └── CapabilityRequirements
```

View render schema; không copy-paste view cho từng feature nếu layout có thể tái sử dụng.

## 12. SERVER/CLIENT

Server quản lý:
- authentication/license;
- configuration/version;
- feature flags;
- UI schema;
- static catalogs;
- offset/signature metadata;
- telemetry tối thiểu cần thiết.

Client xử lý runtime hot path. Không gửi raw memory/state tick liên tục lên server.

Mọi server mutation quan trọng phải có audit/version.

## 13. SECURITY & PRIVACY

- Secrets không hardcode trong client.
- Không log token, key, HWID hoặc dữ liệu nhạy cảm.
- TLS bắt buộc cho production.
- Validate server response trước khi apply.
- Security feature không được phá ổn định client.
- Anti-tamper/anti-debug chỉ triển khai trong phạm vi hợp pháp của ứng dụng và phải có kill-switch cấu hình.

## 14. ERROR HANDLING

Không dùng exception làm control flow trong hot path.

Mỗi lỗi phải có:
- error code;
- module;
- operation;
- recoverability;
- retry policy;
- context id.

## 15. OBSERVABILITY

Log structured:
`timestamp / level / module / operation / result / duration / platform / gameVersion / contextId`.

Có metrics:
- cache hit rate;
- scan duration;
- action latency;
- queue depth;
- frame time;
- GC allocation;
- network RTT;
- platform attach time.

## 16. TESTING & REGRESSION

Mỗi feature phải có:
- unit test cho pure logic;
- mock integration test cho adapter;
- regression case;
- runtime verification nếu cần.

Trạng thái test:
`[COMPILED]`, `[UNIT_TESTED]`, `[MOCK_VERIFIED]`, `[INGAME_VERIFIED]`.

Không được gọi một tính năng “đã fix” chỉ vì build thành công.

## 17. CẤU TRÚC FOLDER CHUẨN

```text
DTA_Tool/
├── docs/
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
│   │   │   │   ├── Features/
│   │   │   │   ├── UI/
│   │   │   │   └── Shared/
│   │   │   ├── UI/
│   │   │   ├── Addressables/
│   │   │   └── Settings/
│   │   └── Packages/
│   ├── Server/
│   ├── Shared/
│   └── Tools/
└── tests/
```

## 18. DEFINITION OF DONE

Một task chỉ hoàn thành khi:
1. Không vi phạm rule.
2. Không duplicate logic có thể tái sử dụng.
3. Có cache/invalidation nếu dữ liệu phù hợp.
4. Có platform abstraction.
5. UI responsive và có đủ state.
6. Không regression chức năng cũ.
7. Build/test thành công.
8. Tài liệu skill được cập nhật nếu kiến trúc thay đổi.
9. Nếu server thay đổi: **push server lên Git sau khi task hoàn tất**.
