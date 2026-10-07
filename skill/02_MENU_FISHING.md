# 02 — FISHING MODULE

> Module `Fishing` phải giữ workflow của DTA_Tool nhưng được triển khai theo kiến trúc plugin/dynamic chung. Không copy logic từ module khác; dùng Core abstractions.

## 1. CONTRACT

```csharp
public interface IFishingService : IFeatureService
{
    ValueTask<FeatureResult> ExecuteAsync(FishingCommand command, CancellationToken ct);
    ValueTask<FishingSnapshot> ReadSnapshotAsync(CancellationToken ct);
}
```

Service không phụ thuộc Unity View. View chỉ bind state/command.

## 2. MODULE STRUCTURE

```text
Features/Fishing/
├── IFishingService.cs
├── FishingService.cs
├── FishingBot.cs
├── FishScanner.cs
├── FishingModels.cs
├── FishingPolicies.cs
├── FishingCatalog.cs
├── FishingModule.cs
└── UI/
    ├── FishingView.cs
    └── FishingViewModel.cs
```

`Catalog` và static metadata phải cache. `Scanner` chỉ phát hiện state; `Bot` quyết định state transition; `Service` thực thi action.

## 3. GENERIC PIPELINE

```text
Observe -> Filter -> Score -> Select -> Validate -> Approach -> Action -> Verify -> Collect/Result -> Cooldown -> Observe
```

Không dùng sleep cứng. Dùng condition/event/timeout.

## 4. DYNAMIC TARGET SELECTION

Mọi filter/priority phải là data-driven:

```text
TargetRule
 ├── Enabled
 ├── Priority
 ├── RequiredTags
 ├── ExcludedTags
 ├── MinValue / MaxValue
 └── CustomScore
```

Có thể thay đổi rule từ UI/schema mà không sửa business code.

## 5. SCAN OPTIMIZATION

- Snapshot entity list một lần cho mỗi cycle.
- Spatial index/quadtree/grid nếu số entity lớn.
- Reuse entity buffers.
- Chỉ rescan khi `SceneVersion`, `EntityRevision` hoặc TTL hết hạn.
- Khi map mới: ưu tiên warm-up scan và cache metadata trước khi bot bắt đầu hành động.

## 6. ACTIONS

Các action chuẩn của module phải map qua `IGameActionDispatcher`; không hardcode RVA trong feature.

| Action | Native binding |
|---|---|
| CastRod | Resolve từ `IGameBindingProvider` |
| ReelIn | Resolve từ binding profile |
| KeepFish | Resolve từ binding profile |
| SellFish | Resolve từ binding profile |
| RepairRod | Resolve từ binding profile |

Mọi binding phải có `GameIdentity`, signature/version và evidence tag.

## 7. STATE MACHINE

Các state phải implement `IState<TContext>` và dùng FSM Core. Không tạo vòng `while(true)` riêng cho feature.

```text
Idle
  -> Scanning
  -> TargetSelected
  -> Approaching
  -> Acting
  -> Verifying
  -> Collecting/HandlingResult
  -> Cooldown
  -> Scanning
```

Mỗi state có:
- entry condition;
- exit condition;
- timeout;
- retry policy;
- cancellation;
- telemetry.

## 8. FAILURE RECOVERY

Nếu action fail:
1. Verify target còn tồn tại.
2. Refresh volatile snapshot.
3. Retry theo policy giới hạn.
4. Nếu target stale: bỏ target và rescan.
5. Nếu platform/game state lỗi: pause module và yêu cầu Core recovery.

Không retry vô hạn.

## 9. UI REQUIREMENTS

View phải tái sử dụng các component chung:
`FeatureHeader`, `StatusBadge`, `Toggle`, `Slider`, `FilterList`, `TargetPreview`, `StatsCard`, `ActionButton`, `EventLog`.

Không hardcode layout cho từng resolution.

## 10. PERFORMANCE TARGET

Không đặt con số giả nếu chưa benchmark. Dùng baseline đo thực tế:
- scan p50/p95;
- action p50/p95;
- allocations/frame;
- target selection duration;
- stale target rate;
- successful action rate.

## 11. ACCEPTANCE CRITERIA

- Giữ nguyên chức năng DTA_Tool.
- Có dynamic filter/priority.
- Không duplicate scanner/cache/dispatcher.
- Có cache và invalidation.
- Chuyển platform không sửa module.
- UI responsive.
- Regression test cho bug đã biết.
