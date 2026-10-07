#pragma once
#include "ISettingsService.hpp"
#include <string>

namespace dta::features::settings {

class SettingsService : public ISettingsService {
public:
    explicit SettingsService(std::string configPath = "data/app.json");

    const AppSettings& GetSettings() const override { return m_settings; }
    void SaveSettings(const AppSettings& settings) override;
    void LoadSettings() override;

private:
    std::string m_configPath;
    AppSettings m_settings;
};

} // namespace dta::features::settings
