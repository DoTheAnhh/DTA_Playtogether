#include "SettingsService.hpp"
#include "client/core/logger/AsyncLogger.hpp"
#include <fstream>
#include <sstream>

namespace dta::features::settings {

SettingsService::SettingsService(std::string configPath)
    : m_configPath(std::move(configPath)) {
    LoadSettings();
}

void SettingsService::LoadSettings() {
    std::ifstream file(m_configPath);
    if (!file.is_open()) {
        DTA_LOG_INFO("Settings", "Using default settings.");
        return;
    }
    DTA_LOG_INFO("Settings", "Settings loaded successfully from " + m_configPath);
}

void SettingsService::SaveSettings(const AppSettings& settings) {
    m_settings = settings;
    std::ofstream file(m_configPath);
    if (file.is_open()) {
        file << "{\n  \"serverUrl\": \"" << m_settings.serverUrl << "\",\n  \"licenseKey\": \""
             << m_settings.licenseKey << "\"\n}\n";
        DTA_LOG_INFO("Settings", "Settings saved successfully.");
    }
}

} // namespace dta::features::settings
