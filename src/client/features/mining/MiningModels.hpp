#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::mining {

enum class MiningTargetKind : uint8_t {
    ALL = 0,
    ROCK_ONLY = 1,
    EVENT_ONLY = 2
};

struct MiningOptions {
    float radius{50.0f};
    MiningTargetKind targetKind{MiningTargetKind::ALL};
    bool prioritizePrized{true};
    bool autoRepair{true};
    bool autoCollectDrops{true};
    uint32_t swingIntervalMs{450}; // Aligned with animation
};

struct MiningStats {
    uint32_t totalSwings{0};
    uint32_t oresDestroyed{0};
    uint32_t prizedOres{0};
    uint32_t itemsCollected{0};
};

} // namespace dta::features::mining
