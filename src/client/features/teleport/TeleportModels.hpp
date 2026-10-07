#pragma once
#include <string>
#include <vector>
#include <cstdint>
#include "shared/Types.hpp"
#include "shared/GameEnums.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::teleport {

struct MapInfo {
    uint32_t mapId;
    std::string name;
    std::string description;
};

} // namespace dta::features::teleport
