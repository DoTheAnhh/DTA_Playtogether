#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::insect {

struct SwingZone {
    float reach{1.86f};
    float radius{0.48f};
    float low{-0.28f};
    float high{1.38f};
};

struct InsectOptions {
    float radius{60.0f};
    bool onlyCrowns{false};
    ItemGrade minGrade{ItemGrade::COMMON};
    bool autoRepair{true};
    float swingLatencySeconds{0.12f};
};

struct InsectStats {
    uint32_t totalSwings{0};
    uint32_t insectsCaught{0};
    uint32_t crownsCaught{0};
    uint32_t rareCaught{0};
    std::string lastInsectName{"Chưa có"};
};

} // namespace dta::features::insect
