#include "FarmView.hpp"

namespace dta::ui::views {

FarmView::FarmView(std::shared_ptr<features::farm::FarmBot> bot)
    : m_bot(std::move(bot)) {}

void FarmView::Render() {
    // Renders Farm configuration:
    // Auto water, Auto harvest, Auto plant seeds, Stats
}

} // namespace dta::ui::views
