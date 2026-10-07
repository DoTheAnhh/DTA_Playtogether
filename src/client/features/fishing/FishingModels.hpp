#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <unordered_set>
#include "shared/GameEnums.hpp"
#include "shared/FeatureModels.hpp"

namespace dta::features::fishing {

enum class AfterCatchAction : uint8_t {
    KEEP_TO_BAG = 0,
    SELL_FAST = 1
};

struct FishingFilterConfig {
    bool enabledShadows[8]{false, true, true, true, true, true, true, true}; // Index 1-7
    bool enabledGrades[6]{false, true, true, true, true, true};             // Index 1-5
    bool keepVariants{true};
    bool keepCrowns{true};
    std::unordered_set<uint32_t> targetFishIds;
};

struct FishingStats {
    uint32_t totalCast{0};
    uint32_t totalCaught{0};
    uint32_t crownsCount{0};
    uint32_t legendaryCount{0};
    std::string lastFishName{"Chưa có"};
    float catchRatePerMin{0.0f};
};

struct FishingBotOptions {
    bool autoCastReel{true};
    bool autoRepair{true};
    AfterCatchAction afterCatch{AfterCatchAction::KEEP_TO_BAG};
    FishingFilterConfig filter;
    uint32_t biteReactionDelayMs{15}; // Micro-jitter
    bool isFreeTier{false};           // Bản miễn phí hạn chế
    bool enableTugHpPull{true};       // Cơ chế giật tụt HP cá bóng 6-7 (Chỉ VIP)
};

} // namespace dta::features::fishing
