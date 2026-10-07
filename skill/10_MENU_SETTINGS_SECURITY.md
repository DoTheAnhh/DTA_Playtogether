# 10 — SETTINGS, CONFIGURATION & SECURITY

## 1. SETTINGS ARCHITECTURE

Settings phải theo schema:

```text
SettingsSchema
 ├── Section
 ├── SettingId
 ├── Type
 ├── DefaultValue
 ├── Validation
 ├── VisibilityRule
 ├── AvailabilityRule
 └── Version
```

UI render settings từ schema. Không tạo 100 file View chỉ vì có 100 setting.

## 2. SETTINGS STORAGE

Ưu tiên:
`Memory -> Local Persistent -> Server Defaults`.

Settings có version/migration:
```text
Load -> Validate -> Migrate -> Normalize -> Cache -> Publish SettingsChanged
```

## 3. SECURITY BOUNDARIES

- Secrets/keys không nằm trong source.
- Token lưu secure storage.
- Server response phải verify signature/schema/version trước khi áp dụng.
- Không log credentials.
- Không tin dữ liệu từ client.

## 4. ANTI-TAMPER / ANTI-DEBUG

Các lớp bảo vệ chỉ dùng ở mức cần thiết cho bảo vệ ứng dụng:
- integrity check;
- debugger/environment detection;
- module verification;
- signed configuration;
- tamper event.

Security module không được nằm trong feature hot path nếu không cần.

## 5. HWID / DEVICE ID

Dùng abstract provider:
```csharp
IDeviceIdentityProvider
```

Không khóa cứng vào Windows. Platform adapter quyết định nguồn identity phù hợp.

## 6. FEATURE FLAGS

Feature flag server-driven nhưng client phải có local safe default. Nếu server không khả dụng, app không được crash.

## 7. SECURITY EVENTS

Các event chuẩn:
`AuthFailed`, `IntegrityMismatch`, `SessionExpired`, `InvalidSchema`, `UnsupportedBuild`, `PlatformChanged`.

## 8. SETTINGS UI

Giao diện có:
- search setting;
- categories;
- reset per section;
- import/export profile;
- unsaved indicator;
- validation message;
- keyboard navigation.
