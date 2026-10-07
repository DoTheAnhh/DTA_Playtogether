#pragma once
#include <string>
#include <string_view>
#include "client/ui/DesignSystem.hpp"

namespace dta::ui::components {

struct CyberCard {
    std::string title;
    std::string subtitle;
};

struct StatusBadge {
    std::string text;
    bool isActive{false};
    ColorRGBA activeColor{design::Colors::Success};
    ColorRGBA idleColor{design::Colors::TextSecondary};
};

struct StatCounter {
    std::string label;
    uint32_t count{0};
};

} // namespace dta::ui::components
