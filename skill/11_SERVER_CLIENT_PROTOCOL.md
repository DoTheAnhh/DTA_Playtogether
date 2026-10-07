# 11 — SERVER / CLIENT PROTOCOL & SERVER-DRIVEN CONFIG

## 1. NGUYÊN TẮC

Server là control/config plane; Client là runtime/data plane.

Server **không** điều khiển từng tick runtime. Client phải hoạt động ổn định với cache khi server tạm thời không khả dụng.

## 2. SHARED CONTRACT

```text
src/Shared/
├── Contracts/
├── Protocol/
├── Models/
├── Enums/
└── Validation/
```

Dùng cùng contract C# giữa ASP.NET Core và Unity client.

## 3. REQUEST ENVELOPE

```text
Version
RequestId
Timestamp
SessionId
Operation
Payload
Signature
```

Mỗi response có:
`RequestId / Status / ServerVersion / DataVersion / Payload / Error`.

## 4. SERVER-DRIVEN UI

Server có thể gửi schema:
```json
{
  "version": 12,
  "theme": "dta-modern",
  "menus": [],
  "features": [],
  "settings": []
}
```

Client phải validate schema trước khi render.

Không để server gửi arbitrary executable code. Server chỉ gửi data/config.

## 5. CACHE STRATEGY

Mỗi resource có:
`ResourceKey / Version / ETag / TTL / Signature`.

Flow:
```text
Memory Cache
  ↓ miss
Persistent Cache
  ↓ miss/stale
Server
  ↓
Validate -> Store -> Publish
```

Dữ liệu ít thay đổi như UI schema, catalog, waypoint list, feature metadata phải cache mạnh.

## 6. SERVER MUTATION CACHE

Mỗi lần server thêm/sửa/xóa key/config/waypoint/feature metadata:
1. Update database.
2. Update in-memory cache.
3. Increment data version.
4. Publish invalidation/event.
5. Persist/replicate nếu hệ thống có nhiều node.

Mục tiêu là không query database ở mọi request.

## 7. MULTI-INSTANCE / MULTI-PLATFORM

Session phải nhận diện:
`User + DeviceIdentity + PlatformKind + GameIdentity + ClientVersion`.

Không dùng một global singleton session cho nhiều emulator instance.

## 8. NETWORK RESILIENCE

- timeout;
- retry có exponential backoff + jitter;
- circuit breaker;
- offline cache;
- request deduplication;
- cancellation;
- compression cho payload lớn.

## 9. SECURITY

Production dùng TLS và signed responses. Sensitive payload mã hóa theo nhu cầu; không tự chế crypto.

## 10. SERVER PERFORMANCE

- Memory cache cho read-heavy data.
- Batch operations.
- Async I/O.
- Pagination.
- Rate limiting.
- Connection pooling.
- Metrics p50/p95/p99.

## 11. VERSION COMPATIBILITY

```text
ClientVersion
ProtocolVersion
SchemaVersion
GameVersion
PlatformKind
```

Server phải từ chối/giảm tính năng rõ ràng khi incompatible, thay vì gửi schema không tương thích.

## 12. SERVER DEFINITION OF DONE

Nếu task thay đổi server:
- build/test server;
- update shared contract nếu cần;
- cập nhật migration/cache invalidation;
- kiểm tra backward compatibility;
- cập nhật docs;
- **push server lên Git sau khi hoàn thành task nếu có thay đổi server**.
