#pragma once
#include <windows.h>
#include <gdiplus.h>
#include <string>
#include <vector>
#include "shared/Theme.hpp"

#pragma comment(lib, "gdiplus.lib")

namespace dta::uikit {

using namespace Gdiplus;
namespace tok = dta::uikit::theme;

inline Color ToGdiPlus(tok::Rgba c) {
    return Color(c.a, c.r, c.g, c.b);
}

class GdiplusScope {
public:
    static GdiplusScope& Instance() {
        static GdiplusScope instance;
        return instance;
    }
    void Init() {
        if (!m_initialized) {
            GdiplusStartupInput input;
            GdiplusStartup(&m_token, &input, nullptr);
            m_initialized = true;
        }
    }
    void Shutdown() {
        if (m_initialized) {
            GdiplusShutdown(m_token);
            m_initialized = false;
        }
    }
private:
    GdiplusScope() { Init(); }
    ~GdiplusScope() { Shutdown(); }
    ULONG_PTR m_token{0};
    bool m_initialized{false};
};

inline void AddRoundedRectangle(GraphicsPath& path, RectF rect, float radius) {
    float diameter = radius * 2.0f;
    if (diameter > rect.Width) diameter = rect.Width;
    if (diameter > rect.Height) diameter = rect.Height;

    RectF arc(rect.X, rect.Y, diameter, diameter);
    path.AddArc(arc, 180.0f, 90.0f);
    arc.X = rect.X + rect.Width - diameter;
    path.AddArc(arc, 270.0f, 90.0f);
    arc.Y = rect.Y + rect.Height - diameter;
    path.AddArc(arc, 0.0f, 90.0f);
    arc.X = rect.X;
    path.AddArc(arc, 90.0f, 90.0f);
    path.CloseFigure();
}

inline void FillRoundedRect(Graphics& g, RectF rect, float radius, Color bg) {
    GraphicsPath path;
    AddRoundedRectangle(path, rect, radius);
    SolidBrush brush(bg);
    g.FillPath(&brush, &path);
}

inline void DrawRoundedRect(Graphics& g, RectF rect, float radius, Color border, float width = 1.0f) {
    GraphicsPath path;
    AddRoundedRectangle(path, rect, radius);
    Pen pen(border, width);
    g.DrawPath(&pen, &path);
}

inline void DrawModernCard(Graphics& g, RectF rect, float radius, Color bg, Color border) {
    FillRoundedRect(g, rect, radius, bg);
    DrawRoundedRect(g, rect, radius, border, 1.0f);
}

inline void DrawModernButton(Graphics& g, RectF rect, const std::wstring& text, Font* font,
                             Color bg, Color border, Color textCol, float radius = 8.0f) {
    FillRoundedRect(g, rect, radius, bg);
    DrawRoundedRect(g, rect, radius, border, 1.0f);

    if (font && !text.empty()) {
        SolidBrush textBrush(textCol);
        StringFormat format;
        format.SetAlignment(StringAlignmentCenter);
        format.SetLineAlignment(StringAlignmentCenter);
        g.DrawString(text.c_str(), -1, font, rect, &format, &textBrush);
    }
}

inline void DrawModernToggle(Graphics& g, RectF rect, bool checked, const std::wstring& label, Font* font) {
    // 1. Draw modern pill track (34 x 18)
    float trackW = 34.0f;
    float trackH = 18.0f;
    float trackY = rect.Y + (rect.Height - trackH) / 2.0f;
    RectF trackRect(rect.X, trackY, trackW, trackH);

    Color trackBg = checked ? ToGdiPlus(tok::Color::Accent) : ToGdiPlus(tok::Color::TrackOff);
    Color trackBorder = checked ? ToGdiPlus(tok::Color::AccentHover) : ToGdiPlus(tok::Color::Border);

    FillRoundedRect(g, trackRect, trackH / 2.0f, trackBg);
    DrawRoundedRect(g, trackRect, trackH / 2.0f, trackBorder, 1.0f);

    // 2. Draw thumb circle
    float thumbD = 14.0f;
    float thumbX = checked ? (trackRect.X + trackRect.Width - thumbD - 2.0f) : (trackRect.X + 2.0f);
    float thumbY = trackRect.Y + (trackH - thumbD) / 2.0f;
    RectF thumbRect(thumbX, thumbY, thumbD, thumbD);

    SolidBrush thumbBrush(Color(255, 255, 255, 255));
    g.FillEllipse(&thumbBrush, thumbRect);

    // 3. Draw label text
    if (font && !label.empty()) {
        RectF textRect(rect.X + trackW + 10.0f, rect.Y, rect.Width - trackW - 10.0f, rect.Height);
        SolidBrush textBrush(ToGdiPlus(tok::Color::TextPrimary));
        StringFormat format;
        format.SetAlignment(StringAlignmentNear);
        format.SetLineAlignment(StringAlignmentCenter);
        g.DrawString(label.c_str(), -1, font, textRect, &format, &textBrush);
    }
}

inline void DrawModernChip(Graphics& g, RectF rect, const std::wstring& text, Font* font,
                           bool selected, Color dotColor, bool hasDot = false) {
    Color bg = selected ? ToGdiPlus(tok::Color::AccentSoft) : ToGdiPlus(tok::Color::PanelAlt);
    Color border = selected ? ToGdiPlus(tok::Color::Accent) : ToGdiPlus(tok::Color::Border);
    Color textCol = selected ? ToGdiPlus(tok::Color::AccentCyan) : ToGdiPlus(tok::Color::TextSecondary);

    FillRoundedRect(g, rect, 6.0f, bg);
    DrawRoundedRect(g, rect, 6.0f, border, 1.0f);

    if (hasDot) {
        float dotD = 6.0f;
        float dotX = rect.X + 8.0f;
        float dotY = rect.Y + (rect.Height - dotD) / 2.0f;
        SolidBrush dotBrush(dotColor);
        g.FillEllipse(&dotBrush, RectF(dotX, dotY, dotD, dotD));

        if (font && !text.empty()) {
            RectF textRect(dotX + dotD + 4.0f, rect.Y, rect.Width - (dotX + dotD + 4.0f - rect.X), rect.Height);
            SolidBrush tb(textCol);
            StringFormat fmt;
            fmt.SetAlignment(StringAlignmentNear);
            fmt.SetLineAlignment(StringAlignmentCenter);
            g.DrawString(text.c_str(), -1, font, textRect, &fmt, &tb);
        }
    } else {
        if (font && !text.empty()) {
            SolidBrush tb(textCol);
            StringFormat fmt;
            fmt.SetAlignment(StringAlignmentCenter);
            fmt.SetLineAlignment(StringAlignmentCenter);
            g.DrawString(text.c_str(), -1, font, rect, &fmt, &tb);
        }
    }
}

inline void DrawStatusPill(Graphics& g, RectF rect, const std::wstring& text, Font* font,
                           Color dotColor, Color bg, Color border) {
    FillRoundedRect(g, rect, rect.Height / 2.0f, bg);
    DrawRoundedRect(g, rect, rect.Height / 2.0f, border, 1.0f);

    float dotD = 8.0f;
    float dotX = rect.X + 12.0f;
    float dotY = rect.Y + (rect.Height - dotD) / 2.0f;
    SolidBrush dotBrush(dotColor);
    g.FillEllipse(&dotBrush, RectF(dotX, dotY, dotD, dotD));

    if (font && !text.empty()) {
        RectF textRect(dotX + dotD + 6.0f, rect.Y, rect.Width - (dotX + dotD + 16.0f - rect.X), rect.Height);
        SolidBrush tb(dotColor);
        StringFormat fmt;
        fmt.SetAlignment(StringAlignmentNear);
        fmt.SetLineAlignment(StringAlignmentCenter);
        g.DrawString(text.c_str(), -1, font, textRect, &fmt, &tb);
    }
}

} // namespace dta::uikit
