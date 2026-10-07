#include "SettingsView.hpp"

namespace dta::ui::views {

SettingsView::SettingsView(std::shared_ptr<features::settings::ISettingsService> service)
    : m_service(std::move(service)) {}

void SettingsView::Render() {
    // Renders Settings configuration:
    // License Key input, HWID display, Server URL, Anti-Detection jitter parameters, Panic Hotkey
}

} // namespace dta::ui::views
