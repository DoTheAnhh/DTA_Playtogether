#pragma once
#include <cstdint>
#include <string>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::excavation {

struct RadarSample {
    Vector3 position;
    float signalIntensity{0.0f};
};

struct ExcavationOptions {
    bool autoDig{true};
    bool autoOpenChest{true};
    bool autoRepair{true};
    uint32_t digIntervalMs{350};
};

struct ExcavationStats {
    uint32_t totalDigs{0};
    uint32_t chestsFound{0};
    uint32_t artifactsCollected{0};
};

} // namespace dta::features::excavation
