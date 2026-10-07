#include "MiningView.hpp"

namespace dta::ui::views {

MiningView::MiningView(std::shared_ptr<features::mining::MiningBot> bot)
    : m_bot(std::move(bot)) {}

void MiningView::Render() {
    // Renders Mining configuration:
    // Radius slider, Prioritize Prized toggle, Auto Repair, Auto Collect
}

} // namespace dta::ui::views
