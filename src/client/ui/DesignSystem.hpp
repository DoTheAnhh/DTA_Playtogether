#pragma once
#include <cstdint>
#include "shared/Types.hpp"

namespace dta::ui::design {

// Cyber Graphite & Arctic Ice Color Palette
namespace Colors {
    constexpr ColorRGBA Background   {0x0B, 0x0D, 0x11, 0xFF}; // Deep Charcoal
    constexpr ColorRGBA Surface      {0x14, 0x17, 0x1F, 0xFF}; // Dark Slate
    constexpr ColorRGBA CardBorder   {0x23, 0x28, 0x34, 0xFF}; // Graphite Border
    constexpr ColorRGBA PrimaryAccent{0x00, 0xE5, 0xFF, 0xFF}; // Cold Cyan
    constexpr ColorRGBA Success      {0x10, 0xB9, 0x81, 0xFF}; // Emerald Green
    constexpr ColorRGBA Warning      {0xF5, 0x9E, 0x0B, 0xFF}; // Amber Gold
    constexpr ColorRGBA Danger       {0xEF, 0x44, 0x44, 0xFF}; // Crimson Red
    constexpr ColorRGBA TextPrimary  {0xFA, 0xFA, 0xFA, 0xFF}; // High Contrast White
    constexpr ColorRGBA TextSecondary{0x94, 0xA3, 0xB8, 0xFF}; // Slate Gray
}

// Spacing & Metrics
namespace Metrics {
    constexpr float Padding         = 12.0f;
    constexpr float ItemSpacing     = 8.0f;
    constexpr float CardCornerRadius= 5.0f;
    constexpr float BorderThickness = 1.0f;
}

} // namespace dta::ui::design
