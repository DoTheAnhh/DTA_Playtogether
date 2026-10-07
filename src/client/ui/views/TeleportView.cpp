#include "TeleportView.hpp"

namespace dta::ui::views {

TeleportView::TeleportView(std::shared_ptr<features::teleport::ITeleportService> service)
    : m_service(std::move(service)) {}

void TeleportView::Render() {
    // Renders Teleport configuration:
    // Direct Zone Move buttons (Plaza, Downtown, Camping, Resort, Home)
    // Waypoints table, Current Coordinates display, Save Waypoint
}

} // namespace dta::ui::views
