#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <unordered_set>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::collect {

struct CollectFilter {
    bool pickWood{true};
    bool pickShells{true};
    bool pickTrash{true};
    bool pickCards{true};
    bool pickMushrooms{true};
    float maxRadius{150.0f};
};

struct CollectStats {
    uint32_t itemsPicked{0};
    uint32_t routeCount{0};
};

} // namespace dta::features::collect
