#pragma once
#include <vector>
#include <cstdint>
#include <optional>
#include "MiningModels.hpp"
#include "shared/GameEnums.hpp"

namespace dta::features::mining {

class IMiningService {
public:
    virtual ~IMiningService() = default;

    virtual bool SwingPickax() = 0;
    virtual bool PickOreItem(uint32_t objectUid) = 0;
    virtual bool RepairPickax() = 0;
    virtual bool TeleportToOre(const Vector3& pos) = 0;

    virtual std::vector<OreModel> ScanOresAround(const Vector3& playerPos, float radius) = 0;
    virtual std::vector<uint32_t> ScanDroppedItemsAround(const Vector3& pos, float radius) = 0;
    virtual PickaxState GetPickaxState() = 0;
    virtual bool IsPickaxBroken() = 0;
};

} // namespace dta::features::mining
