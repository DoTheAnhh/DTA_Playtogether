#pragma once
#include <windows.h>
#include <string>

namespace dta::theme {

namespace Color {
    // Backgrounds
    inline constexpr COLORREF BgMain         = RGB(11, 15, 23);   // #0B0F17 Deep Dark
    inline constexpr COLORREF Sidebar        = RGB(13, 18, 28);   // #0D121C Sidebar
    inline constexpr COLORREF Card           = RGB(17, 24, 39);   // #111827 Panel / Card
    inline constexpr COLORREF CardHover      = RGB(21, 30, 46);   // #151E2E Panel Hover
    inline constexpr COLORREF CardSecondary  = RGB(26, 36, 56);   // #1A2438 Secondary Panel
    inline constexpr COLORREF InputBg        = RGB(17, 24, 39);   // #111827 Input
    inline constexpr COLORREF InputBorder    = RGB(42, 54, 76);   // #2A364C

    // Borders
    inline constexpr COLORREF BorderSubtle   = RGB(31, 41, 55);   // #1F2937 (~6% white on dark)
    inline constexpr COLORREF BorderLight    = RGB(55, 65, 81);   // #374151
    inline constexpr COLORREF BorderFocus    = RGB(14, 165, 233); // #0EA5E9 Accent Border

    // Typography
    inline constexpr COLORREF TextPrimary    = RGB(243, 244, 246);// #F3F4F6 Pure Light
    inline constexpr COLORREF TextSecondary  = RGB(156, 163, 175);// #9CA3AF Muted Light
    inline constexpr COLORREF TextMuted      = RGB(107, 114, 128);// #6B7280 Dim
    inline constexpr COLORREF TextDark       = RGB(11, 15, 23);   // #0B0F17 Dark on Accent

    // Accents & Semantics
    inline constexpr COLORREF Accent         = RGB(14, 165, 233); // #0EA5E9 Electric Sky Blue
    inline constexpr COLORREF AccentHover    = RGB(56, 189, 248); // #38BDF8 Bright Sky
    inline constexpr COLORREF CyanNeon       = RGB(0, 217, 255);  // #00D9FF Arctic Cyan
    inline constexpr COLORREF Success        = RGB(34, 197, 94);  // #22C55E Emerald
    inline constexpr COLORREF SuccessHover   = RGB(22, 163, 74);  // #16A34A
    inline constexpr COLORREF Warning        = RGB(245, 158, 11); // #F59E0B Amber
    inline constexpr COLORREF Danger         = RGB(239, 68, 68);  // #EF4444 Crimson
    inline constexpr COLORREF DangerHover    = RGB(220, 38, 38);  // #DC2626
    inline constexpr COLORREF DangerBg       = RGB(45, 18, 25);   // Subtle Red Panel
    inline constexpr COLORREF VipGold        = RGB(245, 196, 81); // #F5C451 Premium Gold
    inline constexpr COLORREF Purple         = RGB(168, 85, 247); // #A855F7
}

namespace Radius {
    inline constexpr int Small  = 6;
    inline constexpr int Normal = 8;
    inline constexpr int Medium = 12;
    inline constexpr int Large  = 16;
    inline constexpr int Pill   = 20;
}

// Utilities for clean GDI Drawing with RAII
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

    // Chấm tròn indicator
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

    // Chữ trạng thái
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
