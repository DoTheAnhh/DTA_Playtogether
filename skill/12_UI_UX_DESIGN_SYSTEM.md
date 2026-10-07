# 12 — DTA_TOOL MODERN UNITY DESIGN SYSTEM

## 1. DESIGN GOAL

**DTA_Tool là baseline trải nghiệm; Unity là nền tảng nâng cấp.** Mục tiêu là cảm giác quen thuộc nhưng hiện đại hơn, đẹp hơn, mượt hơn và thao tác nhanh hơn.

Không copy từng pixel bằng hardcode. Dùng Design Tokens + reusable components + responsive constraints.

## 2. DESIGN TOKENS

```text
Theme
├── ColorTokens
├── TypographyTokens
├── SpacingTokens
├── RadiusTokens
├── ShadowTokens
├── MotionTokens
├── IconTokens
└── Breakpoints
```

Mọi view dùng token. Không viết màu/radius/font size rải rác trong code.

## 3. VISUAL LANGUAGE

- Dark graphite base.
- Accent lạnh, tương phản vừa phải.
- Card bo góc rõ nhưng không quá tròn.
- Border/subtle elevation.
- Typography hierarchy mạnh.
- Icon nhất quán.
- Khoảng trắng đủ rộng.
- Không lạm dụng glow/gradient.

## 4. LAYOUT

```text
AppShell
├── TopBar
│   ├── Brand
│   ├── ConnectionStatus
│   └── WindowActions
├── Sidebar
│   ├── Dashboard
│   ├── FeatureMenu (dynamic)
│   └── Settings
└── Content
    ├── Breadcrumb/Header
    ├── Toolbar
    └── ViewContent
```

Feature menu được render từ registry/schema.

## 5. REUSABLE COMPONENTS

```text
UI/Components/
├── AppShell
├── Sidebar
├── TopBar
├── Card
├── Section
├── Toggle
├── Slider
├── Dropdown
├── SearchBox
├── FilterChip
├── StatusBadge
├── MetricCard
├── PrimaryButton
├── SecondaryButton
├── IconButton
├── DataTable
├── EmptyState
├── LoadingState
├── ErrorState
└── Toast
```

Một component phải dùng được ở nhiều feature.

## 6. ANIMATION

Motion tokens:
- hover/press: rất ngắn;
- panel transition: ngắn;
- modal: ngắn + easing mềm;
- list update: subtle;
- no perpetual animation nếu không cần.

Animation phải bị disable/reduce khi performance mode hoặc accessibility yêu cầu.

## 7. RESPONSIVE

UI phải thích ứng:
- 16:9;
- ultrawide;
- window resize;
- high DPI;
- scaling;
- nhiều resolution emulator.

Không đặt vị trí absolute cho toàn bộ UI.

## 8. DATA BINDING

Dùng ViewModel/state binding:
```text
Service -> Observable/Signal -> ViewModel -> View
```

Chỉ update component dirty. Không rebuild cả menu khi một toggle đổi.

## 9. UX STATES

Mọi màn hình cần:
- Loading;
- Ready;
- Empty;
- Error;
- Disabled;
- Offline/Disconnected;
- Updating.

## 10. ACCESSIBILITY

- Contrast đủ.
- Tooltip cho icon-only control.
- Keyboard focus.
- Font scaling.
- Reduced motion.

## 11. UNITY IMPLEMENTATION

Ưu tiên **UI Toolkit** cho cấu trúc UI data-driven và styling; dùng uGUI khi cần tương thích/overlay/runtime đặc thù. Không trộn hai hệ thống tùy tiện trong cùng một component.

## 12. PERFORMANCE

- Pool dynamic rows/cards.
- Avoid layout rebuilds không cần thiết.
- Cache references.
- Không Instantiate/Destroy liên tục.
- Profile Canvas/UIToolkit update.
- Asset loading qua Addressables khi quy mô lớn.

## 13. DTA_TOOL PARITY CHECKLIST

Trước khi coi UI mới hoàn thành phải kiểm tra:
- Menu cũ có đủ không?
- Workflow cũ có giữ nguyên không?
- Hotkey/setting có giữ nguyên không?
- Trạng thái connection có rõ hơn không?
- Error feedback có tốt hơn không?
- Có thể thêm menu mới mà không sửa AppShell không?
