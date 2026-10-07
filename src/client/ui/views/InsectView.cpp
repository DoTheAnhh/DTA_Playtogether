#include "InsectView.hpp"

namespace dta::ui::views {

InsectView::InsectView(std::shared_ptr<features::insect::InsectBot> bot)
    : m_bot(std::move(bot)) {}

void InsectView::Render() {
    // Renders Insect configuration:
    // Only crowns toggle, min grade, auto repair, swing latency tuning
}

} // namespace dta::ui::views
