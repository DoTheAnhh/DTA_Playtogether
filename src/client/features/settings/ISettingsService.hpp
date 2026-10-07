#pragma once
#include "ConfigModels.hpp"

namespace dta::features::settings {

class ISettingsService {
public:
    virtual ~ISettingsService() = default;

    virtual const AppSettings& GetSettings() const = 0;
    virtual void SaveSettings(const AppSettings& settings) = 0;
    virtual void LoadSettings() = 0;
};

} // namespace dta::features::settings
