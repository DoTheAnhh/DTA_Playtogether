#include "ExcavationView.hpp"

namespace dta::ui::views {

ExcavationView::ExcavationView(std::shared_ptr<features::excavation::ExcavationBot> bot)
    : m_bot(std::move(bot)) {}

void ExcavationView::Render() {
    // Renders Excavation configuration:
    // Auto dig, Auto open chest, Auto repair shovel, Dig interval
}

} // namespace dta::ui::views
