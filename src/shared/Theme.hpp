#pragma once
// ============================================================================
//  DTA Design System — single source of truth for every visual token.
//  Change a value here => the whole Client / Server UI updates in sync.
// ============================================================================
#include <windows.h>
#include <cstdint>

namespace dta::uikit::theme {

// 8-bit RGBA colour usable by both GDI (COLORREF) and GDI+ (ARGB).
struct Rgba {
    uint8_t r{0}, g{0}, b{0}, a{255};

    [[nodiscard]] constexpr COLORREF Ref() const { return RGB(r, g, b); }
    [[nodiscard]] constexpr uint32_t Argb() const {
        return (static_cast<uint32_t>(a) << 24) | (static_cast<uint32_t>(r) << 16) |
               (static_cast<uint32_t>(g) << 8) | static_cast<uint32_t>(b);
    }
    [[nodiscard]] constexpr Rgba Alpha(uint8_t na) const { return {r, g, b, na}; }
    [[nodiscard]] constexpr bool operator==(const Rgba& o) const {
        return r == o.r && g == o.g && b == o.b && a == o.a;
    }
};

[[nodiscard]] constexpr Rgba Hex(uint32_t rgb, uint8_t a = 255) {
    return {static_cast<uint8_t>((rgb >> 16) & 0xFF), static_cast<uint8_t>((rgb >> 8) & 0xFF),
            static_cast<uint8_t>(rgb & 0xFF), a};
}

[[nodiscard]] constexpr Rgba Mix(Rgba from, Rgba to, float t) {
    auto lerp = [t](uint8_t x, uint8_t y) {
        return static_cast<uint8_t>(static_cast<float>(x) + (static_cast<float>(y) - static_cast<float>(x)) * t + 0.5f);
    };
    return {lerp(from.r, to.r), lerp(from.g, to.g), lerp(from.b, to.b), lerp(from.a, to.a)};
}

// Composite a translucent colour over an opaque one (used where GDI needs solid colours).
[[nodiscard]] constexpr Rgba Over(Rgba top, Rgba bottom) {
    return Mix(bottom.Alpha(255), top.Alpha(255), static_cast<float>(top.a) / 255.0f);
}

// ---------------------------------------------------------------------------
// Colour tokens
// ---------------------------------------------------------------------------
namespace Color {
    // Surfaces
    inline constexpr Rgba Background    = Hex(0x0B0F17);
    inline constexpr Rgba Sidebar       = Hex(0x0D121C);
    inline constexpr Rgba Panel         = Hex(0x111827);
    inline constexpr Rgba PanelAlt      = Hex(0x151E2E);
    inline constexpr Rgba PanelHover    = Hex(0x18233A);
    inline constexpr Rgba Raised        = Hex(0x1A2436);
    inline constexpr Rgba Input         = Hex(0x0E1420);
    inline constexpr Rgba InputDisabled = Hex(0x111722);
    inline constexpr Rgba Overlay       = Hex(0x141C2B);   // popups / menus / toasts

    // Lines (translucent white on dark, as specified: rgba(255,255,255,0.06))
    inline constexpr Rgba Border        = Hex(0xFFFFFF, 15);
    inline constexpr Rgba BorderStrong  = Hex(0xFFFFFF, 26);
    inline constexpr Rgba BorderHover   = Hex(0xFFFFFF, 38);
    inline constexpr Rgba Hairline      = Hex(0xFFFFFF, 10);
    inline constexpr Rgba HoverFill     = Hex(0xFFFFFF, 9);
    inline constexpr Rgba PressFill     = Hex(0xFFFFFF, 16);

    // Typography
    inline constexpr Rgba TextPrimary   = Hex(0xF3F4F6);
    inline constexpr Rgba TextSecondary = Hex(0x9CA3AF);
    inline constexpr Rgba TextMuted     = Hex(0x7B8494);
    inline constexpr Rgba TextDisabled  = Hex(0x4B5563);
    inline constexpr Rgba TextOnAccent  = Hex(0xFFFFFF);

    // Accent — Electric Blue / Cyan
    inline constexpr Rgba Accent        = Hex(0x0EA5E9);
    inline constexpr Rgba AccentHover   = Hex(0x38BDF8);
    inline constexpr Rgba AccentPressed = Hex(0x0284C7);
    inline constexpr Rgba AccentDeep    = Hex(0x0369A1);
    inline constexpr Rgba AccentCyan    = Hex(0x00D9FF);
    inline constexpr Rgba AccentSoft    = Hex(0x0EA5E9, 30);
    inline constexpr Rgba AccentRing    = Hex(0x38BDF8, 70);

    // Semantics
    inline constexpr Rgba Success       = Hex(0x22C55E);
    inline constexpr Rgba SuccessText   = Hex(0x4ADE80);
    inline constexpr Rgba Warning       = Hex(0xF59E0B);
    inline constexpr Rgba WarningText   = Hex(0xFBBF24);
    inline constexpr Rgba Danger        = Hex(0xEF4444);
    inline constexpr Rgba DangerText    = Hex(0xF87171);
    inline constexpr Rgba Vip           = Hex(0xF5C451);
    inline constexpr Rgba Purple        = Hex(0xA855F7);
    inline constexpr Rgba White         = Hex(0xFFFFFF);

    // Toggle track when OFF
    inline constexpr Rgba TrackOff      = Hex(0x2A3446);
    inline constexpr Rgba Shadow        = Hex(0x000000, 70);

    // Window caption close-button hover (Windows convention)
    inline constexpr Rgba CloseHover    = Hex(0xC42B1C);
} // namespace Color

// ---------------------------------------------------------------------------
// Geometry tokens (all values are in DIPs @ 96 DPI; scaled at runtime)
// ---------------------------------------------------------------------------
namespace Radius {
    inline constexpr int Small   = 6;
    inline constexpr int Control = 8;   // inputs, buttons, tabs, sidebar items
    inline constexpr int Medium  = 10;
    inline constexpr int Card    = 12;
    inline constexpr int Panel   = 14;
    inline constexpr int Modal   = 18;
    inline constexpr int Pill    = 999;
} // namespace Radius

namespace Space {
    inline constexpr int XS   = 4;
    inline constexpr int S    = 8;
    inline constexpr int M    = 12;
    inline constexpr int L    = 16;
    inline constexpr int XL   = 20;
    inline constexpr int XXL  = 24;
    inline constexpr int XXXL = 32;
} // namespace Space

namespace Size {
    inline constexpr int TitleBar       = 48;
    inline constexpr int CaptionButtonW = 46;
    inline constexpr int Sidebar        = 232;
    inline constexpr int NavItem        = 38;
    inline constexpr int Control        = 36;   // inputs / dropdowns / md buttons
    inline constexpr int ControlSm      = 30;
    inline constexpr int ControlLg      = 44;
    inline constexpr int ToggleRow      = 44;
    inline constexpr int ToggleW        = 36;
    inline constexpr int ToggleH        = 20;
    inline constexpr int Checkbox       = 18;
    inline constexpr int Icon           = 16;
    inline constexpr int IconLg         = 20;
    inline constexpr int TableRow       = 40;
    inline constexpr int TableHeader    = 36;
    inline constexpr int LogRow         = 30;
    inline constexpr int ScrollBar      = 6;
    inline constexpr int ToastW         = 340;
    inline constexpr int StatusBar      = 30;
} // namespace Size

// ---------------------------------------------------------------------------
// Typography (pixel sizes @ 96 DPI)
// ---------------------------------------------------------------------------
namespace Type {
    inline constexpr int Display = 24;
    inline constexpr int Title   = 22;
    inline constexpr int Section = 15;
    inline constexpr int Body    = 14;
    inline constexpr int Small   = 12;
    inline constexpr int Caption = 11;
    inline constexpr int Stat    = 26;
    inline constexpr int Mono    = 13;
} // namespace Type

// ---------------------------------------------------------------------------
// Motion — short, ease-out, event-driven only (never looping on idle).
// ---------------------------------------------------------------------------
namespace Motion {
    inline constexpr uint32_t Fast   = 120;
    inline constexpr uint32_t Normal = 170;
    inline constexpr uint32_t Slow   = 220;
    inline constexpr uint32_t FrameMs = 15;
    inline constexpr uint32_t TooltipDelay = 450;
    inline constexpr uint32_t ToastLifetime = 3200;
} // namespace Motion

inline bool AnimationsEnabled() { return true; }
inline void SetAnimationsEnabled(bool) {}

} // namespace dta::uikit::theme

// Backward-compatible alias for existing modules
namespace dta::theme {
    using namespace dta::uikit::theme;
    namespace Color {
        inline constexpr COLORREF BgMain        = dta::uikit::theme::Color::Background.Ref();
        inline constexpr COLORREF Sidebar       = dta::uikit::theme::Color::Sidebar.Ref();
        inline constexpr COLORREF Card          = dta::uikit::theme::Color::Panel.Ref();
        inline constexpr COLORREF CardHover     = dta::uikit::theme::Color::PanelAlt.Ref();
        inline constexpr COLORREF CardSecondary = dta::uikit::theme::Color::Raised.Ref();
        inline constexpr COLORREF InputBg       = dta::uikit::theme::Color::Input.Ref();
        inline constexpr COLORREF InputBorder   = RGB(38, 48, 66);
        inline constexpr COLORREF BorderSubtle  = RGB(28, 38, 54);
        inline constexpr COLORREF BorderLight   = RGB(45, 55, 75);
        inline constexpr COLORREF BorderFocus   = dta::uikit::theme::Color::Accent.Ref();
        inline constexpr COLORREF TextPrimary   = dta::uikit::theme::Color::TextPrimary.Ref();
        inline constexpr COLORREF TextSecondary = dta::uikit::theme::Color::TextSecondary.Ref();
        inline constexpr COLORREF TextMuted     = dta::uikit::theme::Color::TextMuted.Ref();
        inline constexpr COLORREF TextDark      = RGB(11, 15, 23);
        inline constexpr COLORREF Accent        = dta::uikit::theme::Color::Accent.Ref();
        inline constexpr COLORREF AccentHover   = dta::uikit::theme::Color::AccentHover.Ref();
        inline constexpr COLORREF CyanNeon      = dta::uikit::theme::Color::AccentCyan.Ref();
        inline constexpr COLORREF Success       = dta::uikit::theme::Color::Success.Ref();
        inline constexpr COLORREF SuccessHover  = RGB(22, 163, 74);
        inline constexpr COLORREF Warning       = dta::uikit::theme::Color::Warning.Ref();
        inline constexpr COLORREF Danger        = dta::uikit::theme::Color::Danger.Ref();
        inline constexpr COLORREF DangerHover   = RGB(220, 38, 38);
        inline constexpr COLORREF DangerBg      = RGB(45, 18, 25);
        inline constexpr COLORREF VipGold       = dta::uikit::theme::Color::Vip.Ref();
        inline constexpr COLORREF Purple        = dta::uikit::theme::Color::Purple.Ref();
    }
    namespace Radius {
        inline constexpr int Small  = dta::uikit::theme::Radius::Small;
        inline constexpr int Normal = dta::uikit::theme::Radius::Control;
        inline constexpr int Medium = dta::uikit::theme::Radius::Card;
        inline constexpr int Large  = dta::uikit::theme::Radius::Panel;
        inline constexpr int Pill   = dta::uikit::theme::Radius::Pill;
    }

    inline void DrawRoundedRect(HDC hdc, const RECT& rc, COLORREF bg, COLORREF border, int radius) {
        HBRUSH hBrush = CreateSolidBrush(bg);
        HPEN hPen = CreatePen(PS_SOLID, 1, border);
        HGDIOBJ oldBrush = SelectObject(hdc, hBrush);
        HGDIOBJ oldPen = SelectObject(hdc, hPen);

        RoundRect(hdc, rc.left, rc.top, rc.right, rc.bottom, radius * 2, radius * 2);

        SelectObject(hdc, oldBrush);
        SelectObject(hdc, oldPen);
        DeleteObject(hBrush);
        DeleteObject(hPen);
    }

    inline void DrawModernCard(HDC hdc, const RECT& rc, const std::wstring& title = L"", HFONT hTitleFont = nullptr) {
        DrawRoundedRect(hdc, rc, Color::Card, Color::BorderSubtle, Radius::Medium);
        if (!title.empty() && hTitleFont) {
            SetBkMode(hdc, TRANSPARENT);
            HGDIOBJ oldFont = SelectObject(hdc, hTitleFont);
            SetTextColor(hdc, Color::TextPrimary);
            TextOutW(hdc, rc.left + 16, rc.top + 14, title.c_str(), static_cast<int>(title.size()));
            SelectObject(hdc, oldFont);
        }
    }

    inline void DrawModernButton(HDC hdc, const RECT& rc, const std::wstring& text,
                                 COLORREF bg, COLORREF border, COLORREF textCol,
                                 HFONT hFont, int radius = Radius::Normal) {
        DrawRoundedRect(hdc, rc, bg, border, radius);
        if (!text.empty() && hFont) {
            SetBkMode(hdc, TRANSPARENT);
            HGDIOBJ oldFont = SelectObject(hdc, hFont);
            SetTextColor(hdc, textCol);
            RECT rcText = rc;
            DrawTextW(hdc, text.c_str(), -1, &rcText, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            SelectObject(hdc, oldFont);
        }
    }

    inline void DrawStatusBadge(HDC hdc, const RECT& rc, const std::wstring& text,
                                COLORREF dotColor, COLORREF bg, COLORREF border,
                                HFONT hFont) {
        DrawRoundedRect(hdc, rc, bg, border, Radius::Pill);

        int dotSize = 6;
        int dotX = rc.left + 12;
        int dotY = rc.top + (rc.bottom - rc.top - dotSize) / 2;
        HBRUSH hDotBrush = CreateSolidBrush(dotColor);
        HPEN hDotPen = CreatePen(PS_SOLID, 1, dotColor);
        HGDIOBJ oldB = SelectObject(hdc, hDotBrush);
        HGDIOBJ oldP = SelectObject(hdc, hDotPen);
        Ellipse(hdc, dotX, dotY, dotX + dotSize, dotY + dotSize);
        SelectObject(hdc, oldB);
        SelectObject(hdc, oldP);
        DeleteObject(hDotBrush);
        DeleteObject(hDotPen);

        SetBkMode(hdc, TRANSPARENT);
        HGDIOBJ oldFont = SelectObject(hdc, hFont);
        SetTextColor(hdc, dotColor);
        RECT rcText = rc;
        rcText.left = dotX + dotSize + 6;
        rcText.right -= 10;
        DrawTextW(hdc, text.c_str(), -1, &rcText, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
        SelectObject(hdc, oldFont);
    }
} // namespace dta::theme
