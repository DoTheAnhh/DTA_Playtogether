#pragma once
#include <vector>
#include <cstdint>
#include "CollectModels.hpp"
#include "shared/Types.hpp"

namespace dta::features::collect {

class ICollectService {
public:
    virtual ~ICollectService() = default;

    virtual bool PickObject(uint32_t objectUid) = 0;
    virtual bool TeleportToObject(const Vector3& pos) = 0;
    virtual bool CloseResultDialog() = 0;

    virtual std::vector<CollectItemModel> ScanFieldObjects(const Vector3& playerPos, float radius) = 0;
};

} // namespace dta::features::collect
