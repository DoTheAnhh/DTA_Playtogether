#include "FishingView.hpp"

namespace dta::ui::views {

FishingView::FishingView(std::shared_ptr<features::fishing::FishingBot> bot)
    : m_bot(std::move(bot)) {}

void FishingView::Render() {
    // Renders Fishing configuration:
    // Toggle: Auto Cast/Reel
    // MultiSelect: Shadows 1-7
    // Dropdown: Keep vs Sell
    // Toggle: Auto Repair
    // Session Stats: Caught, Legendary, Crowns
}

} // namespace dta::ui::views
