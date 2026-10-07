# 01 — CORE ENGINE, PLATFORM RUNTIME & PERFORMANCE

## 1. MỤC TIÊU

Core là lớp ổn định nhất của dự án. Core không biết Fishing/Mining/UI cụ thể và không được phụ thuộc emulator cụ thể.

## 2. PLATFORM RUNTIME

```csharp
public enum PlatformKind { Unknown, LDPlayer, MEmu, AndroidApk }

public interface IPlatformRuntime : IDisposable
{
    PlatformKind Kind { get; }
    bool IsAttached { get; }
    ValueTask<PlatformInfo> DetectAsync(CancellationToken ct);
    ValueTask<bool> AttachAsync(CancellationToken ct);
    ValueTask DetachAsync(CancellationToken ct);
    ValueTask<MemoryReadResult> ReadAsync(nint address, Span<byte> buffer, CancellationToken ct);
    ValueTask<MemoryWriteResult> WriteAsync(nint address, ReadOnlySpan<byte> buffer, CancellationToken ct);
    ScreenMetrics GetScreenMetrics();
    RuntimeCapabilities Capabilities { get; }
}
```

### Adapter rules
- `LDPlayerRuntimeAdapter`: process/device discovery, runtime-specific transport.
- `MEmuRuntimeAdapter`: MEmu discovery and transport.
- `AndroidApkRuntimeAdapter`: future in-device/in-process bridge; không giả định ADB là transport duy nhất.
- Có thể thêm `MuMuRuntimeAdapter`, `BlueStacksRuntimeAdapter` mà không sửa feature service.

**Không** để `if (LDPlayer)` hoặc `if (MEmu)` xuất hiện trong feature business logic.

## 3. AUTO DISCOVERY PIPELINE

```text
Discover -> Identify -> Validate -> Select Adapter -> Attach -> Verify Game Identity -> Load Versioned Cache
```

Discovery phải có:
- timeout;
- cancellation;
- nhiều instance;
- deterministic selection;
- attach retry có backoff;
- trạng thái rõ ràng trên UI.

## 4. MEMORY SERVICE

Tách abstraction:

```csharp
public interface IMemoryReader
{
    bool IsValidAddress(nint address);
    bool Read<T>(nint address, out T value) where T : unmanaged;
    bool ReadBlock(nint address, Span<byte> destination);
}
```

`MemorySnapshotService` gom các field cần thiết thành snapshot. Feature đọc snapshot thay vì tự đọc hàng chục địa chỉ.

### Batch policy
- Group các offset gần nhau.
- Reuse buffer.
- Cache pointer chain đã xác minh.
- Invalidate khi scene/map/game version thay đổi.

## 5. GAME IDENTITY & OFFSET PROVIDER

```text
GameIdentity = package + version + build + architecture + platform
OffsetProfile = GameIdentity + signatureVersion
```

Không dùng profile nếu identity không match.

```csharp
public interface IOffsetProvider
{
    ValueTask<OffsetProfile?> GetAsync(GameIdentity identity, CancellationToken ct);
    void Invalidate(GameIdentity identity);
}
```

## 6. ACTION DISPATCHER

```csharp
public interface IGameActionDispatcher
{
    ValueTask<ActionResult> EnqueueAsync(GameAction action, CancellationToken ct);
}
```

Dispatcher chịu trách nhiệm:
- main-thread affinity;
- queue priority;
- duplicate suppression;
- timeout;
- cancellation;
- exception boundary;
- duration metric.

Không để View gọi bridge trực tiếp.

## 7. FSM ENGINE DÙNG CHUNG

Không tạo một FSM framework riêng cho từng feature.

```text
StateMachine<TContext>
 ├── Enter
 ├── Tick
 ├── Exit
 ├── CanTransition
 └── Timeout
```

Fishing/Mining/Insect/... chỉ định state và policy.

## 8. CACHE ENGINE

Có generic cache:

```csharp
ICache<TKey,TValue>
 ├── Get
 ├── TryGet
 ├── Set
 ├── Remove
 ├── InvalidateByTag
 └── Metrics
```

Cache layers:
`Memory -> LocalPersistent -> Server`.

Read path:
`Memory hit -> Persistent hit -> Server -> validate -> populate upper layers`.

## 9. EVENT BUS

Events phải là domain events typed, không string event.

Ví dụ:
- `GameAttachedEvent`
- `GameVersionChangedEvent`
- `MapChangedEvent`
- `PlayerStateChangedEvent`
- `FeatureStateChangedEvent`
- `CacheInvalidatedEvent`

## 10. HOT PATH RULES

- Không LINQ.
- Không reflection.
- Không JSON serialize mỗi frame.
- Không `FindObjectOfType` trong loop.
- Không `GameObject.Find` trong loop.
- Không tạo `new List<>` mỗi tick.
- Không polling toàn bộ game state nếu chỉ một field thay đổi.

## 11. SAFE FAILURE

Mọi adapter phải có circuit breaker. Nếu platform disconnect:
`Stop actions -> preserve configuration -> invalidate volatile pointers -> reconnect -> verify identity -> resume only if safe`.

## 12. CORE TELEMETRY

Dashboard phải hiển thị tối thiểu:
- platform;
- instance;
- game version;
- attach state;
- dispatcher queue;
- average/p95 action latency;
- memory scan latency;
- cache hit rate;
- FPS/UI frame time;
- GC allocation.
