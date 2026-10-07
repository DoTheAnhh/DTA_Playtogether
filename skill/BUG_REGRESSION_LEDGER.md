# BUG & REGRESSION LEDGER

> Đây là ledger sống. Mỗi bug phải có nguyên nhân gốc, fix, regression test và trạng thái xác minh.

## 1. FORMAT

```text
ID:
Module:
Platform:
Symptom:
Root Cause:
Fix:
Regression Test:
Status: OPEN / FIXED / VERIFIED
Verified Build:
Notes:
```

## 2. BUG CLASSES CẦN THEO DÕI

### R001 — Platform attach thất bại
- Scope: LDPlayer/MEmu/APK.
- Requirement: adapter discovery không được làm crash app.
- Test: mock từng adapter + nhiều instance.

### R002 — Scan map mới chậm
- Root-cause candidates: cold cache, full scan, stale pointer.
- Fix pattern: invalidate volatile state -> warm static cache -> incremental scan.

### R003 — Action bị bỏ qua
- Kiểm tra dispatcher queue, duplicate suppression, target revision và action precondition.
- Không chữa bằng cách spam action.

### R004 — Dialog/Result không đóng
- Kiểm tra state machine và verified game binding.
- Không hardcode UI object path.

### R005 — Target stale
- Target phải có entity revision/UID + validation trước action.

### R006 — Teleport/map change dùng dữ liệu cũ
- Bắt buộc MapChangedEvent invalidation.

### R007 — UI lag khi bật nhiều feature
- Kiểm tra layout rebuild, polling, allocation và render count.

### R008 — Memory/GC spike
- Profile allocations/frame, buffer reuse, LINQ/reflection/string formatting.

### R009 — Server overload
- Kiểm tra cache hit rate, duplicate request, database query per request.
- Mutation phải update cache ngay.

### R010 — Config/schema incompatible
- Validate version/schema trước khi apply; fallback cached-known-good.

## 3. PLATFORM MATRIX

Mỗi regression quan trọng phải ghi rõ:
`Windows + LDPlayer`, `Windows + MEmu`, `Android APK (future)`.

## 4. RELEASE GATE

Không release khi:
- P0/P1 open;
- build không reproducible;
- cache corruption chưa có fallback;
- platform adapter chưa pass smoke test;
- UI có regression workflow cũ.

## 5. PERFORMANCE REGRESSION

Theo dõi baseline:
- startup;
- attach;
- first scan;
- map transition;
- action latency;
- frame time;
- GC;
- network RTT.

Chỉ kết luận regression dựa trên benchmark/profiling, không dựa cảm giác.
