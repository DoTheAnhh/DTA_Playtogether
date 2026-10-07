#include "EspView.hpp"

namespace dta::ui::views {

EspView::EspView(std::shared_ptr<features::esp::IEspService> service)
    : m_service(std::move(service)) {}

void EspView::Render() {
    // Renders ESP configuration:
    // Toggles: Ore ESP, Insect ESP, Fish ESP, Player Warning Radar, Distance Slider
}

} // namespace dta::ui::views
