#include "CollectView.hpp"

namespace dta::ui::views {

CollectView::CollectView(std::shared_ptr<features::collect::CollectBot> bot)
    : m_bot(std::move(bot)) {}

void CollectView::Render() {
    // Renders Collect configuration:
    // Filter toggles (Wood, Shells, Trash, Cards, Mushrooms), TSP Route Stats
}

} // namespace dta::ui::views
