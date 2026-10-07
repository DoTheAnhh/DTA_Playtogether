#pragma once
#include <vector>
#include <cstdint>
#include <optional>
#include "ExcavationModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::excavation {

class IExcavationService {
public:
    virtual ~IExcavationService() = default;

    virtual bool DigShovel() = 0;
    virtual bool TeleportToPoint(const Vector3& pos) = 0;
    virtual bool OpenTreasureChest() = 0;
    virtual bool RepairShovel() = 0;
    virtual bool CloseRewardDialog() = 0;

    virtual float ReadCurrentRadarSignal() = 0;
    virtual std::vector<RelicModel> ScanExcavationSpots() = 0;
    virtual bool IsShovelBroken() = 0;
    virtual bool IsChestOpen() = 0;
};

} // namespace dta::features::excavation
