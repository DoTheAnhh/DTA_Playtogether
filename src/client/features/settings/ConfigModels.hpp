#pragma once
#include <string>
#include <cstdint>

namespace dta::features::settings {

struct AntiDetectionConfig {
    bool enableMicroJitter{true};
    uint32_t jitterMinMs{8};
    uint32_t jitterMaxMs{28};
    bool pauseOnPlayerNearby{true};
    float proximityRadius{10.0f};
    uint32_t panicHotkeyVk{0x7B}; // VK_F12
};

struct AppSettings {
    std::string licenseKey;
    std::string serverUrl{"https://dta-vps.playtogether.network"};
    bool startMinimized{false};
    bool autoConnectEmulator{true};
    AntiDetectionConfig antiDetection;
};

} // namespace dta::features::settings
