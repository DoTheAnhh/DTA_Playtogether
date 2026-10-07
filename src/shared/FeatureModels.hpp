#pragma once
#include <string>
#include <vector>
#include <cstdint>
#include "Types.hpp"
#include "GameEnums.hpp"

namespace dta {

struct FishModel {
    uint32_t fishId{0};
    uint8_t shadowTier{1}; // 1 to 7
    uint8_t grade{1};      // 1 to 5
    std::string name;
    bool isVariant{false};
    bool isCrown{false};
};

struct OreModel {
    uint32_t uid{0};
    Vector3 position;
    std::string name;
    int32_t hp{1};
    int32_t maxHp{1};
    bool isPrized{false};
    bool isEvent{false};
    bool isLarge{false};
};

struct InsectModel {
    uint32_t uid{0};
    Vector3 position;
    Vector3 velocity;
    std::string name;
    ItemGrade grade{ItemGrade::COMMON};
    bool isCrown{false};
    bool isFlying{false};
};

struct RelicModel {
    uint32_t uid{0};
    Vector3 position;
    std::string name;
    int32_t currentHp{1};
    int32_t maxHp{1};
    bool isTreasureIsland{false};
};

enum class FarmPlotState : uint8_t {
    EMPTY = 0,
    GROWING = 1,
    NEED_WATER = 2,
    RIPE_CAN_HARVEST = 3,
    WITHERED = 4
};

struct FarmPlotModel {
    uint32_t uid{0};
    Vector3 position;
    FarmPlotState state{FarmPlotState::EMPTY};
    std::string plantName;
    uint32_t seedId{0};
    int32_t remainingTimeSeconds{0};
};

struct CollectItemModel {
    uint32_t uid{0};
    Vector3 position;
    std::string name;
    std::string kind; // Wood, Mushroom, Shrimp, Egg, Card, Trash, Shell
    std::string assetName;
};

struct Waypoint {
    std::string name;
    Vector3 position;
    uint32_t mapId{1};
};

} // namespace dta
