#pragma once
#include <string>
#include <vector>
#include <cstdint>
#include "shared/Types.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::esp {

enum class EspEntityType : uint8_t {
    ORE,
    INSECT,
    FISH,
    PLAYER,
    CHEST,
    ITEM
};

struct EspEntity {
    EspEntityType type{EspEntityType::ORE};
    Vector3 worldPosition;
    std::string label;
    ColorRGBA color{0, 229, 255, 255}; // Arctic Cyan default
    float distance{0.0f};
    int32_t currentHp{1};
    int32_t maxHp{1};
};

struct EspConfig {
    bool enabled{true};
    bool showOres{true};
    bool showInsects{true};
    bool showFish{true};
    bool showPlayers{true};
    bool showChests{true};
    bool showTracers{false};
    float maxDistance{150.0f};
    float playerWarningDistance{15.0f};
};

} // namespace dta::features::esp
