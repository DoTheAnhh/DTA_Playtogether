#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::farm {

struct FarmOptions {
    bool autoWater{true};
    bool autoHarvest{true};
    bool autoPlant{true};
    uint32_t defaultSeedId{0};
};

struct FarmStats {
    uint32_t totalHarvested{0};
    uint32_t totalWatered{0};
    uint32_t totalPlanted{0};
};

} // namespace dta::features::farm
