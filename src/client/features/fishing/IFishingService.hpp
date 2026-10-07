#pragma once
#include <cstdint>
#include <string>
#include <optional>
#include "FishingModels.hpp"
#include "shared/GameEnums.hpp"

namespace dta::features::fishing {

class IFishingService {
public:
    virtual ~IFishingService() = default;

    // Direct game actions (on Unity Main Thread)
    virtual bool CastRod() = 0;
    virtual bool ReelIn() = 0;
    virtual bool KeepFish() = 0;
    virtual bool SellFish() = 0;
    virtual bool OpenBox() = 0;
    virtual bool RepairRod() = 0;

    // Memory status reading
    virtual FishingState GetFishingState() = 0;
    virtual std::optional<FishModel> GetCurrentFishInfo() = 0;
    virtual bool IsRodBroken() = 0;
    virtual bool IsResultDialogOpen() = 0;
};

} // namespace dta::features::fishing
